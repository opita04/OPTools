using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading.Tasks;

namespace OPTools.Core;
/// <summary>
/// Scans for developer tool processes that can be monitored and killed.
/// </summary>
public static class DevProcessScanner
{
    /// <summary>
    /// Process names to monitor as dev tools.
    /// </summary>
    public static readonly Dictionary<string, string> DevProcessCategories = new()
    {
        { "node", "Node.js" },
        { "bun", "Bun" },
        { "bash", "Git Bash" },
        { "git", "Git" },
        { "wslrelay", "WSL Relay" },
        { "npm", "npm" },
        { "npx", "npx" },
        { "pnpm", "pnpm" },
        { "yarn", "Yarn" },
        { "deno", "Deno" },
        { "python", "Python" },
        { "python3", "Python" },
        { "pwsh", "PowerShell" },
        { "powershell", "PowerShell" },
        { "wsl", "WSL" },
        { "docker", "Docker" },
        { "podman", "Podman" }
    };

    /// <summary>
    /// Gets all running dev tool processes with full details.
    /// </summary>
    public static async Task<List<DevProcessInfo>> GetRunningDevProcessesAsync()
    {
        var processes = new List<DevProcessInfo>();

        await Task.Run(() =>
        {
            foreach (var kvp in DevProcessCategories)
            {
                try
                {
                    var procs = Process.GetProcessesByName(kvp.Key);
                    foreach (var proc in procs)
                    {
                        try
                        {
                            var info = new DevProcessInfo
                            {
                                ProcessId = proc.Id,
                                ProcessName = proc.ProcessName,
                                Category = kvp.Value,
                                MemoryMB = proc.WorkingSet64 / (1024 * 1024),
                                StartTime = GetProcessStartTimeSafe(proc),
                                CommandLine = GetCommandLineSafe(proc.Id),
                                LoadingState = ProcessLoadingState.Loaded
                            };
                            processes.Add(info);
                        }
                        catch
                        {
                            // Process may have exited
                        }
                        finally
                        {
                            proc.Dispose();
                        }
                    }
                }
                catch
                {
                    // Access denied or other error
                }
            }
        });

        return processes.OrderBy(p => p.Category).ThenBy(p => p.ProcessId).ToList();
    }

    /// <summary>
    /// Gets running dev tool processes FAST - only name, PID, and category.
    /// Skips expensive WMI queries for instant loading.
    /// </summary>
    public static async Task<List<DevProcessInfo>> GetRunningDevProcessesFastAsync()
    {
        var processes = new List<DevProcessInfo>();

        await Task.Run(() =>
        {
            foreach (var kvp in DevProcessCategories)
            {
                try
                {
                    var procs = Process.GetProcessesByName(kvp.Key);
                    foreach (var proc in procs)
                    {
                        try
                        {
                            var info = new DevProcessInfo
                            {
                                ProcessId = proc.Id,
                                ProcessName = proc.ProcessName,
                                Category = kvp.Value,
                                LoadingState = ProcessLoadingState.NotLoaded,
                                MemoryMB = 0,
                                StartTime = DateTime.MinValue,
                                CommandLine = string.Empty
                            };
                            processes.Add(info);
                        }
                        catch
                        {
                            // Process may have exited
                        }
                        finally
                        {
                            proc.Dispose();
                        }
                    }
                }
                catch
                {
                    // Access denied or other error
                }
            }
        });

        return processes.OrderBy(p => p.Category).ThenBy(p => p.ProcessId).ToList();
    }

    /// <summary>
    /// Populates detailed information (memory, start time, command line) for processes.
    /// Uses batch WMI queries for efficiency.
    /// </summary>
    /// <param name="processes">List of processes to enrich with details</param>
    /// <param name="batchSize">Number of processes to query at once (default: 50)</param>
    /// <param name="progressCallback">Optional callback for progress updates (0.0 to 1.0)</param>
    public static async Task PopulateProcessDetailsAsync(
        List<DevProcessInfo> processes, 
        int batchSize = 50,
        Action<float>? progressCallback = null)
    {
        if (processes.Count == 0)
            return;

        await Task.Run(() =>
        {
            int totalProcessed = 0;
            
            // Process in batches to avoid overwhelming WMI
            for (int i = 0; i < processes.Count; i += batchSize)
            {
                var batch = processes.Skip(i).Take(batchSize).ToList();
                
                // Mark as loading
                foreach (var proc in batch)
                {
                    proc.LoadingState = ProcessLoadingState.Loading;
                }

                // Batch WMI query for command lines
                var processIds = batch.Select(p => p.ProcessId).ToList();
                var commandLines = GetCommandLineBatch(processIds);

                // Populate details for each process in batch
                foreach (var proc in batch)
                {
                    try
                    {
                        var winProc = Process.GetProcessById(proc.ProcessId);
                        
                        proc.MemoryMB = winProc.WorkingSet64 / (1024 * 1024);
                        proc.StartTime = GetProcessStartTimeSafe(winProc);
                        proc.CommandLine = commandLines.GetValueOrDefault(proc.ProcessId) ?? string.Empty;
                        
                        proc.LoadingState = ProcessLoadingState.Loaded;
                        winProc.Dispose();
                    }
                    catch
                    {
                        // Process exited or access denied
                        proc.LoadingState = ProcessLoadingState.Failed;
                        proc.CommandLine = "N/A";
                    }
                    
                    totalProcessed++;
                }

                // Report progress
                progressCallback?.Invoke((float)totalProcessed / processes.Count);
            }
        });
    }

    /// <summary>
    /// Gets counts of running processes by category.
    /// </summary>
    public static async Task<Dictionary<string, int>> GetProcessCountsByCategoryAsync()
    {
        var counts = new Dictionary<string, int>();
        var processes = await GetRunningDevProcessesAsync();

        foreach (var proc in processes)
        {
            if (counts.ContainsKey(proc.Category))
                counts[proc.Category]++;
            else
                counts[proc.Category] = 1;
        }

        return counts;
    }

    /// <summary>
    /// Gets all unique categories for filtering.
    /// </summary>
    public static IEnumerable<string> GetAllCategories()
    {
        return DevProcessCategories.Values.Distinct().OrderBy(c => c);
    }

    private static DateTime GetProcessStartTimeSafe(Process proc)
    {
        try
        {
            return proc.StartTime;
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    /// <summary>
    /// Gets command lines for multiple processes in a single batch query.
    /// </summary>
    private static Dictionary<int, string> GetCommandLineBatch(List<int> processIds)
    {
        var commandLines = new Dictionary<int, string>();
        
        if (processIds.Count == 0)
            return commandLines;

        try
        {
            // Build batch WMI query
            var idList = string.Join(",", processIds);
            var query = $"SELECT ProcessId, CommandLine FROM Win32_Process WHERE ProcessId IN ({idList})";
            
            using var searcher = new ManagementObjectSearcher(query);
            
            foreach (ManagementObject obj in searcher.Get())
            {
                var processId = Convert.ToInt32(obj["ProcessId"]);
                var cmdLine = obj["CommandLine"]?.ToString();
                
                if (!string.IsNullOrEmpty(cmdLine))
                {
                    // Truncate long command lines
                    commandLines[processId] = cmdLine.Length > 200 ? cmdLine.Substring(0, 200) + "..." : cmdLine;
                }
                else
                {
                    commandLines[processId] = string.Empty;
                }
            }
        }
        catch
        {
            // WMI query failed - return empty dict
        }
        
        return commandLines;
    }

    private static string GetCommandLineSafe(int processId)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");
            
            foreach (ManagementObject obj in searcher.Get())
            {
                var cmdLine = obj["CommandLine"]?.ToString();
                if (!string.IsNullOrEmpty(cmdLine))
                {
                    // Truncate long command lines
                    return cmdLine.Length > 200 ? cmdLine.Substring(0, 200) + "..." : cmdLine;
                }
            }
        }
        catch
        {
            // WMI query failed
        }
        return string.Empty;
    }
}
