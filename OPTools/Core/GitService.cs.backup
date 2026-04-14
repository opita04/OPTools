using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

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
        /// Gets the remote commit hash for the current branch
        /// </summary>
        public async Task<string?> GetRemoteCommitHashAsync(string path)
        {
            // First fetch to ensure we have latest
            await RunGitCommandAsync(path, "fetch");
            
            // Get current branch
            var branch = await RunGitCommandAsync(path, "rev-parse --abbrev-ref HEAD");
            branch = branch?.Trim();
            
            if (string.IsNullOrEmpty(branch) || branch == "HEAD") return null;

            // Get remote ref for this branch (assuming origin)
            var result = await RunGitCommandAsync(path, $"rev-parse origin/{branch}");
            return result?.Trim();
        }

        /// <summary>
        /// Checks if there are updates available (behind remote)
        /// </summary>
        public async Task<(bool UpdateAvailable, string? Local, string? Remote)> CheckForUpdatesAsync(string path)
        {
            if (!IsGitRepository(path)) return (false, null, null);

            var local = await GetLocalCommitHashAsync(path);
            if (local == null) return (false, null, null);

            var remote = await GetRemoteCommitHashAsync(path);
            if (remote == null) return (false, local, null);

            return (local != remote, local, remote);
        }

        /// <summary>
        /// Pulls the latest changes
        /// </summary>
        public async Task<bool> PullAsync(string path, IProgress<string>? progress = null)
        {
            progress?.Report("Executing git pull...");
            try 
            {
                var output = await RunGitCommandAsync(path, "pull");
                progress?.Report(output ?? "Pull complete");
                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"Pull failed: {ex.Message}");
                return false;
            }
        }

        private async Task<string?> RunGitCommandAsync(string workingDir, string arguments)
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
                    // Log error if needed, but for now just return null or throw?
                    // Some commands like status might exit non-zero? No, status usually 0.
                    // rev-parse exits non-zero if not a repo.
                    Debug.WriteLine($"Git command failed: {arguments}. Error: {error}");
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
