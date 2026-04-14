using System;
using System.IO;
using OPTools.Core;
using Xunit;

namespace OPTools.Tests;

public sealed class HandleEnumeratorTests
{
    [Fact]
    public void ShouldUseRestartManager_ReturnsFalseForDirectoryTargets()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            HandleEnumerator enumerator = new HandleEnumerator(tempDirectory);

            Assert.False(enumerator.ShouldUseRestartManager());
        }
        finally
        {
            Directory.Delete(tempDirectory);
        }
    }

    [Fact]
    public void ShouldUseRestartManager_ReturnsTrueForFileTargets()
    {
        string tempFile = Path.GetTempFileName();

        try
        {
            HandleEnumerator enumerator = new HandleEnumerator(tempFile);

            Assert.True(enumerator.ShouldUseRestartManager());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
