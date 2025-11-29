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

    public FileUnlocker(string targetPath)
    {
        _targetPath = PathHelper.NormalizePath(targetPath);
        _enumerator = new HandleEnumerator(_targetPath);
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

    public bool DeleteFileOrFolder()
    {
        if (!PathHelper.IsValidPath(_targetPath))
        {
            return false;
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
                File.SetAttributes(_targetPath, FileAttributes.Normal);
                File.Delete(_targetPath);
                return true;
            }
            else if (Directory.Exists(_targetPath))
            {
                DeleteDirectoryRecursive(_targetPath);
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private void DeleteDirectoryRecursive(string directoryPath)
    {
        try
        {
            DirectoryInfo directory = new DirectoryInfo(directoryPath);

            foreach (FileInfo file in directory.GetFiles())
            {
                file.Attributes = FileAttributes.Normal;
                file.Delete();
            }

            foreach (DirectoryInfo subDirectory in directory.GetDirectories())
            {
                DeleteDirectoryRecursive(subDirectory.FullName);
            }

            directory.Attributes = FileAttributes.Normal;
            Directory.Delete(directoryPath);
        }
        catch
        {
            try
            {
                UnlockResult result = UnlockAll(true);
                Thread.Sleep(200);

                DirectoryInfo directory = new DirectoryInfo(directoryPath);
                directory.Attributes = FileAttributes.Normal;
                Directory.Delete(directoryPath, true);
            }
            catch
            {
            }
        }
    }

    public List<LockInfo> GetLocks()
    {
        return _enumerator.EnumerateLocks();
    }
}

