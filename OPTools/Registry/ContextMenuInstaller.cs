using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace OPTools.Registry;

public static class ContextMenuInstaller
{
    private const string MenuName = "Delete with OPTools";
    private const string RegistryKeyPath = @"*\shell\OPTools";
    private const string RegistryKeyPathDirectory = @"Directory\shell\OPTools";

    public static bool Install()
    {
        try
        {
            string exePath = GetExecutablePath();

            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                throw new FileNotFoundException($"Executable not found. Tried: {exePath}", exePath);
            }
            
            // Ensure the path is fully qualified and normalized
            exePath = Path.GetFullPath(exePath);

            using (RegistryKey? key = Microsoft.Win32.Registry.ClassesRoot.CreateSubKey(RegistryKeyPath))
            {
                if (key != null)
                {
                    key.SetValue("", MenuName);
                    key.SetValue("Icon", exePath);

                    using (RegistryKey? commandKey = key.CreateSubKey("command"))
                    {
                        if (commandKey != null)
                        {
                            commandKey.SetValue("", $"\"{exePath}\" \"%1\"");
                        }
                    }
                }
            }

            using (RegistryKey? key = Microsoft.Win32.Registry.ClassesRoot.CreateSubKey(RegistryKeyPathDirectory))
            {
                if (key != null)
                {
                    key.SetValue("", MenuName);
                    key.SetValue("Icon", exePath);

                    using (RegistryKey? commandKey = key.CreateSubKey("command"))
                    {
                        if (commandKey != null)
                        {
                            commandKey.SetValue("", $"\"{exePath}\" \"%1\"");
                        }
                    }
                }
            }

            return true;
        }
        catch (SecurityException)
        {
            throw new UnauthorizedAccessException("Administrator privileges required to install context menu.");
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to install context menu: {ex.Message}", ex);
        }
    }

    public static bool Uninstall()
    {
        try
        {
            Microsoft.Win32.Registry.ClassesRoot.DeleteSubKeyTree(RegistryKeyPath, false);
            Microsoft.Win32.Registry.ClassesRoot.DeleteSubKeyTree(RegistryKeyPathDirectory, false);
            return true;
        }
        catch (SecurityException)
        {
            throw new UnauthorizedAccessException("Administrator privileges required to uninstall context menu.");
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool IsInstalled()
    {
        try
        {
            using (RegistryKey? key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(RegistryKeyPath))
            {
                return key != null;
            }
        }
        catch
        {
            return false;
        }
    }

    private static string GetExecutablePath()
    {
        // Try multiple methods to get the executable path, preferring .exe over .dll
        
        // 1. Environment.ProcessPath (most reliable for single-file published apps)
        string? exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
        {
            // If it's a .dll, try changing to .exe
            if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                string exeCandidate = Path.ChangeExtension(exePath, ".exe");
                if (File.Exists(exeCandidate))
                {
                    return exeCandidate;
                }
            }
            return exePath;
        }

        // 2. Process.MainModule.FileName
        try
        {
            Process currentProcess = Process.GetCurrentProcess();
            exePath = currentProcess.MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                // If it's a .dll, try changing to .exe
                if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    string exeCandidate = Path.ChangeExtension(exePath, ".exe");
                    if (File.Exists(exeCandidate))
                    {
                        return exeCandidate;
                    }
                }
                if (File.Exists(exePath))
                {
                    return exePath;
                }
            }
        }
        catch { }

        // 3. Look for OPTools.exe in the same directory as the running process
        try
        {
            Process currentProcess = Process.GetCurrentProcess();
            string? processPath = currentProcess.MainModule?.FileName;
            if (!string.IsNullOrEmpty(processPath))
            {
                string? dir = Path.GetDirectoryName(processPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    string candidate = Path.Combine(dir, "OPTools.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }
        catch { }

        // 4. Try AppContext.BaseDirectory
        try
        {
            string? baseDir = AppContext.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir))
            {
                string candidate = Path.Combine(baseDir, "OPTools.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        catch { }

        // 5. Try Assembly.Location (last resort, may not work in single-file)
        try
        {
            exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(exePath))
            {
                if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    string exeCandidate = Path.ChangeExtension(exePath, ".exe");
                    if (File.Exists(exeCandidate))
                    {
                        return exeCandidate;
                    }
                }
                if (File.Exists(exePath))
                {
                    return exePath;
                }
            }
        }
        catch { }

        return string.Empty;
    }
}

