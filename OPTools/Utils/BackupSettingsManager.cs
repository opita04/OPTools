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
            var jobs = JsonSerializer.Deserialize<List<BackupJob>>(json, JsonOptions);

            if (jobs == null)
            {
                System.Diagnostics.Debug.WriteLine("Failed to deserialize backup jobs: null result");
                return new List<BackupJob>();
            }

            return jobs;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load backup jobs: {ex.Message}");
            // Create backup of corrupted file if it exists and has content
            if (File.Exists(SettingsFile) && new FileInfo(SettingsFile).Length > 5)
            {
                try
                {
                    var backupPath = SettingsFile + ".corrupted";
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                    File.Copy(SettingsFile, backupPath);
                    System.Diagnostics.Debug.WriteLine($"Backed up corrupted settings to: {backupPath}");
                }
                catch { /* Ignore backup failure */ }
            }
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

            // Don't overwrite existing file with empty content
            if (File.Exists(SettingsFile))
            {
                var existingContent = File.ReadAllText(SettingsFile);
                if (json.Trim() == "[]" && existingContent.Trim() != "[]")
                {
                    System.Diagnostics.Debug.WriteLine("WARNING: Skipping save - would overwrite backup jobs with empty array");
                    return;
                }
            }

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
