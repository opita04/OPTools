using System;
using System.Text.Json.Serialization;

namespace OPTools.Core;

/// <summary>
/// Represents a scheduled backup job configuration
/// </summary>
public class BackupJob
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "New Backup";

    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; set; } = string.Empty;

    [JsonPropertyName("destinationPath")]
    public string DestinationPath { get; set; } = string.Empty;

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("scheduleType")]
    public BackupScheduleType ScheduleType { get; set; } = BackupScheduleType.Daily;

    [JsonPropertyName("scheduledTime")]
    public TimeSpan ScheduledTime { get; set; } = TimeSpan.FromHours(12); // Default noon

    [JsonPropertyName("dayOfWeek")]
    public DayOfWeek? DayOfWeek { get; set; } = null; // For weekly backups

    [JsonPropertyName("dayOfMonth")]
    public int? DayOfMonth { get; set; } = null; // For monthly backups

    [JsonPropertyName("intervalMinutes")]
    public int IntervalMinutes { get; set; } = 60; // For interval-based backups

    [JsonPropertyName("includeSubfolders")]
    public bool IncludeSubfolders { get; set; } = true;

    [JsonPropertyName("keepVersions")]
    public int KeepVersions { get; set; } = 5; // Number of backup versions to keep

    [JsonPropertyName("useTimestampFolder")]
    public bool UseTimestampFolder { get; set; } = true;

    [JsonPropertyName("lastRunTime")]
    public DateTime? LastRunTime { get; set; }

    [JsonPropertyName("lastRunSuccess")]
    public bool? LastRunSuccess { get; set; }

    [JsonPropertyName("lastRunMessage")]
    public string? LastRunMessage { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Gets a human-readable schedule description
    /// </summary>
    [JsonIgnore]
    public string ScheduleDescription
    {
        get
        {
            var timeStr = DateTime.Today.Add(ScheduledTime).ToString("h:mm tt");
            return ScheduleType switch
            {
                BackupScheduleType.Interval => $"Every {IntervalMinutes} minutes",
                BackupScheduleType.Daily => $"Daily at {timeStr}",
                BackupScheduleType.Weekly => $"Weekly on {DayOfWeek?.ToString() ?? "Sunday"} at {timeStr}",
                BackupScheduleType.Monthly => $"Monthly on day {DayOfMonth ?? 1} at {timeStr}",
                _ => "Not scheduled"
            };
        }
    }

    /// <summary>
    /// Calculates the next run time based on the schedule
    /// </summary>
    public DateTime GetNextRunTime(DateTime fromTime)
    {
        switch (ScheduleType)
        {
            case BackupScheduleType.Interval:
                return fromTime.AddMinutes(IntervalMinutes);

            case BackupScheduleType.Daily:
                var nextDaily = fromTime.Date.Add(ScheduledTime);
                if (nextDaily <= fromTime)
                    nextDaily = nextDaily.AddDays(1);
                return nextDaily;

            case BackupScheduleType.Weekly:
                var targetDay = DayOfWeek ?? System.DayOfWeek.Sunday;
                var nextWeekly = fromTime.Date.Add(ScheduledTime);
                while (nextWeekly.DayOfWeek != targetDay || nextWeekly <= fromTime)
                {
                    nextWeekly = nextWeekly.AddDays(1);
                }
                return nextWeekly;

            case BackupScheduleType.Monthly:
                var day = Math.Min(DayOfMonth ?? 1, DateTime.DaysInMonth(fromTime.Year, fromTime.Month));
                var nextMonthly = new DateTime(fromTime.Year, fromTime.Month, day).Add(ScheduledTime);
                if (nextMonthly <= fromTime)
                {
                    var nextMonth = fromTime.AddMonths(1);
                    day = Math.Min(DayOfMonth ?? 1, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                    nextMonthly = new DateTime(nextMonth.Year, nextMonth.Month, day).Add(ScheduledTime);
                }
                return nextMonthly;

            default:
                return fromTime.AddDays(1);
        }
    }
}

public enum BackupScheduleType
{
    Interval,   // Every X minutes
    Daily,      // Every day at specific time
    Weekly,     // Every week on specific day
    Monthly     // Every month on specific day
}

public class BackupResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int FilesCopied { get; set; }
    public int FilesSkipped { get; set; }
    public int FilesFailed { get; set; }
    public long BytesCopied { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
}
