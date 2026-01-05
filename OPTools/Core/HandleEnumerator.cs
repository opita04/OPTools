using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using OPTools.Utils;

namespace OPTools.Core;

public class LockInfo
{
    public uint ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public IntPtr Handle { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string HandleType { get; set; } = string.Empty;
}

public class HandleEnumerator
{
    private readonly string _targetPath;
    private readonly bool _isDirectory;

    private const uint INITIAL_BUFFER_SIZE = 0x10000;
    private const uint BUFFER_INCREMENT = 0x1000;
    private const uint OBJECT_NAME_BUFFER_SIZE = 0x1000;
    private const int MAX_BUFFER_RETRIES = 5;

    public HandleEnumerator(string targetPath)
    {
        _targetPath = PathHelper.NormalizePath(targetPath);
        _isDirectory = Directory.Exists(_targetPath);
    }

    public List<LockInfo> EnumerateLocks()
    {
        List<LockInfo> locks = new List<LockInfo>();

        if (!PathHelper.IsValidPath(_targetPath))
        {
            return locks;
        }

        // Use Restart Manager API - the official Windows mechanism for file lock detection
        try
        {
            List<string> filesToCheck = new List<string>();

            if (_isDirectory)
            {
                // For directories, we need to check all files within
                try
                {
                    filesToCheck.AddRange(Directory.GetFiles(_targetPath, "*", SearchOption.AllDirectories));
                }
                catch
                {
                    // If we can't enumerate, just check the directory itself
                    filesToCheck.Add(_targetPath);
                }
            }
            else
            {
                filesToCheck.Add(_targetPath);
            }

            // Check each file using Restart Manager
            foreach (string file in filesToCheck)
            {
                var fileLocks = GetLockingProcesses(file);
                locks.AddRange(fileLocks);
            }
        }
        catch
        {
            // Fall back to the old method if Restart Manager fails
            locks = EnumerateLocksLegacy();
        }

        return locks.DistinctBy(l => new { l.ProcessId, l.FilePath }).ToList();
    }

    private List<LockInfo> GetLockingProcesses(string filePath)
    {
        List<LockInfo> locks = new List<LockInfo>();

        uint sessionHandle;
        string sessionKey = Guid.NewGuid().ToString();

        int result = WindowsApi.RmStartSession(out sessionHandle, 0, sessionKey);
        if (result != 0)
        {
            return locks;
        }

        try
        {
            string[] resources = new string[] { filePath };
            result = WindowsApi.RmRegisterResources(sessionHandle, (uint)resources.Length, resources, 0, null, 0, null);

            if (result != 0)
            {
                return locks;
            }

            uint pnProcInfoNeeded = 0;
            uint pnProcInfo = 0;
            uint lpdwRebootReasons = 0;

            // First call to get the count
            result = WindowsApi.RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, null, out lpdwRebootReasons);

            if (result == WindowsApi.ERROR_MORE_DATA && pnProcInfoNeeded > 0)
            {
                WindowsApi.RM_PROCESS_INFO[] processInfo = new WindowsApi.RM_PROCESS_INFO[pnProcInfoNeeded];
                pnProcInfo = pnProcInfoNeeded;

                result = WindowsApi.RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, processInfo, out lpdwRebootReasons);

                if (result == 0)
                {
                    for (int i = 0; i < pnProcInfo; i++)
                    {
                        try
                        {
                            locks.Add(new LockInfo
                            {
                                ProcessId = (uint)processInfo[i].Process.dwProcessId,
                                ProcessName = processInfo[i].strAppName,
                                Handle = IntPtr.Zero,
                                FilePath = filePath,
                                HandleType = processInfo[i].ApplicationType.ToString()
                            });
                        }
                        catch(Exception ex) { System.Diagnostics.Debug.WriteLine($"Error processing process info: {ex.Message}"); }
                    }
                }
            }
        }
        finally
        {
            WindowsApi.RmEndSession(sessionHandle);
        }

        return locks;
    }

    private List<LockInfo> EnumerateLocksLegacy()
    {
        List<LockInfo> locks = new List<LockInfo>();

        WindowsApi.EnablePrivilege(WindowsApi.SE_DEBUG_NAME);
        WindowsApi.EnablePrivilege(WindowsApi.SE_BACKUP_NAME);

        uint bufferSize = INITIAL_BUFFER_SIZE;
        IntPtr buffer = IntPtr.Zero;

        try
        {
            int retryCount = 0;
            while (retryCount < MAX_BUFFER_RETRIES)
            {
                retryCount++;
                buffer = Marshal.AllocHGlobal((int)bufferSize);
                uint returnLength = 0;

                uint status = WindowsApi.NtQuerySystemInformation(
                    WindowsApi.SYSTEM_INFORMATION_CLASS.SystemHandleInformation,
                    buffer,
                    bufferSize,
                    out returnLength);

                if (status == 0)
                {
                    break;
                }

                if (status == WindowsApi.STATUS_INFO_LENGTH_MISMATCH)
                {
                    Marshal.FreeHGlobal(buffer);
                    buffer = IntPtr.Zero;
                    bufferSize = returnLength + BUFFER_INCREMENT;
                }
                else
                {
                    Marshal.FreeHGlobal(buffer);
                    return locks;
                }
            }

            if (retryCount >= MAX_BUFFER_RETRIES)
            {
                System.Diagnostics.Debug.WriteLine("EnumerateLocksLegacy: Max buffer retries exceeded.");
                return locks;
            }

            WindowsApi.SYSTEM_HANDLE_INFORMATION handleInfo = Marshal.PtrToStructure<WindowsApi.SYSTEM_HANDLE_INFORMATION>(buffer);
            IntPtr handlePtr = new IntPtr(buffer.ToInt64() + Marshal.SizeOf(typeof(uint)));

            for (uint i = 0; i < handleInfo.HandleCount; i++)
            {
                WindowsApi.SYSTEM_HANDLE handle = Marshal.PtrToStructure<WindowsApi.SYSTEM_HANDLE>(handlePtr);

                try
                {
                    string? filePath = GetFilePathFromHandle(handle);
                    if (filePath != null && IsPathLocked(filePath))
                    {
                        string processName = GetProcessName(handle.ProcessId);
                        if (!string.IsNullOrEmpty(processName))
                        {
                            locks.Add(new LockInfo
                            {
                                ProcessId = handle.ProcessId,
                                ProcessName = processName,
                                Handle = new IntPtr(handle.Handle),
                                FilePath = filePath,
                                HandleType = "File"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing handle: {ex.Message}");
                }

                handlePtr = new IntPtr(handlePtr.ToInt64() + Marshal.SizeOf(typeof(WindowsApi.SYSTEM_HANDLE)));
            }
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return locks;
    }

    private bool IsPathLocked(string filePath)
    {
        try
        {
            string normalizedPath = PathHelper.NormalizePath(filePath);
            string normalizedTarget = PathHelper.NormalizePath(_targetPath);

            if (PathHelper.PathMatches(normalizedPath, normalizedTarget))
            {
                return true;
            }

            if (_isDirectory)
            {
                return normalizedPath.StartsWith(normalizedTarget + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                       normalizedPath.StartsWith(normalizedTarget + System.IO.Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private string? GetFilePathFromHandle(WindowsApi.SYSTEM_HANDLE handle)
    {
        IntPtr processHandle = IntPtr.Zero;
        IntPtr duplicateHandle = IntPtr.Zero;

        try
        {
            processHandle = WindowsApi.OpenProcess(
                WindowsApi.PROCESS_DUP_HANDLE | WindowsApi.PROCESS_QUERY_INFORMATION,
                false,
                handle.ProcessId);

            if (processHandle == IntPtr.Zero)
            {
                return null;
            }

            IntPtr currentProcess = Process.GetCurrentProcess().Handle;
            uint status = WindowsApi.NtDuplicateObject(
                processHandle,
                new IntPtr(handle.Handle),
                currentProcess,
                out duplicateHandle,
                0,
                0,
                WindowsApi.DUPLICATE_SAME_ACCESS);

            if (status != 0 || duplicateHandle == IntPtr.Zero)
            {
                return null;
            }

            return GetObjectName(duplicateHandle);
        }
        catch
        {
            return null;
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

    private string? GetObjectName(IntPtr handle)
    {
        uint bufferSize = OBJECT_NAME_BUFFER_SIZE;
        IntPtr buffer = IntPtr.Zero;

        try
        {
            buffer = Marshal.AllocHGlobal((int)bufferSize);
            uint returnLength = 0;

            uint status = WindowsApi.NtQueryObject(
                handle,
                WindowsApi.OBJECT_INFORMATION_CLASS.ObjectNameInformation,
                buffer,
                bufferSize,
                out returnLength);

            if (status == 0 && returnLength > 0)
            {
                WindowsApi.UNICODE_STRING unicodeString = Marshal.PtrToStructure<WindowsApi.UNICODE_STRING>(buffer);
                if (unicodeString.Buffer != IntPtr.Zero && unicodeString.Length > 0)
                {
                    byte[] bytes = new byte[unicodeString.Length];
                    Marshal.Copy(unicodeString.Buffer, bytes, 0, (int)unicodeString.Length);
                    string path = Encoding.Unicode.GetString(bytes);
                    return PathHelper.ConvertDosDevicePathToNormalPath(path);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error querying object: {ex.Message}");
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return null;
    }

    private string GetProcessName(uint processId)
    {
        try
        {
            Process? process = Process.GetProcessById((int)processId);
            return process?.ProcessName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}

