using System;
using System.IO;
using Serilog;

namespace OPTools.Utils;

public static class AuditLogger
{
    private static ILogger? _logger;
    
    public static void Initialize()
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OPTools", "logs", "audit-.log");
            
            _logger = new LoggerConfiguration()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }
        catch
        {
            // Fail silently if logging cannot be initialized to avoid crashing the app
        }
    }
    
    public static void LogDelete(string path, bool success) =>
        _logger?.Information("DELETE {Path} {Status}", path, success ? "SUCCESS" : "FAILED");
    
    public static void LogProcessKill(uint pid, string name, bool success) =>
        _logger?.Information("KILL {ProcessName} ({PID}) {Status}", name, pid, success ? "SUCCESS" : "FAILED");
    
    public static void LogNetworkReset(string operation, bool success) =>
        _logger?.Information("NETWORK {Operation} {Status}", operation, success ? "SUCCESS" : "FAILED");
        
    public static void LogOperation(string operation, string details, bool success) =>
        _logger?.Information("OP {Operation} {Details} {Status}", operation, details, success ? "SUCCESS" : "FAILED");
}
