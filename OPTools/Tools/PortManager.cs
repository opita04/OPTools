using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace OPTools.Tools;

public static class PortManager
{
    public static async Task<List<PortInfo>> GetActivePorts()
    {
        List<PortInfo> ports = new List<PortInfo>();

        await Task.Run(() =>
        {
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();

            // Get TCP listeners with process info
            IPEndPoint[] tcpListeners = properties.GetActiveTcpListeners();
            TcpConnectionInformation[] tcpConnections = properties.GetActiveTcpConnections();

            foreach (var listener in tcpListeners)
            {
                try
                {
                    int pid = GetProcessIdForTcpPort(listener.Port, tcpConnections);
                    string processName = "<exited>";
                    
                    if (pid > 0)
                    {
                        try
                        {
                            var proc = Process.GetProcessById(pid);
                            processName = proc.ProcessName;
                        }
                        catch { }
                    }

                    ports.Add(new PortInfo
                    {
                        Protocol = "TCP",
                        Port = listener.Port,
                        ProcessId = pid,
                        ProcessName = processName,
                        State = "LISTEN"
                    });
                }
                catch { }
            }

            // Get UDP listeners (note: UDP doesn't provide PID directly in .NET, using alternative method)
            IPEndPoint[] udpListeners = properties.GetActiveUdpListeners();
            foreach (var listener in udpListeners)
            {
                try
                {
                    int pid = GetProcessIdForUdpPort(listener.Port);
                    string processName = "<exited>";
                    
                    if (pid > 0)
                    {
                        try
                        {
                            var proc = Process.GetProcessById(pid);
                            processName = proc.ProcessName;
                        }
                        catch { }
                    }

                    ports.Add(new PortInfo
                    {
                        Protocol = "UDP",
                        Port = listener.Port,
                        ProcessId = pid,
                        ProcessName = processName,
                        State = "LISTEN"
                    });
                }
                catch { }
            }
        });

        return ports.OrderBy(p => p.Port).DistinctBy(p => new { p.Protocol, p.Port }).ToList();
    }

    private static int GetProcessIdForTcpPort(int port, TcpConnectionInformation[] connections)
    {
        try
        {
            // Use netstat to get process ID for TCP ports
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (Process proc = Process.Start(psi)!)
            {
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                foreach (string line in output.Split('\n'))
                {
                    if (line.Contains("TCP") && line.Contains($":{port} "))
                    {
                        string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1], out int pid))
                        {
                            return pid;
                        }
                    }
                }
            }
        }
        catch { }

        return 0;
    }

    private static int GetProcessIdForUdpPort(int port)
    {
        try
        {
            // Use netstat equivalent via Process.Start to get UDP port info
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using (Process proc = Process.Start(psi)!)
            {
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                foreach (string line in output.Split('\n'))
                {
                    if (line.Contains("UDP") && line.Contains($":{port} "))
                    {
                        string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1], out int pid))
                        {
                            return pid;
                        }
                    }
                }
            }
        }
        catch { }

        return 0;
    }
}

public class PortInfo
{
    public string Protocol { get; set; } = string.Empty;
    public int Port { get; set; }
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

