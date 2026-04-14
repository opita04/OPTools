using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using OPTools.Core;
using OPTools.Tools;
using OPTools.Utils;

namespace OPTools.Forms;

/// <summary>
/// Panel for monitoring and killing dev tool processes in real-time.
/// </summary>
public class ProcessMonitorPanel : Panel
{
    // Theme colors (matching MainForm)
    private readonly Color _cBackground = Color.FromArgb(30, 30, 30);
    private readonly Color _cSidebar = Color.FromArgb(45, 45, 45);
    private readonly Color _cText = Color.FromArgb(230, 230, 230);
    private readonly Color _cTextDim = Color.FromArgb(150, 150, 150);
    private readonly Color _cAccent = Color.FromArgb(0, 122, 204);
    private readonly Color _cDanger = Color.FromArgb(220, 53, 69);
    private readonly Color _cGridHeader = Color.FromArgb(60, 60, 60);

    // Controls
    private ListView _listView = null!;
    private TextBox _txtSearch = null!;
    private ComboBox _cboCategory = null!;
    private CheckBox _chkAutoRefresh = null!;
    private Label _lblStatus = null!;
    private ProgressBar _progressBar = null!;
    private ModernButton _btnRefresh = null!;
    private ModernButton _btnKillSelected = null!;
    private ModernButton _btnKillAllNode = null!;
    private ModernButton _btnKillAllDev = null!;
    private System.Windows.Forms.Timer _refreshTimer = null!;
    private Panel _headerPanel = null!;

    // Data
    private List<DevProcessInfo> _allProcesses = new();
    private bool _isLoading = false;
    private CancellationTokenSource? _detailLoadingCts; // Cancel token for detail loading
    private List<DevProcessInfo> _partialProcesses = new();  // Processes loaded without details

    public ProcessMonitorPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Dock = DockStyle.Fill;
        this.BackColor = _cBackground;
        this.Padding = new Padding(24);

        // Title
        var lblTitle = new Label
        {
            Text = "Kill Processes",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = _cText,
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 0, 0, 16)
        };

        // Header panel with controls
        _headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            Padding = new Padding(0, 0, 0, 8)
        };

        // Search box
        var lblSearch = new Label
        {
            Text = "Search:",
            ForeColor = _cText,
            Font = new Font("Segoe UI", 10),
            AutoSize = true,
            Location = new Point(0, 8)
        };

        _txtSearch = new TextBox
        {
            Location = new Point(60, 5),
            Width = 180,
            Font = new Font("Segoe UI", 10),
            BackColor = _cSidebar,
            ForeColor = _cText,
            BorderStyle = BorderStyle.FixedSingle
        };
        _txtSearch.TextChanged += TxtSearch_TextChanged;

        // Category filter
        var lblCategory = new Label
        {
            Text = "Filter:",
            ForeColor = _cText,
            Font = new Font("Segoe UI", 10),
            AutoSize = true,
            Location = new Point(260, 8)
        };

        _cboCategory = new ComboBox
        {
            Location = new Point(310, 5),
            Width = 130,
            Font = new Font("Segoe UI", 10),
            BackColor = _cSidebar,
            ForeColor = _cText,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat
        };
        _cboCategory.Items.Add("All Categories");
        foreach (var category in DevProcessScanner.GetAllCategories())
        {
            _cboCategory.Items.Add(category);
        }
        _cboCategory.SelectedIndex = 0;
        _cboCategory.SelectedIndexChanged += CboCategory_SelectedIndexChanged;

        // Auto-refresh checkbox
        _chkAutoRefresh = new CheckBox
        {
            Text = "Auto-refresh",
            ForeColor = _cText,
            Font = new Font("Segoe UI", 10),
            AutoSize = true,
            Checked = true,
            Location = new Point(460, 8),
            BackColor = _cBackground
        };
        _chkAutoRefresh.CheckedChanged += ChkAutoRefresh_CheckedChanged;

        // Refresh button
        _btnRefresh = new ModernButton
        {
            Text = "Refresh",
            Image = IconHelper.GetActionIcon("Refresh"),
            BackColor = _cAccent,
            Width = 100,
            Height = 32,
            Location = new Point(580, 3)
        };
        _btnRefresh.Click += BtnRefresh_Click;

        _headerPanel.Controls.Add(lblSearch);
        _headerPanel.Controls.Add(_txtSearch);
        _headerPanel.Controls.Add(lblCategory);
        _headerPanel.Controls.Add(_cboCategory);
        _headerPanel.Controls.Add(_chkAutoRefresh);
        _headerPanel.Controls.Add(_btnRefresh);

        // ListView
        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            MultiSelect = true,
            BackColor = _cBackground,
            ForeColor = _cText,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10),
            OwnerDraw = true,
            CheckBoxes = true
        };

        _listView.Columns.Add("", 30);  // Checkbox column
        _listView.Columns.Add("Type", 100);
        _listView.Columns.Add("Process", 100);
        _listView.Columns.Add("PID", 70);
        _listView.Columns.Add("Memory", 80);
        _listView.Columns.Add("Running", 90);
        _listView.Columns.Add("Command Line", 350);

        _listView.DrawColumnHeader += ListView_DrawColumnHeader;
        _listView.DrawItem += ListView_DrawItem;
        _listView.DrawSubItem += ListView_DrawSubItem;
        _listView.ItemChecked += ListView_ItemChecked;
        _listView.DoubleClick += ListView_DoubleClick;

        // Bottom action panel
        var actionPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 80,
            Padding = new Padding(0, 16, 0, 0)
        };

        _btnKillSelected = new ModernButton
        {
            Text = "Kill Selected (0)",
            Image = IconHelper.GetActionIcon("Kill"),
            BackColor = _cDanger,
            Width = 160,
            Height = 40,
            Location = new Point(0, 16),
            Enabled = false
        };
        _btnKillSelected.Click += BtnKillSelected_Click;

        _btnKillAllNode = new ModernButton
        {
            Text = "Kill All Node.js (0)",
            Image = IconHelper.GetActionIcon("Kill"),
            BackColor = _cAccent,
            Width = 170,
            Height = 40,
            Location = new Point(180, 16)
        };
        _btnKillAllNode.Click += BtnKillAllNode_Click;

        _btnKillAllDev = new ModernButton
        {
            Text = "Kill All Dev (0)",
            Image = IconHelper.GetActionIcon("Kill"),
            BackColor = _cDanger,
            Width = 150,
            Height = 40,
            Location = new Point(370, 16)
        };
        _btnKillAllDev.Click += BtnKillAllDev_Click;

        actionPanel.Controls.Add(_btnKillSelected);
        actionPanel.Controls.Add(_btnKillAllNode);
        actionPanel.Controls.Add(_btnKillAllDev);

        // Status bar
        var statusPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 30
        };

        _lblStatus = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = _cTextDim,
            Font = new Font("Segoe UI", 9),
            BackColor = _cBackground
        };

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Bottom,
            Height = 3,
            Style = ProgressBarStyle.Marquee,
            Visible = false
        };

        statusPanel.Controls.Add(_lblStatus);
        statusPanel.Controls.Add(_progressBar);

        // Auto-refresh timer
        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 30000  // 30 seconds
        };
        _refreshTimer.Tick += RefreshTimer_Tick;

        // Assemble layout
        this.Controls.Add(_listView);
        this.Controls.Add(actionPanel);
        this.Controls.Add(statusPanel);
        this.Controls.Add(_headerPanel);
        this.Controls.Add(lblTitle);
    }

    /// <summary>
    /// Refreshes the process list.
    /// </summary>

    /// <summary>
    /// Refreshes the process list using fast loading, then loads details in background.
    /// </summary>
    public async void RefreshProcesses()
    {
        if (_isLoading) return;
        _isLoading = true;

        // Cancel any ongoing detail loading
        _detailLoadingCts?.Cancel();
        _detailLoadingCts = new CancellationTokenSource();

        _progressBar.Visible = true;
        _lblStatus.Text = "Loading process list...";
        _btnRefresh.Enabled = false;

        try
        {
            // PHASE 1: Fast load - name, PID, category only (instant)
            var fastProcesses = await DevProcessScanner.GetRunningDevProcessesFastAsync();
            
            // Merge with existing partial data (preserve loaded details for matching PIDs)
            _allProcesses = MergeProcessLists(fastProcesses, _partialProcesses);
            
            // Update partial processes for next refresh
            _partialProcesses = _allProcesses.Where(p => p.IsFullyLoaded).ToList();
            
            // Display immediately with partial data
            ApplyFilters();
            UpdateButtonCounts();
            
            _lblStatus.Text = $"{_allProcesses.Count} dev process(es) running • Last refresh: {DateTime.Now:HH:mm:ss}";
            _progressBar.Visible = false;

            // Start auto-refresh if enabled
            if (_chkAutoRefresh.Checked && !_refreshTimer.Enabled)
            {
                _refreshTimer.Start();
            }

            // PHASE 2: Load details in background (non-blocking)
            _ = LoadProcessDetailsInBackground(_detailLoadingCts.Token);
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Error: {ex.Message}";
        }
        finally
        {
            _btnRefresh.Enabled = true;
            _isLoading = false;
        }
    }


    /// <summary>
    /// Merges fast-loaded processes with existing partial data to preserve already-loaded details.
    /// </summary>
    private List<DevProcessInfo> MergeProcessLists(List<DevProcessInfo> newProcesses, List<DevProcessInfo> existingProcesses)
    {
        var merged = new List<DevProcessInfo>();
        var existingMap = existingProcesses.ToDictionary(p => p.ProcessId);

        foreach (var newProc in newProcesses)
        {
            if (existingMap.TryGetValue(newProc.ProcessId, out var existingProc))
            {
                // Preserve loaded details from existing process
                newProc.MemoryMB = existingProc.MemoryMB;
                newProc.StartTime = existingProc.StartTime;
                newProc.CommandLine = existingProc.CommandLine;
                newProc.LoadingState = existingProc.LoadingState;
            }
            merged.Add(newProc);
        }

        return merged;
    }


    /// <summary>
    /// Loads process details in background and updates UI incrementally.
    /// </summary>
    private async Task LoadProcessDetailsInBackground(CancellationToken cancellationToken)
    {
        try
        {
            // Filter processes that need details loaded
            var processesNeedingDetails = _allProcesses
                .Where(p => !p.IsFullyLoaded && p.LoadingState != ProcessLoadingState.Failed)
                .ToList();

            if (processesNeedingDetails.Count == 0)
                return;

            await DevProcessScanner.PopulateProcessDetailsAsync(
                processesNeedingDetails,
                batchSize: 50,
                progressCallback: progress =>
                {
                    // Update progress bar
                    this.Invoke((MethodInvoker)(() =>
                    {
                        if (!_progressBar.Visible)
                            _progressBar.Visible = true;
                        _lblStatus.Text = $"Loading details... {(int)(progress * 100)}%";
                    }));
                });

            cancellationToken.ThrowIfCancellationRequested();

            // Update UI with loaded details
            this.Invoke((MethodInvoker)(() =>
            {
                RefreshDisplayedRows();
                _progressBar.Visible = false;
                _lblStatus.Text = $"{_allProcesses.Count} dev process(es) running • Last refresh: {DateTime.Now:HH:mm:ss}";
            }));
        }
        catch (OperationCanceledException)
        {
            // Loading was cancelled, ignore
        }
        catch (Exception ex)
        {
            this.Invoke((MethodInvoker)(() =>
            {
                _lblStatus.Text = $"Error loading details: {ex.Message}";
                _progressBar.Visible = false;
            }));
        }
    }


    /// <summary>
    /// Refreshes only the displayed rows without rebuilding the entire list.
    /// Updates only the columns that have been loaded.
    /// </summary>
    private void RefreshDisplayedRows()
    {
        _listView.BeginUpdate();

        foreach (ListViewItem item in _listView.Items)
        {
            if (item.Tag is DevProcessInfo proc)
            {
                // Update only the detail columns
                item.SubItems[4].Text = proc.LoadingState == ProcessLoadingState.Loading 
                    ? "Loading..." 
                    : $"{proc.MemoryMB} MB";
                
                item.SubItems[5].Text = proc.RunningTime;
                item.SubItems[6].Text = proc.LoadingState == ProcessLoadingState.Loading 
                    ? "Loading..." 
                    : (proc.CommandLine ?? string.Empty);
                
                item.SubItems[6].Tag = proc.LoadingState;  // Store loading state for drawing
            }
        }

        _listView.EndUpdate();
    }


    private void ApplyFilters()
    {
        var filtered = _allProcesses.AsEnumerable();

        // Apply category filter
        if (_cboCategory.SelectedIndex > 0)
        {
            var category = _cboCategory.SelectedItem?.ToString() ?? "";
            filtered = filtered.Where(p => p.Category == category);
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(_txtSearch.Text))
        {
            var search = _txtSearch.Text.ToLowerInvariant();
            filtered = filtered.Where(p =>
                p.ProcessName.ToLowerInvariant().Contains(search) ||
                p.CommandLine.ToLowerInvariant().Contains(search) ||
                p.ProcessId.ToString().Contains(search));
        }

        DisplayProcesses(filtered.ToList());
    }

    private void DisplayProcesses(List<DevProcessInfo> processes)
    {
        _listView.BeginUpdate();
        _listView.Items.Clear();

        foreach (var proc in processes)
        {
            var item = new ListViewItem();
            item.SubItems.Add(proc.Category);
            item.SubItems.Add(proc.ProcessName);
            item.SubItems.Add(proc.ProcessId.ToString());
            // Detail columns - show loading indicator or actual data
            item.SubItems.Add(
                proc.LoadingState == ProcessLoadingState.Loading ? "Loading..." : 
                (proc.IsFullyLoaded ? $"{proc.MemoryMB} MB" : "N/A")
            );
            item.SubItems.Add(proc.RunningTime);
            item.SubItems.Add(
                proc.LoadingState == ProcessLoadingState.Loading ? "Loading..." : 
                proc.CommandLine
            );
            item.Tag = proc;
            _listView.Items.Add(item);
        }

        _listView.EndUpdate();
    }

    private void UpdateButtonCounts()
    {
        int selectedCount = _listView.CheckedItems.Count;
        int nodeCount = _allProcesses.Count(p => p.Category == "Node.js");
        int totalCount = _allProcesses.Count;

        _btnKillSelected.Text = $"Kill Selected ({selectedCount})";
        _btnKillSelected.Enabled = selectedCount > 0;
        _btnKillAllNode.Text = $"Kill All Node.js ({nodeCount})";
        _btnKillAllNode.Enabled = nodeCount > 0;
        _btnKillAllDev.Text = $"Kill All Dev ({totalCount})";
        _btnKillAllDev.Enabled = totalCount > 0;
    }

    private async Task KillProcessAsync(DevProcessInfo proc)
    {
        try
        {
            var result = await ProcessKiller.KillProcessById(proc.ProcessId);
            if (!result.Success)
            {
                MessageBox.Show($"Failed to kill {proc.ProcessName} (PID: {proc.ProcessId}):\n{string.Join("\n", result.Errors)}",
                    "Kill Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error killing process: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    #region Event Handlers

    private void TxtSearch_TextChanged(object? sender, EventArgs e)
    {
        ApplyFilters();
    }

    private void CboCategory_SelectedIndexChanged(object? sender, EventArgs e)
    {
        ApplyFilters();
    }

    private void ChkAutoRefresh_CheckedChanged(object? sender, EventArgs e)
    {
        if (_chkAutoRefresh.Checked)
        {
            if (!_refreshTimer.Enabled)
            {
                _refreshTimer.Start();
            }

            RefreshProcesses();
        }
        else
        {
            _refreshTimer.Stop();
        }
    }

    private void BtnRefresh_Click(object? sender, EventArgs e)
    {
        RefreshProcesses();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        RefreshProcesses();
    }

    private void ListView_ItemChecked(object? sender, ItemCheckedEventArgs e)
    {
        UpdateButtonCounts();
    }

    private async void ListView_DoubleClick(object? sender, EventArgs e)
    {
        if (_listView.SelectedItems.Count > 0)
        {
            var item = _listView.SelectedItems[0];
            if (item.Tag is DevProcessInfo proc)
            {
                var result = MessageBox.Show(
                    $"Kill process {proc.ProcessName} (PID: {proc.ProcessId})?\n\nMemory: {proc.MemoryMB} MB\nRunning: {proc.RunningTime}",
                    "Confirm Kill",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    await KillProcessAsync(proc);
                    RefreshProcesses();
                }
            }
        }
    }

    private async void BtnKillSelected_Click(object? sender, EventArgs e)
    {
        var checkedItems = _listView.CheckedItems.Cast<ListViewItem>().ToList();
        if (checkedItems.Count == 0) return;

        var result = MessageBox.Show(
            $"Kill {checkedItems.Count} selected process(es)?",
            "Confirm Kill",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            _progressBar.Visible = true;
            _lblStatus.Text = "Killing selected processes...";

            foreach (var item in checkedItems)
            {
                if (item.Tag is DevProcessInfo proc)
                {
                    await KillProcessAsync(proc);
                }
            }

            RefreshProcesses();
        }
    }

    private async void BtnKillAllNode_Click(object? sender, EventArgs e)
    {
        var nodeProcs = _allProcesses.Where(p => p.Category == "Node.js").ToList();
        if (nodeProcs.Count == 0) return;

        var result = MessageBox.Show(
            $"Kill all {nodeProcs.Count} Node.js process(es)?",
            "Confirm Kill",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            _progressBar.Visible = true;
            _lblStatus.Text = "Killing Node.js processes...";

            var killResult = await ProcessKiller.KillNodeJs();
            
            if (killResult.Errors.Count > 0)
            {
                MessageBox.Show($"Some processes failed to kill:\n{string.Join("\n", killResult.Errors)}",
                    "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            RefreshProcesses();
        }
    }

    private async void BtnKillAllDev_Click(object? sender, EventArgs e)
    {
        if (_allProcesses.Count == 0) return;

        var result = MessageBox.Show(
            $"Kill ALL {_allProcesses.Count} dev process(es)?\n\nThis will terminate Node.js, Bun, Git, WSL, and other dev tools.",
            "Confirm Kill All",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result == DialogResult.Yes)
        {
            _progressBar.Visible = true;
            _lblStatus.Text = "Killing all dev processes...";

            var killResult = await ProcessKiller.KillAllDevTools();
            
            if (killResult.Errors.Count > 0)
            {
                MessageBox.Show($"Some processes failed to kill:\n{string.Join("\n", killResult.Errors)}",
                    "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            RefreshProcesses();
        }
    }

    #endregion

    #region Custom Drawing

    private void ListView_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        e.Graphics.FillRectangle(new SolidBrush(_cGridHeader), e.Bounds);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", 
            new Font("Segoe UI", 9, FontStyle.Bold), e.Bounds, _cText,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    private void ListView_DrawItem(object? sender, DrawListViewItemEventArgs e)
    {
        e.DrawDefault = false;
    }

    private void ListView_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e.Item == null) return;

        // Background
        Color bgColor = e.Item.Selected ? _cAccent : 
            (e.ItemIndex % 2 == 0 ? _cBackground : _cSidebar);
        e.Graphics.FillRectangle(new SolidBrush(bgColor), e.Bounds);

        // Checkbox column
        if (e.ColumnIndex == 0)
        {
            CheckBoxRenderer.DrawCheckBox(e.Graphics,
                new Point(e.Bounds.X + 6, e.Bounds.Y + 4),
                e.Item.Checked 
                    ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal 
                    : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal);
            return;
        }

        // Category column - add color indicator
        if (e.ColumnIndex == 1 && e.Item.Tag is DevProcessInfo proc)
        {
            Color indicatorColor = proc.Category switch
            {
                "Node.js" => Color.FromArgb(68, 168, 67),  // Node green
                "Bun" => Color.FromArgb(251, 242, 225),    // Bun cream
                "Git" or "Git Bash" => Color.FromArgb(240, 80, 51),  // Git orange
                "WSL" or "WSL Relay" => Color.FromArgb(200, 100, 200),  // Purple
                "Docker" => Color.FromArgb(33, 150, 243),  // Docker blue
                "Python" => Color.FromArgb(55, 118, 171),  // Python blue
                _ => _cAccent
            };

            using var brush = new SolidBrush(indicatorColor);
            e.Graphics.FillEllipse(brush, e.Bounds.X + 4, e.Bounds.Y + 8, 8, 8);
            
            var textBounds = new Rectangle(e.Bounds.X + 16, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", e.Item.Font ?? this.Font, 
                textBounds, _cText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            return;
        }


        // Detail columns - show spinner if loading
        if (e.Item.Tag is DevProcessInfo procInfo && 
            (e.ColumnIndex == 4 || e.ColumnIndex == 5 || e.ColumnIndex == 6))
        {
            bool isLoading = procInfo.LoadingState == ProcessLoadingState.Loading;
            string text = e.SubItem?.Text ?? "";

            if (isLoading)
            {
                // Draw loading spinner
                var spinnerBounds = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 4, 12, 12);
                DrawLoadingSpinner(e.Graphics, spinnerBounds);
                
                // Draw "Loading..." text
                var textBounds = new Rectangle(e.Bounds.X + 16, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, "Loading...", e.Item.Font ?? this.Font, 
                    textBounds, _cTextDim,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                return;
            }
        }
        // Text
        TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", e.Item.Font ?? this.Font, e.Bounds, _cText,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    /// <summary>
    /// Draws a simple loading spinner.
    /// </summary>
    private void DrawLoadingSpinner(Graphics g, Rectangle bounds)
    {
        using var pen = new Pen(_cAccent, 2);
        
        // Draw a spinning circle (simple visual effect)
        float angle = (float)(DateTime.Now.Millisecond / 1000.0 * Math.PI * 2);
        for (int i = 0; i < 8; i++)
        {
            float segmentAngle = angle + (float)(i * Math.PI / 4);
            float alpha = 255 - (i * 30);
            using var segmentPen = new Pen(Color.FromArgb((int)alpha, _cAccent), 2);
            
            float x1 = bounds.X + bounds.Width / 2 + (float)Math.Cos(segmentAngle) * 2;
            float y1 = bounds.Y + bounds.Height / 2 + (float)Math.Sin(segmentAngle) * 2;
            float x2 = bounds.X + bounds.Width / 2 + (float)Math.Cos(segmentAngle) * (bounds.Width / 2);
            float y2 = bounds.Y + bounds.Height / 2 + (float)Math.Sin(segmentAngle) * (bounds.Height / 2);
            
            g.DrawLine(segmentPen, x1, y1, x2, y2);
        }
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            _detailLoadingCts?.Cancel();
            _detailLoadingCts?.Dispose();
        }
        base.Dispose(disposing);
    }
}
