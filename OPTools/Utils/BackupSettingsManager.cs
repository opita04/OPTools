using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OPTools.Core;

namespace OPTools.Utils;

/// <summary>
/// Manages persistence of backup job configurations
/// </summary>
public static class BackupSettingsManager
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OPTools"
    );
    
    private static readonly string SettingsFile = Path.Combine(SettingsFolder, "backup_jobs.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Load all saved backup jobs
    /// </summary>
    public static List<BackupJob> LoadJobs()
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return new List<BackupJob>();

            var json = File.ReadAllText(SettingsFile);
            return JsonSerializer.Deserialize<List<BackupJob>>(json, JsonOptions) ?? new List<BackupJob>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load backup jobs: {ex.Message}");
            return new List<BackupJob>();
        }
    }

    /// <summary>
    /// Save all backup jobs
    /// </summary>
    public static void SaveJobs(IEnumerable<BackupJob> jobs)
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            var json = JsonSerializer.Serialize(jobs, JsonOptions);
            File.WriteAllText(SettingsFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save backup jobs: {ex.Message}");
        }
    }

    /// <summary>
    /// Add or update a single job
    /// </summary>
    public static void SaveJob(BackupJob job)
    {
        var jobs = LoadJobs();
        var existingIndex = jobs.FindIndex(j => j.Id == job.Id);
        if (existingIndex >= 0)
        {
            jobs[existingIndex] = job;
        }
        else
        {
            jobs.Add(job);
        }
        SaveJobs(jobs);
    }

    /// <summary>
    /// Remove a job by ID
    /// </summary>
    public static void RemoveJob(string jobId)
    {
        var jobs = LoadJobs();
        jobs.RemoveAll(j => j.Id == jobId);
        SaveJobs(jobs);
    }
}
