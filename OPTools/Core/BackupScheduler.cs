using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace OPTools.Core;

/// <summary>
/// Manages scheduled backup jobs and executes them based on their schedules
/// </summary>
public class BackupScheduler : IDisposable
{
    private readonly List<BackupJob> _jobs = new();
    private readonly Timer _checkTimer;
    private readonly object _lock = new();
    private bool _isRunning;
    private CancellationTokenSource? _cancellationSource;

    public event EventHandler<BackupJobEventArgs>? JobStarted;
    public event EventHandler<BackupJobCompletedEventArgs>? JobCompleted;
    public event EventHandler<BackupProgressEventArgs>? ProgressChanged;
    public event EventHandler? JobsChanged;

    public IReadOnlyList<BackupJob> Jobs => _jobs.AsReadOnly();
    public bool IsRunning => _isRunning;

    public BackupScheduler()
    {
        // Check every minute for jobs that need to run
        _checkTimer = new Timer(60000);
        _checkTimer.Elapsed += CheckTimer_Elapsed;
        _checkTimer.AutoReset = true;
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        _checkTimer.Start();
        
        // Check immediately on start
        Task.Run(() => CheckAndRunDueJobs());
    }

    public void Stop()
    {
        _isRunning = false;
        _checkTimer.Stop();
        _cancellationSource?.Cancel();
    }

    public void AddJob(BackupJob job)
    {
        lock (_lock)
        {
            _jobs.Add(job);
            JobsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void UpdateJob(BackupJob job)
    {
        lock (_lock)
        {
            var index = _jobs.FindIndex(j => j.Id == job.Id);
            if (index >= 0)
            {
                _jobs[index] = job;
                JobsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void RemoveJob(string jobId)
    {
        lock (_lock)
        {
            _jobs.RemoveAll(j => j.Id == jobId);
            JobsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public BackupJob? GetJob(string jobId)
    {
        lock (_lock)
        {
            return _jobs.FirstOrDefault(j => j.Id == jobId);
        }
    }

    public void LoadJobs(IEnumerable<BackupJob> jobs)
    {
        lock (_lock)
        {
            _jobs.Clear();
            _jobs.AddRange(jobs);
            JobsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void CheckTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        CheckAndRunDueJobs();
    }

    private void CheckAndRunDueJobs()
    {
        if (!_isRunning) return;

        List<BackupJob> jobsToRun;
        lock (_lock)
        {
            var now = DateTime.Now;
            jobsToRun = _jobs
                .Where(j => j.IsEnabled && ShouldRunJob(j, now))
                .ToList();
        }

        foreach (var job in jobsToRun)
        {
            Task.Run(() => RunJobAsync(job));
        }
    }

    private bool ShouldRunJob(BackupJob job, DateTime now)
    {
        if (!job.LastRunTime.HasValue)
            return true;

        var nextRun = job.GetNextRunTime(job.LastRunTime.Value);
        return now >= nextRun;
    }

    /// <summary>
    /// Run a backup job immediately (manual trigger)
    /// </summary>
    public async Task<BackupResult> RunJobNowAsync(string jobId)
    {
        var job = GetJob(jobId);
        if (job == null)
        {
            return new BackupResult
            {
                Success = false,
                Message = "Job not found"
            };
        }

        return await RunJobAsync(job);
    }

    private async Task<BackupResult> RunJobAsync(BackupJob job)
    {
        var result = new BackupResult
        {
            StartTime = DateTime.Now
        };

        try
        {
            JobStarted?.Invoke(this, new BackupJobEventArgs(job));

            _cancellationSource = new CancellationTokenSource();
            var token = _cancellationSource.Token;

            // Validate paths
            if (!Path.Exists(job.SourcePath))
            {
                result.Success = false;
                result.Message = $"Source path does not exist: {job.SourcePath}";
                result.EndTime = DateTime.Now;
                UpdateJobResult(job, result);
                return result;
            }

            // Create destination folder with timestamp if configured
            string destFolder = job.DestinationPath;
            if (job.UseTimestampFolder)
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                destFolder = Path.Combine(job.DestinationPath, $"backup_{timestamp}");
            }

            Directory.CreateDirectory(destFolder);

            // Determine if source is file or directory
            var attrs = File.GetAttributes(job.SourcePath);
            bool isDirectory = (attrs & FileAttributes.Directory) == FileAttributes.Directory;

            if (isDirectory)
            {
                await CopyDirectoryAsync(job, job.SourcePath, destFolder, result, token);
            }
            else
            {
                await CopyFileAsync(job, job.SourcePath, destFolder, result, token);
            }

            // Cleanup old versions if configured
            if (job.UseTimestampFolder && job.KeepVersions > 0)
            {
                CleanupOldBackups(job.DestinationPath, job.KeepVersions);
            }

            result.Success = result.FilesFailed == 0;
            result.Message = result.Success
                ? $"Backup completed: {result.FilesCopied} files copied ({FormatBytes(result.BytesCopied)})"
                : $"Backup completed with errors: {result.FilesFailed} files failed";
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.Message = "Backup was cancelled";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Backup failed: {ex.Message}";
        }
        finally
        {
            result.EndTime = DateTime.Now;
            UpdateJobResult(job, result);
            JobCompleted?.Invoke(this, new BackupJobCompletedEventArgs(job, result));
            _cancellationSource?.Dispose();
            _cancellationSource = null;
        }

        return result;
    }

    private async Task CopyDirectoryAsync(BackupJob job, string sourceDir, string destDir, BackupResult result, CancellationToken token)
    {
        var searchOption = job.IncludeSubfolders 
            ? SearchOption.AllDirectories 
            : SearchOption.TopDirectoryOnly;

        var files = Directory.GetFiles(sourceDir, "*", searchOption);
        var totalFiles = files.Length;
        var processedFiles = 0;

        foreach (var sourceFile in files)
        {
            token.ThrowIfCancellationRequested();

            // Calculate relative path and destination
            var relativePath = Path.GetRelativePath(sourceDir, sourceFile);
            var destFile = Path.Combine(destDir, relativePath);

            // Ensure destination directory exists
            var destFileDir = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(destFileDir))
            {
                Directory.CreateDirectory(destFileDir);
            }

            try
            {
                await Task.Run(() => File.Copy(sourceFile, destFile, true), token);
                result.FilesCopied++;
                result.BytesCopied += new FileInfo(sourceFile).Length;
            }
            catch (Exception)
            {
                result.FilesFailed++;
            }

            processedFiles++;
            var progress = (int)((processedFiles / (double)totalFiles) * 100);
            ProgressChanged?.Invoke(this, new BackupProgressEventArgs(job, progress, processedFiles, totalFiles));
        }
    }

    private async Task CopyFileAsync(BackupJob job, string sourceFile, string destDir, BackupResult result, CancellationToken token)
    {
        var destFile = Path.Combine(destDir, Path.GetFileName(sourceFile));

        try
        {
            await Task.Run(() => File.Copy(sourceFile, destFile, true), token);
            result.FilesCopied++;
            result.BytesCopied += new FileInfo(sourceFile).Length;
            ProgressChanged?.Invoke(this, new BackupProgressEventArgs(job, 100, 1, 1));
        }
        catch (Exception)
        {
            result.FilesFailed++;
        }
    }

    private void CleanupOldBackups(string backupRoot, int keepCount)
    {
        try
        {
            var backupDirs = Directory.GetDirectories(backupRoot, "backup_*")
                .Select(d => new DirectoryInfo(d))
                .OrderByDescending(d => d.CreationTime)
                .Skip(keepCount)
                .ToList();

            foreach (var dir in backupDirs)
            {
                try
                {
                    dir.Delete(true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private void UpdateJobResult(BackupJob job, BackupResult result)
    {
        lock (_lock)
        {
            job.LastRunTime = result.StartTime;
            job.LastRunSuccess = result.Success;
            job.LastRunMessage = result.Message;
            JobsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double size = bytes;
        while (size >= 1024 && i < suffixes.Length - 1)
        {
            size /= 1024;
            i++;
        }
        return $"{size:0.##} {suffixes[i]}";
    }

    public void Dispose()
    {
        Stop();
        _checkTimer.Dispose();
        _cancellationSource?.Dispose();
    }
}

public class BackupJobEventArgs : EventArgs
{
    public BackupJob Job { get; }
    public BackupJobEventArgs(BackupJob job) => Job = job;
}

public class BackupJobCompletedEventArgs : EventArgs
{
    public BackupJob Job { get; }
    public BackupResult Result { get; }
    public BackupJobCompletedEventArgs(BackupJob job, BackupResult result)
    {
        Job = job;
        Result = result;
    }
}

public class BackupProgressEventArgs : EventArgs
{
    public BackupJob Job { get; }
    public int ProgressPercent { get; }
    public int FilesProcessed { get; }
    public int TotalFiles { get; }

    public BackupProgressEventArgs(BackupJob job, int progress, int processed, int total)
    {
        Job = job;
        ProgressPercent = progress;
        FilesProcessed = processed;
        TotalFiles = total;
    }
}
