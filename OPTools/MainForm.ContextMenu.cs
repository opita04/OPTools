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
        // Context Menu Manager Panel & Components
        private Panel _contextMenuContentPanel = null!;
        private TabControl _ctxTabControl = null!;
        private ListView _ctxListView = null!;
        private ModernButton _ctxBtnRefresh = null!;
        private ModernButton _ctxBtnEdit = null!;
        private ModernButton _ctxBtnDelete = null!;
        private Label _ctxLblStatus = null!;
        private TextBox _ctxTxtAppPath = null!;
        private TextBox _ctxTxtMenuName = null!;
        private CheckBox _ctxChkFolder = null!;
        private CheckBox _ctxChkFile = null!;
        private CheckBox _ctxChkFolderBackground = null!;
        private ModernButton _ctxBtnBrowse = null!;
        private ModernButton _ctxBtnAdd = null!;
        private readonly ContextMenuRegistryManager _registryManager = new();

        private void InitializeContextMenuPanel()
        {
            try
            {
                _contextMenuContentPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(30),
                    BackColor = _cBackground,
                    Visible = false
                };

                // Tab Control
                _ctxTabControl = new TabControl
                {
                    Dock = DockStyle.Fill,
                    Font = new Font("Segoe UI", 10),
                    Appearance = TabAppearance.FlatButtons,
                    Padding = new Point(20, 5)
                };
                
                // Tab 1: Existing Entries
                TabPage tabExisting = new TabPage("Existing Entries")
                {
                    BackColor = _cBackground,
                    Padding = new Padding(15)
                };
                tabExisting.Controls.Add(CreateExistingEntriesPanel());
                _ctxTabControl.TabPages.Add(tabExisting);
                
                // Tab 2: Add New Entry
                TabPage tabAdd = new TabPage("Add New Entry")
                {
                    BackColor = _cBackground,
                    Padding = new Padding(15)
                };
                tabAdd.Controls.Add(CreateAddEntryPanel());
                _ctxTabControl.TabPages.Add(tabAdd);
                
                _contextMenuContentPanel.Controls.Add(_ctxTabControl);
                
                // Status Bar for Context Menu
                _ctxLblStatus = new Label
                {
                    Dock = DockStyle.Bottom,
                    Height = 30,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = _cTextDim,
                    Font = new Font("Segoe UI", 9),
                    BackColor = _cBackground,
                    Text = "Ready"
                };
                _contextMenuContentPanel.Controls.Add(_ctxLblStatus);

                _contentPanel.Controls.Add(_contextMenuContentPanel);
            }
            catch (Exception ex)
            {
                // Ensure panel exists to prevent NRE in ShowView
                if (_contextMenuContentPanel == null)
                {
                    _contextMenuContentPanel = new Panel { Visible = false };
                    _contentPanel?.Controls.Add(_contextMenuContentPanel);
                }

                MessageBox.Show($"Error initializing Context Menu Manager:\n{ex.Message}\n\nStack:\n{ex.StackTrace}", 
                    "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Panel CreateExistingEntriesPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill };
            
            // Header with buttons
            Panel headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(0, 0, 0, 10)
            };
            
            _ctxBtnRefresh = new ModernButton
            {
                Text = "Refresh",
                IconChar = "\uE72C",
                BackColor = _cAccent,
                CornerBackColor = _cBackground,
                Location = new Point(0, 0),
                Width = 120
            };
            _ctxBtnRefresh.Click += (s, e) => RefreshContextEntries();
            _toolTip.SetToolTip(_ctxBtnRefresh, "Refresh context menu entries");
            
            _ctxBtnEdit = new ModernButton
            {
                Text = "Edit Selected",
                IconChar = "\uE70F",
                BackColor = _cAccent,
                CornerBackColor = _cBackground,
                Location = new Point(130, 0),
                Width = 130,
                Enabled = false
            };
            _ctxBtnEdit.Click += CtxBtnEdit_Click;
            _toolTip.SetToolTip(_ctxBtnEdit, "Edit selected context menu entry");
            
            _ctxBtnDelete = new ModernButton
            {
                Text = "Delete Selected",
                IconChar = "\uE74D",
                BackColor = _cDanger,
                CornerBackColor = _cBackground,
                Location = new Point(270, 0),
                Width = 150,
                Enabled = false
            };
            _ctxBtnDelete.Click += CtxBtnDelete_Click;
            _toolTip.SetToolTip(_ctxBtnDelete, "Delete selected context menu entry");
            
            headerPanel.Controls.Add(_ctxBtnRefresh);
            headerPanel.Controls.Add(_ctxBtnEdit);
            headerPanel.Controls.Add(_ctxBtnDelete);
            
            // ListView
            _ctxListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                MultiSelect = false,
                BackColor = _cBackground,
                ForeColor = _cText,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10),
                OwnerDraw = true
            };
            
            _ctxListView.Columns.Add("Menu Name", 250);
            _ctxListView.Columns.Add("Menu Type", 180);
            _ctxListView.Columns.Add("Application Path", 500);
            
            _ctxListView.DrawColumnHeader += ListView_DrawColumnHeader; // Reuse existing handler
            _ctxListView.DrawItem += ListView_DrawItem; // Reuse existing handler
            _ctxListView.DrawSubItem += ListView_DrawSubItem; // Reuse existing handler
            _ctxListView.SelectedIndexChanged += (s, e) => 
            {
                bool hasSelection = _ctxListView.SelectedItems.Count > 0;
                _ctxBtnEdit.Enabled = hasSelection;
                _ctxBtnDelete.Enabled = hasSelection;
            };
            _ctxListView.DoubleClick += (s, e) => CtxBtnEdit_Click(s, e); // Double-click to edit
            
            panel.Controls.Add(_ctxListView);
            panel.Controls.Add(headerPanel);
            
            return panel;
        }

        private Panel CreateAddEntryPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill };
            
            int yPos = 20;
            int labelWidth = 150;
            int fieldWidth = 600;
            int spacing = 30;
            
            // Application Path
            Label lblAppPath = new Label
            {
                Text = "Application Path:",
                Location = new Point(0, yPos),
                Size = new Size(labelWidth, 25),
                ForeColor = _cText,
                Font = new Font("Segoe UI", 10)
            };
            
            Panel appPathPanel = new Panel
            {
                Location = new Point(labelWidth, yPos),
                Size = new Size(fieldWidth, 30)
            };
            
            _ctxTxtAppPath = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = _cText,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10),
                Padding = new Padding(5)
            };
            
            _ctxBtnBrowse = new ModernButton
            {
                Text = "Browse...",
                IconChar = "\uE8B7",
                BackColor = _cAccent,
                CornerBackColor = Color.FromArgb(45, 45, 48),
                Dock = DockStyle.Right,
                Width = 100,
                Margin = new Padding(5, 0, 0, 0)
            };
            _ctxBtnBrowse.Click += CtxBtnBrowse_Click;
            _toolTip.SetToolTip(_ctxBtnBrowse, "Browse for application executable");
            
            appPathPanel.Controls.Add(_ctxTxtAppPath);
            appPathPanel.Controls.Add(_ctxBtnBrowse);
            
            yPos += spacing + 10;
            
            // Menu Name
            Label lblMenuName = new Label
            {
                Text = "Menu Name:",
                Location = new Point(0, yPos),
                Size = new Size(labelWidth, 25),
                ForeColor = _cText,
                Font = new Font("Segoe UI", 10)
            };
            
            _ctxTxtMenuName = new TextBox
            {
                Location = new Point(labelWidth, yPos),
                Size = new Size(fieldWidth, 30),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = _cText,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10),
                Padding = new Padding(5)
            };
            
            yPos += spacing + 10;
            
            // Menu Types
            Label lblMenuTypes = new Label
            {
                Text = "Add to:",
                Location = new Point(0, yPos),
                Size = new Size(labelWidth, 25),
                ForeColor = _cText,
                Font = new Font("Segoe UI", 10)
            };
            
            Panel menuTypesPanel = new Panel
            {
                Location = new Point(labelWidth, yPos),
                Size = new Size(fieldWidth, 100)
            };
            
            _ctxChkFolder = new CheckBox
            {
                Text = "Folder",
                Location = new Point(0, 0),
                ForeColor = _cText,
                Font = new Font("Segoe UI", 10),
                Checked = true,
                AutoSize = true
            };
            
            _ctxChkFile = new CheckBox
            {
                Text = "File",
                Location = new Point(100, 0),
                ForeColor = _cText,
                Font = new Font("Segoe UI", 10),
                Checked = true,
                AutoSize = true
            };
            
            _ctxChkFolderBackground = new CheckBox
            {
                Text = "Folder Background",
                Location = new Point(200, 0),
                ForeColor = _cText,
                Font = new Font("Segoe UI", 10),
                Checked = true,
                AutoSize = true
            };
            
            menuTypesPanel.Controls.Add(_ctxChkFolder);
            menuTypesPanel.Controls.Add(_ctxChkFile);
            menuTypesPanel.Controls.Add(_ctxChkFolderBackground);
            
            yPos += spacing + 40;
            
            // Add Button
            _ctxBtnAdd = new ModernButton
            {
                Text = "Add Entry",
                IconChar = "\uE710",
                BackColor = _cAccent,
                CornerBackColor = _cBackground,
                Location = new Point(labelWidth, yPos),
                Width = 150
            };
            _ctxBtnAdd.Click += CtxBtnAdd_Click;
            _toolTip.SetToolTip(_ctxBtnAdd, "Add new context menu entry");
            
            panel.Controls.Add(lblAppPath);
            panel.Controls.Add(appPathPanel);
            panel.Controls.Add(lblMenuName);
            panel.Controls.Add(_ctxTxtMenuName);
            panel.Controls.Add(lblMenuTypes);
            panel.Controls.Add(menuTypesPanel);
            panel.Controls.Add(_ctxBtnAdd);
            
            return panel;
        }

        private async void RefreshContextEntries()
        {
            try 
            {
                if (_ctxListView == null || _ctxLblStatus == null) return;

                _ctxListView.Items.Clear();
                _ctxBtnDelete.Enabled = false;
                _ctxBtnRefresh.Enabled = false;
                _ctxLblStatus.Text = "Loading registry entries...";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error determining context menu state: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            try
            {
                var entries = await Task.Run(() => _registryManager.ListEntries());
                
                if (entries.Count == 0)
                {
                    _ctxLblStatus.Text = "No custom context menu entries found";
                }
                else
                {
                    foreach (var entry in entries)
                    {
                        if (entry == null) continue;
                        
                        ListViewItem item = new ListViewItem(entry.DisplayName ?? "Unknown");
                        item.SubItems.Add(entry.MenuType ?? "-");
                        item.SubItems.Add(entry.Command ?? "-");
                        item.Tag = entry;
                        _ctxListView.Items.Add(item);
                    }
                    _ctxLblStatus.Text = $"Found {entries.Count} context menu entr{(entries.Count == 1 ? "y" : "ies")}";
                }
            }
            catch (Exception ex)
            {
                _ctxLblStatus.Text = $"Error loading entries: {ex.Message}";
                MessageBox.Show($"Error loading entries: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (_ctxBtnRefresh != null)
                    _ctxBtnRefresh.Enabled = true;
            }
        }

        private void CtxBtnBrowse_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select Application";
                dialog.Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*";
                dialog.FilterIndex = 1;
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _ctxTxtAppPath.Text = dialog.FileName;
                }
            }
        }

        private void CtxBtnAdd_Click(object? sender, EventArgs e)
        {
            string appPath = _ctxTxtAppPath.Text.Trim();
            string menuName = _ctxTxtMenuName.Text.Trim();
            
            if (string.IsNullOrEmpty(appPath))
            {
                MessageBox.Show("Please select an application path", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            if (string.IsNullOrEmpty(menuName))
            {
                MessageBox.Show("Please enter a menu name", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            var selectedTypes = new List<string>();
            if (_ctxChkFolder.Checked) selectedTypes.Add("Folder");
            if (_ctxChkFile.Checked) selectedTypes.Add("File");
            if (_ctxChkFolderBackground.Checked) selectedTypes.Add("Folder Background");
            
            if (selectedTypes.Count == 0)
            {
                MessageBox.Show("Please select at least one menu type", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            try
            {
                var (success, message) = _registryManager.AddEntry(appPath, menuName, selectedTypes);
                
                if (success)
                {
                    MessageBox.Show(message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _ctxLblStatus.Text = message;
                    
                    _ctxTxtAppPath.Text = "";
                    _ctxTxtMenuName.Text = "";
                    _ctxChkFolder.Checked = true;
                    _ctxChkFile.Checked = true;
                    _ctxChkFolderBackground.Checked = true;
                    
                    RefreshContextEntries();
                }
                else
                {
                    MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _ctxLblStatus.Text = $"Error: {message}";
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"Failed to add entry: {ex.Message}";
                MessageBox.Show(errorMsg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _ctxLblStatus.Text = errorMsg;
            }
        }

        private void CtxBtnEdit_Click(object? sender, EventArgs e)
        {
            if (_ctxListView.SelectedItems.Count == 0) return;
            
            ListViewItem selectedItem = _ctxListView.SelectedItems[0];
            if (selectedItem.Tag is not ContextMenuEntry entry) return;
            
            // Show edit dialog
            using (Form editForm = CreateEditEntryForm(entry))
            {
                if (editForm.ShowDialog() == DialogResult.OK)
                {
                    RefreshContextEntries();
                }
            }
        }

        private Form CreateEditEntryForm(ContextMenuEntry entry)
        {
            Form form = new Form
            {
                Text = "Edit Context Menu Entry",
                Size = new Size(700, 280),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = _cBackground,
                ForeColor = _cText
            };
            
            int yPos = 25;
            int labelWidth = 130;
            int fieldWidth = 500;
            int spacing = 50;
            
            // Menu Name
            Label lblMenuName = new Label
            {
                Text = "Menu Name:",
                Location = new Point(20, yPos),
                Size = new Size(labelWidth, 25),
                ForeColor = _cText,
                Font = new Font("Segoe UI", 10)
            };
            
            TextBox txtMenuName = new TextBox
            {
                Text = entry.DisplayName,
                Location = new Point(labelWidth + 30, yPos),
                Size = new Size(fieldWidth, 28),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = _cText,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10)
            };
            
            yPos += spacing;
            
            // Application Path
            Label lblAppPath = new Label
            {
                Text = "Application Path:",
                Location = new Point(20, yPos),
                Size = new Size(labelWidth, 25),
                ForeColor = _cText,
                Font = new Font("Segoe UI", 10)
            };
            
            TextBox txtAppPath = new TextBox
            {
                Text = entry.Command,
                Location = new Point(labelWidth + 30, yPos),
                Size = new Size(fieldWidth - 90, 28),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = _cText,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10)
            };
            
            ModernButton btnBrowse = new ModernButton
            {
                Text = "Browse",
                IconChar = "\uE8B7",
                BackColor = _cAccent,
                CornerBackColor = _cBackground,
                Location = new Point(labelWidth + 30 + fieldWidth - 80, yPos - 3),
                Size = new Size(80, 34)
            };
            btnBrowse.Click += (s, e) =>
            {
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Title = "Select Application";
                    dialog.Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*";
                    dialog.FilterIndex = 1;
                    
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        txtAppPath.Text = dialog.FileName;
                    }
                }
            };
            
            yPos += spacing + 20;
            
            // Menu Type (read-only info)
            Label lblMenuType = new Label
            {
                Text = $"Menu Type: {entry.MenuType}",
                Location = new Point(labelWidth + 30, yPos),
                Size = new Size(fieldWidth, 25),
                ForeColor = _cTextDim,
                Font = new Font("Segoe UI", 9, FontStyle.Italic)
            };
            
            yPos += spacing;
            
            // Buttons
            ModernButton btnSave = new ModernButton
            {
                Text = "Save Changes",
                IconChar = "\uE74E",
                BackColor = _cAccent,
                CornerBackColor = _cBackground,
                Location = new Point(labelWidth + 30, yPos),
                Size = new Size(140, 38)
            };
            _toolTip.SetToolTip(btnSave, "Save changes to context menu entry");
            
            ModernButton btnCancel = new ModernButton
            {
                Text = "Cancel",
                IconChar = "\uE711",
                BackColor = Color.FromArgb(60, 60, 60),
                CornerBackColor = _cBackground,
                Location = new Point(labelWidth + 30 + 150, yPos),
                Size = new Size(100, 38)
            };
            _toolTip.SetToolTip(btnCancel, "Cancel editing");
            
            btnSave.Click += (s, ev) =>
            {
                string newMenuName = txtMenuName.Text.Trim();
                string newAppPath = txtAppPath.Text.Trim();
                
                if (string.IsNullOrEmpty(newMenuName))
                {
                    MessageBox.Show("Menu name cannot be empty", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                if (string.IsNullOrEmpty(newAppPath))
                {
                    MessageBox.Show("Application path cannot be empty", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                try
                {
                    var (success, message) = _registryManager.UpdateEntry(entry.RegistryPath, newMenuName, newAppPath, entry.MenuType);
                    
                    if (success)
                    {
                        MessageBox.Show(message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        _ctxLblStatus.Text = message;
                        form.DialogResult = DialogResult.OK;
                        form.Close();
                    }
                    else
                    {
                        MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to update entry: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            btnCancel.Click += (s, ev) =>
            {
                form.DialogResult = DialogResult.Cancel;
                form.Close();
            };
            
            form.Controls.Add(lblMenuName);
            form.Controls.Add(txtMenuName);
            form.Controls.Add(lblAppPath);
            form.Controls.Add(txtAppPath);
            form.Controls.Add(btnBrowse);
            form.Controls.Add(lblMenuType);
            form.Controls.Add(btnSave);
            form.Controls.Add(btnCancel);
            
            form.AcceptButton = null; // Prevent Enter from closing dialog
            form.CancelButton = null;
            
            return form;
        }

        private void CtxBtnDelete_Click(object? sender, EventArgs e)
        {
            if (_ctxListView.SelectedItems.Count == 0) return;
            
            ListViewItem selectedItem = _ctxListView.SelectedItems[0];
            if (selectedItem.Tag is not ContextMenuEntry entry) return;
            
            DialogResult result = MessageBox.Show(
                $"Are you sure you want to delete '{entry.DisplayName}'?\n\nThis action cannot be undone.",
                "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            
            if (result != DialogResult.Yes) return;
            
            try
            {
                var (success, message) = _registryManager.DeleteEntry(entry.RegistryPath);
                
                if (success)
                {
                    MessageBox.Show(message, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _ctxLblStatus.Text = message;
                    RefreshContextEntries();
                }
                else
                {
                    MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _ctxLblStatus.Text = $"Error: {message}";
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"Failed to delete entry: {ex.Message}";
                MessageBox.Show(errorMsg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _ctxLblStatus.Text = errorMsg;
            }
        }
    }
}
