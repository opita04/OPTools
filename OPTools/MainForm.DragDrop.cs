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
            RegisterDropTarget(this);
            RegisterDropTarget(_listView);
            RegisterDropTarget(_applicationsPanel);
            RegisterDropTarget(_applicationsContentPanel);
            RegisterDropTarget(_contentPanel);
            RegisterDropTarget(_sidebarPanel);
            RegisterDropTarget(_headerPanel);
        }

        private void RegisterDropTarget(Control? c)
        {
            if (c == null) return;
            
            if (c.IsHandleCreated)
            {
                WindowsApi.DragAcceptFiles(c.Handle, true);
            }
            c.HandleCreated += (s, e) => WindowsApi.DragAcceptFiles(c.Handle, true);
        }

        private void HandleDropFiles(IntPtr hDrop)
        {
            try
            {
                uint count = WindowsApi.DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
                for (uint i = 0; i < count; i++)
                {
                    StringBuilder sb = new StringBuilder(260);
                    if (WindowsApi.DragQueryFile(hDrop, i, sb, (uint)sb.Capacity) > 0)
                    {
                        string path = sb.ToString();
                        if (System.IO.File.Exists(path))
                        {
                            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                            
                            // If we are in Applications view, add to launcher
                            if (_navApplications.IsActive && (ext == ".exe" || ext == ".lnk"))
                            {
                                AddApplicationButton(path);
                                _appLauncher?.AddShortcut(path);
                            }
                            // If we are in Unlocker view (default), load locks
                            else if (_navUnlocker.IsActive)
                            {
                                _targetPath = path;
                                LoadLocks();
                            }
                        }
                        else if (System.IO.Directory.Exists(path) && _navUnlocker.IsActive)
                        {
                             _targetPath = path;
                             LoadLocks();
                        }
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

        private void MainForm_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data == null)
                return;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
                
                if (files != null && files.Length > 0)
                {
                    string droppedPath = files[0];
                    
                    if (System.IO.File.Exists(droppedPath) || System.IO.Directory.Exists(droppedPath))
                    {
                        _targetPath = droppedPath;
                        LoadLocks();
                    }
                    else
                    {
                        _lblStatus.Text = $"Path does not exist: {droppedPath}";
                        MessageBox.Show($"The dropped path does not exist:\n{droppedPath}", 
                            "Invalid Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
        }
    }
}
