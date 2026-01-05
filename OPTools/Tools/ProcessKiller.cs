using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace OPTools.Tools;

public static class ProcessKiller
{
    public static async Task<KillResult> KillNodeJs()
    {
        return await KillProcessesByName("node");
    }

    public static async Task<KillResult> KillGitBash()
    {
        return await KillProcessesByName("bash");
    }

    public static async Task<KillResult> KillGit()
    {
        return await KillProcessesByName("git");
    }

    public static async Task<KillResult> KillWslRelay()
    {
        return await KillProcessesByName("wslrelay");
    }

    public static async Task<KillResult> KillAllDevTools()
    {
        KillResult result = new KillResult();

        var nodeResult = await KillProcessesByName("node");
        var bashResult = await KillProcessesByName("bash");
        var gitResult = await KillProcessesByName("git");
        var wslResult = await KillProcessesByName("wslrelay");

        result.ProcessesKilled = nodeResult.ProcessesKilled + bashResult.ProcessesKilled + 
                                  gitResult.ProcessesKilled + wslResult.ProcessesKilled;
        result.Errors.AddRange(nodeResult.Errors);
        result.Errors.AddRange(bashResult.Errors);
        result.Errors.AddRange(gitResult.Errors);
        result.Errors.AddRange(wslResult.Errors);
        result.Success = result.Errors.Count == 0;

        return result;
    }

    public static async Task<KillResult> KillProcessesByName(string processName)
    {
        KillResult result = new KillResult();

        await Task.Run(() =>
        {
            try
            {
                Process[] processes = Process.GetProcessesByName(processName);

                if (processes.Length == 0)
                {
                    result.Success = true;
                    return;
                }

                foreach (Process proc in processes)
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(5000);
                        result.ProcessesKilled++;
                        OPTools.Utils.AuditLogger.LogProcessKill((uint)proc.Id, processName, true);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Failed to kill {processName} (PID: {proc.Id}): {ex.Message}");
                        OPTools.Utils.AuditLogger.LogProcessKill((uint)proc.Id, processName, false);
                    }
                    finally
                    {
                        try { proc.Dispose(); } catch { }
                    }
                }

                result.Success = result.Errors.Count == 0;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error finding processes: {ex.Message}");
            }
        });

        return result;
    }

    public static async Task<KillResult> KillProcessById(int processId)
    {
        KillResult result = new KillResult();

        await Task.Run(() =>
        {
            try
            {
                Process proc = Process.GetProcessById(processId);
                proc.Kill();
                proc.WaitForExit(5000);
                result.ProcessesKilled = 1;
                result.Success = true;
                OPTools.Utils.AuditLogger.LogProcessKill((uint)processId, proc.ProcessName, true);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to kill process (PID: {processId}): {ex.Message}");
                OPTools.Utils.AuditLogger.LogProcessKill((uint)processId, "Unknown", false);
            }
        });

        return result;
    }
}

public class KillResult
{
    public bool Success { get; set; }
    public int ProcessesKilled { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
}

