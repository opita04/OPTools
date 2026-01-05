using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OPTools.Core
{
    /// <summary>
    /// Handles checking for updates and updating npm packages
    /// </summary>
    public class NpmUpdater : IDisposable
    {
        private readonly HttpClient _httpClient;
        private bool _disposed;

        public NpmUpdater()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "OPTools/1.0");
        }

        /// <summary>
        /// Checks the npm registry for the latest version of a package
        /// </summary>
        public async Task<(string? latestVersion, bool notFound)> GetLatestVersionAsync(string packageName)
        {
            try
            {
                var url = $"https://registry.npmjs.org/{Uri.EscapeDataString(packageName)}/latest";
                var response = await _httpClient.GetAsync(url);
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return (null, true);
                }
                
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(content);
                
                var version = json["version"]?.ToString();
                return (version, false);
            }
            catch (HttpRequestException)
            {
                return (null, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting latest version for {packageName}: {ex.Message}");
                return (null, false);
            }
        }

        /// <summary>
        /// Checks multiple packages for updates
        /// </summary>
        public async Task<List<(NpmPackage package, string? latestVersion, bool isOutdated, bool notFound)>> CheckForUpdatesAsync(
            IEnumerable<NpmPackage> packages, 
            IProgress<(int current, int total, string packageName)>? progress = null)
        {
            var results = new List<(NpmPackage package, string? latestVersion, bool isOutdated, bool notFound)>();
            var packageList = new List<NpmPackage>(packages);
            var total = packageList.Count;
            var current = 0;
            
            foreach (var package in packageList)
            {
                current++;
                progress?.Report((current, total, package.Name));
                
                var (latestVersion, notFound) = await GetLatestVersionAsync(package.Name);
                var isOutdated = !notFound && !string.IsNullOrEmpty(latestVersion) && 
                                 CompareVersions(package.Version, latestVersion) < 0;
                
                results.Add((package, latestVersion, isOutdated, notFound));
                
                // Small delay to avoid hammering the registry
                await Task.Delay(50);
            }
            
            return results;
        }

        /// <summary>
        /// Updates a package to a specific version
        /// </summary>
        public async Task<NpmUpdateResult> UpdatePackageAsync(NpmPackage package, string? targetVersion = null)
        {
            var result = new NpmUpdateResult
            {
                PackageName = package.Name,
                OldVersion = package.Version
            };
            
            try
            {
                var version = targetVersion ?? package.LatestVersion ?? "latest";
                var isGlobal = package.ProjectPath == "__GLOBAL__";
                
                var command = isGlobal
                    ? $"npm install -g {package.Name}@{version}"
                    : $"npm install {package.Name}@{version}";
                
                var workingDir = isGlobal ? null : package.ProjectPath;
                
                var (success, output, error) = await RunNpmCommandAsync(command, workingDir);
                
                if (success)
                {
                    result.Success = true;
                    result.NewVersion = version;
                    result.Details = output;
                }
                else
                {
                    result.Success = false;
                    result.NewVersion = package.Version;
                    result.ErrorMessage = error;
                    result.Details = $"Command: {command}\nError: {error}";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.NewVersion = package.Version;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }

        /// <summary>
        /// Uninstalls a package from a project
        /// </summary>
        public async Task<bool> UninstallPackageAsync(NpmPackage package)
        {
            try
            {
                var isGlobal = package.ProjectPath == "__GLOBAL__";
                
                var command = isGlobal
                    ? $"npm uninstall -g {package.Name}"
                    : $"npm uninstall {package.Name}";
                
                var workingDir = isGlobal ? null : package.ProjectPath;
                
                var (success, _, _) = await RunNpmCommandAsync(command, workingDir);
                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error uninstalling {package.Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Compares two semantic versions
        /// Returns: -1 if v1 < v2, 0 if equal, 1 if v1 > v2
        /// </summary>
        private int CompareVersions(string version1, string version2)
        {
            try
            {
                // Clean versions
                version1 = CleanVersion(version1);
                version2 = CleanVersion(version2);
                
                var v1Parts = version1.Split('.');
                var v2Parts = version2.Split('.');
                
                var maxLen = Math.Max(v1Parts.Length, v2Parts.Length);
                
                for (int i = 0; i < maxLen; i++)
                {
                    var p1 = i < v1Parts.Length ? ParseVersionPart(v1Parts[i]) : 0;
                    var p2 = i < v2Parts.Length ? ParseVersionPart(v2Parts[i]) : 0;
                    
                    if (p1 < p2) return -1;
                    if (p1 > p2) return 1;
                }
                
                return 0;
            }
            catch
            {
                // Fallback to string comparison
                return string.Compare(version1, version2, StringComparison.Ordinal);
            }
        }

        private string CleanVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return "0.0.0";
            
            // Remove common prefixes
            version = version.TrimStart('v', 'V', '^', '~', '>', '<', '=', ' ');
            
            // Take only the version part before any hyphen (remove prerelease tags for comparison)
            var hyphenIndex = version.IndexOf('-');
            if (hyphenIndex > 0)
                version = version.Substring(0, hyphenIndex);
            
            return version;
        }

        private int ParseVersionPart(string part)
        {
            // Extract numeric portion
            var numericPart = "";
            foreach (var c in part)
            {
                if (char.IsDigit(c))
                    numericPart += c;
                else
                    break;
            }
            
            return int.TryParse(numericPart, out var num) ? num : 0;
        }

        /// <summary>
        /// Updates all packages in a project (npm update)
        /// </summary>
        public async Task<NpmUpdateResult> UpdateProjectAsync(string projectPath)
        {
            var result = new NpmUpdateResult
            {
                PackageName = "Project Update",
                OldVersion = "Current",
                NewVersion = "Updated"
            };
            
            try
            {
                // Run npm update in the project directory
                // This respects semver in package.json and updates package-lock.json
                var (success, output, error) = await RunNpmCommandAsync("npm update", projectPath);
                
                if (success)
                {
                    result.Success = true;
                    // Try to parse something meaningful or just use "Success"
                    result.NewVersion = "Completed"; 
                    result.Details = output;
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = error;
                    result.Details = $"Command: npm update\nError: {error}";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            
            return result;
        }

        private async Task<(bool success, string output, string error)> RunNpmCommandAsync(string command, string? workingDirectory = null)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }
            
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            return (process.ExitCode == 0, output, error);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }
}
