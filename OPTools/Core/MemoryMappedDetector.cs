using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using OPTools.Utils;

namespace OPTools.Core;

/// <summary>
/// Detects memory-mapped file handles (Section objects) that may be blocking file access
/// </summary>
public class MemoryMappedDetector
{
    private readonly string _targetPath;
    private readonly bool _isDirectory;

    // Object type number for Section objects (memory-mapped files)
    // This value can vary between Windows versions, so we detect it dynamically
    private static byte? _sectionTypeNumber;

    public MemoryMappedDetector(string targetPath)
    {
        _targetPath = PathHelper.NormalizePath(targetPath);
        _isDirectory = Directory.Exists(_targetPath);
    }

    /// <summary>
    /// Detect processes that have memory-mapped handles to the target path
    /// </summary>
    public List<LockInfo> DetectMemoryMappedLocks()
    {
        var locks = new List<LockInfo>();

        try
        {
            WindowsApi.EnablePrivilege(WindowsApi.SE_DEBUG_NAME);

            // Get the Section object type number if we haven't already
            if (_sectionTypeNumber == null)
            {
                _sectionTypeNumber = GetSectionTypeNumber();
            }

            if (_sectionTypeNumber == null)
            {
                System.Diagnostics.Debug.WriteLine("Could not determine Section object type number");
                return locks;
            }

            var handles = EnumerateSystemHandles();

            foreach (var handle in handles)
            {
                // Only check Section objects
                if (handle.ObjectTypeNumber != _sectionTypeNumber.Value)
                    continue;

                try
                {
                    string? filePath = GetMappedFilePath(handle);
                    if (filePath != null && IsPathMatch(filePath))
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
                                HandleType = "MemoryMapped"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing Section handle: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Memory-mapped detection failed: {ex.Message}");
        }

        return locks;
    }

    private bool IsPathMatch(string filePath)
    {
        try
        {
            string normalizedPath = PathHelper.NormalizePath(filePath);
            string normalizedTarget = PathHelper.NormalizePath(_targetPath);

            if (PathHelper.PathMatches(normalizedPath, normalizedTarget))
                return true;

            if (_isDirectory)
            {
                return normalizedPath.StartsWith(normalizedTarget + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                       normalizedPath.StartsWith(normalizedTarget + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static List<WindowsApi.SYSTEM_HANDLE> EnumerateSystemHandles()
    {
        var handles = new List<WindowsApi.SYSTEM_HANDLE>();
        uint bufferSize = 0x10000;
        IntPtr buffer = IntPtr.Zero;
        const int MAX_RETRIES = 5;

        try
        {
            int retryCount = 0;
            while (retryCount < MAX_RETRIES)
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
                    bufferSize = returnLength + 0x1000;
                }
                else
                {
                    Marshal.FreeHGlobal(buffer);
                    return handles;
                }
            }

            if (buffer == IntPtr.Zero)
                return handles;

            var handleInfo = Marshal.PtrToStructure<WindowsApi.SYSTEM_HANDLE_INFORMATION>(buffer);
            IntPtr handlePtr = new IntPtr(buffer.ToInt64() + Marshal.SizeOf(typeof(uint)));

            for (uint i = 0; i < handleInfo.HandleCount; i++)
            {
                var handle = Marshal.PtrToStructure<WindowsApi.SYSTEM_HANDLE>(handlePtr);
                handles.Add(handle);
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

        return handles;
    }

    private static byte? GetSectionTypeNumber()
    {
        // Find the object type number for Section objects by examining handles
        // in the current process where we can reliably query the type
        var handles = EnumerateSystemHandles();
        uint currentPid = (uint)Process.GetCurrentProcess().Id;

        foreach (var handle in handles)
        {
            if (handle.ProcessId != currentPid)
                continue;

            IntPtr duplicateHandle = IntPtr.Zero;
            try
            {
                IntPtr currentProcess = Process.GetCurrentProcess().Handle;
                uint status = WindowsApi.NtDuplicateObject(
                    currentProcess,
                    new IntPtr(handle.Handle),
                    currentProcess,
                    out duplicateHandle,
                    0,
                    0,
                    WindowsApi.DUPLICATE_SAME_ACCESS);

                if (status != 0 || duplicateHandle == IntPtr.Zero)
                    continue;

                string? typeName = GetObjectTypeName(duplicateHandle);
                if (typeName == "Section")
                {
                    return handle.ObjectTypeNumber;
                }
            }
            catch
            {
                continue;
            }
            finally
            {
                if (duplicateHandle != IntPtr.Zero)
                {
                    WindowsApi.CloseHandle(duplicateHandle);
                }
            }
        }

        // Fallback: Try common values (these vary by Windows version)
        // Windows 10/11 typically uses values in range 40-45 for Section
        return null;
    }

    private static string? GetObjectTypeName(IntPtr handle)
    {
        uint bufferSize = 0x1000;
        IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);

        try
        {
            uint returnLength = 0;
            uint status = WindowsApi.NtQueryObject(
                handle,
                WindowsApi.OBJECT_INFORMATION_CLASS.ObjectTypeInformation,
                buffer,
                bufferSize,
                out returnLength);

            if (status == 0)
            {
                // The type name is at offset 0x60 on x64, read as UNICODE_STRING
                var unicodeString = Marshal.PtrToStructure<WindowsApi.UNICODE_STRING>(buffer);
                if (unicodeString.Buffer != IntPtr.Zero && unicodeString.Length > 0)
                {
                    byte[] bytes = new byte[unicodeString.Length];
                    Marshal.Copy(unicodeString.Buffer, bytes, 0, unicodeString.Length);
                    return Encoding.Unicode.GetString(bytes);
                }
            }
        }
        catch
        {
            // Ignore
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return null;
    }

    private static string? GetMappedFilePath(WindowsApi.SYSTEM_HANDLE handle)
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
                return null;

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
                return null;

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

    private static string? GetObjectName(IntPtr handle)
    {
        uint bufferSize = 0x1000;
        IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);

        try
        {
            uint returnLength = 0;
            uint status = WindowsApi.NtQueryObject(
                handle,
                WindowsApi.OBJECT_INFORMATION_CLASS.ObjectNameInformation,
                buffer,
                bufferSize,
                out returnLength);

            if (status == 0 && returnLength > 0)
            {
                var unicodeString = Marshal.PtrToStructure<WindowsApi.UNICODE_STRING>(buffer);
                if (unicodeString.Buffer != IntPtr.Zero && unicodeString.Length > 0)
                {
                    byte[] bytes = new byte[unicodeString.Length];
                    Marshal.Copy(unicodeString.Buffer, bytes, 0, unicodeString.Length);
                    string path = Encoding.Unicode.GetString(bytes);
                    return PathHelper.ConvertDosDevicePathToNormalPath(path);
                }
            }
        }
        catch
        {
            // Ignore
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return null;
    }

    private static string GetProcessName(uint processId)
    {
        try
        {
            var process = Process.GetProcessById((int)processId);
            return process?.ProcessName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
