using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using OPTools.Core;
using OPTools.Forms;
using OPTools.Registry;
using OPTools.Tools;
using OPTools.Utils;

using System.Text;
using System.IO;

namespace OPTools
{
    public partial class MainForm : Form, IMessageFilter
    {
    // UI Components
    private ListView _listView = null!;
    private ModernButton _btnUnlockAll = null!;
    private ModernButton _btnKillProcess = null!;
    private ModernButton _btnDelete = null!;
    private ModernButton _btnMove = null!;
    private ModernButton _btnRefresh = null!;
    private Label _lblStatus = null!;
    private ProgressBar _progressBar = null!;
    
    // Modern UI Layout Components
    private Panel _sidebarPanel = null!;
    private Panel _contentPanel = null!;
    private Panel _headerPanel = null!;
    
    // Sidebar Buttons
    private SidebarButton _navUnlocker = null!;
    private SidebarButton _navCleaner = null!;
    private SidebarButton _navNetwork = null!;
    private SidebarButton _navProcesses = null!;
    private SidebarButton _navContextMenu = null!;
    private SidebarButton _navApplications = null!;
    private SidebarButton _navNpmHandler = null!;
    private SidebarButton _navSettings = null!;

    // Theme Colors
    private readonly Color _cBackground = Color.FromArgb(30, 30, 30);      // #1E1E1E
    private readonly Color _cSidebar = Color.FromArgb(25, 25, 26);         // Darker sidebar
    private readonly Color _cAccent = Color.FromArgb(0, 122, 204);         // #007ACC
    private readonly Color _cDanger = Color.FromArgb(217, 83, 79);         // #D9534F
    private readonly Color _cText = Color.FromArgb(241, 241, 241);         // #F1F1F1
    private readonly Color _cTextDim = Color.FromArgb(150, 150, 150);      // #969696
    private readonly Color _cGridHeader = Color.FromArgb(45, 45, 48);      // #2D2D30
    private readonly Color _cHover = Color.FromArgb(60, 60, 60);           // #3C3C3C

    private string _targetPath;
    private FileUnlocker? _unlocker;
    private ApplicationLauncher? _appLauncher;

    // Applications Panel
    private FlowLayoutPanel _applicationsPanel = null!;
    private Panel _applicationsContentPanel = null!;
    private ContextMenuStrip _contextMenuApplications = null!;

    // Network Reset Panel
    private Panel _networkContentPanel = null!;
    private FlowLayoutPanel _networkButtonsPanel = null!;

    // System Cleaner Panel
    private Panel _cleanerContentPanel = null!;
    private FlowLayoutPanel _cleanerButtonsPanel = null!;

    // Processes Panel
    private Panel _processesContentPanel = null!;
    private FlowLayoutPanel _processesButtonsPanel = null!;

    // Settings Panel
    private Panel _settingsContentPanel = null!;
    private FlowLayoutPanel _settingsButtonsPanel = null!;
    private CheckBox _chkStartOnStartup = null!;
    private CheckBox _chkMinimizeToTray = null!;

    // NPM Handler Panel
    private NpmHandlerPanel _npmHandlerPanel = null!;

    // System Tray
    private NotifyIcon _notifyIcon = null!;





    // Menu Items for Context Menus (kept for logic reuse)
    // Removed unused menu items

    public MainForm(string? targetPath = null)
    {
        _targetPath = targetPath ?? string.Empty;
        InitializeComponent();
        AllowDragDropMessages();
        InitializeDragDrop();
        Application.AddMessageFilter(this);
        LoadLocks();
    }

    private void InitializeComponent()
    {
        this.Text = "OPTools";
        this.Size = new Size(1050, 680);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MinimumSize = new Size(800, 500);
        this.BackColor = _cBackground;
        this.ForeColor = _cText;
        this.ShowInTaskbar = true;
        
        // Set application icon - load from embedded executable icon (set via ApplicationIcon in .csproj)
        // This ensures the icon always appears in the taskbar
        try
        {
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            }
            
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                // Extract icon from the executable itself (embedded via ApplicationIcon in .csproj)
                Icon? extractedIcon = Icon.ExtractAssociatedIcon(exePath);
                if (extractedIcon != null)
                {
                    this.Icon = extractedIcon;
                }
            }
        }
        catch
        {
            // Fallback to file path if extraction fails
            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icons", "chip_icon.ico");
                if (!File.Exists(iconPath))
                {
                    iconPath = Path.Combine(Application.StartupPath, "Icons", "chip_icon.ico");
                }
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
            }
            catch
            {
                // Use default icon if nothing works
            }
        }

        // --- Sidebar ---
        _sidebarPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 240,
            BackColor = _cSidebar,
            Padding = new Padding(0, 20, 0, 0)
        };

        // Sidebar Navigation Buttons
        _navUnlocker = CreateSidebarButton("File Unlocker", "\uE785", true);
        _navCleaner = CreateSidebarButton("System Cleaner", "\uE896");
        _navNetwork = CreateSidebarButton("Network Reset", "\uE968");
        _navProcesses = CreateSidebarButton("Kill Processes", "\uE90F");
        _navContextMenu = CreateSidebarButton("Context Menu Manager", "\uE81C");
        _navApplications = CreateSidebarButton("Applications", "\uE7FC");
        _navNpmHandler = CreateSidebarButton("Package Handler", "\uE74C");


        // Add to sidebar (reverse order for Dock.Top)
        _navSettings = CreateSidebarButton("Settings", "\uE713");
        _navSettings.Dock = DockStyle.Bottom;

        Panel sidebarDivider = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = _cGridHeader
        };

        // Add to sidebar (reverse order for Dock.Top)
        _sidebarPanel.Controls.Add(_navSettings);
        _sidebarPanel.Controls.Add(sidebarDivider);
        _sidebarPanel.Controls.Add(_navNpmHandler);
        _sidebarPanel.Controls.Add(_navContextMenu);
        _sidebarPanel.Controls.Add(_navProcesses);
        _sidebarPanel.Controls.Add(_navNetwork);
        _sidebarPanel.Controls.Add(_navCleaner);
        _sidebarPanel.Controls.Add(_navUnlocker);
        _sidebarPanel.Controls.Add(_navApplications);

        // --- Main Content Area ---
        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(30)
        };

        // Header / Action Bar
        _headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            Padding = new Padding(0, 0, 0, 15)
        };

        _btnUnlockAll = CreateActionButton("Unlock All", "\uE72E", _cAccent);
        _btnKillProcess = CreateActionButton("Kill Process", "\uE71A", _cDanger);
        _btnDelete = CreateActionButton("Delete", "\uE74D", _cDanger);
        _btnMove = CreateActionButton("Move", "\uE8DE", _cAccent);
        
        _btnRefresh = new ModernButton
        {
            Text = "",
            IconChar = "\uE72C",
            BackColor = Color.FromArgb(60, 60, 60),
            Width = 50,
            Dock = DockStyle.Right
        };

        // Initialize Header Layout
        InitializeHeaderButtons();

        // Status Bar
        _lblStatus = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 30,
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
            OwnerDraw = true
        };

        _listView.Columns.Add("Process Name", 200);
        _listView.Columns.Add("PID", 80);
        _listView.Columns.Add("File Path", 450);
        _listView.Columns.Add("Handle Type", 120);

        _listView.DrawColumnHeader += ListView_DrawColumnHeader;
        _listView.DrawItem += ListView_DrawItem;
        _listView.DrawSubItem += ListView_DrawSubItem;

        // Assemble Layout
        _contentPanel.Controls.Add(_listView);
        _contentPanel.Controls.Add(_progressBar);
        _contentPanel.Controls.Add(_lblStatus);
        _contentPanel.Controls.Add(_headerPanel);

        this.Controls.Add(_contentPanel);
        this.Controls.Add(_sidebarPanel);

        // Disable WinForms OLE drag and drop to allow WM_DROPFILES to work
        this.AllowDrop = false;
        _contentPanel.AllowDrop = false;

        // Handle window state change for system tray
        this.Resize += MainForm_Resize;
        // this.DragEnter += MainForm_DragEnter;
        // this.DragDrop += MainForm_DragDrop;
        // _contentPanel.DragEnter += MainForm_DragEnter;
        // _contentPanel.DragDrop += MainForm_DragDrop;

        // Initialize Application Launcher
        _appLauncher = new ApplicationLauncher();
        InitializeApplicationsPanel();
        InitializeNetworkPanel();
        InitializeCleanerPanel();
        InitializeProcessesPanel();
        InitializeContextMenuPanel();
        InitializeSettingsPanel();
        InitializeNpmHandlerPanel();
        InitializeSystemTray();
        LoadSettings();

        // Wire up Navigation Events
        _navUnlocker.Click += (s, e) => ShowView("unlocker");
        _navCleaner.Click += (s, e) => ShowView("cleaner");
        _navNetwork.Click += (s, e) => ShowView("network");
        _navProcesses.Click += (s, e) => ShowView("processes");
        _navContextMenu.Click += (s, e) => ShowView("contextmenu");
        _navApplications.Click += (s, e) => ShowView("applications");
        _navNpmHandler.Click += (s, e) => ShowView("npmhandler");
        _navSettings.Click += (s, e) => ShowView("settings");
        
        // Wire up Action Buttons
        _btnUnlockAll.Click += BtnUnlockAll_Click;
        _btnKillProcess.Click += BtnKillProcess_Click;
        _btnDelete.Click += BtnDelete_Click;
        _btnMove.Click += BtnMove_Click;
        _btnRefresh.Click += BtnRefresh_Click;

        // Initialize Context Menu Items
        // Removed unused menu items
        
        UpdateContextMenuStatus();
        
        // Show default view (Applications)
        ShowView("applications");
    }

    private void InitializeApplicationsPanel()
    {
        // Create applications content panel
        _applicationsContentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(30),
            BackColor = _cBackground,
            Visible = false
        };

        _applicationsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = _cBackground
        };
        
        // Disable WinForms OLE drag and drop
        _applicationsContentPanel.AllowDrop = false;
        _applicationsPanel.AllowDrop = false;

        // Wire up drag and drop - REMOVED as we use WM_DROPFILES
        // _applicationsPanel.DragEnter += ApplicationsPanel_DragEnter;
        // _applicationsPanel.DragDrop += ApplicationsPanel_DragDrop;
        
        // Also wire up the content panel to catch drops between buttons
        // _applicationsContentPanel.DragEnter += ApplicationsPanel_DragEnter;
        // _applicationsContentPanel.DragDrop += ApplicationsPanel_DragDrop;

        _applicationsContentPanel.Controls.Add(_applicationsPanel);
        _contentPanel.Controls.Add(_applicationsContentPanel);

        // Initialize Context Menu
        _contextMenuApplications = new ContextMenuStrip();
        _contextMenuApplications.RenderMode = ToolStripRenderMode.System; // Use System to avoid custom renderer complexity for now, or Professional
        _contextMenuApplications.BackColor = _cSidebar;
        _contextMenuApplications.ForeColor = _cText;
        // _contextMenuApplications.ShowImageMargin = false; // Optional style

        ToolStripMenuItem renameItem = new ToolStripMenuItem("Rename");
        renameItem.Click += MenuRenameApplication_Click;
        
        ToolStripMenuItem deleteItem = new ToolStripMenuItem("Remove");
        deleteItem.Click += MenuDeleteApplication_Click;
        
        _contextMenuApplications.Items.Add(renameItem);
        _contextMenuApplications.Items.Add(new ToolStripSeparator());
        _contextMenuApplications.Items.Add(deleteItem);

        // Load existing applications
        LoadApplications();
    }

    private void InitializeCleanerPanel()
    {
        _cleanerContentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(30),
            BackColor = _cBackground,
            Visible = false
        };

        Label lblTitle = new Label
        {
            Text = "System Cleaner Tools",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = _cText,
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 0, 0, 20)
        };

        _cleanerButtonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = _cBackground,
            Padding = new Padding(0, 20, 0, 0),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        // Add buttons
        AddCleanerButton("Clean Folder Contents", "\uE896", (s, e) => MenuCleanFolder_Click(s, e));
        AddCleanerButton("Remove Prefetch", "\uE74D", (s, e) => MenuCleanPrefetch_Click(s, e));
        AddCleanerButton("Empty Recycle Bin", "\uE74D", (s, e) => MenuEmptyRecycleBin_Click(s, e));
        AddCleanerButton("Clean All (System)", "\uE74D", (s, e) => MenuCleanSystem_Click(s, e));

        _cleanerContentPanel.Controls.Add(_cleanerButtonsPanel);
        _cleanerContentPanel.Controls.Add(lblTitle);
        _contentPanel.Controls.Add(_cleanerContentPanel);
    }

    private void InitializeNetworkPanel()
    {
        _networkContentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(30),
            BackColor = _cBackground,
            Visible = false
        };

        Label lblTitle = new Label
        {
            Text = "Network Reset Tools",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = _cText,
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 0, 0, 20)
        };

        _networkButtonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = _cBackground,
            Padding = new Padding(0, 20, 0, 0),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        // Add buttons
        AddNetworkButton("Flush DNS", "\uE896", async () => await NetworkReset.FlushDns());
        AddNetworkButton("Release IP", "\uE896", async () => await NetworkReset.ReleaseIp());
        AddNetworkButton("Renew IP", "\uE896", async () => await NetworkReset.RenewIp());
        AddNetworkButton("Reset Winsock", "\uE896", async () => await NetworkReset.ResetWinsock());
        AddNetworkButton("Reset TCP/IP", "\uE896", async () => await NetworkReset.ResetTcpIp());
        AddNetworkButton("Reset Proxy", "\uE896", async () => await NetworkReset.ResetProxy());
        
        // Separator or distinct style for Reset All
        AddNetworkButton("Reset All Network", "\uE968", async () => await NetworkReset.ResetInternetStack(), true);

        _networkContentPanel.Controls.Add(_networkButtonsPanel);
        _networkContentPanel.Controls.Add(lblTitle);
        _contentPanel.Controls.Add(_networkContentPanel);
    }

    private void InitializeProcessesPanel()
    {
        _processesContentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(30),
            BackColor = _cBackground,
            Visible = false
        };

        Label lblTitle = new Label
        {
            Text = "Kill Processes",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = _cText,
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 0, 0, 20)
        };

        _processesButtonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = _cBackground,
            Padding = new Padding(0, 20, 0, 0),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        // Add buttons
        AddProcessButton("Kill Node.js", "\uE71A", (s, e) => MenuKillNodeJs_Click(s, e));
        AddProcessButton("Kill Dev Tools", "\uE71A", (s, e) => MenuKillDevTools_Click(s, e));
        AddProcessButton("Kill by Port", "\uE71A", (s, e) => MenuKillPort_Click(s, e));

        _processesContentPanel.Controls.Add(_processesButtonsPanel);
        _processesContentPanel.Controls.Add(lblTitle);
        _contentPanel.Controls.Add(_processesContentPanel);
    }

    private void InitializeSettingsPanel()
    {
        _settingsContentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(30),
            BackColor = _cBackground,
            Visible = false
        };

        Label lblTitle = new Label
        {
            Text = "Settings",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = _cText,
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 0, 0, 20)
        };

        Panel settingsOptionsPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 150,
            Padding = new Padding(0, 0, 0, 20),
            BackColor = _cBackground
        };

        // Start on Startup checkbox
        _chkStartOnStartup = new CheckBox
        {
            Text = "Start on Windows startup",
            ForeColor = _cText,
            Font = new Font("Segoe UI", 11),
            AutoSize = true,
            Location = new Point(0, 20),
            BackColor = _cBackground
        };
        _chkStartOnStartup.CheckedChanged += ChkStartOnStartup_CheckedChanged;

        // Minimize to System Tray checkbox
        _chkMinimizeToTray = new CheckBox
        {
            Text = "Minimize to system tray",
            ForeColor = _cText,
            Font = new Font("Segoe UI", 11),
            AutoSize = true,
            Location = new Point(0, 60),
            BackColor = _cBackground
        };
        _chkMinimizeToTray.CheckedChanged += ChkMinimizeToTray_CheckedChanged;

        settingsOptionsPanel.Controls.Add(_chkStartOnStartup);
        settingsOptionsPanel.Controls.Add(_chkMinimizeToTray);

        _settingsButtonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = _cBackground,
            Padding = new Padding(0, 20, 0, 0),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        // Add buttons
        AddSettingsButton("Install Context Menu", "\uE710", (s, e) => MenuInstallContext_Click(s, e));
        AddSettingsButton("Uninstall Context Menu", "\uE74D", (s, e) => MenuUninstallContext_Click(s, e));
        AddSettingsButton("About", "\uE946", (s, e) => MenuAbout_Click(s, e));

        _settingsContentPanel.Controls.Add(_settingsButtonsPanel);
        _settingsContentPanel.Controls.Add(settingsOptionsPanel);
        _settingsContentPanel.Controls.Add(lblTitle);
        _contentPanel.Controls.Add(_settingsContentPanel);
    }

    private void InitializeNpmHandlerPanel()
    {
        _npmHandlerPanel = new NpmHandlerPanel
        {
            Visible = false
        };
        _contentPanel.Controls.Add(_npmHandlerPanel);
    }

    private void InitializeSystemTray()
    {
        Icon? trayIcon = this.Icon;
        if (trayIcon == null)
        {
            // Try to load from embedded executable icon
            try
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                }
                
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    trayIcon = Icon.ExtractAssociatedIcon(exePath);
                }
            }
            catch
            {
                // Fallback to file path
            }
            
            if (trayIcon == null)
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icons", "chip_icon.ico");
                if (!File.Exists(iconPath))
                {
                    iconPath = Path.Combine(Application.StartupPath, "Icons", "chip_icon.ico");
                }
                if (File.Exists(iconPath))
                {
                    trayIcon = new Icon(iconPath);
                }
                else
                {
                    trayIcon = SystemIcons.Application;
                }
            }
        }

        _notifyIcon = new NotifyIcon
        {
            Icon = trayIcon,
            Text = "OPTools",
            Visible = false
        };

        _notifyIcon.DoubleClick += (s, e) =>
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
        };

        ContextMenuStrip trayMenu = new ContextMenuStrip();
        trayMenu.BackColor = _cSidebar;
        trayMenu.ForeColor = _cText;

        ToolStripMenuItem showItem = new ToolStripMenuItem("Show OPTools");
        showItem.Click += (s, e) =>
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
        };

        ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => Application.Exit();

        trayMenu.Items.Add(showItem);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = trayMenu;
    }

    private void LoadSettings()
    {
        // Load settings and sync with actual registry state
        bool startOnStartup = SettingsManager.GetStartOnStartup();
        bool isStartupEnabled = StartupManager.IsStartupEnabled();

        // If settings say enabled but registry doesn't, enable it
        // If settings say disabled but registry is enabled, disable it
        if (startOnStartup && !isStartupEnabled)
        {
            StartupManager.EnableStartup();
        }
        else if (!startOnStartup && isStartupEnabled)
        {
            StartupManager.DisableStartup();
        }

        _chkStartOnStartup.Checked = startOnStartup;
        _chkMinimizeToTray.Checked = SettingsManager.GetMinimizeToTray();
    }

    private void ChkStartOnStartup_CheckedChanged(object? sender, EventArgs e)
    {
        bool enabled = _chkStartOnStartup.Checked;
        SettingsManager.SetStartOnStartup(enabled);

        if (enabled)
        {
            if (!StartupManager.EnableStartup())
            {
                MessageBox.Show("Failed to enable startup. Please try again.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _chkStartOnStartup.Checked = false;
            }
        }
        else
        {
            StartupManager.DisableStartup();
        }
    }

    private void ChkMinimizeToTray_CheckedChanged(object? sender, EventArgs e)
    {
        bool enabled = _chkMinimizeToTray.Checked;
        SettingsManager.SetMinimizeToTray(enabled);
        _notifyIcon.Visible = enabled;
    }

    private void AddProcessButton(string text, string icon, EventHandler action)
    {
        var btn = new ModernButton
        {
            Text = text,
            IconChar = icon,
            BackColor = _cAccent,
            Width = 200,
            Height = 50,
            Margin = new Padding(0, 0, 15, 15)
        };
        btn.Click += action;
        _processesButtonsPanel.Controls.Add(btn);
    }

    private void AddSettingsButton(string text, string icon, EventHandler action)
    {
        var btn = new ModernButton
        {
            Text = text,
            IconChar = icon,
            BackColor = _cAccent,
            Width = 200,
            Height = 50,
            Margin = new Padding(0, 0, 15, 15)
        };
        btn.Click += action;
        _settingsButtonsPanel.Controls.Add(btn);
    }

    private void AddCleanerButton(string text, string icon, EventHandler action)
    {
        var btn = new ModernButton
        {
            Text = text,
            IconChar = icon,
            BackColor = _cAccent,
            Width = 200,
            Height = 50,
            Margin = new Padding(0, 0, 15, 15)
        };
        btn.Click += action;
        _cleanerButtonsPanel.Controls.Add(btn);
    }

    private void AddNetworkButton(string text, string icon, Func<Task<ResetResult>> action, bool isDanger = false)
    {
        var btn = new ModernButton
        {
            Text = text,
            IconChar = icon,
            BackColor = isDanger ? _cDanger : _cAccent,
            Width = 200,
            Height = 50,
            Margin = new Padding(0, 0, 15, 15)
        };

        btn.Click += async (s, e) =>
        {
            if (isDanger)
            {
                DialogResult confirm = MessageBox.Show(
                    "This will reset your entire network stack. You may need to restart your computer.\n\nContinue?",
                    "Confirm Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;
            }

            using (Form progressForm = CreateProgressForm($"Running {text}..."))
            {
                progressForm.Show();
                Application.DoEvents();

                var result = await action();
                progressForm.Close();

                string message = result.Success
                    ? $"{text} completed successfully!"
                    : $"{text} completed with errors:\n\n{string.Join("\n", result.Errors)}";

                MessageBox.Show(message, result.Success ? "Success" : "Warning",
                    MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
        };

        _networkButtonsPanel.Controls.Add(btn);
    }

    private void ShowView(string viewName)
    {
        // Update sidebar button states
        _navUnlocker.IsActive = viewName == "unlocker";
        _navCleaner.IsActive = viewName == "cleaner";
        _navNetwork.IsActive = viewName == "network";
        _navProcesses.IsActive = viewName == "processes";
        _navContextMenu.IsActive = viewName == "contextmenu";
        _navApplications.IsActive = viewName == "applications";
        _navNpmHandler.IsActive = viewName == "npmhandler";
        _navSettings.IsActive = viewName == "settings";

        // Show/hide panels
        _listView.Visible = viewName == "unlocker";
        _headerPanel.Visible = viewName == "unlocker";
        _listView.Visible = viewName == "unlocker";
        _headerPanel.Visible = viewName == "unlocker";
        _applicationsContentPanel.Visible = viewName == "applications";
        _cleanerContentPanel.Visible = viewName == "cleaner";
        _networkContentPanel.Visible = viewName == "network";
        _processesContentPanel.Visible = viewName == "processes";
        _contextMenuContentPanel.Visible = viewName == "contextmenu";
        _npmHandlerPanel.Visible = viewName == "npmhandler";
        _settingsContentPanel.Visible = viewName == "settings";

        if (viewName == "npmhandler")
        {
            _npmHandlerPanel.Initialize();
        }

        if (viewName == "contextmenu")
        {
            RefreshContextEntries();
        }

        // Refresh sidebar
        _sidebarPanel.Invalidate();
    }

    private void ApplicationsPanel_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data == null)
        {
            e.Effect = DragDropEffects.None;
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effect = DragDropEffects.Copy;
        }
        else
        {
            e.Effect = DragDropEffects.None;
        }
    }

    private void ApplicationsPanel_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data == null || _appLauncher == null)
            return;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            
            if (files != null && files.Length > 0)
            {
                foreach (string filePath in files)
                {
                    if (System.IO.File.Exists(filePath))
                    {
                        string ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
                        if (ext == ".exe" || ext == ".lnk")
                        {
                            AddApplicationButton(filePath);
                            _appLauncher.AddShortcut(filePath);
                        }
                    }
                }
            }
        }
    }

    private void AddApplicationButton(string path)
    {
        if (!System.IO.File.Exists(path))
            return;

        try
        {
            Button btn = new Button
            {
                Size = new Size(100, 100),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = _cText,
                Text = System.IO.Path.GetFileNameWithoutExtension(path),
                Font = new Font("Segoe UI", 8),
                TextAlign = ContentAlignment.BottomCenter,
                ImageAlign = ContentAlignment.TopCenter,
                Tag = path,
                Cursor = Cursors.Hand,
                Padding = new Padding(5),
                Margin = new Padding(10)
            };
            
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 60);

            // Extract icon
            try
            {
                Icon? icon = Icon.ExtractAssociatedIcon(path);
                if (icon != null)
                {
                    btn.Image = icon.ToBitmap();
                    icon.Dispose();
                }
            }
            catch
            {
                // If icon extraction fails, continue without icon
            }

            // Handle text wrapping for long names
            if (btn.Text.Length > 15)
            {
                btn.Text = btn.Text.Substring(0, 12) + "...";
            }

            btn.Click += ApplicationButton_Click;
            btn.ContextMenuStrip = _contextMenuApplications;
            btn.MouseEnter += (s, e) => { btn.BackColor = _cHover; };
            btn.MouseLeave += (s, e) => { btn.BackColor = Color.FromArgb(45, 45, 48); };

            _applicationsPanel.Controls.Add(btn);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding application button: {ex.Message}");
        }
    }

    private void ApplicationButton_Click(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.Tag is string path)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error launching application:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void LoadApplications()
    {
        if (_appLauncher == null)
            return;

        _applicationsPanel.Controls.Clear();

        foreach (string shortcut in _appLauncher.GetShortcuts())
        {
            if (System.IO.File.Exists(shortcut))
            {
                AddApplicationButton(shortcut);
            }
        }
    }

    private void MenuRenameApplication_Click(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem menuItem)
        {
            ContextMenuStrip? menu = menuItem.Owner as ContextMenuStrip;
            Button? button = menu?.SourceControl as Button;
            
            if (button != null && button.Tag is string path)
            {
                string currentText = button.Text;
                string newText = ShowInputDialog("Enter new name for application:", currentText);
                
                if (!string.IsNullOrEmpty(newText) && newText != currentText)
                {
                    button.Text = newText.Length > 15 ? newText.Substring(0, 12) + "..." : newText;
                }
            }
        }
    }

    private void MenuDeleteApplication_Click(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem menuItem)
        {
            ContextMenuStrip? menu = menuItem.Owner as ContextMenuStrip;
            Button? button = menu?.SourceControl as Button;
            
            if (button != null && button.Tag is string path && _appLauncher != null)
            {
                _appLauncher.RemoveShortcut(path);
                _applicationsPanel.Controls.Remove(button);
                button.Dispose();
            }
        }
    }

    private static string ShowInputDialog(string prompt, string defaultValue)
    {
        Form promptForm = new Form
        {
            Width = 300,
            Height = 150,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Rename Application",
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        Label promptLabel = new Label
        {
            Left = 20,
            Top = 20,
            Width = 250,
            Text = prompt
        };

        TextBox textBox = new TextBox
        {
            Left = 20,
            Top = 50,
            Width = 250,
            Text = defaultValue
        };

        Button confirmation = new Button
        {
            Text = "OK",
            Left = 100,
            Width = 75,
            Top = 80,
            DialogResult = DialogResult.OK
        };

        Button cancel = new Button
        {
            Text = "Cancel",
            Left = 180,
            Width = 75,
            Top = 80,
            DialogResult = DialogResult.Cancel
        };

        promptForm.Controls.AddRange(new Control[] { promptLabel, textBox, confirmation, cancel });
        promptForm.AcceptButton = confirmation;
        promptForm.CancelButton = cancel;

        DialogResult result = promptForm.ShowDialog();
        return result == DialogResult.OK ? textBox.Text : defaultValue;
    }

    private void InitializeHeaderButtons()
    {
        _headerPanel.Controls.Clear();
        
        // Use FlowLayoutPanel for left-aligned buttons
        FlowLayoutPanel flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };

        _btnUnlockAll.Margin = new Padding(0, 0, 10, 0);
        _btnKillProcess.Margin = new Padding(0, 0, 10, 0);
        _btnDelete.Margin = new Padding(0, 0, 10, 0);
        _btnMove.Margin = new Padding(0, 0, 10, 0);

        flow.Controls.Add(_btnUnlockAll);
        flow.Controls.Add(_btnKillProcess);
        flow.Controls.Add(_btnDelete);
        flow.Controls.Add(_btnMove);

        _headerPanel.Controls.Add(flow);
        _headerPanel.Controls.Add(_btnRefresh); // Refresh docked Right
    }

    private SidebarButton CreateSidebarButton(string text, string icon, bool isActive = false)
    {
        return new SidebarButton
        {
            Text = text,
            IconChar = icon,
            IsActive = isActive,
            Dock = DockStyle.Top,
            Height = 55
        };
    }

    private ModernButton CreateActionButton(string text, string icon, Color color)
    {
        return new ModernButton
        {
            Text = text,
            IconChar = icon,
            BackColor = color,
            Width = 140,
            Margin = new Padding(0, 0, 15, 0)
        };
    }

    // Custom Drawing for ListView
    private void ListView_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        using (var brush = new SolidBrush(_cGridHeader))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }
        using (var brush = new SolidBrush(_cText))
        {
            var format = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Near };
            e.Graphics.DrawString(e.Header.Text, _listView.Font, brush, new Rectangle(e.Bounds.X + 5, e.Bounds.Y, e.Bounds.Width - 5, e.Bounds.Height), format);
        }
    }

    private void ListView_DrawItem(object? sender, DrawListViewItemEventArgs e)
    {
        e.DrawDefault = true;
    }

    private void ListView_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        // Custom draw to handle selection color
        if (e.Item.Selected)
        {
            using (var brush = new SolidBrush(Color.FromArgb(50, 50, 50)))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
        }
        else
        {
            using (var brush = new SolidBrush(_cBackground))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
        }

        // Draw Text
        var textBrush = e.Item.Selected ? Brushes.White : new SolidBrush(_cText);
        
        // Handle system process warning color
        if (e.Item.Tag is LockInfo info && ProcessManager.IsSystemProcess(info.ProcessId))
        {
            textBrush = Brushes.Yellow;
        }

        e.Graphics.DrawString(e.SubItem.Text, _listView.Font, textBrush, new Rectangle(e.Bounds.X + 5, e.Bounds.Y, e.Bounds.Width - 5, e.Bounds.Height), new StringFormat { LineAlignment = StringAlignment.Center });
    }

    private void ShowContextMenuForButton(Button btn, ContextMenuStrip menu)
    {
        menu.Show(btn, new Point(btn.Width, 0));
    }



    // Removed GetToolsMenu as it is no longer used

    private async void LoadLocks()
    {
        if (string.IsNullOrWhiteSpace(_targetPath))
        {
            _lblStatus.Text = "No target path specified";
            return;
        }

        _progressBar.Visible = true;
        _lblStatus.Text = "Enumerating locks...";
        _listView.Items.Clear();
        _btnUnlockAll.Enabled = false;
        _btnKillProcess.Enabled = false;
        _btnDelete.Enabled = false;
        _btnMove.Enabled = false;

        try
        {
            await Task.Run(() =>
            {
                _unlocker = new FileUnlocker(_targetPath);
                List<LockInfo> locks = _unlocker.GetLocks();

                this.Invoke((MethodInvoker)delegate
                {
                    foreach (LockInfo lockInfo in locks)
                    {
                        ListViewItem item = new ListViewItem(lockInfo.ProcessName);
                        item.SubItems.Add(lockInfo.ProcessId.ToString());
                        item.SubItems.Add(lockInfo.FilePath);
                        item.SubItems.Add(lockInfo.HandleType);
                        item.Tag = lockInfo;

                        // Color logic moved to DrawSubItem
                        _listView.Items.Add(item);
                    }

                    _lblStatus.Text = locks.Count == 0 
                        ? $"No locks found for: {_targetPath}" 
                        : $"Found {locks.Count} lock(s) for: {_targetPath}";

                    _btnUnlockAll.Enabled = locks.Count > 0;
                    _btnKillProcess.Enabled = locks.Count > 0;
                    _btnDelete.Enabled = true;
                    _btnMove.Enabled = true;
                });
            });
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Error enumerating locks: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _progressBar.Visible = false;
        }
    }

    private async void BtnUnlockAll_Click(object? sender, EventArgs e)
    {
        if (_unlocker == null)
            return;

        DialogResult result = MessageBox.Show(
            "This will unlock all handles. Continue?",
            "Confirm Unlock",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
            return;

        _progressBar.Visible = true;
        _lblStatus.Text = "Unlocking handles...";
        _btnUnlockAll.Enabled = false;
        _btnKillProcess.Enabled = false;
        _btnDelete.Enabled = false;
        _btnMove.Enabled = false;

        try
        {
            UnlockResult unlockResult = await Task.Run(() => _unlocker.UnlockAll(false));

            _lblStatus.Text = unlockResult.Success
                ? $"Successfully unlocked {unlockResult.UnlockedHandles} handle(s)"
                : $"Unlocked {unlockResult.UnlockedHandles} handle(s). {unlockResult.Errors.Count} error(s).";

            if (unlockResult.Errors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", unlockResult.Errors), "Unlock Errors",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (unlockResult.Success)
            {
                LoadLocks();
            }
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Error unlocking handles: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _progressBar.Visible = false;
            _btnUnlockAll.Enabled = true;
            _btnKillProcess.Enabled = true;
            _btnDelete.Enabled = true;
            _btnMove.Enabled = true;
        }
    }

    private async void BtnKillProcess_Click(object? sender, EventArgs e)
    {
        if (_listView.SelectedItems.Count == 0)
        {
            MessageBox.Show("Please select a process to kill.", "No Selection",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ListViewItem? selectedItem = _listView.SelectedItems.Count > 0 ? _listView.SelectedItems[0] : null;
        if (selectedItem == null || selectedItem.Tag == null)
            return;
        
        LockInfo lockInfo = (LockInfo)selectedItem.Tag;

        if (ProcessManager.IsSystemProcess(lockInfo.ProcessId))
        {
            DialogResult warning = MessageBox.Show(
                $"Warning: {lockInfo.ProcessName} appears to be a system process.\n\n" +
                "Killing system processes may cause system instability.\n\n" +
                "Are you sure you want to continue?",
                "System Process Warning",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (warning != DialogResult.Yes)
                return;
        }

        DialogResult result = MessageBox.Show(
            $"Kill process: {lockInfo.ProcessName} (PID: {lockInfo.ProcessId})?",
            "Confirm Kill",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
            return;

        _progressBar.Visible = true;
        _lblStatus.Text = $"Killing process: {lockInfo.ProcessName}...";
        _btnUnlockAll.Enabled = false;
        _btnKillProcess.Enabled = false;
        _btnDelete.Enabled = false;
        _btnMove.Enabled = false;

        try
        {
            bool success = await Task.Run(() => ProcessManager.KillProcess(lockInfo.ProcessId));

            if (success)
            {
                _lblStatus.Text = $"Successfully killed process: {lockInfo.ProcessName}";
                LoadLocks();
            }
            else
            {
                _lblStatus.Text = $"Failed to kill process: {lockInfo.ProcessName}";
                MessageBox.Show($"Failed to kill process: {lockInfo.ProcessName} (PID: {lockInfo.ProcessId})",
                    "Kill Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Error killing process: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _progressBar.Visible = false;
            _btnUnlockAll.Enabled = true;
            _btnKillProcess.Enabled = true;
            _btnDelete.Enabled = true;
            _btnMove.Enabled = true;
        }
    }

    private async void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (_unlocker == null)
            return;

        if (PathHelper.IsSystemPath(_targetPath))
        {
            DialogResult systemWarning = MessageBox.Show(
                $"Warning: {_targetPath} appears to be in a system directory.\n\n" +
                "Deleting system files may cause system instability or prevent Windows from functioning.\n\n" +
                "Are you absolutely sure you want to continue?",
                "System Path Warning",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (systemWarning != DialogResult.Yes)
                return;
        }

        DialogResult result = MessageBox.Show(
            $"Delete: {_targetPath}?\n\n" +
            "This will unlock all handles and delete the file/folder.",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
            return;

        _progressBar.Visible = true;
        _lblStatus.Text = "Deleting...";
        _btnUnlockAll.Enabled = false;
        _btnKillProcess.Enabled = false;
        _btnDelete.Enabled = false;
        _btnMove.Enabled = false;

        try
        {
            await Task.Run(() => _unlocker.DeleteFileOrFolder());

            _lblStatus.Text = "Successfully deleted";
            MessageBox.Show("File/folder deleted successfully.", "Success",
                MessageBoxButtons.OK, MessageBoxIcon.Information);

        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Error: {ex.Message}";
            
            DialogResult scheduleResult = MessageBox.Show(
                $"{ex.Message}\n\nWould you like to schedule this item for deletion on the next computer restart?",
                "Delete Failed",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Error);

            if (scheduleResult == DialogResult.Yes)
            {
                try
                {
                    if (_unlocker.ScheduleDeleteOnReboot())
                    {
                        MessageBox.Show("The item has been scheduled for deletion on the next restart.", 
                            "Scheduled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show("Failed to schedule deletion. Ensure you are running as Administrator.", 
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception scheduleEx)
                {
                    MessageBox.Show($"Failed to schedule deletion: {scheduleEx.Message}", 
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        finally
        {
            _progressBar.Visible = false;
            _btnUnlockAll.Enabled = true;
            _btnKillProcess.Enabled = true;
            _btnDelete.Enabled = true;
            _btnMove.Enabled = true;
        }
    }

    private async void BtnMove_Click(object? sender, EventArgs e)
    {
        if (_unlocker == null)
            return;

        if (PathHelper.IsSystemPath(_targetPath))
        {
            DialogResult systemWarning = MessageBox.Show(
                $"Warning: {_targetPath} appears to be in a system directory.\n\n" +
                "Moving system files may cause system instability or prevent Windows from functioning.\n\n" +
                "Are you absolutely sure you want to continue?",
                "System Path Warning",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (systemWarning != DialogResult.Yes)
                return;
        }

        using (FolderBrowserDialog dialog = new FolderBrowserDialog())
        {
            dialog.Description = "Select destination folder:";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                DialogResult result = MessageBox.Show(
                    $"Move: {_targetPath}\nTo: {dialog.SelectedPath}?\n\n" +
                    "This will unlock all handles and move the file/folder.",
                    "Confirm Move",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result != DialogResult.Yes)
                    return;

                _progressBar.Visible = true;
                _lblStatus.Text = "Moving...";
                _btnUnlockAll.Enabled = false;
                _btnKillProcess.Enabled = false;
                _btnDelete.Enabled = false;
                _btnMove.Enabled = false;

                try
                {
                    await Task.Run(() => _unlocker.MoveFileOrFolder(dialog.SelectedPath));

                    _lblStatus.Text = "Successfully moved";
                    MessageBox.Show("File/folder moved successfully.", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                }
                catch (Exception ex)
                {
                    _lblStatus.Text = $"Error: {ex.Message}";
                    MessageBox.Show(ex.Message, "Move Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                finally
                {
                    _progressBar.Visible = false;
                    _btnUnlockAll.Enabled = true;
                    _btnKillProcess.Enabled = true;
                    _btnDelete.Enabled = true;
                    _btnMove.Enabled = true;
                }
            }
        }
    }

    private void BtnRefresh_Click(object? sender, EventArgs e)
    {
        LoadLocks();
    }

    private void UpdateContextMenuStatus()
    {
        bool isInstalled = ContextMenuInstaller.IsInstalled();
        // _menuInstallContext.Enabled = !isInstalled;
        // _menuUninstallContext.Enabled = isInstalled;
        // Buttons in Settings panel will need to be updated if we want to enable/disable them dynamically
        // For now, we'll leave them enabled and let the logic handle it, or we could store references to the buttons.
    }

    private void MenuInstallContext_Click(object? sender, EventArgs e)
    {
        if (!Program.IsRunningAsAdministrator())
        {
            DialogResult result = MessageBox.Show(
                "Administrator privileges are required to install the context menu.\n\n" +
                "Would you like to restart the application as administrator?",
                "Elevation Required",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    string exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                    System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "/INSTALL",
                        Verb = "runas",
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(startInfo);
                    Application.Exit();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to restart as administrator: {ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            return;
        }

        try
        {
            // Show what path will be used before installing (for user info)
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath) || !exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                exePath = currentProcess.MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath) && exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    exePath = System.IO.Path.ChangeExtension(exePath, ".exe");
                }
            }
            
            if (ContextMenuInstaller.Install())
            {
                string message = "Context menu installed successfully!\n\n" +
                    "You can now right-click on files and folders to use 'Delete with OPTools'.";
                
                if (!string.IsNullOrEmpty(exePath) && System.IO.File.Exists(exePath))
                {
                    message += $"\n\nInstalled path: {exePath}";
                }
                
                MessageBox.Show(message,
                    "Installation Successful",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                UpdateContextMenuStatus();
            }
            else
            {
                MessageBox.Show("Failed to install context menu.",
                    "Installation Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show("Administrator privileges are required to install the context menu.",
                "Access Denied",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error installing context menu: {ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void MenuUninstallContext_Click(object? sender, EventArgs e)
    {
        if (!Program.IsRunningAsAdministrator())
        {
            DialogResult result = MessageBox.Show(
                "Administrator privileges are required to uninstall the context menu.\n\n" +
                "Would you like to restart the application as administrator?",
                "Elevation Required",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    string exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                    System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = "/UNINSTALL",
                        Verb = "runas",
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(startInfo);
                    Application.Exit();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to restart as administrator: {ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            return;
        }

        DialogResult confirm = MessageBox.Show(
            "Are you sure you want to uninstall the context menu?",
            "Confirm Uninstall",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
            return;

        try
        {
            if (ContextMenuInstaller.Uninstall())
            {
                MessageBox.Show("Context menu uninstalled successfully.",
                    "Uninstallation Successful",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                UpdateContextMenuStatus();
            }
            else
            {
                MessageBox.Show("Failed to uninstall context menu. It may not be installed.",
                    "Uninstallation Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show("Administrator privileges are required to uninstall the context menu.",
                "Access Denied",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error uninstalling context menu: {ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void MenuAbout_Click(object? sender, EventArgs e)
    {
        string aboutText = "OPTools - Operating System Tools\n\n" +
            "Version: 1.0.0\n\n" +
            "A Windows application suite with various operating system tools.\n\n" +
            "Available Tools:\n" +
            "• Force Delete - Delete locked files/folders\n" +
            "• Clean Folder Contents - Delete all contents inside a folder\n" +
            "• Kill Processes - Kill Node.js, Git, Bash, WSL processes\n" +
            "• Kill by Port - View and kill processes using specific ports\n" +
            "• Reset Internet - Reset network stack (DNS, TCP/IP, Winsock)\n" +
            "• System Cleaner - Remove prefetch files and empty Recycle Bin\n\n" +
            "Requires administrator privileges for full functionality.";

        MessageBox.Show(aboutText, "About OPTools",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async void MenuCleanFolder_Click(object? sender, EventArgs e)
    {
        using (FolderBrowserDialog dialog = new FolderBrowserDialog())
        {
            dialog.Description = "Select folder to clean contents from:";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                DialogResult confirm = MessageBox.Show(
                    $"This will delete ALL contents inside:\n{dialog.SelectedPath}\n\n" +
                    "The folder itself will be kept. Continue?",
                    "Confirm Clean",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm == DialogResult.Yes)
                {
                    using (Form progressForm = CreateProgressForm("Cleaning folder contents..."))
                    {
                        progressForm.Show();
                        Application.DoEvents();

                        var result = await FolderCleaner.CleanFolderContents(dialog.SelectedPath);
                        progressForm.Close();

                        string message = result.Success
                            ? $"Successfully cleaned folder!\n\nFiles deleted: {result.FilesDeleted}\nFolders deleted: {result.FoldersDeleted}"
                            : $"Cleaned with errors:\n\nFiles deleted: {result.FilesDeleted}\nFolders deleted: {result.FoldersDeleted}\n\nErrors:\n{string.Join("\n", result.Errors)}";

                        MessageBox.Show(message, result.Success ? "Success" : "Warning",
                            MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                    }
                }
            }
        }
    }

    private async void MenuKillNodeJs_Click(object? sender, EventArgs e)
    {
        DialogResult confirm = MessageBox.Show(
            "This will kill all Node.js processes. Continue?",
            "Confirm Kill",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm == DialogResult.Yes)
        {
            var result = await ProcessKiller.KillNodeJs();
            ShowKillResult("Node.js", result);
        }
    }

    private async void MenuKillDevTools_Click(object? sender, EventArgs e)
    {
        DialogResult confirm = MessageBox.Show(
            "This will kill all Node.js, Git Bash, Git, and WSL Relay processes. Continue?",
            "Confirm Kill",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm == DialogResult.Yes)
        {
            using (Form progressForm = CreateProgressForm("Killing processes..."))
            {
                progressForm.Show();
                Application.DoEvents();

                var result = await ProcessKiller.KillAllDevTools();
                progressForm.Close();

                ShowKillResult("Dev Tools", result);
            }
        }
    }

    private void MenuKillPort_Click(object? sender, EventArgs e)
    {
        using (PortManagerForm form = new PortManagerForm())
        {
            form.ShowDialog();
        }
    }

    private async void MenuResetInternet_Click(object? sender, EventArgs e)
    {
        DialogResult confirm = MessageBox.Show(
            "This will reset your network stack (DNS, TCP/IP, Winsock, etc.).\n\n" +
            "You may need to restart your computer for full effect.\n\n" +
            "Continue?",
            "Confirm Reset",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm == DialogResult.Yes)
        {
            using (Form progressForm = CreateProgressForm("Resetting network stack..."))
            {
                progressForm.Show();
                Application.DoEvents();

                var result = await NetworkReset.ResetInternetStack();
                progressForm.Close();

                string message = result.Success
                    ? "Network stack reset successfully!\n\nPlease restart your computer for full effect."
                    : $"Reset completed with errors:\n\n{string.Join("\n", result.Errors)}";

                MessageBox.Show(message, result.Success ? "Success" : "Warning",
                    MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
        }
    }

    private async void MenuCleanPrefetch_Click(object? sender, EventArgs e)
    {
        DialogResult confirm = MessageBox.Show(
            "This will delete all prefetch files. Continue?",
            "Confirm Clean",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm == DialogResult.Yes)
        {
            using (Form progressForm = CreateProgressForm("Removing prefetch files..."))
            {
                progressForm.Show();
                Application.DoEvents();

                var result = await SystemCleaner.RemovePrefetchFiles();
                progressForm.Close();

                string message = result.Success
                    ? $"Successfully removed {result.FilesDeleted} prefetch file(s)!"
                    : $"Removed {result.FilesDeleted} file(s) with errors:\n\n{string.Join("\n", result.Errors)}";

                MessageBox.Show(message, result.Success ? "Success" : "Warning",
                    MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
        }
    }

    private async void MenuEmptyRecycleBin_Click(object? sender, EventArgs e)
    {
        DialogResult confirm = MessageBox.Show(
            "This will empty the Recycle Bin on drives C: and D:. Continue?",
            "Confirm Empty",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm == DialogResult.Yes)
        {
            using (Form progressForm = CreateProgressForm("Emptying Recycle Bin..."))
            {
                progressForm.Show();
                Application.DoEvents();

                var result = await SystemCleaner.EmptyRecycleBin();
                progressForm.Close();

                string message = result.Success
                    ? $"Successfully emptied Recycle Bin!\n\nFiles deleted: {result.FilesDeleted}\nFolders deleted: {result.FoldersDeleted}"
                    : $"Cleaned with errors:\n\nFiles deleted: {result.FilesDeleted}\nFolders deleted: {result.FoldersDeleted}\n\nErrors:\n{string.Join("\n", result.Errors)}";

                MessageBox.Show(message, result.Success ? "Success" : "Warning",
                    MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
        }
    }

    private async void MenuCleanSystem_Click(object? sender, EventArgs e)
    {
        DialogResult confirm = MessageBox.Show(
            "This will remove prefetch files and empty the Recycle Bin. Continue?",
            "Confirm Clean",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm == DialogResult.Yes)
        {
            using (Form progressForm = CreateProgressForm("Cleaning system..."))
            {
                progressForm.Show();
                Application.DoEvents();

                var result = await SystemCleaner.CleanAll();
                progressForm.Close();

                string message = result.Success
                    ? $"System cleaned successfully!\n\nFiles deleted: {result.FilesDeleted}\nFolders deleted: {result.FoldersDeleted}"
                    : $"Cleaned with errors:\n\nFiles deleted: {result.FilesDeleted}\nFolders deleted: {result.FoldersDeleted}\n\nErrors:\n{string.Join("\n", result.Errors)}";

                MessageBox.Show(message, result.Success ? "Success" : "Warning",
                    MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
        }
    }

    private void ShowKillResult(string toolName, KillResult result)
    {
        string message = result.Success
            ? $"Successfully killed {result.ProcessesKilled} {toolName} process(es)!"
            : $"Killed {result.ProcessesKilled} process(es) with errors:\n\n{string.Join("\n", result.Errors)}";

        MessageBox.Show(message, result.Success ? "Success" : "Warning",
            MessageBoxButtons.OK, result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private Form CreateProgressForm(string message)
    {
        Form form = new Form
        {
            Text = "Processing...",
            Size = new Size(300, 100),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false
        };

        Label label = new Label
        {
            Text = message,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        ProgressBar progressBar = new ProgressBar
        {
            Dock = DockStyle.Bottom,
            Style = ProgressBarStyle.Marquee,
            Height = 20
        };

        form.Controls.Add(label);
        form.Controls.Add(progressBar);
        return form;
    }

    private void MainForm_Resize(object? sender, EventArgs e)
    {
        if (this.WindowState == FormWindowState.Minimized && _chkMinimizeToTray.Checked)
        {
            this.Hide();
            _notifyIcon.Visible = true;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        base.OnFormClosing(e);
    }

}
}
