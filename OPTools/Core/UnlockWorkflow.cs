using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OPTools.Utils;

namespace OPTools.Core;

public sealed class UnlockWorkflowOptions
{
    public bool AutoRepairPermissionsOnAccessDenied { get; set; } = true;
    public bool AutoKillNonSystemProcesses { get; set; } = true;
    public bool AllowSystemProcessTermination { get; set; } = false;
    public bool AllowScheduleOnReboot { get; set; } = true;
}

public sealed class UnlockWorkflowResult
{
    public bool Success { get; set; }
    public bool PermissionRepairAttempted { get; set; }
    public bool PermissionRepairSucceeded { get; set; }
    public int UnlockedHandles { get; set; }
    public int KilledProcesses { get; set; }
    public bool ScheduledOnReboot { get; set; }
    public List<LockInfo> RemainingLocks { get; set; } = new();
    public PermissionDiagnostics? Diagnostics { get; set; }
    public List<string> Errors { get; } = new();
}

internal interface IUnlockWorkflowOperations
{
    UnlockResult UnlockAll(bool killProcesses, IProgress<string>? progress);
    List<LockInfo> GetLocks(IProgress<string>? progress);
    PermissionDiagnostics GetDiagnostics();
    PermissionRepairResult RepairPermissions();
    void DeleteCore();
    void MoveCore(string destinationPath);
    bool ScheduleDeleteOnReboot();
    bool KillProcess(uint processId);
    bool IsSystemProcess(uint processId);
}

internal sealed class DefaultUnlockWorkflowOperations : IUnlockWorkflowOperations
{
    private readonly string _targetPath;
    private readonly FileUnlocker _unlocker;
    private readonly PermissionRepairer _permissionRepairer;

    public DefaultUnlockWorkflowOperations(string targetPath)
    {
        _targetPath = targetPath;
        _unlocker = new FileUnlocker(targetPath);
        _permissionRepairer = new PermissionRepairer(targetPath);
    }

    public UnlockResult UnlockAll(bool killProcesses, IProgress<string>? progress) => _unlocker.UnlockAll(killProcesses, progress);
    public List<LockInfo> GetLocks(IProgress<string>? progress) => _unlocker.GetLocks(progress);
    public PermissionDiagnostics GetDiagnostics() => PermissionChecker.GetDiagnostics(_targetPath);
    public PermissionRepairResult RepairPermissions() => _permissionRepairer.Repair();
    public void DeleteCore() => _unlocker.DeleteFileOrFolderCore();
    public void MoveCore(string destinationPath) => _unlocker.MoveFileOrFolderCore(destinationPath);
    public bool ScheduleDeleteOnReboot() => _unlocker.ScheduleDeleteOnReboot();
    public bool KillProcess(uint processId) => ProcessManager.KillProcess(processId);
    public bool IsSystemProcess(uint processId) => ProcessManager.IsSystemProcess(processId);
}

public sealed class UnlockWorkflow
{
    private readonly string _targetPath;
    private readonly UnlockWorkflowOptions _options;
    private readonly IUnlockWorkflowOperations _operations;

    public UnlockWorkflow(string targetPath, UnlockWorkflowOptions? options = null)
        : this(targetPath, options, new DefaultUnlockWorkflowOperations(targetPath))
    {
    }

    internal UnlockWorkflow(string targetPath, UnlockWorkflowOptions? options, IUnlockWorkflowOperations operations)
    {
        _targetPath = PathHelper.NormalizePath(targetPath);
        _options = options ?? new UnlockWorkflowOptions();
        _operations = operations;
    }

    public List<LockInfo> GetLocks(IProgress<string>? progress = null)
    {
        return _operations.GetLocks(progress);
    }

    public PermissionRepairResult RepairPermissions()
    {
        return _operations.RepairPermissions();
    }

    public bool ScheduleDeleteOnReboot()
    {
        return _options.AllowScheduleOnReboot && _operations.ScheduleDeleteOnReboot();
    }

    public UnlockWorkflowResult Delete(bool autoScheduleOnReboot = false, IProgress<string>? progress = null)
    {
        return Execute(progress, () => _operations.DeleteCore(), null, autoScheduleOnReboot);
    }

    public UnlockWorkflowResult Move(string destinationPath, IProgress<string>? progress = null)
    {
        return Execute(progress, () => _operations.MoveCore(destinationPath), destinationPath, false);
    }

    private UnlockWorkflowResult Execute(
        IProgress<string>? progress,
        Action operation,
        string? destinationPath,
        bool autoScheduleOnReboot)
    {
        UnlockWorkflowResult result = new UnlockWorkflowResult();

        if (!PathHelper.IsValidPath(_targetPath))
        {
            result.Errors.Add("Invalid path specified");
            return result;
        }

        if (PathHelper.IsDangerousPath(_targetPath))
        {
            result.Errors.Add($"Cannot operate on protected path: {_targetPath}");
            return result;
        }

        bool targetExists = File.Exists(_targetPath) || Directory.Exists(_targetPath);
        if (!targetExists)
        {
            if (destinationPath == null)
            {
                result.Success = true;
            }
            else
            {
                result.Errors.Add("Target path does not exist");
            }

            return result;
        }

        result.Diagnostics = _operations.GetDiagnostics();
        AttemptHandleUnlock(progress, result);

        bool permissionRepairNeeded = _options.AutoRepairPermissionsOnAccessDenied &&
            result.Diagnostics is { Exists: true, HasDeletePermission: false };

        Exception? operationException = null;
        if (!TryRunOperation(operation, out operationException))
        {
            if (permissionRepairNeeded || ShouldRepairPermissions(operationException))
            {
                AttemptPermissionRepair(result);
                result.Diagnostics = _operations.GetDiagnostics();
                if (result.PermissionRepairSucceeded && TryRunOperation(operation, out operationException))
                {
                    result.Success = true;
                    result.RemainingLocks = new List<LockInfo>();
                    return result;
                }
            }

            HandleRemainingLocks(progress, result);
            if (result.RemainingLocks.Count > 0 && _options.AutoKillNonSystemProcesses)
            {
                TryKillNonSystemLocks(result);
                if (TryRunOperation(operation, out operationException))
                {
                    result.Success = true;
                    result.RemainingLocks = new List<LockInfo>();
                    return result;
                }

                HandleRemainingLocks(progress, result);
            }

            if (operationException != null)
            {
                result.Errors.Add(BuildFailureMessage(operationException, result.RemainingLocks));
            }
        }
        else
        {
            result.Success = true;
            result.RemainingLocks = new List<LockInfo>();
            return result;
        }

        result.Diagnostics = _operations.GetDiagnostics();

        if (!result.Success &&
            destinationPath == null &&
            autoScheduleOnReboot &&
            _options.AllowScheduleOnReboot)
        {
            result.ScheduledOnReboot = _operations.ScheduleDeleteOnReboot();
            if (!result.ScheduledOnReboot)
            {
                result.Errors.Add("Failed to schedule deletion on reboot");
            }
        }

        return result;
    }

    private void AttemptHandleUnlock(IProgress<string>? progress, UnlockWorkflowResult result)
    {
        try
        {
            UnlockResult unlockResult = _operations.UnlockAll(false, progress);
            result.UnlockedHandles += unlockResult.UnlockedHandles;

            foreach (string error in unlockResult.Errors)
            {
                result.Errors.Add(error);
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Unlock failed: {ex.Message}");
        }
    }

    private void AttemptPermissionRepair(UnlockWorkflowResult result)
    {
        result.PermissionRepairAttempted = true;

        try
        {
            PermissionRepairResult repairResult = _operations.RepairPermissions();
            result.PermissionRepairSucceeded = repairResult.Success;

            foreach (string error in repairResult.Errors)
            {
                result.Errors.Add(error);
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Permission repair failed: {ex.Message}");
        }
    }

    private void HandleRemainingLocks(IProgress<string>? progress, UnlockWorkflowResult result)
    {
        result.RemainingLocks = _operations.GetLocks(progress);

        if (result.RemainingLocks.Count == 0)
        {
            return;
        }

        if (!_options.AllowSystemProcessTermination && result.RemainingLocks.Any(lockInfo => lockInfo.IsSystemProcess))
        {
            result.Errors.Add("System process locks remain and require interactive confirmation.");
        }
    }

    private void TryKillNonSystemLocks(UnlockWorkflowResult result)
    {
        foreach (uint processId in result.RemainingLocks
            .Where(lockInfo => !lockInfo.IsSystemProcess)
            .Select(lockInfo => lockInfo.ProcessId)
            .Distinct())
        {
            if (_operations.KillProcess(processId))
            {
                result.KilledProcesses++;
            }
            else
            {
                result.Errors.Add($"Failed to kill process with PID: {processId}");
            }
        }
    }

    private static bool TryRunOperation(Action operation, out Exception? exception)
    {
        try
        {
            operation();
            exception = null;
            return true;
        }
        catch (Exception ex)
        {
            exception = ex;
            return false;
        }
    }

    private static bool ShouldRepairPermissions(Exception? exception)
    {
        return exception is UnauthorizedAccessException;
    }

    private static string BuildFailureMessage(Exception exception, IReadOnlyCollection<LockInfo> remainingLocks)
    {
        if (remainingLocks.Count == 0)
        {
            return exception.Message;
        }

        string processes = string.Join(", ", remainingLocks.Select(lockInfo => $"{lockInfo.ProcessName} ({lockInfo.ProcessId})"));
        return $"{exception.Message} Remaining locks: {processes}";
    }
}
