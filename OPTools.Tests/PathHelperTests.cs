using Xunit;
using OPTools.Utils;

namespace OPTools.Tests;

public class PathHelperTests
{
    [Theory]
    [InlineData("C:\\Windows\\System32\\driver.sys", true)]
    [InlineData("C:\\Program Files\\App\\file.exe", true)]
    [InlineData("C:\\Users\\User\\Desktop\\file.txt", true)] // Users folder is protected by IsDangerousPath logic
    [InlineData("D:\\Projects\\MyApp\\file.cs", false)]
    [InlineData("C:\\", true)] // Drive root is dangerous
    [InlineData("C:\\Windows\\..\\Windows", true)] // Path traversal simulation (if normalized)
    [InlineData("c:\\windows", true)] // Case insensitive
    public void IsDangerousPath_ShouldIdentifySystemPaths(string path, bool expected)
    {
        bool result = PathHelper.IsDangerousPath(path);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void IsDangerousPath_ShouldHandleInvalidInput(string? path)
    {
        // Empty/null paths should not crash, should return false (not dangerous, just invalid)
        // Wait, logic says: if whitespace return false.
        bool result = PathHelper.IsDangerousPath(path!);
        Assert.False(result);
    }

    [Theory]
    [InlineData("C:\\ValidFolder\\file.txt", true)]
    [InlineData("relative\\path", true)] // Relative paths resolve to absolute (via GetFullPath) and are valid
    public void IsValidPath_ShouldValidatePathStructure(string path, bool expected)
    {
        bool result = PathHelper.IsValidPath(path);
        Assert.Equal(expected, result);
    }
}
