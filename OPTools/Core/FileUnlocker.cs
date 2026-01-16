using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using OPTools.Utils;

namespace OPTools.Core;

public class UnlockResult
{
    public bool Success { get; set; }
    public int UnlockedHandles { get; set; }
    public int KilledProcesses { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
}

public class FileUnlocker
{
    private readonly string _targetPath;
    private readonly HandleEnumerator _enumerator;
    private static readonly HashSet<string> ReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public FileUnlocker(string targetPath)
    {
        _targetPath = PathHelper.NormalizePath(targetPath);
        _enumerator = new HandleEnumerator(_targetPath);
    }

    private static bool IsReservedFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;

        string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        return ReservedNames.Contains(nameWithoutExtension);
    }

    private static bool DeleteReservedFile(string filePath)
    {
        try
        {
            // Use extended-length path prefix to bypass Win32 reserved name parsing
            string extendedPath = @"\\?\" + Path.GetFullPath(filePath);
            File.SetAttributes(extendedPath, FileAttributes.Normal);
            File.Delete(extendedPath);
            System.Diagnostics.Debug.WriteLine($"Deleted reserved file: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete reserved file {filePath}: {ex.Message}");
            return false;
        }
    }

    public UnlockResult UnlockAll(bool killProcesses = false)
    {
        UnlockResult result = new UnlockResult();

        if (!PathHelper.IsValidPath(_targetPath))
        {
            result.Errors.Add("Invalid path specified");
            return result;
        }

        WindowsApi.EnablePrivilege(WindowsApi.SE_DEBUG_NAME);
        WindowsApi.EnablePrivilege(WindowsApi.SE_BACKUP_NAME);

        List<LockInfo> locks = _enumerator.EnumerateLocks();

        if (locks.Count == 0)
        {
            result.Success = true;
            return result;
        }

        HashSet<uint> processedProcesses = new HashSet<uint>();

        foreach (LockInfo lockInfo in locks)
        {
            try
            {
                if (UnlockHandle(lockInfo))
                {
                    result.UnlockedHandles++;
                }
                else if (killProcesses && !processedProcesses.Contains(lockInfo.ProcessId))
                {
                    if (ProcessManager.KillProcess(lockInfo.ProcessId))
                    {
                        result.KilledProcesses++;
                        processedProcesses.Add(lockInfo.ProcessId);
                    }
                    else
                    {
                        result.Errors.Add($"Failed to kill process: {lockInfo.ProcessName} (PID: {lockInfo.ProcessId})");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error processing lock: {ex.Message}");
            }
        }

        Thread.Sleep(100);

        List<LockInfo> remainingLocks = _enumerator.EnumerateLocks();
        result.Success = remainingLocks.Count == 0;

        if (!result.Success && result.Errors.Count == 0)
        {
            result.Errors.Add($"{remainingLocks.Count} handle(s) could not be unlocked");
        }

        return result;
    }

    private bool UnlockHandle(LockInfo lockInfo)
    {
        IntPtr processHandle = IntPtr.Zero;
        IntPtr duplicateHandle = IntPtr.Zero;

        try
        {
            processHandle = WindowsApi.OpenProcess(
                WindowsApi.PROCESS_DUP_HANDLE,
                false,
                lockInfo.ProcessId);

            if (processHandle == IntPtr.Zero)
            {
                return false;
            }

            IntPtr currentProcess = System.Diagnostics.Process.GetCurrentProcess().Handle;

            uint status = WindowsApi.NtDuplicateObject(
                processHandle,
                lockInfo.Handle,
                currentProcess,
                out duplicateHandle,
                0,
                0,
                WindowsApi.DUPLICATE_CLOSE_SOURCE);

            if (status == 0 && duplicateHandle != IntPtr.Zero)
            {
                WindowsApi.CloseHandle(duplicateHandle);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (duplicateHandle != IntPtr.Zero)
            {
                WindowsApi.CloseHandle(duplicateHandle);
            }
            if (processHandle != IntPtr.Zero)
            {
                WindowsApi.CloseHandle(processHandle);
            }
        }
    }

    public void DeleteFileOrFolder()
    {
        if (!PathHelper.IsValidPath(_targetPath))
        {
            throw new ArgumentException("Invalid path specified");
        }

        // Tier 1 Audit Fix: Block protected paths
        if (PathHelper.IsDangerousPath(_targetPath))
        {
            throw new InvalidOperationException($"Cannot delete protected path: {_targetPath}");
        }

        UnlockResult unlockResult = UnlockAll(false);

        if (!unlockResult.Success)
        {
            unlockResult = UnlockAll(true);
        }

        Thread.Sleep(200);

        try
        {
            if (File.Exists(_targetPath))
            {
                // Check if it's a reserved filename and use special deletion method
                if (IsReservedFileName(Path.GetFileName(_targetPath)))
                {
                    DeleteReservedFile(_targetPath);
                }
                else
                {
                    File.SetAttributes(_targetPath, FileAttributes.Normal);
                    File.Delete(_targetPath);
                }
                AuditLogger.LogDelete(_targetPath, true);
            }
            else if (Directory.Exists(_targetPath))
            {
                DeleteDirectoryRecursive(_targetPath);
                AuditLogger.LogDelete(_targetPath, true);
            }
        }
        catch (Exception ex)
        {
            string errorMsg = $"Failed to delete: {ex.Message}";

            try
            {
                var currentLocks = _enumerator.EnumerateLocks();
                if (currentLocks.Count > 0)
                {
                    errorMsg += "\n\nProcesses still holding locks:";
                    foreach (var lockInfo in currentLocks)
                    {
                        errorMsg += $"\n• {lockInfo.ProcessName} (PID: {lockInfo.ProcessId})";
                    }
                }
                else
                {
                    // No locks found but deletion failed - use PermissionChecker for diagnostics
                    var diagnostics = PermissionChecker.GetDiagnostics(_targetPath);
                    
                    if (diagnostics.Issues.Count > 0)
                    {
                        errorMsg += "\n\nPermission/attribute issues:";
                        foreach (var issue in diagnostics.Issues)
                        {
                            errorMsg += $"\n• {issue}";
                        }
                        
                        if (!diagnostics.IsOwner && !string.IsNullOrEmpty(diagnostics.Owner))
                        {
                            errorMsg += $"\n• Owner: {diagnostics.Owner} (you are: {diagnostics.CurrentUser})";
                        }
                    }
                    else
                    {
                        errorMsg += "\n\nNo permission issues detected. Possible causes:";
                        errorMsg += "\n• Antivirus/security software may be blocking access";
                        errorMsg += "\n• Kernel-level lock not detectable by user-mode APIs";
                    }
                    
                    if (ex is UnauthorizedAccessException && !diagnostics.HasDeletePermission)
                    {
                        errorMsg += "\n\n💡 Try running OPTools as Administrator";
                    }
                }
            }
            catch (Exception innerEx)
            {
                System.Diagnostics.Debug.WriteLine($"Error enumerating locks: {innerEx.Message}");
            }

            if (!unlockResult.Success && unlockResult.Errors.Count > 0)
            {
                errorMsg += $"\n\nUnlock errors: {string.Join("; ", unlockResult.Errors)}";
            }
            throw new IOException(errorMsg, ex);
        }
    }

    public void MoveFileOrFolder(string destinationPath)
    {
        if (!PathHelper.IsValidPath(_targetPath))
        {
            throw new ArgumentException("Invalid path specified");
        }

        // Tier 1 Audit Fix: Block protected paths
        if (PathHelper.IsDangerousPath(_targetPath))
        {
            throw new InvalidOperationException($"Cannot move protected path: {_targetPath}");
        }

        UnlockResult unlockResult = UnlockAll(false);

        if (!unlockResult.Success)
        {
            unlockResult = UnlockAll(true);
        }

        Thread.Sleep(200);

        try
        {
            if (File.Exists(_targetPath))
            {
                string destFile = Path.Combine(destinationPath, Path.GetFileName(_targetPath));
                if (File.Exists(destFile))
                {
                     File.Delete(destFile);
                }
                File.Move(_targetPath, destFile);
            }
            else if (Directory.Exists(_targetPath))
            {
                string destDir = Path.Combine(destinationPath, new DirectoryInfo(_targetPath).Name);
                if (Directory.Exists(destDir))
                {
                    // If destination directory exists, we might need to merge or fail.
                    // For simplicity, let's try to move.
                }
                Directory.Move(_targetPath, destDir);
            }
            AuditLogger.LogOperation("MOVE", $"{_targetPath} -> {destinationPath}", true);
        }
        catch (Exception ex)
        {
            string errorMsg = $"Failed to move: {ex.Message}";

            try
            {
                var currentLocks = _enumerator.EnumerateLocks();
                if (currentLocks.Count > 0)
                {
                    errorMsg += "\n\nProcesses still holding locks:";
                    foreach (var lockInfo in currentLocks)
                    {
                        errorMsg += $"\n• {lockInfo.ProcessName} (PID: {lockInfo.ProcessId})";
                    }
                }
            }
            catch (Exception innerEx)
            {
                System.Diagnostics.Debug.WriteLine($"Error enumerating locks: {innerEx.Message}");
            }

            if (!unlockResult.Success && unlockResult.Errors.Count > 0)
            {
                errorMsg += $"\n\nUnlock errors: {string.Join("; ", unlockResult.Errors)}";
            }
            throw new IOException(errorMsg, ex);
        }
    }

    private void DeleteDirectoryRecursive(string directoryPath)
    {
        try
        {
            DirectoryInfo directory = new DirectoryInfo(directoryPath);

            // Priority: Delete reserved files first (NUL, CON, PRN, etc.)
            // These files cannot be deleted using standard Win32 APIs
            foreach (FileInfo file in directory.GetFiles())
            {
                if (IsReservedFileName(file.Name))
                {
                    DeleteReservedFile(file.FullName);
                }
            }

            // Delete remaining normal files
            foreach (FileInfo file in directory.GetFiles())
            {
                file.Attributes = FileAttributes.Normal;
                file.Delete();
            }

            // Recursively delete subdirectories
            foreach (DirectoryInfo subDirectory in directory.GetDirectories())
            {
                DeleteDirectoryRecursive(subDirectory.FullName);
            }

            // Finally delete the directory itself
            directory.Attributes = FileAttributes.Normal;
            Directory.Delete(directoryPath);

            // Verify deletion succeeded
            if (Directory.Exists(directoryPath))
            {
                throw new IOException($"Directory still exists after deletion attempt: {directoryPath}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"First attempt failed: {ex.Message}");

            try
            {
                UnlockResult result = UnlockAll(true);
                Thread.Sleep(200);

                DirectoryInfo directory = new DirectoryInfo(directoryPath);
                directory.Attributes = FileAttributes.Normal;

                // Use Delete with recursive flag to clean up any remaining items
                Directory.Delete(directoryPath, true);

                // Verify deletion succeeded
                if (Directory.Exists(directoryPath))
                {
                    throw new IOException($"Directory still exists after force deletion attempt: {directoryPath}");
                }
            }
            catch (Exception innerEx)
            {
                System.Diagnostics.Debug.WriteLine($"Force deletion failed: {innerEx.Message}");

                // Check what's remaining and provide helpful error message
                string remainingItems = GetRemainingItems(directoryPath);
                string errorMsg = $"Failed to delete directory: {directoryPath}";

                if (!string.IsNullOrEmpty(remainingItems))
                {
                    errorMsg += $"\n\nRemaining items:\n{remainingItems}";
                }

                throw new IOException(errorMsg, innerEx);
            }
        }
    }

    private string GetRemainingItems(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return string.Empty;

        var remaining = new List<string>();
        DirectoryInfo directory = new DirectoryInfo(directoryPath);

        try
        {
            foreach (FileInfo file in directory.GetFiles())
            {
                remaining.Add($"File: {file.Name} ({file.Length} bytes)");
            }

            foreach (DirectoryInfo dir in directory.GetDirectories())
            {
                remaining.Add($"Directory: {dir.Name}");
            }
        }
        catch
        {
            remaining.Add("(Unable to enumerate remaining items)");
        }

        return string.Join("\n", remaining);
    }

    public bool ScheduleDeleteOnReboot()
    {
        if (!PathHelper.IsValidPath(_targetPath))
        {
             throw new ArgumentException("Invalid path specified");
        }

        if (File.Exists(_targetPath))
        {
            return WindowsApi.MoveFileEx(_targetPath, null, WindowsApi.MOVEFILE_DELAY_UNTIL_REBOOT);
        }
        else if (Directory.Exists(_targetPath))
        {
            return ScheduleDirectoryDeleteOnReboot(_targetPath);
        }
        return false;
    }

    private bool ScheduleDirectoryDeleteOnReboot(string dirPath)
    {
        bool success = true;
        try
        {
            foreach (string file in Directory.GetFiles(dirPath))
            {
                if (!WindowsApi.MoveFileEx(file, null, WindowsApi.MOVEFILE_DELAY_UNTIL_REBOOT))
                    success = false;
            }

            foreach (string subDir in Directory.GetDirectories(dirPath))
            {
                if (!ScheduleDirectoryDeleteOnReboot(subDir))
                    success = false;
            }

            if (!WindowsApi.MoveFileEx(dirPath, null, WindowsApi.MOVEFILE_DELAY_UNTIL_REBOOT))
                success = false;
        }
        catch
        {
            return false;
        }
        return success;
    }

    public List<LockInfo> GetLocks()
    {
        return _enumerator.EnumerateLocks();
    }
}

