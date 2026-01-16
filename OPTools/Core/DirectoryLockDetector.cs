using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using OPTools.Utils;

namespace OPTools.Core;

/// <summary>
/// Detects processes that have a directory as their current working directory (CWD)
/// This is a common cause of "phantom" directory locks that other methods can't detect
/// </summary>
public class DirectoryLockDetector
{
    private readonly string _targetPath;

    // Windows API for getting process CWD
    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref PROCESS_BASIC_INFORMATION processInformation,
        int processInformationLength,
        out int returnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_BASIC_INFORMATION
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniqueProcessId;
        public IntPtr Reserved3;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UNICODE_STRING
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        byte[] lpBuffer,
        int dwSize,
        out int lpNumberOfBytesRead);

    public DirectoryLockDetector(string targetPath)
    {
        _targetPath = PathHelper.NormalizePath(targetPath);
    }

    /// <summary>
    /// Find processes that have the target directory (or a subdirectory) as their CWD
    /// </summary>
    public List<LockInfo> DetectDirectoryLocks()
    {
        var locks = new List<LockInfo>();

        try
        {
            // Method 1: Use WMI to query process command lines and current directory
            locks.AddRange(DetectViaWmi());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WMI directory lock detection failed: {ex.Message}");
        }

        try
        {
            // Method 2: Check for processes with handles to the directory
            locks.AddRange(DetectViaProcessHandles());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Process handle directory detection failed: {ex.Message}");
        }

        return locks;
    }

    private List<LockInfo> DetectViaWmi()
    {
        var locks = new List<LockInfo>();
        
        try
        {
            // Use WMI to find processes - this can sometimes reveal CWD info
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, Name, CommandLine, ExecutablePath FROM Win32_Process");
            
            foreach (ManagementObject process in searcher.Get())
            {
                try
                {
                    string? commandLine = process["CommandLine"]?.ToString();
                    string? execPath = process["ExecutablePath"]?.ToString();
                    string? name = process["Name"]?.ToString();
                    uint pid = Convert.ToUInt32(process["ProcessId"]);

                    // Check if target path appears in command line (common for node, git, etc.)
                    if (!string.IsNullOrEmpty(commandLine) && 
                        commandLine.IndexOf(_targetPath, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        locks.Add(new LockInfo
                        {
                            ProcessId = pid,
                            ProcessName = name ?? "Unknown",
                            Handle = IntPtr.Zero,
                            FilePath = _targetPath,
                            HandleType = "WorkingDir"
                        });
                    }
                }
                catch
                {
                    // Skip processes we can't query
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WMI query failed: {ex.Message}");
        }

        return locks;
    }

    private List<LockInfo> DetectViaProcessHandles()
    {
        var locks = new List<LockInfo>();
        
        try
        {
            WindowsApi.EnablePrivilege(WindowsApi.SE_DEBUG_NAME);

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    // Try to get the process's current directory by reading PEB
                    string? cwd = GetProcessCurrentDirectory(process);
                    
                    if (!string.IsNullOrEmpty(cwd))
                    {
                        string normalizedCwd = PathHelper.NormalizePath(cwd);
                        string normalizedTarget = PathHelper.NormalizePath(_targetPath);

                        // Check if CWD matches or is within target path
                        if (string.Equals(normalizedCwd, normalizedTarget, StringComparison.OrdinalIgnoreCase) ||
                            normalizedCwd.StartsWith(normalizedTarget + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        {
                            locks.Add(new LockInfo
                            {
                                ProcessId = (uint)process.Id,
                                ProcessName = process.ProcessName,
                                Handle = IntPtr.Zero,
                                FilePath = cwd,
                                HandleType = "CurrentDir"
                            });
                        }
                    }
                }
                catch
                {
                    // Skip processes we can't access
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Process handle detection failed: {ex.Message}");
        }

        return locks;
    }

    private static string? GetProcessCurrentDirectory(Process process)
    {
        try
        {
            IntPtr processHandle = WindowsApi.OpenProcess(
                WindowsApi.PROCESS_QUERY_INFORMATION | 0x0010, // PROCESS_VM_READ
                false,
                (uint)process.Id);

            if (processHandle == IntPtr.Zero)
                return null;

            try
            {
                // Get PEB address
                PROCESS_BASIC_INFORMATION pbi = new PROCESS_BASIC_INFORMATION();
                int returnLength;
                int status = NtQueryInformationProcess(processHandle, 0, ref pbi, 
                    Marshal.SizeOf(pbi), out returnLength);

                if (status != 0 || pbi.PebBaseAddress == IntPtr.Zero)
                    return null;

                // Read RTL_USER_PROCESS_PARAMETERS pointer from PEB
                // Offset 0x20 on x64 for ProcessParameters pointer
                int paramsOffset = IntPtr.Size == 8 ? 0x20 : 0x10;
                byte[] buffer = new byte[IntPtr.Size];
                int bytesRead;

                if (!ReadProcessMemory(processHandle, 
                    IntPtr.Add(pbi.PebBaseAddress, paramsOffset), 
                    buffer, buffer.Length, out bytesRead))
                    return null;

                IntPtr processParams = IntPtr.Size == 8 
                    ? new IntPtr(BitConverter.ToInt64(buffer, 0))
                    : new IntPtr(BitConverter.ToInt32(buffer, 0));

                if (processParams == IntPtr.Zero)
                    return null;

                // Read CurrentDirectory.DosPath UNICODE_STRING
                // Offset 0x38 on x64, 0x24 on x86
                int cwdOffset = IntPtr.Size == 8 ? 0x38 : 0x24;
                byte[] unicodeStringBuffer = new byte[Marshal.SizeOf(typeof(UNICODE_STRING))];

                if (!ReadProcessMemory(processHandle, 
                    IntPtr.Add(processParams, cwdOffset), 
                    unicodeStringBuffer, unicodeStringBuffer.Length, out bytesRead))
                    return null;

                // Parse UNICODE_STRING
                ushort length = BitConverter.ToUInt16(unicodeStringBuffer, 0);
                IntPtr stringBuffer = IntPtr.Size == 8
                    ? new IntPtr(BitConverter.ToInt64(unicodeStringBuffer, 8))
                    : new IntPtr(BitConverter.ToInt32(unicodeStringBuffer, 4));

                if (length == 0 || stringBuffer == IntPtr.Zero)
                    return null;

                // Read the actual string
                byte[] pathBuffer = new byte[length];
                if (!ReadProcessMemory(processHandle, stringBuffer, pathBuffer, length, out bytesRead))
                    return null;

                string path = Encoding.Unicode.GetString(pathBuffer).TrimEnd('\0');
                
                // Remove trailing backslash for consistency
                if (path.EndsWith("\\") && path.Length > 3)
                    path = path.TrimEnd('\\');

                return path;
            }
            finally
            {
                WindowsApi.CloseHandle(processHandle);
            }
        }
        catch
        {
            return null;
        }
    }
}
