using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OPTools.Core;
using Xunit;

namespace OPTools.Tests;

public sealed class UnlockWorkflowTests
{
    [Fact]
    public void Delete_ShouldBlockDangerousPathBeforeRunningOperations()
    {
        FakeUnlockWorkflowOperations operations = new FakeUnlockWorkflowOperations();
        UnlockWorkflow workflow = new UnlockWorkflow(
            @"C:\Windows\System32\locked.txt",
            new UnlockWorkflowOptions(),
            operations);

        UnlockWorkflowResult result = workflow.Delete();

        Assert.False(result.Success);
        Assert.Contains("protected path", string.Join(" ", result.Errors), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, operations.UnlockCalls);
        Assert.Equal(0, operations.DeleteCalls);
        Assert.Equal(0, operations.RepairCalls);
    }

    [Fact]
    public void Delete_ShouldRepairPermissionsAndRetryOnce()
    {
        string targetPath = CreateTempFile();
        FakeUnlockWorkflowOperations operations = new FakeUnlockWorkflowOperations
        {
            Diagnostics = new PermissionDiagnostics
            {
                Exists = true,
                HasDeletePermission = false
            },
            DeleteFailures = new Queue<Exception?>(new Exception?[]
            {
                new UnauthorizedAccessException("Access denied"),
                null
            }),
            RepairResult = new PermissionRepairResult { Success = true }
        };

        UnlockWorkflow workflow = new UnlockWorkflow(
            targetPath,
            new UnlockWorkflowOptions(),
            operations);

        try
        {
            UnlockWorkflowResult result = workflow.Delete();

            Assert.True(result.Success);
            Assert.True(result.PermissionRepairAttempted);
            Assert.True(result.PermissionRepairSucceeded);
            Assert.Equal(1, operations.RepairCalls);
            Assert.Equal(2, operations.DeleteCalls);
        }
        finally
        {
            TryDelete(targetPath);
        }
    }

    [Fact]
    public void Delete_ShouldKillNonSystemLocksAndRetry()
    {
        string targetPath = CreateTempFile();
        FakeUnlockWorkflowOperations operations = new FakeUnlockWorkflowOperations
        {
            Diagnostics = new PermissionDiagnostics
            {
                Exists = true,
                HasDeletePermission = true
            },
            DeleteFailures = new Queue<Exception?>(new Exception?[]
            {
                new IOException("Locked"),
                null
            }),
            LocksByCall = new Queue<List<LockInfo>>(new[]
            {
                new List<LockInfo>
                {
                    new LockInfo { ProcessId = 123, ProcessName = "editor", IsSystemProcess = false }
                },
                new List<LockInfo>()
            })
        };

        UnlockWorkflow workflow = new UnlockWorkflow(
            targetPath,
            new UnlockWorkflowOptions(),
            operations);

        try
        {
            UnlockWorkflowResult result = workflow.Delete();

            Assert.True(result.Success);
            Assert.Equal(1, result.KilledProcesses);
            Assert.Contains((uint)123, operations.KilledProcesses);
            Assert.Equal(2, operations.DeleteCalls);
        }
        finally
        {
            TryDelete(targetPath);
        }
    }

    [Fact]
    public void Delete_ShouldNotAutoKillSystemLocksInSilentMode()
    {
        string targetPath = CreateTempFile();
        FakeUnlockWorkflowOperations operations = new FakeUnlockWorkflowOperations
        {
            Diagnostics = new PermissionDiagnostics
            {
                Exists = true,
                HasDeletePermission = true
            },
            DeleteFailures = new Queue<Exception?>(new Exception?[]
            {
                new IOException("Locked"),
                new IOException("Still locked")
            }),
            LocksByCall = new Queue<List<LockInfo>>(new[]
            {
                new List<LockInfo>
                {
                    new LockInfo { ProcessId = 4, ProcessName = "system", IsSystemProcess = true }
                }
            }),
            ScheduleDeleteOnRebootResult = true
        };

        UnlockWorkflow workflow = new UnlockWorkflow(
            targetPath,
            new UnlockWorkflowOptions(),
            operations);

        try
        {
            UnlockWorkflowResult result = workflow.Delete(autoScheduleOnReboot: true);

            Assert.False(result.Success);
            Assert.Empty(operations.KilledProcesses);
            Assert.True(result.ScheduledOnReboot);
            Assert.Contains(result.Errors, error => error.Contains("interactive confirmation", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            TryDelete(targetPath);
        }
    }

    [Fact]
    public void Delete_ShouldScheduleOnRebootOnlyAfterRetriesFail()
    {
        string targetPath = CreateTempFile();
        FakeUnlockWorkflowOperations operations = new FakeUnlockWorkflowOperations
        {
            Diagnostics = new PermissionDiagnostics
            {
                Exists = true,
                HasDeletePermission = true
            },
            DeleteFailures = new Queue<Exception?>(new Exception?[]
            {
                new IOException("Still locked")
            }),
            ScheduleDeleteOnRebootResult = true
        };

        UnlockWorkflow workflow = new UnlockWorkflow(
            targetPath,
            new UnlockWorkflowOptions(),
            operations);

        try
        {
            UnlockWorkflowResult result = workflow.Delete(autoScheduleOnReboot: true);

            Assert.False(result.Success);
            Assert.True(result.ScheduledOnReboot);
            Assert.Equal(1, operations.ScheduleCalls);
            Assert.Equal(1, operations.DeleteCalls);
        }
        finally
        {
            TryDelete(targetPath);
        }
    }

    private static string CreateTempFile()
    {
        string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string filePath = Path.Combine(repoRoot, "temp_test", "unlock-workflow", Guid.NewGuid().ToString("N"), "locked.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, "content");
        return filePath;
    }

    private static void TryDelete(string filePath)
    {
        try
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
        catch
        {
        }
    }
}

internal sealed class FakeUnlockWorkflowOperations : IUnlockWorkflowOperations
{
    public PermissionDiagnostics Diagnostics { get; set; } = new PermissionDiagnostics
    {
        Exists = true,
        HasDeletePermission = true
    };

    public PermissionRepairResult RepairResult { get; set; } = new PermissionRepairResult { Success = true };
    public Queue<Exception?> DeleteFailures { get; set; } = new();
    public Queue<List<LockInfo>> LocksByCall { get; set; } = new();
    public bool ScheduleDeleteOnRebootResult { get; set; }
    public int UnlockCalls { get; private set; }
    public int DeleteCalls { get; private set; }
    public int RepairCalls { get; private set; }
    public int ScheduleCalls { get; private set; }
    public List<uint> KilledProcesses { get; } = new();

    public UnlockResult UnlockAll(bool killProcesses, IProgress<string>? progress)
    {
        UnlockCalls++;
        return new UnlockResult { Success = true };
    }

    public List<LockInfo> GetLocks(IProgress<string>? progress)
    {
        if (LocksByCall.Count == 0)
        {
            return new List<LockInfo>();
        }

        return LocksByCall.Dequeue();
    }

    public PermissionDiagnostics GetDiagnostics() => Diagnostics;

    public PermissionRepairResult RepairPermissions()
    {
        RepairCalls++;
        return RepairResult;
    }

    public void DeleteCore()
    {
        DeleteCalls++;

        if (DeleteFailures.Count == 0)
        {
            return;
        }

        Exception? failure = DeleteFailures.Dequeue();
        if (failure != null)
        {
            throw failure;
        }
    }

    public void MoveCore(string destinationPath)
    {
        throw new NotImplementedException();
    }

    public bool ScheduleDeleteOnReboot()
    {
        ScheduleCalls++;
        return ScheduleDeleteOnRebootResult;
    }

    public bool KillProcess(uint processId)
    {
        KilledProcesses.Add(processId);
        return true;
    }

    public bool IsSystemProcess(uint processId)
    {
        return LocksByCall.SelectMany(locks => locks).Any(lockInfo => lockInfo.ProcessId == processId && lockInfo.IsSystemProcess);
    }
}
