using System;
using System.IO;
using Microsoft.Win32;

namespace OPTools.Registry;

public static class StartupManager
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "OPTools";

    public static bool IsStartupEnabled()
    {
        try
        {
            using (RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
            {
                if (key != null)
                {
                    object? value = key.GetValue(AppName);
                    return value != null && !string.IsNullOrEmpty(value.ToString());
                }
            }
        }
        catch
        {
            // Return false if read fails
        }
        return false;
    }

    public static bool EnableStartup()
    {
        try
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath) || !exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                exePath = currentProcess.MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath) && exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    exePath = Path.ChangeExtension(exePath, ".exe");
                }
            }

            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                return false;
            }

            using (RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true))
            {
                if (key != null)
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                    return true;
                }
            }
        }
        catch
        {
            // Return false if write fails
        }
        return false;
    }

    public static bool DisableStartup()
    {
        try
        {
            using (RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true))
            {
                if (key != null)
                {
                    object? value = key.GetValue(AppName);
                    if (value != null)
                    {
                        key.DeleteValue(AppName, false);
                        return true;
                    }
                }
            }
        }
        catch
        {
            // Return false if delete fails
        }
        return false;
    }
}

