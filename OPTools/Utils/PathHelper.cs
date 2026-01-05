using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OPTools.Utils;

public static class PathHelper
{
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        try
        {
            path = Path.GetFullPath(path);
            
            if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar))
            {
                path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            return path;
        }
        catch
        {
            return path;
        }
    }

    public static bool IsValidPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            string normalized = NormalizePath(path);
            
            if (normalized.Contains(".."))
                return false;

            if (!Path.IsPathRooted(normalized))
                return false;

            string root = Path.GetPathRoot(normalized) ?? string.Empty;
            if (string.IsNullOrEmpty(root))
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsSystemPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            string normalized = NormalizePath(path).ToLowerInvariant();
            string root = Path.GetPathRoot(normalized) ?? string.Empty;

            string[] systemPaths = {
                Path.Combine(root, "windows"),
                Path.Combine(root, "program files"),
                Path.Combine(root, "program files (x86)"),
                Path.Combine(root, "programdata"),
                Path.Combine(root, "users"), // Protect Users folder generally
                Path.Combine(root, "system32"),
                Path.Combine(root, "syswow64")
            };

            foreach (string systemPath in systemPaths)
            {
                if (normalized.StartsWith(systemPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsDangerousPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            var normalized = NormalizePath(path).ToLowerInvariant();
            
            // Block root of any drive (e.g. "c:\" or "c:")
            // NormalizePath strips trailing slashes, so "C:\" becomes "C:"
            if (normalized.Length <= 3 && normalized.Contains(":")) 
                return true;
            
            // Block user profile root
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).ToLowerInvariant();
            if (normalized.Equals(userProfile, StringComparison.OrdinalIgnoreCase)) 
                return true;
            
            // Re-use system path check
            return IsSystemPath(path);
        }
        catch (Exception ex)
        {
            // If we can't determine safety, assume dangerous
            System.Diagnostics.Debug.WriteLine($"Error checking dangerous path: {ex.Message}");
            return true;
        }
    }

    public static string ConvertDosDevicePathToNormalPath(string dosDevicePath)
    {
        if (string.IsNullOrWhiteSpace(dosDevicePath))
            return dosDevicePath;

        try
        {
            var deviceMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    string driveLetter = drive.Name.TrimEnd('\\'); // "C:"
                    StringBuilder sb = new StringBuilder(256);
                    if (WindowsApi.QueryDosDevice(driveLetter, sb, 256))
                    {
                        string devicePath = sb.ToString();
                        deviceMap[devicePath] = driveLetter;
                    }
                }
                catch { }
            }

            if (dosDevicePath.StartsWith("\\??\\"))
            {
                dosDevicePath = dosDevicePath.Substring(4);
            }
            
            foreach (var kvp in deviceMap)
            {
                if (dosDevicePath.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value + dosDevicePath.Substring(kvp.Key.Length);
                }
            }

            return dosDevicePath;
        }
        catch
        {
            return dosDevicePath;
        }
    }

    public static bool PathMatches(string path1, string path2)
    {
        if (string.IsNullOrWhiteSpace(path1) || string.IsNullOrWhiteSpace(path2))
            return false;

        try
        {
            string normalized1 = NormalizePath(path1).ToLowerInvariant();
            string normalized2 = NormalizePath(path2).ToLowerInvariant();

            return normalized1 == normalized2 || 
                   normalized1.StartsWith(normalized2 + Path.DirectorySeparatorChar) ||
                   normalized2.StartsWith(normalized1 + Path.DirectorySeparatorChar);
        }
        catch
        {
            return false;
        }
    }
}

