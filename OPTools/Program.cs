using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows.Forms;
using OPTools.Core;
using OPTools.Registry;

namespace OPTools;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Add error handling wrapper
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Debug: Log what arguments we received (remove after debugging)
            // Debug: Log what arguments we received (remove after debugging)
            if (args.Length > 0)
            {
                System.Diagnostics.Debug.WriteLine($"OPTools launched with {args.Length} argument(s):");
                for (int i = 0; i < args.Length; i++)
                {
                    System.Diagnostics.Debug.WriteLine($"  Arg[{i}]: '{args[i]}'");
                }
            }

            // Initialize Audit Logging
            OPTools.Utils.AuditLogger.Initialize();

            bool silentMode = false;
            string? targetPath = null;
            bool installContextMenu = false;
            bool uninstallContextMenu = false;

        // Process arguments first
        if (args.Length > 0)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLowerInvariant();

                if (arg == "/s" || arg == "-s" || arg == "/silent" || arg == "-silent")
                {
                    silentMode = true;
                }
                else if (arg == "/install" || arg == "-install")
                {
                    installContextMenu = true;
                }
                else if (arg == "/uninstall" || arg == "-uninstall")
                {
                    uninstallContextMenu = true;
                }
                else if (arg == "/h" || arg == "-h" || arg == "/help" || arg == "-help" || arg == "/?")
                {
                    ShowHelp();
                    return;
                }
                else if (!arg.StartsWith("/") && !arg.StartsWith("-"))
                {
                    // Handle quoted paths from context menu
                    targetPath = args[i].Trim('"');
                }
            }
        }

        // Handle install/uninstall commands first (before admin check)
        if (installContextMenu)
        {
            InstallContextMenu();
            return;
        }

        if (uninstallContextMenu)
        {
            UninstallContextMenu();
            return;
        }


        // Handle empty arguments or no target path
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            if (!silentMode)
            {
                Application.Run(new MainForm());
            }
            return;
        }

        // Clean up the path - remove quotes and get full path
        targetPath = targetPath.Trim('"', ' ', '\t');
        try
        {
            targetPath = Path.GetFullPath(targetPath);
        }
        catch
        {
            if (!silentMode)
            {
                MessageBox.Show($"Invalid path: {targetPath}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return;
        }

        if (!File.Exists(targetPath) && !Directory.Exists(targetPath))
        {
            if (!silentMode)
            {
                DialogResult result = MessageBox.Show(
                    $"Path does not exist: {targetPath}\n\nWould you like to open OPTools anyway?",
                    "Path Not Found",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                
                if (result == DialogResult.Yes)
                {
                    Application.Run(new MainForm());
                }
            }
            else
            {
                Environment.Exit(1);
            }
            return;
        }

        if (silentMode)
        {
            RunSilentMode(targetPath);
        }
        else
        {
            Application.Run(new MainForm(targetPath));
        }
        }
        catch (Exception ex)
        {
            // Log full details for diagnostics
            OPTools.Utils.AuditLogger.LogOperation("STARTUP_ERROR", ex.ToString(), false);
            
            MessageBox.Show(
                $"An error occurred starting OPTools:\n\n{ex.Message}\n\nCheck logs for details.",
                "OPTools Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void RunSilentMode(string targetPath)
    {
        // Tier 1 Audit Fix: Block system paths in silent mode
        if (OPTools.Utils.PathHelper.IsDangerousPath(targetPath))
        {
            // Exit with error code 2 (Access Denied / Protected)
            Environment.Exit(2);
            return;
        }

        try
        {
            FileUnlocker unlocker = new FileUnlocker(targetPath);
            
            UnlockResult result = unlocker.UnlockAll(true);

            if (!result.Success)
            {
                result = unlocker.UnlockAll(true);
            }

            System.Threading.Thread.Sleep(200);

            try
            {
                unlocker.DeleteFileOrFolder();
            }
            catch
            {
                Environment.Exit(1);
            }
        }
        catch
        {
            Environment.Exit(1);
        }
    }

    private static void InstallContextMenu()
    {
        if (!IsRunningAsAdministrator())
        {
            MessageBox.Show("Administrator privileges are required to install the context menu.",
                "Elevation Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            if (ContextMenuInstaller.Install())
            {
                MessageBox.Show("Context menu installed successfully.", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Failed to install context menu.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error installing context menu: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void UninstallContextMenu()
    {
        if (!IsRunningAsAdministrator())
        {
            MessageBox.Show("Administrator privileges are required to uninstall the context menu.",
                "Elevation Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            if (ContextMenuInstaller.Uninstall())
            {
                MessageBox.Show("Context menu uninstalled successfully.", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Failed to uninstall context menu.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error uninstalling context menu: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void ShowHelp()
    {
        string help = @"OPTools - Operating System Tools

Usage:
  OPTools.exe [path] [options]

Options:
  /S, -S, /SILENT, -SILENT    Silent mode (force delete without GUI)
  /INSTALL, -INSTALL          Install context menu (requires admin)
  /UNINSTALL, -UNINSTALL     Uninstall context menu (requires admin)
  /H, -H, /HELP, -HELP, /?   Show this help message

Examples:
  OPTools.exe ""C:\locked\file.txt""
  OPTools.exe ""C:\locked\folder"" /S
  OPTools.exe /INSTALL

If no path is specified, the GUI will open without a target.";
        
        MessageBox.Show(help, "OPTools Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public static bool IsRunningAsAdministrator()
    {
        try
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}

