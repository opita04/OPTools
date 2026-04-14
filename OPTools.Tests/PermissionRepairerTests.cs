using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using OPTools.Core;
using Xunit;

namespace OPTools.Tests;

public sealed class PermissionRepairerTests : IDisposable
{
    private readonly string _tempRoot;

    public PermissionRepairerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "OPToolsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void Repair_ShouldClearReadOnlyAndGrantCurrentUserAccess_ForFile()
    {
        string filePath = Path.Combine(_tempRoot, "locked.txt");
        File.WriteAllText(filePath, "test");
        File.SetAttributes(filePath, FileAttributes.ReadOnly);

        PermissionRepairer repairer = new PermissionRepairer(filePath);

        PermissionRepairResult result = repairer.Repair();

        Assert.True(result.Success);
        Assert.True(result.AttributesCleared);
        Assert.True(result.AccessGranted);
        Assert.True(result.OwnershipUpdated);
        Assert.False(File.GetAttributes(filePath).HasFlag(FileAttributes.ReadOnly));

        WindowsIdentity currentUser = WindowsIdentity.GetCurrent();
        FileSecurity security = new FileInfo(filePath).GetAccessControl();
        AuthorizationRuleCollection rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));

        bool hasFullControl = rules
            .OfType<FileSystemAccessRule>()
            .Any(rule =>
                Equals(rule.IdentityReference, currentUser.User) &&
                rule.AccessControlType == AccessControlType.Allow &&
                rule.FileSystemRights.HasFlag(FileSystemRights.FullControl));

        Assert.True(hasFullControl);
    }

    [Fact]
    public void Repair_ShouldProcessDirectoriesRecursively()
    {
        string childDirectory = Path.Combine(_tempRoot, "child");
        string childFile = Path.Combine(childDirectory, "child.txt");

        Directory.CreateDirectory(childDirectory);
        File.WriteAllText(childFile, "content");
        new DirectoryInfo(childDirectory).Attributes |= FileAttributes.ReadOnly;
        File.SetAttributes(childFile, FileAttributes.ReadOnly);

        PermissionRepairer repairer = new PermissionRepairer(_tempRoot);

        PermissionRepairResult result = repairer.Repair();

        Assert.True(result.Success);
        Assert.True(result.ProcessedItems >= 3);
        Assert.False(new DirectoryInfo(childDirectory).Attributes.HasFlag(FileAttributes.ReadOnly));
        Assert.False(File.GetAttributes(childFile).HasFlag(FileAttributes.ReadOnly));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, true);
        }
    }
}
