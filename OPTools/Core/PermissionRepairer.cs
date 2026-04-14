using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using OPTools.Utils;

namespace OPTools.Core;

public sealed class PermissionRepairResult
{
    public bool Success { get; set; }
    public bool OwnershipUpdated { get; set; }
    public bool AccessGranted { get; set; }
    public bool AttributesCleared { get; set; }
    public int ProcessedItems { get; set; }
    public List<string> Errors { get; } = new();
}

public sealed class PermissionRepairer
{
    private readonly string _targetPath;

    public PermissionRepairer(string targetPath)
    {
        _targetPath = PathHelper.NormalizePath(targetPath);
    }

    public PermissionRepairResult Repair()
    {
        PermissionRepairResult result = new PermissionRepairResult();

        if (!PathHelper.IsValidPath(_targetPath))
        {
            result.Errors.Add("Invalid path specified");
            return result;
        }

        if (!File.Exists(_targetPath) && !Directory.Exists(_targetPath))
        {
            result.Errors.Add("Path does not exist");
            return result;
        }

        WindowsApi.EnablePrivilege(WindowsApi.SE_BACKUP_NAME);
        WindowsApi.EnablePrivilege(WindowsApi.SE_RESTORE_NAME);
        WindowsApi.EnablePrivilege(WindowsApi.SE_TAKE_OWNERSHIP_NAME);

        WindowsIdentity identity = WindowsIdentity.GetCurrent();
        SecurityIdentifier? currentUserSid = identity.User;

        if (currentUserSid == null)
        {
            result.Errors.Add("Could not determine current user SID");
            return result;
        }

        IEnumerable<string> paths = EnumerateTargetPaths();

        foreach (string path in paths)
        {
            try
            {
                bool isDirectory = Directory.Exists(path);
                ClearAttributes(path, isDirectory, result);
                UpdateOwner(path, isDirectory, currentUserSid, result);
                GrantFullControl(path, isDirectory, currentUserSid, result);
                result.ProcessedItems++;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{path}: {ex.Message}");
            }
        }

        result.Success = result.Errors.Count == 0;
        return result;
    }

    private IEnumerable<string> EnumerateTargetPaths()
    {
        yield return _targetPath;

        if (!Directory.Exists(_targetPath))
        {
            yield break;
        }

        foreach (string directory in Directory.GetDirectories(_targetPath, "*", SearchOption.AllDirectories))
        {
            yield return directory;
        }

        foreach (string file in Directory.GetFiles(_targetPath, "*", SearchOption.AllDirectories))
        {
            yield return file;
        }
    }

    private static void ClearAttributes(string path, bool isDirectory, PermissionRepairResult result)
    {
        FileAttributes attributes = isDirectory
            ? new DirectoryInfo(path).Attributes
            : File.GetAttributes(path);

        FileAttributes clearedAttributes = attributes & ~FileAttributes.ReadOnly;

        if (isDirectory)
        {
            new DirectoryInfo(path).Attributes = clearedAttributes;
        }
        else
        {
            File.SetAttributes(path, clearedAttributes);
        }

        result.AttributesCleared = true;
    }

    private static void UpdateOwner(string path, bool isDirectory, SecurityIdentifier currentUserSid, PermissionRepairResult result)
    {
        if (isDirectory)
        {
            DirectoryInfo directory = new DirectoryInfo(path);
            DirectorySecurity security = directory.GetAccessControl(AccessControlSections.Owner);
            security.SetOwner(currentUserSid);
            directory.SetAccessControl(security);
        }
        else
        {
            FileInfo file = new FileInfo(path);
            FileSecurity security = file.GetAccessControl(AccessControlSections.Owner);
            security.SetOwner(currentUserSid);
            file.SetAccessControl(security);
        }

        result.OwnershipUpdated = true;
    }

    private static void GrantFullControl(string path, bool isDirectory, SecurityIdentifier currentUserSid, PermissionRepairResult result)
    {
        FileSystemAccessRule rule = isDirectory
            ? new FileSystemAccessRule(
                currentUserSid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow)
            : new FileSystemAccessRule(
                currentUserSid,
                FileSystemRights.FullControl,
                AccessControlType.Allow);

        if (isDirectory)
        {
            DirectoryInfo directory = new DirectoryInfo(path);
            DirectorySecurity security = directory.GetAccessControl(AccessControlSections.Access);
            security.AddAccessRule(rule);
            directory.SetAccessControl(security);
        }
        else
        {
            FileInfo file = new FileInfo(path);
            FileSecurity security = file.GetAccessControl(AccessControlSections.Access);
            security.AddAccessRule(rule);
            file.SetAccessControl(security);
        }

        result.AccessGranted = true;
    }
}
