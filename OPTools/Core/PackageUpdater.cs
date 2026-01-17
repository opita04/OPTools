using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace OPTools.Core
{
    /// <summary>
    /// Handles checking for updates and updating packages across multiple ecosystems
    /// </summary>
    public class PackageUpdater : IDisposable
    {
        private readonly HttpClient _httpClient;
        private bool _disposed;

        public PackageUpdater()
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
        /// Checks multiple packages for updates in parallel
        /// NOTE: Currently only supports checking NPM/Bun packages against the npm registry.
        /// Python and C++ packages are skipped (marked as "Unknown" status).
        /// </summary>
        public async Task<List<(PackageInfo package, string? latestVersion, bool isOutdated, bool notFound)>> CheckForUpdatesAsync(
            IEnumerable<PackageInfo> packages, 
            IProgress<(int current, int total, string packageName)>? progress = null)
        {
            var results = new System.Collections.Concurrent.ConcurrentBag<(PackageInfo, string?, bool, bool)>();
            var packageList = new List<PackageInfo>(packages);
            var total = packageList.Count;
            int current = 0;
            
            // Filter to only packages we can check against npm registry
            // Exclude Python and C++ projects by checking project paths
            var npmPackages = packageList.Where(p => 
            {
                // Explicitly skip non-npm global packages
                if (p.ProjectPath == "__GLOBAL_PYTHON__") return false;
                
                // Include npm and bun global packages
                if (p.ProjectPath == "__GLOBAL__" || p.ProjectPath == "__GLOBAL_BUN__") return true;
                
                // For local projects, check ecosystem (though all may default to NPM due to database)
                // The safest approach is to ONLY check packages we're sure are NPM
                // For now, assume local projects in database are NPM unless we have better metadata
                return p.Ecosystem == Ecosystem.NPM || p.Ecosystem == Ecosystem.Bun;
            }).ToList();
            
            // For non-NPM packages, add them as "not checked" (keep existing state)
            var nonNpmPackages = packageList.Except(npmPackages).ToList();
            foreach (var pkg in nonNpmPackages)
            {
                // Preserve existing outdated state, don't mark as up-to-date
                results.Add((pkg, pkg.LatestVersion, pkg.IsOutdated, pkg.NotFound));
                var newCount = System.Threading.Interlocked.Increment(ref current);
                progress?.Report((newCount, total, $"{pkg.Name} (skipped - {pkg.Ecosystem})"));
            }
            
            // Limit concurrency to avoid overwhelming the registry or network
            using var semaphore = new System.Threading.SemaphoreSlim(10);
            
            var tasks = npmPackages.Select(async package =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var (latestVersion, notFound) = await GetLatestVersionAsync(package.Name);
                    var isOutdated = !notFound && !string.IsNullOrEmpty(latestVersion) && 
                                     CompareVersions(package.Version, latestVersion) < 0;
                    
                    results.Add((package, latestVersion, isOutdated, notFound));
                    
                    var newCount = System.Threading.Interlocked.Increment(ref current);
                    progress?.Report((newCount, total, package.Name));
                }
                finally
                {
                    semaphore.Release();
                }
            });
            
            await Task.WhenAll(tasks);
            
            return results.ToList();
        }

        /// <summary>
        /// Updates a package to a specific version
        /// </summary>
        public async Task<PackageUpdateResult> UpdatePackageAsync(PackageInfo package, string? targetVersion = null)
        {
            var result = new PackageUpdateResult
            {
                PackageName = package.Name,
                OldVersion = package.Version
            };
            
            try
            {
                var version = targetVersion ?? package.LatestVersion ?? "latest";
                string command;
                string? workingDir = package.ProjectPath;
                
                if (package.ProjectPath == "__GLOBAL__")
                {
                     command = $"npm install -g {package.Name}@{version}";
                     workingDir = null;
                }
                else if (package.ProjectPath == "__GLOBAL_BUN__")
                {
                     command = $"bun add -g {package.Name}@{version}";
                     workingDir = null;
                }
                else if (package.ProjectPath == "__GLOBAL_PYTHON__")
                {
                     if (version == "latest")
                        command = $"pip install --upgrade {package.Name}";
                     else
                        command = $"pip install --upgrade {package.Name}=={version}";
                        
                     workingDir = null;
                }
                else
                {
                    // Local project - default to npm for now
                    command = $"npm install {package.Name}@{version}";
                }
                
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
        public async Task<bool> UninstallPackageAsync(PackageInfo package)
        {
            try
            {
                string command;
                string? workingDir = package.ProjectPath;
                
                if (package.ProjectPath == "__GLOBAL__")
                {
                    command = $"npm uninstall -g {package.Name}";
                    workingDir = null;
                }
                else if (package.ProjectPath == "__GLOBAL_BUN__")
                {
                    command = $"bun remove -g {package.Name}";
                    workingDir = null;
                }
                else if (package.ProjectPath == "__GLOBAL_PYTHON__")
                {
                    command = $"pip uninstall -y {package.Name}";
                    workingDir = null;
                }
                else
                {
                    command = $"npm uninstall {package.Name}";
                }
                
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
        public async Task<PackageUpdateResult> UpdateProjectAsync(string projectPath)
        {
            var result = new PackageUpdateResult
            {
                PackageName = "Project Update",
                OldVersion = "Current",
                NewVersion = "Updated"
            };
            
            try
            {
                string command;
                string? workingDir = projectPath;
                
                // Determine the correct update command based on project type
                if (projectPath == "__GLOBAL_BUN__")
                {
                    command = "bun update -g";
                    workingDir = null;
                }
                else if (projectPath == "__GLOBAL__")
                {
                    command = "npm update -g";
                    workingDir = null;
                }
                else if (projectPath == "__GLOBAL_PYTHON__")
                {
                    // Python doesn't have a global "update all" - skip
                    result.Success = false;
                    result.ErrorMessage = "Python global packages must be updated individually.";
                    return result;
                }
                else
                {
                    // Local project - detect package manager from lockfiles
                    command = DetectUpdateCommand(projectPath);
                }
                
                var (success, output, error) = await RunNpmCommandAsync(command, workingDir);
                
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

        private async Task<(bool success, string output, string error)> RunNpmCommandAsync(string command, string? workingDirectory = null, int timeoutSeconds = 300)
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
            
            try
            {
                process.Start();
                
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                
                var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
                var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);
                
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Timeout occurred
                    try { process.Kill(true); } catch { }
                    return (false, "", "Operation timed out after " + timeoutSeconds + " seconds.");
                }

                var output = await stdoutTask;
                var error = await stderrTask;
                
                return (process.ExitCode == 0, output, error);
            }
            catch (Exception ex)
            {
                return (false, "", $"Process error: {ex.Message}");
            }
        }

        /// <summary>
        /// Detects the package manager for a local project and returns the appropriate update command
        /// </summary>
        private string DetectUpdateCommand(string projectPath)
        {
            try
            {
                // Check for bun lockfile first (bun.lockb or bun.lock)
                if (System.IO.File.Exists(System.IO.Path.Combine(projectPath, "bun.lockb")) ||
                    System.IO.File.Exists(System.IO.Path.Combine(projectPath, "bun.lock")))
                {
                    return "bun update";
                }
                
                // Check for yarn lockfile
                if (System.IO.File.Exists(System.IO.Path.Combine(projectPath, "yarn.lock")))
                {
                    return "yarn upgrade";
                }
                
                // Check for pnpm lockfile
                if (System.IO.File.Exists(System.IO.Path.Combine(projectPath, "pnpm-lock.yaml")))
                {
                    return "pnpm update";
                }
                
                // Check for Python projects
                if (System.IO.File.Exists(System.IO.Path.Combine(projectPath, "requirements.txt")))
                {
                    return "pip install -r requirements.txt --upgrade";
                }
                
                if (System.IO.File.Exists(System.IO.Path.Combine(projectPath, "Pipfile")))
                {
                    return "pipenv update";
                }
                
                // Default to npm (check for package-lock.json or package.json)
                return "npm update";
            }
            catch
            {
                // Fallback to npm on any error
                return "npm update";
            }
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
