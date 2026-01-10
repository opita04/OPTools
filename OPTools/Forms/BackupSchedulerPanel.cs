using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using OPTools.Core;
using OPTools.Utils;

namespace OPTools.Forms;

/// <summary>
/// Panel for managing scheduled backup jobs
/// </summary>
public class BackupSchedulerPanel : Panel
{
    // Theme Colors (matching MainForm)
    private readonly Color _cBackground = Color.FromArgb(30, 30, 30);
    private readonly Color _cSidebar = Color.FromArgb(25, 25, 26);
    private readonly Color _cAccent = Color.FromArgb(0, 122, 204);
    private readonly Color _cDanger = Color.FromArgb(217, 83, 79);
    private readonly Color _cSuccess = Color.FromArgb(92, 184, 92);
    private readonly Color _cText = Color.FromArgb(241, 241, 241);
    private readonly Color _cTextDim = Color.FromArgb(150, 150, 150);
    private readonly Color _cCardBg = Color.FromArgb(45, 45, 48);

    private readonly BackupScheduler _scheduler;
    private FlowLayoutPanel _jobsPanel = null!;
    private Label _lblStatus = null!;
    private ModernButton _btnAddJob = null!;
    private Panel _headerPanel = null!;
    private bool _initialized = false;
    private ToolTip _toolTip = null!;

    public BackupScheduler Scheduler => _scheduler;

    public BackupSchedulerPanel()
    {
        _scheduler = new BackupScheduler();
        _scheduler.JobStarted += Scheduler_JobStarted;
        _scheduler.JobCompleted += Scheduler_JobCompleted;
        _scheduler.JobsChanged += Scheduler_JobsChanged;

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
        this.Padding = new Padding(24);

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
            Text = "Backup Scheduler",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = _cText,
            AutoSize = true,
            Location = new Point(0, 8)
        };

        _btnAddJob = new ModernButton
        {
            Text = "Add Backup Job",
            IconChar = "\uE710",
            BackColor = _cAccent,
            Width = 160,
            Height = 40,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(this.Width - 184, 8)
        };
        _btnAddJob.Click += BtnAddJob_Click;

        _headerPanel.Controls.Add(lblTitle);
        _headerPanel.Controls.Add(_btnAddJob);

        // Status bar
        _lblStatus = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 30,
            ForeColor = _cTextDim,
            Font = new Font("Segoe UI", 9),
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Scheduler active"
        };

        // Jobs list
        _jobsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = _cBackground,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };

        this.Controls.Add(_jobsPanel);
        this.Controls.Add(_lblStatus);
        this.Controls.Add(_headerPanel);

        this.Resize += (s, e) =>
        {
            _btnAddJob.Location = new Point(this.Width - 184, 8);
            RefreshJobCards();
        };
    }

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        // Load saved jobs
        var jobs = BackupSettingsManager.LoadJobs();
        _scheduler.LoadJobs(jobs);

        // Start the scheduler
        _scheduler.Start();

        RefreshJobCards();
    }

    public void Shutdown()
    {
        _scheduler.Stop();
        SaveJobs();
    }

    private void SaveJobs()
    {
        BackupSettingsManager.SaveJobs(_scheduler.Jobs);
    }

    private void RefreshJobCards()
    {
        _jobsPanel.SuspendLayout();
        _jobsPanel.Controls.Clear();

        if (_scheduler.Jobs.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = "No backup jobs configured.\nClick 'Add Backup Job' to create one.",
                Font = new Font("Segoe UI", 12),
                ForeColor = _cTextDim,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Width = _jobsPanel.Width - 40,
                Height = 100
            };
            _jobsPanel.Controls.Add(emptyLabel);
        }
        else
        {
            foreach (var job in _scheduler.Jobs)
            {
                var card = CreateJobCard(job);
                _jobsPanel.Controls.Add(card);
            }
        }

        _jobsPanel.ResumeLayout();
        _lblStatus.Text = $"{_scheduler.Jobs.Count} backup job(s) configured";
    }

    private Panel CreateJobCard(BackupJob job)
    {
        var cardWidth = Math.Max(400, _jobsPanel.Width - 40);
        
        var card = new Panel
        {
            Width = cardWidth,
            Height = 130,
            BackColor = _cCardBg,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(16),
            Tag = job.Id
        };

        // Round the corners
        card.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = GetRoundedPath(card.ClientRectangle, 12);
            using var brush = new SolidBrush(_cCardBg);
            e.Graphics.FillPath(brush, path);
        };

        // Job name
        var lblName = new Label
        {
            Text = job.Name,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = _cText,
            AutoSize = true,
            Location = new Point(16, 12)
        };

        // Status indicator
        var statusColor = job.IsEnabled 
            ? (job.LastRunSuccess == false ? _cDanger : _cSuccess) 
            : _cTextDim;
        var lblStatus = new Label
        {
            Text = job.IsEnabled ? "● Enabled" : "○ Disabled",
            Font = new Font("Segoe UI", 9),
            ForeColor = statusColor,
            AutoSize = true,
            Location = new Point(16, 36)
        };

        // Source path
        var lblSource = new Label
        {
            Text = $"Source: {TruncatePath(job.SourcePath, 50)}",
            Font = new Font("Segoe UI", 9),
            ForeColor = _cTextDim,
            AutoSize = true,
            Location = new Point(16, 56)
        };

        // Destination path
        var lblDest = new Label
        {
            Text = $"Destination: {TruncatePath(job.DestinationPath, 50)}",
            Font = new Font("Segoe UI", 9),
            ForeColor = _cTextDim,
            AutoSize = true,
            Location = new Point(16, 74)
        };

        // Schedule info
        var lblSchedule = new Label
        {
            Text = job.ScheduleDescription,
            Font = new Font("Segoe UI", 9),
            ForeColor = _cAccent,
            AutoSize = true,
            Location = new Point(16, 92)
        };

        // Last run info
        var lastRunText = job.LastRunTime.HasValue
            ? $"Last: {job.LastRunTime.Value:g} - {(job.LastRunSuccess == true ? "Success" : "Failed")}"
            : "Never run";
        var lblLastRun = new Label
        {
            Text = lastRunText,
            Font = new Font("Segoe UI", 8),
            ForeColor = job.LastRunSuccess == false ? _cDanger : _cTextDim,
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(cardWidth - 200, 12)
        };

        // Action buttons
        var btnRun = CreateSmallButton("▶", _cSuccess);
        btnRun.Location = new Point(cardWidth - 120, 80);
        btnRun.Click += async (s, e) =>
        {
            btnRun.Enabled = false;
            btnRun.Text = "...";
            await _scheduler.RunJobNowAsync(job.Id);
            btnRun.Enabled = true;
            btnRun.Text = "▶";
            SaveJobs();
            RefreshJobCards();
        };

        var btnEdit = CreateSmallButton("✎", _cAccent);
        btnEdit.Location = new Point(cardWidth - 85, 80);
        btnEdit.Click += (s, e) => EditJob(job);

        var btnDelete = CreateSmallButton("✕", _cDanger);
        btnDelete.Location = new Point(cardWidth - 50, 80);
        btnDelete.Click += (s, e) =>
        {
            if (MessageBox.Show($"Delete backup job '{job.Name}'?", "Confirm Delete",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                _scheduler.RemoveJob(job.Id);
                SaveJobs();
                RefreshJobCards();
            }
        };

        card.Controls.Add(lblName);
        card.Controls.Add(lblStatus);
        card.Controls.Add(lblSource);
        card.Controls.Add(lblDest);
        card.Controls.Add(lblSchedule);
        card.Controls.Add(lblLastRun);
        card.Controls.Add(btnRun);
        card.Controls.Add(btnEdit);
        card.Controls.Add(btnDelete);

        return card;
    }

    private Button CreateSmallButton(string text, Color backColor)
    {
        return new Button
        {
            Text = text,
            Width = 30,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10)
        };
    }

    private void BtnAddJob_Click(object? sender, EventArgs e)
    {
        var newJob = new BackupJob { Name = "New Backup Job" };
        if (ShowJobEditor(newJob, true))
        {
            _scheduler.AddJob(newJob);
            SaveJobs();
            RefreshJobCards();
        }
    }

    private void EditJob(BackupJob job)
    {
        if (ShowJobEditor(job, false))
        {
            _scheduler.UpdateJob(job);
            SaveJobs();
            RefreshJobCards();
        }
    }

    private bool ShowJobEditor(BackupJob job, bool isNew)
    {
        using var dialog = new BackupJobEditorDialog(job, isNew);
        dialog.StartPosition = FormStartPosition.CenterParent;
        return dialog.ShowDialog() == DialogResult.OK;
    }

    private void Scheduler_JobStarted(object? sender, BackupJobEventArgs e)
    {
        if (InvokeRequired)
        {
            Invoke(() => _lblStatus.Text = $"Running: {e.Job.Name}...");
        }
        else
        {
            _lblStatus.Text = $"Running: {e.Job.Name}...";
        }
    }

    private void Scheduler_JobCompleted(object? sender, BackupJobCompletedEventArgs e)
    {
        if (InvokeRequired)
        {
            Invoke(() =>
            {
                _lblStatus.Text = e.Result.Success
                    ? $"Completed: {e.Job.Name} - {e.Result.Message}"
                    : $"Failed: {e.Job.Name} - {e.Result.Message}";
                SaveJobs();
                RefreshJobCards();
            });
        }
        else
        {
            _lblStatus.Text = e.Result.Success
                ? $"Completed: {e.Job.Name} - {e.Result.Message}"
                : $"Failed: {e.Job.Name} - {e.Result.Message}";
            SaveJobs();
            RefreshJobCards();
        }
    }

    private void Scheduler_JobsChanged(object? sender, EventArgs e)
    {
        // Save whenever jobs change
        SaveJobs();
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
/// Dialog for editing backup job settings
/// </summary>
public class BackupJobEditorDialog : Form
{
    private readonly BackupJob _job;
    private readonly bool _isNew;

    // Theme Colors
    private readonly Color _cBackground = Color.FromArgb(30, 30, 30);
    private readonly Color _cCardBg = Color.FromArgb(45, 45, 48);
    private readonly Color _cAccent = Color.FromArgb(0, 122, 204);
    private readonly Color _cText = Color.FromArgb(241, 241, 241);
    private readonly Color _cTextDim = Color.FromArgb(150, 150, 150);

    private TextBox _txtName = null!;
    private TextBox _txtSource = null!;
    private TextBox _txtDestination = null!;
    private ComboBox _cboScheduleType = null!;
    private DateTimePicker _dtpTime = null!;
    private ComboBox _cboDayOfWeek = null!;
    private NumericUpDown _nudDayOfMonth = null!;
    private NumericUpDown _nudInterval = null!;
    private CheckBox _chkEnabled = null!;
    private CheckBox _chkSubfolders = null!;
    private CheckBox _chkTimestamp = null!;
    private NumericUpDown _nudKeepVersions = null!;
    private Panel _scheduleOptionsPanel = null!;

    public BackupJobEditorDialog(BackupJob job, bool isNew)
    {
        _job = job;
        _isNew = isNew;
        InitializeComponent();
        LoadJobData();
    }

    private void InitializeComponent()
    {
        this.Text = _isNew ? "New Backup Job" : "Edit Backup Job";
        this.Size = new Size(550, 580);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = _cBackground;
        this.ForeColor = _cText;

        int y = 20;
        int labelWidth = 120;
        int inputWidth = 360;
        int leftMargin = 20;

        // Name
        AddLabel("Job Name:", leftMargin, y);
        _txtName = AddTextBox(leftMargin + labelWidth, y, inputWidth);
        y += 40;

        // Source
        AddLabel("Source:", leftMargin, y);
        _txtSource = AddTextBox(leftMargin + labelWidth, y, inputWidth - 35);
        var btnBrowseSource = new Button
        {
            Text = "...",
            Location = new Point(leftMargin + labelWidth + inputWidth - 30, y - 2),
            Size = new Size(30, 27),
            FlatStyle = FlatStyle.Flat,
            BackColor = _cCardBg,
            ForeColor = _cText
        };
        btnBrowseSource.Click += (s, e) =>
        {
            using var dialog = new FolderBrowserDialog();
            if (!string.IsNullOrEmpty(_txtSource.Text) && Directory.Exists(_txtSource.Text))
                dialog.SelectedPath = _txtSource.Text;
            if (dialog.ShowDialog() == DialogResult.OK)
                _txtSource.Text = dialog.SelectedPath;
        };
        this.Controls.Add(btnBrowseSource);
        y += 40;

        // Destination
        AddLabel("Destination:", leftMargin, y);
        _txtDestination = AddTextBox(leftMargin + labelWidth, y, inputWidth - 35);
        var btnBrowseDest = new Button
        {
            Text = "...",
            Location = new Point(leftMargin + labelWidth + inputWidth - 30, y - 2),
            Size = new Size(30, 27),
            FlatStyle = FlatStyle.Flat,
            BackColor = _cCardBg,
            ForeColor = _cText
        };
        btnBrowseDest.Click += (s, e) =>
        {
            using var dialog = new FolderBrowserDialog();
            if (!string.IsNullOrEmpty(_txtDestination.Text) && Directory.Exists(_txtDestination.Text))
                dialog.SelectedPath = _txtDestination.Text;
            if (dialog.ShowDialog() == DialogResult.OK)
                _txtDestination.Text = dialog.SelectedPath;
        };
        this.Controls.Add(btnBrowseDest);
        y += 50;

        // Schedule Type
        AddLabel("Schedule:", leftMargin, y);
        _cboScheduleType = new ComboBox
        {
            Location = new Point(leftMargin + labelWidth, y - 2),
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = _cCardBg,
            ForeColor = _cText,
            FlatStyle = FlatStyle.Flat
        };
        _cboScheduleType.Items.AddRange(new object[] { "Every X Minutes", "Daily", "Weekly", "Monthly" });
        _cboScheduleType.SelectedIndexChanged += ScheduleType_Changed;
        this.Controls.Add(_cboScheduleType);
        y += 40;

        // Schedule options panel
        _scheduleOptionsPanel = new Panel
        {
            Location = new Point(leftMargin, y),
            Size = new Size(inputWidth + labelWidth, 80),
            BackColor = _cBackground
        };
        this.Controls.Add(_scheduleOptionsPanel);

        // Time picker (for daily/weekly/monthly)
        var lblTime = new Label { Text = "Time:", Location = new Point(labelWidth, 0), AutoSize = true, ForeColor = _cTextDim };
        _dtpTime = new DateTimePicker
        {
            Location = new Point(labelWidth + 50, -2),
            Width = 100,
            Format = DateTimePickerFormat.Time,
            ShowUpDown = true
        };
        _scheduleOptionsPanel.Controls.Add(lblTime);
        _scheduleOptionsPanel.Controls.Add(_dtpTime);

        // Day of week (for weekly)
        var lblDayOfWeek = new Label { Text = "Day:", Location = new Point(labelWidth + 170, 0), AutoSize = true, ForeColor = _cTextDim };
        _cboDayOfWeek = new ComboBox
        {
            Location = new Point(labelWidth + 210, -2),
            Width = 100,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = _cCardBg,
            ForeColor = _cText
        };
        _cboDayOfWeek.Items.AddRange(Enum.GetNames(typeof(DayOfWeek)));
        _scheduleOptionsPanel.Controls.Add(lblDayOfWeek);
        _scheduleOptionsPanel.Controls.Add(_cboDayOfWeek);

        // Day of month (for monthly)
        var lblDayOfMonth = new Label { Text = "Day:", Location = new Point(labelWidth + 170, 0), AutoSize = true, ForeColor = _cTextDim };
        _nudDayOfMonth = new NumericUpDown
        {
            Location = new Point(labelWidth + 210, -2),
            Width = 60,
            Minimum = 1,
            Maximum = 31,
            Value = 1,
            BackColor = _cCardBg,
            ForeColor = _cText
        };
        _scheduleOptionsPanel.Controls.Add(lblDayOfMonth);
        _scheduleOptionsPanel.Controls.Add(_nudDayOfMonth);

        // Interval (for interval-based)
        var lblInterval = new Label { Text = "Interval (minutes):", Location = new Point(labelWidth, 0), AutoSize = true, ForeColor = _cTextDim };
        _nudInterval = new NumericUpDown
        {
            Location = new Point(labelWidth + 120, -2),
            Width = 80,
            Minimum = 1,
            Maximum = 1440,
            Value = 60,
            BackColor = _cCardBg,
            ForeColor = _cText
        };
        _scheduleOptionsPanel.Controls.Add(lblInterval);
        _scheduleOptionsPanel.Controls.Add(_nudInterval);

        y += 90;

        // Options
        _chkEnabled = new CheckBox
        {
            Text = "Enabled",
            Location = new Point(leftMargin + labelWidth, y),
            AutoSize = true,
            ForeColor = _cText,
            Checked = true
        };
        this.Controls.Add(_chkEnabled);
        y += 30;

        _chkSubfolders = new CheckBox
        {
            Text = "Include subfolders",
            Location = new Point(leftMargin + labelWidth, y),
            AutoSize = true,
            ForeColor = _cText,
            Checked = true
        };
        this.Controls.Add(_chkSubfolders);
        y += 30;

        _chkTimestamp = new CheckBox
        {
            Text = "Create timestamped backup folders",
            Location = new Point(leftMargin + labelWidth, y),
            AutoSize = true,
            ForeColor = _cText,
            Checked = true
        };
        this.Controls.Add(_chkTimestamp);
        y += 40;

        // Keep versions
        AddLabel("Keep versions:", leftMargin, y);
        _nudKeepVersions = new NumericUpDown
        {
            Location = new Point(leftMargin + labelWidth, y - 2),
            Width = 60,
            Minimum = 1,
            Maximum = 100,
            Value = 5,
            BackColor = _cCardBg,
            ForeColor = _cText
        };
        this.Controls.Add(_nudKeepVersions);
        y += 50;

        // Buttons
        var btnSave = new ModernButton
        {
            Text = "Save",
            BackColor = _cAccent,
            Width = 100,
            Height = 40,
            Location = new Point(this.Width - 240, y)
        };
        btnSave.Click += (s, e) =>
        {
            if (ValidateInput())
            {
                SaveJobData();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        };

        var btnCancel = new ModernButton
        {
            Text = "Cancel",
            BackColor = _cCardBg,
            Width = 100,
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

    private void ScheduleType_Changed(object? sender, EventArgs e)
    {
        var type = (BackupScheduleType)_cboScheduleType.SelectedIndex;

        // Hide all schedule-specific controls first
        _dtpTime.Visible = false;
        _cboDayOfWeek.Visible = false;
        _nudDayOfMonth.Visible = false;
        _nudInterval.Visible = false;

        // Find parent labels
        foreach (Control c in _scheduleOptionsPanel.Controls)
        {
            if (c is Label) c.Visible = false;
        }

        switch (type)
        {
            case BackupScheduleType.Interval:
                _nudInterval.Visible = true;
                _scheduleOptionsPanel.Controls.OfType<Label>().First(l => l.Text.Contains("Interval")).Visible = true;
                break;

            case BackupScheduleType.Daily:
                _dtpTime.Visible = true;
                _scheduleOptionsPanel.Controls.OfType<Label>().First(l => l.Text == "Time:").Visible = true;
                break;

            case BackupScheduleType.Weekly:
                _dtpTime.Visible = true;
                _cboDayOfWeek.Visible = true;
                _scheduleOptionsPanel.Controls.OfType<Label>().First(l => l.Text == "Time:").Visible = true;
                _scheduleOptionsPanel.Controls.OfType<Label>().Where(l => l.Text == "Day:").First().Visible = true;
                break;

            case BackupScheduleType.Monthly:
                _dtpTime.Visible = true;
                _nudDayOfMonth.Visible = true;
                _scheduleOptionsPanel.Controls.OfType<Label>().First(l => l.Text == "Time:").Visible = true;
                _scheduleOptionsPanel.Controls.OfType<Label>().Where(l => l.Text == "Day:").Skip(1).First().Visible = true;
                break;
        }
    }

    private void LoadJobData()
    {
        _txtName.Text = _job.Name;
        _txtSource.Text = _job.SourcePath;
        _txtDestination.Text = _job.DestinationPath;
        _cboScheduleType.SelectedIndex = (int)_job.ScheduleType;
        _dtpTime.Value = DateTime.Today.Add(_job.ScheduledTime);
        _cboDayOfWeek.SelectedIndex = (int)(_job.DayOfWeek ?? DayOfWeek.Sunday);
        _nudDayOfMonth.Value = _job.DayOfMonth ?? 1;
        _nudInterval.Value = _job.IntervalMinutes;
        _chkEnabled.Checked = _job.IsEnabled;
        _chkSubfolders.Checked = _job.IncludeSubfolders;
        _chkTimestamp.Checked = _job.UseTimestampFolder;
        _nudKeepVersions.Value = _job.KeepVersions;

        ScheduleType_Changed(null, EventArgs.Empty);
    }

    private void SaveJobData()
    {
        _job.Name = _txtName.Text.Trim();
        _job.SourcePath = _txtSource.Text.Trim();
        _job.DestinationPath = _txtDestination.Text.Trim();
        _job.ScheduleType = (BackupScheduleType)_cboScheduleType.SelectedIndex;
        _job.ScheduledTime = _dtpTime.Value.TimeOfDay;
        _job.DayOfWeek = (DayOfWeek)_cboDayOfWeek.SelectedIndex;
        _job.DayOfMonth = (int)_nudDayOfMonth.Value;
        _job.IntervalMinutes = (int)_nudInterval.Value;
        _job.IsEnabled = _chkEnabled.Checked;
        _job.IncludeSubfolders = _chkSubfolders.Checked;
        _job.UseTimestampFolder = _chkTimestamp.Checked;
        _job.KeepVersions = (int)_nudKeepVersions.Value;
    }

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            MessageBox.Show("Please enter a job name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtName.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(_txtSource.Text))
        {
            MessageBox.Show("Please enter a source path.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtSource.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(_txtDestination.Text))
        {
            MessageBox.Show("Please enter a destination path.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtDestination.Focus();
            return false;
        }

        if (!Directory.Exists(_txtSource.Text) && !File.Exists(_txtSource.Text))
        {
            MessageBox.Show("Source path does not exist.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtSource.Focus();
            return false;
        }

        return true;
    }
}
