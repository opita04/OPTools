using System;
using Microsoft.Win32;

namespace OPTools.Utils;

public static class SettingsManager
{
    private const string RegistryKeyPath = @"Software\OPTools";
    private const string StartOnStartupKey = "StartOnStartup";
    private const string MinimizeToTrayKey = "MinimizeToTray";

    public static bool GetStartOnStartup()
    {
        try
        {
            using (RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
            {
                if (key != null)
                {
                    object? value = key.GetValue(StartOnStartupKey);
                    if (value is int intValue)
                        return intValue != 0;
                    if (value is bool boolValue)
                        return boolValue;
                }
            }
        }
        catch (Exception ex)
        {
            // Return default if read fails
            System.Diagnostics.Debug.WriteLine($"Settings read error: {ex.Message}");
        }
        return false;
    }

    public static void SetStartOnStartup(bool value)
    {
        try
        {
            using (RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RegistryKeyPath))
            {
                if (key != null)
                {
                    key.SetValue(StartOnStartupKey, value ? 1 : 0, RegistryValueKind.DWord);
                }
            }
        }
        catch (Exception ex)
        {
            // Silently fail if write fails
            System.Diagnostics.Debug.WriteLine($"Settings write error: {ex.Message}");
        }
    }

    public static bool GetMinimizeToTray()
    {
        try
        {
            using (RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
            {
                if (key != null)
                {
                    object? value = key.GetValue(MinimizeToTrayKey);
                    if (value is int intValue)
                        return intValue != 0;
                    if (value is bool boolValue)
                        return boolValue;
                }
            }
        }
        catch (Exception ex)
        {
            // Return default if read fails
            System.Diagnostics.Debug.WriteLine($"Settings read error: {ex.Message}");
        }
        return false;
    }

    public static void SetMinimizeToTray(bool value)
    {
        try
        {
            using (RegistryKey? key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RegistryKeyPath))
            {
                if (key != null)
                {
                    key.SetValue(MinimizeToTrayKey, value ? 1 : 0, RegistryValueKind.DWord);
                }
            }
        }
        catch (Exception ex)
        {
            // Silently fail if write fails
            System.Diagnostics.Debug.WriteLine($"Settings write error: {ex.Message}");
        }
    }
}

