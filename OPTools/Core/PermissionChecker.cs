using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace OPTools.Core;

/// <summary>
/// Checks file/folder permissions to diagnose access issues
/// </summary>
public static class PermissionChecker
{
    /// <summary>
    /// Check if the current user has permission to delete the specified path
    /// </summary>
    public static bool HasDeletePermission(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return HasFileDeletePermission(path);
            }
            else if (Directory.Exists(path))
            {
                return HasDirectoryDeletePermission(path);
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get detailed permission diagnostics for a path
    /// </summary>
    public static PermissionDiagnostics GetDiagnostics(string path)
    {
        var diagnostics = new PermissionDiagnostics { Path = path };

        try
        {
            bool isFile = File.Exists(path);
            bool isDirectory = Directory.Exists(path);

            if (!isFile && !isDirectory)
            {
                diagnostics.Exists = false;
                diagnostics.Issues.Add("Path does not exist");
                return diagnostics;
            }

            diagnostics.Exists = true;
            diagnostics.IsFile = isFile;

            // Check attributes
            FileAttributes attrs = isFile 
                ? File.GetAttributes(path) 
                : new DirectoryInfo(path).Attributes;

            if ((attrs & FileAttributes.ReadOnly) != 0)
            {
                diagnostics.IsReadOnly = true;
                diagnostics.Issues.Add("Marked as Read-Only");
            }
            if ((attrs & FileAttributes.System) != 0)
            {
                diagnostics.IsSystem = true;
                diagnostics.Issues.Add("System file/folder");
            }
            if ((attrs & FileAttributes.Hidden) != 0)
            {
                diagnostics.IsHidden = true;
                diagnostics.Issues.Add("Hidden file/folder");
            }

            // Check ACL permissions
            try
            {
                if (isFile)
                {
                    var security = new FileSecurity(path, AccessControlSections.Access);
                    CheckAccessRules(security, diagnostics);
                }
                else
                {
                    var security = new DirectorySecurity(path, AccessControlSections.Access);
                    CheckAccessRules(security, diagnostics);
                }
            }
            catch (UnauthorizedAccessException)
            {
                diagnostics.CanReadPermissions = false;
                diagnostics.Issues.Add("Cannot read security permissions (access denied)");
            }
            catch (Exception ex)
            {
                diagnostics.Issues.Add($"Error reading permissions: {ex.Message}");
            }

            // Check ownership
            try
            {
                var security = isFile 
                    ? (FileSystemSecurity)new FileSecurity(path, AccessControlSections.Owner)
                    : new DirectorySecurity(path, AccessControlSections.Owner);
                
                var owner = security.GetOwner(typeof(NTAccount));
                diagnostics.Owner = owner?.ToString() ?? "Unknown";
                
                var currentUser = WindowsIdentity.GetCurrent().Name;
                diagnostics.CurrentUser = currentUser;
                diagnostics.IsOwner = string.Equals(owner?.ToString(), currentUser, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                diagnostics.Owner = "Unable to determine";
            }

            // Final permission check
            diagnostics.HasDeletePermission = HasDeletePermission(path);
            if (!diagnostics.HasDeletePermission && diagnostics.Issues.Count == 0)
            {
                diagnostics.Issues.Add("Delete permission denied by ACL");
            }
        }
        catch (Exception ex)
        {
            diagnostics.Issues.Add($"Diagnostic error: {ex.Message}");
        }

        return diagnostics;
    }

    private static bool HasFileDeletePermission(string path)
    {
        try
        {
            var security = new FileSecurity(path, AccessControlSections.Access);
            var currentUser = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(currentUser);

            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));
            
            foreach (FileSystemAccessRule rule in rules)
            {
                if (currentUser.User?.Equals(rule.IdentityReference) == true ||
                    principal.IsInRole((SecurityIdentifier)rule.IdentityReference))
                {
                    if ((rule.FileSystemRights & FileSystemRights.Delete) != 0)
                    {
                        if (rule.AccessControlType == AccessControlType.Allow)
                            return true;
                        if (rule.AccessControlType == AccessControlType.Deny)
                            return false;
                    }
                }
            }
            
            // If no explicit rule, try write access as a proxy
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool HasDirectoryDeletePermission(string path)
    {
        try
        {
            var security = new DirectorySecurity(path, AccessControlSections.Access);
            var currentUser = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(currentUser);

            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));
            
            foreach (FileSystemAccessRule rule in rules)
            {
                if (currentUser.User?.Equals(rule.IdentityReference) == true ||
                    principal.IsInRole((SecurityIdentifier)rule.IdentityReference))
                {
                    if ((rule.FileSystemRights & FileSystemRights.Delete) != 0)
                    {
                        if (rule.AccessControlType == AccessControlType.Allow)
                            return true;
                        if (rule.AccessControlType == AccessControlType.Deny)
                            return false;
                    }
                }
            }
            
            return true; // Assume allowed if no explicit deny
        }
        catch
        {
            return false;
        }
    }

    private static void CheckAccessRules(FileSystemSecurity security, PermissionDiagnostics diagnostics)
    {
        diagnostics.CanReadPermissions = true;
        var currentUser = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(currentUser);

        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));
        
        foreach (FileSystemAccessRule rule in rules)
        {
            bool appliesToUser = currentUser.User?.Equals(rule.IdentityReference) == true ||
                                  principal.IsInRole((SecurityIdentifier)rule.IdentityReference);

            if (appliesToUser)
            {
                if (rule.AccessControlType == AccessControlType.Deny)
                {
                    if ((rule.FileSystemRights & FileSystemRights.Delete) != 0)
                    {
                        diagnostics.Issues.Add("Explicit DENY on Delete permission");
                        diagnostics.HasDeletePermission = false;
                    }
                    if ((rule.FileSystemRights & FileSystemRights.Write) != 0)
                    {
                        diagnostics.Issues.Add("Explicit DENY on Write permission");
                    }
                }
            }
        }
    }
}

/// <summary>
/// Detailed permission diagnostics for a file or folder
/// </summary>
public class PermissionDiagnostics
{
    public string Path { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public bool IsFile { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsSystem { get; set; }
    public bool IsHidden { get; set; }
    public bool CanReadPermissions { get; set; } = true;
    public bool HasDeletePermission { get; set; }
    public bool IsOwner { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string CurrentUser { get; set; } = string.Empty;
    public List<string> Issues { get; set; } = new List<string>();

    public string GetSummary()
    {
        if (Issues.Count == 0)
            return "No permission issues detected";
        
        return string.Join("\n• ", Issues);
    }
}
