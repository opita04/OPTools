using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace OPTools.Tools;

public static class NetworkReset
{
    public static async Task<ResetResult> ResetInternetStack()
    {
        ResetResult result = new ResetResult();

        await Task.Run(() =>
        {
            try
            {
                // 1. Flush DNS cache
                RunCommand("ipconfig", "/flushdns", result);
                
                // 2. Release IP configuration
                RunCommand("ipconfig", "/release", result);
                
                // 3. Renew IP configuration
                RunCommand("ipconfig", "/renew", result);
                
                // 4. Reset Winsock
                RunCommand("netsh", "winsock reset", result);
                
                // 5. Reset TCP/IP stack
                RunCommand("netsh", "int ip reset", result);
                
                // 6. Clear proxy settings
                RunCommand("netsh", "winhttp reset proxy", result);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error resetting network: {ex.Message}");
            }
        });

        result.Success = result.Errors.Count == 0;
        return result;
    }

    public static async Task<ResetResult> FlushDns()
    {
        return await RunSingleCommand("ipconfig", "/flushdns");
    }

    public static async Task<ResetResult> ReleaseIp()
    {
        return await RunSingleCommand("ipconfig", "/release");
    }

    public static async Task<ResetResult> RenewIp()
    {
        return await RunSingleCommand("ipconfig", "/renew");
    }

    public static async Task<ResetResult> ResetWinsock()
    {
        return await RunSingleCommand("netsh", "winsock reset");
    }

    public static async Task<ResetResult> ResetTcpIp()
    {
        return await RunSingleCommand("netsh", "int ip reset");
    }

    public static async Task<ResetResult> ResetProxy()
    {
        return await RunSingleCommand("netsh", "winhttp reset proxy");
    }

    private static async Task<ResetResult> RunSingleCommand(string command, string arguments)
    {
        ResetResult result = new ResetResult();
        await Task.Run(() => RunCommand(command, arguments, result));
        result.Success = result.Errors.Count == 0;
        return result;
    }

    private static void RunCommand(string command, string arguments, ResetResult result)
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Verb = "runas"
            };

            using (Process proc = Process.Start(psi)!)
            {
                proc.WaitForExit();
                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();
                
                if (proc.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
                {
                    result.Errors.Add($"{command} {arguments}: {error}");
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to run {command} {arguments}: {ex.Message}");
        }
    }
}

public class ResetResult
{
    public bool Success { get; set; }
    public System.Collections.Generic.List<string> Errors { get; set; } = new System.Collections.Generic.List<string>();
}

