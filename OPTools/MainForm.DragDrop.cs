using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using OPTools.Core;
using OPTools.Registry;
using OPTools.Tools;
using OPTools.Utils;
using System.Text;

namespace OPTools
{
    public partial class MainForm
    {
        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == WindowsApi.WM_DROPFILES)
            {
                HandleDropFiles(m.WParam);
                return true;
            }
            return false;
        }

        private void InitializeDragDrop()
        {
            RegisterShellDropTarget(this);
            RegisterShellDropTarget(_listView);
            RegisterShellDropTarget(_applicationsPanel);
            RegisterShellDropTarget(_applicationsContentPanel);
            RegisterShellDropTarget(_contentPanel);
            RegisterShellDropTarget(_sidebarPanel);
            RegisterShellDropTarget(_headerPanel);

            EnableOleDropTarget(this, MainForm_DragEnter, MainForm_DragDrop);
            EnableOleDropTarget(_contentPanel, MainForm_DragEnter, MainForm_DragDrop);
            EnableOleDropTarget(_listView, MainForm_DragEnter, MainForm_DragDrop);
            EnableOleDropTarget(_headerPanel, MainForm_DragEnter, MainForm_DragDrop, includeChildren: true);

            EnableOleDropTarget(_applicationsPanel, ApplicationsPanel_DragEnter, ApplicationsPanel_DragDrop);
            EnableOleDropTarget(_applicationsContentPanel, ApplicationsPanel_DragEnter, ApplicationsPanel_DragDrop);
        }

        private void RegisterShellDropTarget(Control? c)
        {
            if (c == null) return;
            
            if (c.IsHandleCreated)
            {
                WindowsApi.DragAcceptFiles(c.Handle, true);
            }
            c.HandleCreated += (s, e) => WindowsApi.DragAcceptFiles(c.Handle, true);
        }

        private void EnableOleDropTarget(
            Control? control,
            DragEventHandler dragEnterHandler,
            DragEventHandler dragDropHandler,
            bool includeChildren = false)
        {
            if (control == null)
            {
                return;
            }

            control.AllowDrop = true;
            control.DragEnter -= dragEnterHandler;
            control.DragEnter += dragEnterHandler;
            control.DragDrop -= dragDropHandler;
            control.DragDrop += dragDropHandler;

            if (!includeChildren)
            {
                return;
            }

            foreach (Control child in control.Controls)
            {
                EnableOleDropTarget(child, dragEnterHandler, dragDropHandler, includeChildren: true);
            }
        }

        private void PreProcessDroppedFolder(string folderPath)
        {
            try
            {
                // Explicitly check for 'nul' file using extended path syntax
                // This handles the user requirement: "The first thing it should do always is to delete the nul file"
                string nulPath = System.IO.Path.Combine(folderPath, "nul");
                FileUnlocker.DeleteReservedFile(nulPath);

                // Also scan for other reserved names just in case
                if (System.IO.Directory.Exists(folderPath))
                {
                    var dirInfo = new System.IO.DirectoryInfo(folderPath);
                    foreach (var file in dirInfo.GetFiles())
                    {
                        if (FileUnlocker.IsReservedFileName(file.Name))
                        {
                            FileUnlocker.DeleteReservedFile(file.FullName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error pre-processing folder: {ex.Message}");
            }
        }

        private void HandleDropFiles(IntPtr hDrop)
        {
            try
            {
                uint count = WindowsApi.DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
                for (uint i = 0; i < count; i++)
                {
                    uint pathLength = WindowsApi.DragQueryFile(hDrop, i, null, 0);
                    if (pathLength == 0)
                    {
                        continue;
                    }

                    StringBuilder sb = new StringBuilder((int)pathLength + 1);
                    if (WindowsApi.DragQueryFile(hDrop, i, sb, (uint)sb.Capacity) == 0)
                    {
                        continue;
                    }

                    string path = sb.ToString();
                    if (_navApplications.IsActive)
                    {
                        HandleApplicationDrop(new[] { path });
                    }
                    else if (_navUnlocker.IsActive)
                    {
                        HandleUnlockerDrop(path);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling drop files: {ex.Message}");
            }
            finally
            {
                WindowsApi.DragFinish(hDrop);
            }
        }

        private void AllowDragDropMessages()
        {
            try
            {
                WindowsApi.ChangeWindowMessageFilter(WindowsApi.WM_DROPFILES, WindowsApi.MSGFLT_ADD);
                WindowsApi.ChangeWindowMessageFilter(WindowsApi.WM_COPYDATA, WindowsApi.MSGFLT_ADD);
                WindowsApi.ChangeWindowMessageFilter(WindowsApi.WM_COPYGLOBALDATA, WindowsApi.MSGFLT_ADD);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting message filter: {ex.Message}");
            }
        }

        private void MainForm_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data == null)
                return;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private bool TryNormalizeDroppedPath(string? droppedPath, out string normalizedPath)
        {
            normalizedPath = string.Empty;

            if (string.IsNullOrWhiteSpace(droppedPath))
            {
                return false;
            }

            try
            {
                normalizedPath = System.IO.Path.GetFullPath(droppedPath.Trim().Trim('"'));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void HandleUnlockerDrop(string droppedPath)
        {
            if (!TryNormalizeDroppedPath(droppedPath, out string normalizedPath))
            {
                _lblStatus.Text = "Invalid path dropped.";
                return;
            }

            if (System.IO.Directory.Exists(normalizedPath))
            {
                PreProcessDroppedFolder(normalizedPath);
            }

            if (System.IO.File.Exists(normalizedPath) || System.IO.Directory.Exists(normalizedPath))
            {
                _targetPath = normalizedPath;
                LoadLocks();
                return;
            }

            _lblStatus.Text = $"Path does not exist: {normalizedPath}";
            MessageBox.Show($"The dropped path does not exist:\n{normalizedPath}",
                "Invalid Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void HandleApplicationDrop(IEnumerable<string> droppedPaths)
        {
            if (_appLauncher == null)
            {
                return;
            }

            foreach (string droppedPath in droppedPaths)
            {
                if (!TryNormalizeDroppedPath(droppedPath, out string normalizedPath) ||
                    !System.IO.File.Exists(normalizedPath))
                {
                    continue;
                }

                string ext = System.IO.Path.GetExtension(normalizedPath).ToLowerInvariant();
                if (ext == ".exe" || ext == ".lnk")
                {
                    AddApplicationButton(normalizedPath);
                    _appLauncher.AddShortcut(normalizedPath);
                }
            }
        }

        private void MainForm_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data == null)
                return;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];

                if (files == null || files.Length == 0)
                {
                    return;
                }

                if (_navApplications.IsActive)
                {
                    HandleApplicationDrop(files);
                }
                else if (_navUnlocker.IsActive)
                {
                    HandleUnlockerDrop(files[0]);
                }
            }
        }
    }
}
