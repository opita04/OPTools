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
            
            _ctxBtnDelete = new ModernButton
            {
                Text = "Delete Selected",
                IconChar = "\uE74D",
                BackColor = _cDanger,
                CornerBackColor = _cBackground,
                Location = new Point(130, 0),
                Width = 150,
                Enabled = false
            };
            _ctxBtnDelete.Click += CtxBtnDelete_Click;
            
            headerPanel.Controls.Add(_ctxBtnRefresh);
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
            _ctxListView.SelectedIndexChanged += (s, e) => _ctxBtnDelete.Enabled = _ctxListView.SelectedItems.Count > 0;
            
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
            
            panel.Controls.Add(lblAppPath);
            panel.Controls.Add(appPathPanel);
            panel.Controls.Add(lblMenuName);
            panel.Controls.Add(_ctxTxtMenuName);
            panel.Controls.Add(lblMenuTypes);
            panel.Controls.Add(menuTypesPanel);
            panel.Controls.Add(_ctxBtnAdd);
            
            return panel;
        }

        private void RefreshContextEntries()
        {
            _ctxListView.Items.Clear();
            _ctxBtnDelete.Enabled = false;
            
            try
            {
                var entries = _registryManager.ListEntries();
                
                if (entries.Count == 0)
                {
                    _ctxLblStatus.Text = "No custom context menu entries found";
                    return;
                }
                
                foreach (var entry in entries)
                {
                    ListViewItem item = new ListViewItem(entry.DisplayName);
                    item.SubItems.Add(entry.MenuType);
                    item.SubItems.Add(entry.Command);
                    item.Tag = entry;
                    _ctxListView.Items.Add(item);
                }
                
                _ctxLblStatus.Text = $"Found {entries.Count} context menu entr{(entries.Count == 1 ? "y" : "ies")}";
            }
            catch (Exception ex)
            {
                _ctxLblStatus.Text = $"Error loading entries: {ex.Message}";
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
