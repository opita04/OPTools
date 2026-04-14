using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

namespace OPTools.Core
{
    /// <summary>
    /// Service for handling Git operations
    /// </summary>
    public class GitService
    {
        /// <summary>
        /// Checks if a directory is a git repository
        /// </summary>
        public bool IsGitRepository(string path)
        {
            return Directory.Exists(Path.Combine(path, ".git"));
        }

        /// <summary>
        /// Gets the current local commit hash
        /// </summary>
        public async Task<string?> GetLocalCommitHashAsync(string path)
        {
            var result = await RunGitCommandAsync(path, "rev-parse HEAD");
            return result?.Trim();
        }

        /// <summary>
        /// Gets the remote commit hash for the current branch (without fetching if possible)
        /// </summary>
        public async Task<string?> GetRemoteCommitHashAsync(string path, string? remoteUrl = null, string branch = "HEAD")
        {
            // Use ls-remote to avoid fetching everything
            var target = remoteUrl ?? "origin";
            var result = await RunGitCommandAsync(path, $"ls-remote {target} {branch}");
            if (string.IsNullOrWhiteSpace(result)) return null;
            
            // Output format: <hash>	HEAD
            var parts = result.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : null;
        }

        /// <summary>
        /// Gets the latest local tag
        /// </summary>
        public async Task<string?> GetLatestLocalTagAsync(string path)
        {
            // Get the tag pointing to the current commit, or the most recent reachable tag
            var result = await RunGitCommandAsync(path, "describe --tags --abbrev=0");
            return result?.Trim();
        }

        /// <summary>
        /// Gets the latest remote tag
        /// </summary>
        public async Task<string?> GetLatestRemoteTagAsync(string path, string? remoteUrl = null)
        {
            // List remote tags, sort by version (if possible) or just get the list
            var target = remoteUrl ?? "origin";
            var output = await RunGitCommandAsync(path, $"ls-remote --tags --refs {target}");
            if (string.IsNullOrWhiteSpace(output)) return null;

            // Parse tags
            var tags = new List<string>();
            foreach (var line in output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var refName = parts[1].Trim(); // refs/tags/v1.0.0
                    var tagName = refName.Replace("refs/tags/", "");
                    tags.Add(tagName);
                }
            }

            if (tags.Count == 0) return null;

            // Improved sort for v1.2.3 style
            var versionTags = tags.OrderBy(t => t).ToList(); // Default sort
            
            try
            {
                versionTags.Sort((a, b) => 
                {
                    // Clean versions for comparison
                    var va = a.StartsWith("v") ? a.Substring(1) : a;
                    var vb = b.StartsWith("v") ? b.Substring(1) : b;
                    
                    // Simple numeric/semver-like split comparison
                    var partsA = va.Split('.');
                    var partsB = vb.Split('.');
                    
                    for (int i = 0; i < Math.Max(partsA.Length, partsB.Length); i++)
                    {
                        var pA = i < partsA.Length ? ExtractLeadingNumber(partsA[i]) : 0;
                        var pB = i < partsB.Length ? ExtractLeadingNumber(partsB[i]) : 0;
                        
                        if (pA < pB) return -1;
                        if (pA > pB) return 1;
                    }
                    
                    return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                });
            }
            catch { /* fallback to default list order */ }

            return versionTags.LastOrDefault(); // Ascending sort, so last is newest
        }

        private int ExtractLeadingNumber(string input)
        {
            var numStr = new string(input.TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(numStr, out var n) ? n : 0;
        }

        /// <summary>
        /// Checks if there are updates available based on strategy
        /// </summary>
        public async Task<(bool UpdateAvailable, string? Local, string? Remote)> CheckForUpdatesAsync(string path, string? remoteUrl = null, VersionStrategy strategy = VersionStrategy.Commit, string defaultBranch = "main")
        {
            string? local = null;
            string? remote = null;

            try
            {
                // 1. Get Local Version
                if (strategy == VersionStrategy.File)
                {
                    // Use file-based detection (NPM package.json, etc.)
                    var scanner = new PackageScanner();
                    local = scanner.GetProjectLocalVersion(path, Ecosystem.NPM) ?? 
                            scanner.GetProjectLocalVersion(path, Ecosystem.Python) ??
                            scanner.GetProjectLocalVersion(path, Ecosystem.Cpp);
                }
                
                // If File strategy failed or wasn't used, try Git
                if (string.IsNullOrEmpty(local) && IsGitRepository(path))
                {
                    if (strategy == VersionStrategy.Tag || strategy == VersionStrategy.File)
                    {
                        local = await GetLatestLocalTagAsync(path);
                        if (string.IsNullOrEmpty(local))
                        {
                            local = await GetLocalCommitHashAsync(path);
                        }
                    }
                    else
                    {
                        local = await GetLocalCommitHashAsync(path);
                    }
                }

                // Ultimate fallback for Local: if still null, try file-based detection anyway
                if (string.IsNullOrEmpty(local))
                {
                    var scanner = new PackageScanner();
                    local = scanner.GetProjectLocalVersion(path, Ecosystem.NPM) ?? 
                            scanner.GetProjectLocalVersion(path, Ecosystem.Python) ??
                            scanner.GetProjectLocalVersion(path, Ecosystem.Cpp);
                }

                // 2. Get Remote Version
                // We check remote if we have a URL OR if the local path is a git repo
                if (!string.IsNullOrEmpty(remoteUrl) || IsGitRepository(path))
                {
                    if (strategy == VersionStrategy.Tag || strategy == VersionStrategy.File)
                    {
                        remote = await GetLatestRemoteTagAsync(path, remoteUrl);
                    }
                    
                    // Fallback to commit hash if tag strategy failed or no tags found
                    if (string.IsNullOrEmpty(remote))
                    {
                        // Try default branch first
                        remote = await GetRemoteCommitHashAsync(path, remoteUrl, defaultBranch);
                        
                        // If default branch failed, try "HEAD" as ultimate fallback
                        if (string.IsNullOrEmpty(remote) && defaultBranch != "HEAD")
                        {
                            remote = await GetRemoteCommitHashAsync(path, remoteUrl, "HEAD");
                        }
                    }
                }

                if (local == null && remote == null) return (false, null, null);

                // Update is available if both exist and are different
                bool updateAvailable = local != null && remote != null && local != remote;
                
                return (updateAvailable, local, remote);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CheckForUpdates error: {ex.Message}");
                return (false, local, remote);
            }
        }


        /// <summary>
        /// Pulls the latest changes
        /// </summary>
        public async Task<bool> PullAsync(string path, IProgress<string>? progress = null, string? repoUrl = null)
        {
            try 
            {
                if (!IsGitRepository(path))
                {
                    if (string.IsNullOrEmpty(repoUrl))
                    {
                        progress?.Report("Error: Folder is not a Git repository and no URL provided.");
                        return false;
                    }

                    progress?.Report($"Folder is not a Git repo. Attempting to clone from {repoUrl}...");
                    
                    // If directory is not empty, git clone will fail.
                    // Check if we should try to initialize and pull instead.
                    if (Directory.EnumerateFileSystemEntries(path).Any())
                    {
                        progress?.Report("Directory is not empty. Initializing Git...");
                        await RunGitCommandAsync(path, "init");
                        await RunGitCommandAsync(path, $"remote add origin {repoUrl}");
                    }
                    else
                    {
                        return await CloneAsync(repoUrl, path, progress);
                    }
                }

                progress?.Report("Executing git pull...");
                var output = await RunGitCommandAsync(path, "pull origin");
                
                if (output == null)
                {
                    progress?.Report("Pull failed (see logs). Trying fetch and reset...");
                    await RunGitCommandAsync(path, "fetch --all");
                    output = await RunGitCommandAsync(path, "reset --hard origin/HEAD");
                }

                progress?.Report(output ?? "Update complete");
                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"Update failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clones a repository
        /// </summary>
        public async Task<bool> CloneAsync(string repoUrl, string targetPath, IProgress<string>? progress = null)
        {
            progress?.Report($"Cloning {repoUrl}...");
            try
            {
                // Ensure parent directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                
                var output = await RunGitCommandAsync(Path.GetDirectoryName(targetPath)!, $"clone {repoUrl} \"{Path.GetFileName(targetPath)}\"");
                progress?.Report("Clone complete");
                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"Clone failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Extracts GitHub URL from git remote configuration
        /// </summary>
        public async Task<string?> GetGitHubUrlAsync(string path)
        {
            if (!IsGitRepository(path)) return null;
            
            // Get remote URLs
            var remotes = await RunGitCommandAsync(path, "remote -v");
            if (string.IsNullOrEmpty(remotes)) return null;
            
            // Parse for GitHub URLs (origin usually)
            foreach (var line in remotes.Split('\n'))
            {
                if (line.Trim().StartsWith("origin"))
                {
                    // Extract URL from: origin https://github.com/owner/repo.git (fetch)
                    var parts = line.Split(new[] {' ', '\t'}, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var url = parts[1].Trim();
                        
                        // Convert to HTTPS web URL
                        if (url.StartsWith("git@github.com:"))
                        {
                            url = url.Replace("git@github.com:", "https://github.com/");
                            url = url.Replace(".git", "");
                        }
                        else if (url.StartsWith("https://github.com/"))
                        {
                            url = url.Replace(".git", "");
                        }
                        
                        // Validate it's a GitHub URL
                        if (url.StartsWith("https://github.com/"))
                        {
                            return url;
                        }
                    }
                }
            }
            
            return null;
        }

        public async Task<string?> RunGitCommandAsync(string workingDir, string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    Debug.WriteLine($"Git command failed: {arguments}. Error: {error}");
                    // For ls-remote, error might be auth or net
                    if (arguments.Contains("ls-remote") && error.Contains("fatal")) return null;
                    return null;
                }

                return output;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Git execution failed: {ex.Message}");
                return null;
            }
        }
    }
}
