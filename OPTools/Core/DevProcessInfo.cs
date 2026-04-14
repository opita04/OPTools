using System;

namespace OPTools.Core;

public enum ProcessLoadingState
{
    NotLoaded,      // Initial state
    Loading,        // Currently fetching details
    Loaded,         // Details fully loaded
    Failed          // Failed to load details
}

/// <summary>
/// Information about a running dev tool process.
/// </summary>
public class DevProcessInfo
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    
    // Detail fields - may be empty initially
    public long MemoryMB { get; set; }
    public DateTime StartTime { get; set; }
    public string CommandLine { get; set; } = string.Empty;
    
    // Loading state tracking
    public bool IsFullyLoaded => LoadingState == ProcessLoadingState.Loaded;
    public ProcessLoadingState LoadingState { get; set; } = ProcessLoadingState.NotLoaded;

    /// <summary>
    /// Gets a human-readable running time string.
    /// </summary>
    public string RunningTime
    {
        get
        {
            if (StartTime == DateTime.MinValue || LoadingState != ProcessLoadingState.Loaded)
                return LoadingState == ProcessLoadingState.Loading ? "Loading..." : "Unknown";

            var elapsed = DateTime.Now - StartTime;
            if (elapsed.TotalDays >= 1)
                return $"{(int)elapsed.TotalDays}d {elapsed.Hours}h";
            if (elapsed.TotalHours >= 1)
                return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m";
            if (elapsed.TotalMinutes >= 1)
                return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
            return $"{(int)elapsed.TotalSeconds}s";
        }
    }
}
