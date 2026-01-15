using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using OPTools.Registry;
using OPTools.Utils;

namespace OPTools.Forms;

/// <summary>
/// Panel for managing Windows context menu entries
/// </summary>
public class ContextMenuManagerPanel : Panel
{
    // Theme Colors (matching MainForm)
    private readonly Color _cBackground = Color.FromArgb(30, 30, 30);
    private readonly Color _cAccent = Color.FromArgb(0, 122, 204);
    private readonly Color _cDanger = Color.FromArgb(217, 83, 79);
    private readonly Color _cSuccess = Color.FromArgb(92, 184, 92);
    private readonly Color _cText = Color.FromArgb(241, 241, 241);
    private readonly Color _cTextDim = Color.FromArgb(150, 150, 150);
    private readonly Color _cCardBg = Color.FromArgb(45, 45, 48);

    private readonly ContextMenuRegistryManager _manager;
    private FlowLayoutPanel _entriesPanel = null!;
    private Label _lblStatus = null!;
    private ModernButton _btnAddEntry = null!;
    private Panel _headerPanel = null!;
    private ToolTip _toolTip = null!;

    public ContextMenuManagerPanel()
    {
        _manager = new ContextMenuRegistryManager();
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        _toolTip = new ToolTip();
        _toolTip.AutoPopDelay = 5000;
        _toolTip.InitialDelay = 1000;
        _toolTip.ReshowDelay = 500;
        _toolTip.ShowAlways = true;

        this.Dock = DockStyle.Fill;
        this.BackColor = _cBackground;
        this.Padding = new Padding(24, 24, 24, 24);

        // Header with title and add button
        _headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = _cBackground,
            Padding = new Padding(0, 0, 0, 16)
        };

        Label lblTitle = new Label
        {
            Text = "Context Menu Manager",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = _cText,
            AutoSize = true,
            Location = new Point(0, 8)
        };

        _btnAddEntry = new ModernButton
        {
            Text = "Add Context Menu Entry",
            Image = IconHelper.GetActionIcon("Add"),
            BackColor = _cAccent,
            Width = 200,
            Height = 40,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(this.Width - 260, 8)
        };
        _btnAddEntry.Click += BtnAddEntry_Click;
        _toolTip.SetToolTip(_btnAddEntry, "Add a new context menu entry");

        _headerPanel.Controls.Add(lblTitle);
        _headerPanel.Controls.Add(_btnAddEntry);

        // Status bar
        _lblStatus = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 30,
            ForeColor = _cTextDim,
            Font = new Font("Segoe UI", 9),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Manage Windows right-click menu entries"
        };

        // Entries list
        _entriesPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = _cBackground,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };

        this.Controls.Add(_entriesPanel);
        this.Controls.Add(_lblStatus);
        this.Controls.Add(_headerPanel);

        this.Resize += (s, e) =>
        {
            _btnAddEntry.Location = new Point(this.Width - 260, 8);
            RefreshEntryCards();
        };
    }

    public void LoadEntries()
    {
        var entries = _manager.ListEntries();
        RefreshEntryCards(entries);
        _lblStatus.Text = $"{entries.Count} context menu entr(y/ies) found";
    }

    private void RefreshEntryCards(System.Collections.Generic.List<ContextMenuEntry>? entries = null)
    {
        _entriesPanel.SuspendLayout();
        _entriesPanel.Controls.Clear();

        var entriesToDisplay = entries ?? _manager.ListEntries();

        if (entriesToDisplay.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = "No context menu entries configured.\nClick 'Add Context Menu Entry' to create one.",
                Font = new Font("Segoe UI", 12),
                ForeColor = _cTextDim,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Width = _entriesPanel.Width - 40,
                Height = 100
            };
            _entriesPanel.Controls.Add(emptyLabel);
        }
        else
        {
            foreach (var entry in entriesToDisplay)
            {
                var card = CreateEntryCard(entry);
                _entriesPanel.Controls.Add(card);
            }
        }

        _entriesPanel.ResumeLayout();
        if (entries == null)
        {
            _lblStatus.Text = $"{entriesToDisplay.Count} context menu entr(y/ies) found";
        }
    }

    private Panel CreateEntryCard(ContextMenuEntry entry)
    {
        var cardWidth = Math.Max(450, _entriesPanel.Width - 40);

        var card = new Panel
        {
            Width = cardWidth,
            Height = 80,
            BackColor = _cCardBg,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(16),
            Tag = entry
        };

        // Round the corners
        card.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = GetRoundedPath(card.ClientRectangle, 12);
            using var brush = new SolidBrush(_cCardBg);
            e.Graphics.FillPath(brush, path);
        };

        // Menu name
        var lblName = new Label
        {
            Text = entry.DisplayName,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = _cText,
            AutoSize = true,
            Location = new Point(16, 10)
        };

        // Menu type badge
        var lblType = new Label
        {
            Text = entry.MenuType,
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = _cAccent,
            BackColor = Color.FromArgb(40, 40, 45),
            AutoSize = true,
            Location = new Point(16, 32)
        };

        // Command path (truncated)
        var lblCommand = new Label
        {
            Text = $"Command: {TruncatePath(entry.Command, 50)}",
            Font = new Font("Segoe UI", 8),
            ForeColor = _cTextDim,
            AutoSize = true,
            Location = new Point(16, 50)
        };

        // Icon indicator
        string iconText = string.IsNullOrEmpty(entry.IconPath) ? "No icon" : "Has icon";
        var lblIcon = new Label
        {
            Text = iconText,
            Font = new Font("Segoe UI", 8),
            ForeColor = string.IsNullOrEmpty(entry.IconPath) ? _cTextDim : _cSuccess,
            AutoSize = true,
            Location = new Point(cardWidth - 150, 32)
        };

        // Action buttons
        var btnEdit = CreateSmallButton("", _cAccent, "Edit this entry", IconHelper.GetActionIcon("Edit"));
        btnEdit.Location = new Point(cardWidth - 90, 22);
        btnEdit.Click += (s, e) => EditEntry(entry);

        var btnDelete = CreateSmallButton("", _cDanger, "Delete this entry", IconHelper.GetActionIcon("Delete"));
        btnDelete.Location = new Point(cardWidth - 48, 22);
        btnDelete.Click += (s, e) =>
        {
            if (MessageBox.Show($"Delete context menu entry '{entry.DisplayName}'?", "Confirm Delete",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                var result = _manager.DeleteEntry(entry.RegistryPath);
                if (result.Success)
                {
                    RefreshEntryCards();
                }
                else
                {
                    MessageBox.Show(result.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        };

        card.Controls.Add(lblName);
        card.Controls.Add(lblType);
        card.Controls.Add(lblCommand);
        card.Controls.Add(lblIcon);
        card.Controls.Add(btnEdit);
        card.Controls.Add(btnDelete);

        return card;
    }

    private ModernButton CreateSmallButton(string text, Color accentColor, string tooltipText, Image? icon = null)
    {
        var bgColor = Color.FromArgb(60, 60, 63);
        var hoverColor = Color.FromArgb(80, accentColor.R, accentColor.G, accentColor.B);

        var btn = new ModernButton
        {
            Text = text,
            Image = icon,
            Width = 36,
            Height = 36,
            BackColor = bgColor,
            HoverColor = hoverColor,
            BorderRadius = 10,
            ImagePadding = 6
        };

        if (!string.IsNullOrEmpty(tooltipText))
        {
            _toolTip.SetToolTip(btn, tooltipText);
        }

        return btn;
    }

    private void BtnAddEntry_Click(object? sender, EventArgs e)
    {
        using var dialog = new ContextMenuEntryDialog();
        dialog.StartPosition = FormStartPosition.CenterParent;

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var result = _manager.AddEntry(
                dialog.AppPath,
                dialog.MenuName,
                dialog.SelectedTypes,
                dialog.IconPath);

            if (result.Success)
            {
                RefreshEntryCards();
            }
            else
            {
                MessageBox.Show(result.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void EditEntry(ContextMenuEntry entry)
    {
        using var dialog = new ContextMenuEntryDialog(entry);
        dialog.StartPosition = FormStartPosition.CenterParent;

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var result = _manager.UpdateEntry(
                entry.RegistryPath,
                dialog.MenuName,
                dialog.AppPath,
                entry.MenuType,
                dialog.IconPath);

            if (result.Success)
            {
                RefreshEntryCards();
            }
            else
            {
                MessageBox.Show(result.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private static string TruncatePath(string path, int maxLength)
    {
        if (string.IsNullOrEmpty(path) || path.Length <= maxLength)
            return path;
        return "..." + path.Substring(path.Length - maxLength + 3);
    }

    private static GraphicsPath GetRoundedPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        float r = radius;
        path.AddArc(rect.X, rect.Y, r, r, 180, 90);
        path.AddArc(rect.Right - r, rect.Y, r, r, 270, 90);
        path.AddArc(rect.Right - r, rect.Bottom - r, r, r, 0, 90);
        path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>
/// Dialog for adding/editing context menu entries
/// </summary>
public class ContextMenuEntryDialog : Form
{
    private readonly ContextMenuEntry? _existingEntry;

    // Theme Colors
    private readonly Color _cBackground = Color.FromArgb(30, 30, 30);
    private readonly Color _cCardBg = Color.FromArgb(45, 45, 48);
    private readonly Color _cAccent = Color.FromArgb(0, 122, 204);
    private readonly Color _cText = Color.FromArgb(241, 241, 241);
    private readonly Color _cTextDim = Color.FromArgb(150, 150, 150);

    public string MenuName { get; private set; } = string.Empty;
    public string AppPath { get; private set; } = string.Empty;
    public string? IconPath { get; private set; }
    public System.Collections.Generic.List<string> SelectedTypes { get; } = new();

    private TextBox _txtName = null!;
    private TextBox _txtAppPath = null!;
    private TextBox _txtIconPath = null!;
    private CheckedListBox _lstMenuTypes = null!;
    private PictureBox _picIconPreview = null!;

    public ContextMenuEntryDialog(ContextMenuEntry? existingEntry = null)
    {
        _existingEntry = existingEntry;
        InitializeComponent();
        if (existingEntry != null)
        {
            LoadEntryData(existingEntry);
        }
    }

    private void InitializeComponent()
    {
        this.Text = _existingEntry == null ? "New Context Menu Entry" : "Edit Context Menu Entry";
        this.Size = new Size(600, 500);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = _cBackground;
        this.ForeColor = _cText;

        int y = 20;
        int labelWidth = 120;
        int leftMargin = 20;
        int rightMargin = 20;
        int inputWidth = this.Width - leftMargin - rightMargin - labelWidth - 20;

        // Icon preview (Centered Header)
        _picIconPreview = new PictureBox
        {
            Size = new Size(64, 64),
            Location = new Point((this.Width - 64) / 2, y),
            BackColor = _cCardBg,
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.StretchImage
        };
        this.Controls.Add(_picIconPreview);
        y += 84; // 64 (height) + 20 (padding)

        // Name
        AddLabel("Menu Name:", leftMargin, y);
        _txtName = AddTextBox(leftMargin + labelWidth, y, inputWidth);
        y += 45;

        // App Path
        AddLabel("Application Path:", leftMargin, y);
        _txtAppPath = AddTextBox(leftMargin + labelWidth, y, inputWidth - 35);
        var btnBrowseApp = new Button
        {
            Text = "...",
            Location = new Point(leftMargin + labelWidth + inputWidth - 30, y - 2),
            Size = new Size(30, 27),
            FlatStyle = FlatStyle.Flat,
            BackColor = _cCardBg,
            ForeColor = _cText
        };
        btnBrowseApp.Click += BtnBrowseApp_Click;
        this.Controls.Add(btnBrowseApp);
        y += 50;

        // Icon Path (optional)
        AddLabel("Icon Path (Optional):", leftMargin, y);
        _txtIconPath = AddTextBox(leftMargin + labelWidth, y, inputWidth - 35);
        _txtIconPath.PlaceholderText = "Leave empty to use default icon";

        var btnBrowseIcon = new Button
        {
            Text = "...",
            Location = new Point(leftMargin + labelWidth + inputWidth - 30, y - 2),
            Size = new Size(30, 27),
            FlatStyle = FlatStyle.Flat,
            BackColor = _cCardBg,
            ForeColor = _cText
        };
        btnBrowseIcon.Click += BtnBrowseIcon_Click;
        this.Controls.Add(btnBrowseIcon);

        _txtIconPath.TextChanged += (s, e) => UpdateIconPreview();

        y += 45;

        // Menu Types
        AddLabel("Show in:", leftMargin, y);
        _lstMenuTypes = new CheckedListBox
        {
            Location = new Point(leftMargin + labelWidth, y - 2),
            Size = new Size(inputWidth, 100),
            BackColor = _cCardBg,
            ForeColor = _cText,
            CheckOnClick = true,
            BorderStyle = BorderStyle.FixedSingle
        };
        _lstMenuTypes.Items.AddRange(new[] { "Folder", "Folder Background", "File" });
        _lstMenuTypes.SetItemChecked(0, true); // Default: Folder
        _lstMenuTypes.SetItemChecked(2, true); // Default: File
        this.Controls.Add(_lstMenuTypes);
        y += 120;

        // Buttons
        var btnSave = new ModernButton
        {
            Text = "Save",
            BackColor = _cAccent,
            Width = 120,
            Height = 40,
            Location = new Point(this.Width - 270, y)
        };
        btnSave.Click += BtnSave_Click;

        var btnCancel = new ModernButton
        {
            Text = "Cancel",
            BackColor = _cCardBg,
            Width = 120,
            Height = 40,
            Location = new Point(this.Width - 130, y)
        };
        btnCancel.Click += (s, e) =>
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        };

        this.Controls.Add(btnSave);
        this.Controls.Add(btnCancel);
    }

    private void LoadEntryData(ContextMenuEntry entry)
    {
        _txtName.Text = entry.DisplayName;
        _txtAppPath.Text = entry.Command;
        _txtIconPath.Text = entry.IconPath ?? string.Empty;

        // Select menu types
        _lstMenuTypes.SetItemChecked(0, entry.MenuType == "Folder");
        _lstMenuTypes.SetItemChecked(1, entry.MenuType == "Folder Background");
        _lstMenuTypes.SetItemChecked(2, entry.MenuType == "File");

        UpdateIconPreview();
    }

    private void UpdateIconPreview()
    {
        var iconPath = _txtIconPath.Text;
        if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
        {
            try
            {
                using var icon = Icon.ExtractAssociatedIcon(iconPath);
                _picIconPreview.Image = icon?.ToBitmap();
            }
            catch
            {
                _picIconPreview.Image = null;
            }
        }
        else
        {
            _picIconPreview.Image = null;
        }
    }

    private void BtnBrowseApp_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Executable Files|*.exe;*.bat;*.cmd;*.ps1|All Files|*.*",
            Title = "Select Application"
        };

        if (!string.IsNullOrEmpty(_txtAppPath.Text) && File.Exists(_txtAppPath.Text))
            dialog.FileName = _txtAppPath.Text;

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _txtAppPath.Text = dialog.FileName;

            // Auto-suggest menu name from file name if empty
            if (string.IsNullOrEmpty(_txtName.Text))
            {
                _txtName.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    private void BtnBrowseIcon_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Icon Files|*.ico|Executable Files|*.exe|All Files|*.*",
            Title = "Select Icon File"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _txtIconPath.Text = dialog.FileName;
        }
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        if (ValidateInput())
        {
            MenuName = _txtName.Text.Trim();
            AppPath = _txtAppPath.Text.Trim();
            IconPath = string.IsNullOrEmpty(_txtIconPath.Text.Trim()) ? null : _txtIconPath.Text.Trim();

            SelectedTypes.Clear();
            if (_lstMenuTypes.GetItemChecked(0)) SelectedTypes.Add("Folder");
            if (_lstMenuTypes.GetItemChecked(1)) SelectedTypes.Add("Folder Background");
            if (_lstMenuTypes.GetItemChecked(2)) SelectedTypes.Add("File");

            if (SelectedTypes.Count == 0)
            {
                MessageBox.Show("Please select at least one menu type.", "Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            MessageBox.Show("Please enter a menu name.", "Validation Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtName.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(_txtAppPath.Text))
        {
            MessageBox.Show("Please enter an application path.", "Validation Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtAppPath.Focus();
            return false;
        }

        // Use the same logic as the Registry Manager to extract the executable path
        // This handles arguments in the path field (e.g. "cmd.exe /k")
        var registryManager = new ContextMenuRegistryManager();
        string executablePath = registryManager.ExtractExecutablePath(_txtAppPath.Text);

        if (!File.Exists(executablePath))
        {
            MessageBox.Show($"Application file does not exist:\n{executablePath}", "Validation Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtAppPath.Focus();
            return false;
        }

        if (!string.IsNullOrEmpty(_txtIconPath.Text) && !File.Exists(_txtIconPath.Text))
        {
            MessageBox.Show("Icon file does not exist. Leave empty to use default icon.", "Validation Error",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtIconPath.Focus();
            return false;
        }

        return true;
    }

    private Label AddLabel(string text, int x, int y)
    {
        var lbl = new Label
        {
            Text = text,
            Location = new Point(x, y + 3),
            AutoSize = true,
            ForeColor = _cTextDim
        };
        this.Controls.Add(lbl);
        return lbl;
    }

    private TextBox AddTextBox(int x, int y, int width)
    {
        var txt = new TextBox
        {
            Location = new Point(x, y),
            Width = width,
            BackColor = _cCardBg,
            ForeColor = _cText,
            BorderStyle = BorderStyle.FixedSingle
        };
        this.Controls.Add(txt);
        return txt;
    }
}
