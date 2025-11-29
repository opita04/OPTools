using System;
using System.Diagnostics;
using OPTools.Utils;

namespace OPTools.Core;

public class ProcessManager
{
    public static bool KillProcess(uint processId)
    {
        IntPtr processHandle = IntPtr.Zero;

        try
        {
            processHandle = WindowsApi.OpenProcess(
                WindowsApi.PROCESS_TERMINATE,
                false,
                processId);

            if (processHandle == IntPtr.Zero)
            {
                return false;
            }

            return WindowsApi.TerminateProcess(processHandle, 0);
        }
        catch
        {
            return false;
        }
        finally
        {
            if (processHandle != IntPtr.Zero)
            {
                WindowsApi.CloseHandle(processHandle);
            }
        }
    }

    public static bool KillProcess(string processName)
    {
        try
        {
            Process[] processes = Process.GetProcessesByName(processName);
            bool success = true;

            foreach (Process process in processes)
            {
                try
                {
                    if (!KillProcess((uint)process.Id))
                    {
                        success = false;
                    }
                }
                catch
                {
                    success = false;
                }
            }

            return success;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsSystemProcess(uint processId)
    {
        try
        {
            Process? process = Process.GetProcessById((int)processId);
            if (process == null)
                return false;

            string processName = process.ProcessName.ToLowerInvariant();
            string[] systemProcesses = {
                "csrss", "winlogon", "services", "lsass", "svchost",
                "spoolsv", "explorer", "wininit", "dwm", "smss"
            };

            return Array.Exists(systemProcesses, p => p == processName);
        }
        catch
        {
            return false;
        }
    }

    public static string GetProcessPath(uint processId)
    {
        try
        {
            Process? process = Process.GetProcessById((int)processId);
            return process?.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}

