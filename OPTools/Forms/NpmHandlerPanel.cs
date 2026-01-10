using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using OPTools.Core;
using OPTools.Utils;
using Newtonsoft.Json;

namespace OPTools.Forms
{
    /// <summary>
    /// NPM Package Manager Panel - Discovers and maintains npm packages across projects
    /// </summary>
    public class NpmHandlerPanel : Panel
    {
        // Theme Colors (matching OPTools theme)
        private readonly Color _cBackground = Color.FromArgb(30, 30, 30);
        private readonly Color _cSidebar = Color.FromArgb(25, 25, 26);
        private readonly Color _cAccent = Color.FromArgb(0, 122, 204);
        private readonly Color _cDanger = Color.FromArgb(217, 83, 79);
        private readonly Color _cSuccess = Color.FromArgb(92, 184, 92);
        private readonly Color _cWarning = Color.FromArgb(240, 173, 78);
        private readonly Color _cText = Color.FromArgb(241, 241, 241);
        private readonly Color _cTextDim = Color.FromArgb(150, 150, 150);
        private readonly Color _cGridHeader = Color.FromArgb(45, 45, 48);
        private readonly Color _cHover = Color.FromArgb(60, 60, 60);

        // Core Components
        private NpmDatabase? _database;
        private NpmScanner? _scanner;
        private NpmUpdater? _updater;

        // UI Components
        private Panel _headerPanel = null!;
        private Panel _statsPanel = null!;
        private Panel _filterPanel = null!;
        private Panel _contentPanel = null!; // Container for views
        
        // View Components
        private ListView _packageListView = null!;
        private FlowLayoutPanel _projectsPanel = null!; // New Projects View
        
        private Label _lblStatus = null!;
        private ProgressBar _progressBar = null!;
        private ComboBox _cmbProject = null!;
        private ComboBox _cmbEcosystem = null!; // New Ecosystem Selector
        private TextBox _txtSearch = null!;
        private CheckBox _chkOutdatedOnly = null!;
        private ContextMenuStrip _contextMenu = null!;

        private ModernButton _btnScanDirectory = null!;
        private ModernButton _btnScanGlobal = null!;
        private ModernButton _btnCheckUpdates = null!;
        private ModernButton _btnUpdateAll = null!;
        private ModernButton _btnExport = null!;
        private ModernButton _btnRefresh = null!;
        private ModernButton _btnClearAll = null!;
        private ModernButton _btnBack = null!; // Back Button
        private ModernButton _btnLogs = null!; // Logs Button
        private ModernButton _btnAddFolder = null!; // New Add Manual Folder Button

        // Data
        private List<NpmPackage> _allPackages = new();
        private List<NpmProject> _allProjects = new();
        private List<NpmPackage> _filteredPackages = new();
        
        // Error Logs (matching NPM Handler)
        private List<LogEntry> _errorLogs = new();
        private int _logIdCounter = 0;
        
        // Package Details Panel
        private PackageDetailsPanel? _packageDetailsPanel;
        
        // State
        private enum ViewMode { Projects, Packages }
        private ViewMode _currentView = ViewMode.Projects;
        private string? _currentProjectFilter = null;
        
        // Sorting state
        private int _sortColumn = -1;
        private bool _sortAscending = true;

        private ToolTip _toolTip = null!;

        public NpmHandlerPanel()
        {
            InitializeComponents();
            InitializeContextMenu();
        }

        public void Initialize()
        {
            try
            {
                _database = new NpmDatabase();
                _scanner = new NpmScanner();
                _updater = new NpmUpdater();
                LoadData();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error initializing: {ex.Message}");
            }
        }

        private void InitializeComponents()
        {
            _toolTip = new ToolTip();
            _toolTip.AutoPopDelay = 5000;
            _toolTip.InitialDelay = 1000;
            _toolTip.ReshowDelay = 500;
            _toolTip.ShowAlways = true;

            this.BackColor = _cBackground;
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(30);

            // Header Panel with Title and Actions
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = _cBackground
            };

            Label lblTitle = new Label
            {
                Text = "Package Handler",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = _cText,
                AutoSize = true,
                Location = new Point(0, 0)
            };

            Label lblSubtitle = new Label
            {
                Text = "Discover and maintain packages across your projects",
                Font = new Font("Segoe UI", 9),
                ForeColor = _cTextDim,
                AutoSize = true,
                Location = new Point(0, 30)
            };
            
            // Back Button (Initially Hidden) - Added to action panel for proper flow
            _btnBack = CreateActionButton("\u2190 All Projects", "\uE72B", _cGridHeader, "Return to projects view"); // Back Arrow Icon
            _btnBack.Width = 120;
            _btnBack.Visible = false;
            _btnBack.Click += (s, e) => SwitchToProjects();

            // Action Buttons Panel
            FlowLayoutPanel actionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 45,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = _cBackground
            };

            _btnScanDirectory = CreateActionButton("Scan Directory", "\uE8B7", _cAccent, "Scan current directory for package.json");
            _btnAddFolder = CreateActionButton("Add Folder", "\uE710", _cAccent, "Manually add a project folder"); // Add icon
            _btnScanGlobal = CreateActionButton("Scan Global", "\uE774", _cAccent, "Scan for global npm packages");
            _btnCheckUpdates = CreateActionButton("Check Updates", "\uE777", _cAccent, "Check for package updates");
            _btnUpdateAll = CreateActionButton("Update All", "\uE74A", _cSuccess, "Update all outdated packages");
            _btnExport = CreateActionButton("Export", "\uE78C", Color.FromArgb(60, 60, 60), "Export package list");
            _btnRefresh = CreateActionButton("Refresh", "\uE72C", Color.FromArgb(60, 60, 60), "Refresh package list");
            _btnLogs = CreateActionButton("Logs", "\uE7BA", Color.FromArgb(60, 60, 60), "View error logs");
            _btnClearAll = CreateActionButton("Clear All", "\uE74D", _cDanger, "Clear all projects and packages");

            _btnScanDirectory.Click += BtnScanDirectory_Click;
            _btnAddFolder.Click += BtnAddFolder_Click;
            _btnScanGlobal.Click += BtnScanGlobal_Click;
            _btnCheckUpdates.Click += BtnCheckUpdates_Click;
            _btnUpdateAll.Click += BtnUpdateAll_Click;
            _btnExport.Click += BtnExport_Click;
            _btnRefresh.Click += BtnRefresh_Click;
            _btnLogs.Click += BtnLogs_Click;
            _btnClearAll.Click += BtnClearAll_Click;

            actionPanel.Controls.Add(_btnBack); // Back button first (hidden by default)
            actionPanel.Controls.Add(_btnScanDirectory);
            actionPanel.Controls.Add(_btnAddFolder);
            actionPanel.Controls.Add(_btnScanGlobal);
            actionPanel.Controls.Add(_btnCheckUpdates);
            actionPanel.Controls.Add(_btnUpdateAll);
            actionPanel.Controls.Add(_btnExport);
            actionPanel.Controls.Add(_btnRefresh);
            actionPanel.Controls.Add(_btnLogs);
            actionPanel.Controls.Add(_btnClearAll);

            _headerPanel.Controls.Add(lblTitle);
            _headerPanel.Controls.Add(lblSubtitle);
            _headerPanel.Controls.Add(actionPanel);

            // Stats Panel
            _statsPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = _cBackground,
                Padding = new Padding(0, 10, 0, 10)
            };

            // Filter Panel
            _filterPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 45,
                BackColor = _cBackground,
                Padding = new Padding(0, 5, 0, 5)
            };

            _txtSearch = new TextBox
            {
                Width = 250,
                Height = 30,
                BackColor = _cGridHeader,
                ForeColor = _cText,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10),
                Location = new Point(200, 5) // Moved slightly right
            };
            _txtSearch.PlaceholderText = "Search...";
            _txtSearch.TextChanged += FilterChanged;

            // Ecosystem Selector
            _cmbEcosystem = new ComboBox
            {
                Width = 180,
                Height = 30,
                BackColor = _cGridHeader,
                ForeColor = _cText,
                FlatStyle = FlatStyle.Flat,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10),
                Location = new Point(0, 5)
            };
            _cmbEcosystem.Items.AddRange(new object[] { "All", "NPM", "Python", "C++" });
            _cmbEcosystem.SelectedIndex = 0; // Default to All
            _cmbEcosystem.SelectedIndexChanged += EcosystemChanged;

            _cmbProject = new ComboBox
            {
                Width = 250,
                Height = 30,
                BackColor = _cGridHeader,
                ForeColor = _cText,
                FlatStyle = FlatStyle.Flat,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10),
                Location = new Point(470, 5), // Adjusted position
                Visible = false // Hidden by default now, as we use drill-down
            };
            _cmbProject.SelectedIndexChanged += FilterChanged;

            _chkOutdatedOnly = new CheckBox
            {
                Text = "Show outdated only",
                ForeColor = _cText,
                BackColor = _cBackground,
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                Location = new Point(470, 8) // Adjusted position
            };
            _chkOutdatedOnly.CheckedChanged += FilterChanged;

            _filterPanel.Controls.Add(_cmbEcosystem);
            _filterPanel.Controls.Add(_txtSearch);
            _filterPanel.Controls.Add(_cmbProject);
            _filterPanel.Controls.Add(_chkOutdatedOnly);

            // Status Bar
            Panel statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 35,
                BackColor = _cBackground
            };

            _lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = _cTextDim,
                Font = new Font("Segoe UI", 9),
                BackColor = _cBackground,
                Text = "Ready"
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

            // Main Content Panel (Holds Views)
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _cBackground
            };

            // 1. Package ListView (Hidden initially)
            _packageListView = new ListView
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
                Visible = false 
            };

            _packageListView.Columns.Add("Package Name", 200);
            _packageListView.Columns.Add("Version", 100);
            _packageListView.Columns.Add("Latest", 100);
            _packageListView.Columns.Add("Project", 300);
            _packageListView.Columns.Add("Status", 100);
            _packageListView.Columns.Add("Type", 80);

            _packageListView.DrawColumnHeader += ListView_DrawColumnHeader;
            _packageListView.DrawItem += ListView_DrawItem;
            _packageListView.DrawSubItem += ListView_DrawSubItem;
            _packageListView.ColumnClick += ListView_ColumnClick;
            _packageListView.MouseClick += ListView_MouseClick;
            _packageListView.MouseDoubleClick += ListView_MouseDoubleClick;

            // 2. Projects Flow Panel (Visible initially)
            _projectsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = _cBackground,
                Padding = new Padding(10),
                FlowDirection = FlowDirection.LeftToRight
            };

            _contentPanel.Controls.Add(_packageListView);
            _contentPanel.Controls.Add(_projectsPanel);

            // 3. Package Details Panel (Side drawer)
            _packageDetailsPanel = new PackageDetailsPanel();
            _packageDetailsPanel.CloseRequested += (s, e) => _packageDetailsPanel.HidePanel();
            _packageDetailsPanel.UpdateRequested += async (s, pkg) => 
            {
                if (pkg.IsOutdated && _updater != null)
                {
                    await UpdateSinglePackageAsync(pkg);
                }
            };
            _packageDetailsPanel.UninstallRequested += async (s, pkg) => 
            {
                await UninstallPackageAsync(pkg);
            };
            this.Controls.Add(_packageDetailsPanel);

            // Assemble Layout
            this.Controls.Add(_contentPanel);
            this.Controls.Add(statusPanel);
            this.Controls.Add(_filterPanel);
            this.Controls.Add(_statsPanel);
            this.Controls.Add(_headerPanel);

            UpdateStatsPanel();
        }
        
        private async Task UpdateSinglePackageAsync(NpmPackage package)
        {
            SetLoading(true);
            UpdateStatus($"Updating {package.Name}...");
            
            try
            {
                var result = await _updater!.UpdatePackageAsync(package);
                if (result.Success)
                {
                    UpdateStatus($"Updated {package.Name} to {result.NewVersion}");
                    AddLog(LogLevel.Info, $"Updated {package.Name}", $"From {result.OldVersion} to {result.NewVersion}");
                }
                else
                {
                    UpdateStatus($"Failed to update {package.Name}");
                    AddLog(LogLevel.Error, $"Failed to update {package.Name}", result.ErrorMessage ?? "Unknown error");
                }
                LoadData();
            }
            catch (Exception ex)
            {
                AddLog(LogLevel.Error, $"Error updating {package.Name}", ex.Message);
            }
            finally
            {
                SetLoading(false);
            }
        }
        
        private async Task UninstallPackageAsync(NpmPackage package)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to uninstall {package.Name}?",
                "Confirm Uninstall",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            
            if (result != DialogResult.Yes) return;
            
            SetLoading(true);
            UpdateStatus($"Uninstalling {package.Name}...");
            
            try
            {
                var success = await _updater!.UninstallPackageAsync(package);
                if (success)
                {
                    _database?.DeletePackage(package.ProjectPath, package.Name);
                    UpdateStatus($"Uninstalled {package.Name}");
                    AddLog(LogLevel.Info, $"Uninstalled {package.Name}", null);
                    _packageDetailsPanel?.HidePanel();
                    LoadData();
                }
                else
                {
                    UpdateStatus($"Failed to uninstall {package.Name}");
                    AddLog(LogLevel.Error, $"Failed to uninstall {package.Name}", "npm uninstall command failed");
                }
            }
            catch (Exception ex)
            {
                AddLog(LogLevel.Error, $"Error uninstalling {package.Name}", ex.Message);
            }
            finally
            {
                SetLoading(false);
            }
        }
        
        private void AddLog(LogLevel level, string message, string? details)
        {
            _errorLogs.Insert(0, new LogEntry
            {
                Id = ++_logIdCounter,
                Timestamp = DateTime.Now,
                Level = level,
                Message = message,
                Details = details
            });
            
            // Keep only last 100 logs
            if (_errorLogs.Count > 100)
            {
                _errorLogs.RemoveAt(_errorLogs.Count - 1);
            }
            
            // Update logs button visual indicator if errors
            UpdateLogsButtonIndicator();
        }
        
        private void UpdateLogsButtonIndicator()
        {
            var errorCount = _errorLogs.Count(l => l.Level == LogLevel.Error);
            if (errorCount > 0)
            {
                _btnLogs.Text = $"Logs ({errorCount})";
                _btnLogs.BackColor = _cWarning;
            }
            else
            {
                _btnLogs.Text = "Logs";
                _btnLogs.BackColor = Color.FromArgb(60, 60, 60);
            }
        }
        
        private void BtnLogs_Click(object? sender, EventArgs e)
        {
            using var dialog = new LogsDialog(_errorLogs);
            dialog.LogsCleared += (s, args) => UpdateLogsButtonIndicator();
            dialog.ShowDialog();
            UpdateLogsButtonIndicator();
        }

        private void InitializeContextMenu()
        {
            _contextMenu = new ContextMenuStrip
            {
                BackColor = _cSidebar,
                ForeColor = _cText,
                RenderMode = ToolStripRenderMode.System
            };

            var updateItem = new ToolStripMenuItem("Update Package");
            updateItem.Click += ContextMenu_UpdatePackage;

            var uninstallItem = new ToolStripMenuItem("Uninstall Package");
            uninstallItem.Click += ContextMenu_UninstallPackage;

            var removeItem = new ToolStripMenuItem("Remove from Catalog");
            removeItem.Click += ContextMenu_RemoveFromCatalog;

            var deleteProjectItem = new ToolStripMenuItem("Delete Entire Project");
            deleteProjectItem.Click += ContextMenu_DeleteProject;

            var openFolderItem = new ToolStripMenuItem("Open Folder");
            openFolderItem.Click += ContextMenu_OpenFolder;

            _contextMenu.Items.Add(updateItem);
            _contextMenu.Items.Add(uninstallItem);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(removeItem);
            _contextMenu.Items.Add(deleteProjectItem);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(openFolderItem);
        }

        private ModernButton CreateActionButton(string text, string icon, Color bgColor, string tooltipText = "")
        {
            var btn = new ModernButton
            {
                Text = text,
                IconChar = icon,
                BackColor = bgColor,
                Width = 140,
                Height = 40,
                Margin = new Padding(0, 0, 10, 0)
            };
            if (!string.IsNullOrEmpty(tooltipText))
            {
                _toolTip.SetToolTip(btn, tooltipText);
            }
            return btn;
        }

        #region Data Loading and View Switching

        private void LoadData()
        {
            try
            {
                var allPackages = _database?.GetAllPackages() ?? new List<NpmPackage>();
                var allProjects = _database?.GetAllProjects() ?? new List<NpmProject>();
                
                // Filter by current ecosystem (null = All ecosystems)
                var ecosystem = GetSelectedEcosystem();
                if (ecosystem.HasValue)
                {
                    _allProjects = allProjects.Where(p => p.Ecosystem == ecosystem.Value).ToList();
                    _allPackages = allPackages.Where(p => 
                        _allProjects.Any(proj => proj.Path == p.ProjectPath)
                    ).ToList();
                }
                else
                {
                    // "All" selected - show everything
                    _allProjects = allProjects;
                    _allPackages = allPackages;
                }
                
                // Refresh current view
                if (_currentView == ViewMode.Projects)
                {
                    RenderProjects();
                }
                else
                {
                    ApplyFilters();
                }

                UpdateStatsPanel();
                var ecosystemLabel = ecosystem.HasValue ? ecosystem.Value.ToString() : "all";
                UpdateStatus($"Loaded {_allPackages.Count} {ecosystemLabel} packages from {_allProjects.Count} projects");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading data: {ex.Message}");
            }
        }

        private void SwitchToProjects()
        {
            _currentView = ViewMode.Projects;
            _currentProjectFilter = null;
            _packageListView.Visible = false;
            _projectsPanel.Visible = true;
            _btnBack.Visible = false;
            _chkOutdatedOnly.Visible = false; // Filter applies to packages usually
            _chkOutdatedOnly.Checked = false;
            _txtSearch.Text = "";
            _txtSearch.PlaceholderText = "Search projects...";
            
            // Reset Update button
            _btnUpdateAll.Text = "Update All";
            _btnUpdateAll.IconChar = "\uE74A";
            
            RenderProjects();
        }

        private void SwitchToPackages(string projectPath)
        {
            _currentView = ViewMode.Packages;
            _currentProjectFilter = projectPath;
            _projectsPanel.Visible = false;
            _packageListView.Visible = true;
            _btnBack.Visible = true;
            _chkOutdatedOnly.Visible = true;
            _txtSearch.Text = "";
            _txtSearch.PlaceholderText = "Search packages...";
            
            // Customize Update Button based on scope
            bool isGlobal = projectPath == "__GLOBAL__";
            _btnUpdateAll.Text = isGlobal ? "Update All" : "Update Project";
            // Use specific icon for project update if desired, or keep generic update icon
            
            ApplyFilters();
        }

        private void RenderProjects()
        {
            _projectsPanel.Controls.Clear();
            _projectsPanel.SuspendLayout();

            var searchTerm = _txtSearch.Text.ToLower();

            var projectsToRender = _allProjects.Where(p => 
                string.IsNullOrEmpty(searchTerm) || 
                p.Name.ToLower().Contains(searchTerm) || 
                p.Path.ToLower().Contains(searchTerm)
            )
            .OrderByDescending(p => p.Path == "__GLOBAL__") // Global Packages first
            .ThenBy(p => p.Name)
            .ToList();

            if (projectsToRender.Count == 0)
            {
                var lblEmpty = new Label
                {
                    Text = "No projects found. Scan a directory to get started.",
                    ForeColor = _cTextDim,
                    AutoSize = true,
                    Font = new Font("Segoe UI", 12),
                    Padding = new Padding(20)
                };
                _projectsPanel.Controls.Add(lblEmpty);
            }
            else
            {
                foreach (var project in projectsToRender)
                {
                    _projectsPanel.Controls.Add(CreateProjectCard(project));
                }
            }

            _projectsPanel.ResumeLayout();
        }

        private Panel CreateProjectCard(NpmProject project)
        {
            // Calculate stats for this project
            var packages = _allPackages.Where(p => p.ProjectPath == project.Path).ToList();
            var outdatedCount = packages.Count(p => p.IsOutdated);
            var totalCount = packages.Count;

            var card = new Panel
            {
                Width = 280,
                Height = 140,
                BackColor = _cGridHeader,
                Margin = new Padding(10),
                Cursor = Cursors.Hand
            };

            // Hover effect logic
            void OnMouseEnter(object? s, EventArgs e) => card.BackColor = _cHover;
            void OnMouseLeave(object? s, EventArgs e) 
            {
                // Only revert if not hovering over a child
                if (!card.ClientRectangle.Contains(card.PointToClient(Cursor.Position)))
                    card.BackColor = _cGridHeader;
            }
            
            card.MouseEnter +=OnMouseEnter;
            card.MouseLeave += OnMouseLeave;
            card.Click += (s, e) => SwitchToPackages(project.Path);

            // Icon - Use distinct purple color for Global Packages
            bool isGlobal = project.Path == "__GLOBAL__";
            var globalColor = Color.FromArgb(156, 39, 176); // Purple for Global
            
            var lblIcon = new Label
            {
                Text = isGlobal ? "\uE774" : "\uE8B7", // Globe icon for Global, Folder for others
                Font = new Font("Segoe MDL2 Assets", 24),
                ForeColor = isGlobal ? globalColor : _cAccent,
                AutoSize = true,
                Location = new Point(15, 15),
                BackColor = Color.Transparent
            };
            lblIcon.Click += (s, e) => SwitchToPackages(project.Path);
            lblIcon.MouseEnter += OnMouseEnter; 

            // Title
            var lblName = new Label
            {
                Text = project.DisplayName,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = _cText,
                AutoEllipsis = true,
                Width = 200,
                Location = new Point(15, 50),
                BackColor = Color.Transparent
            };
            lblName.Click += (s, e) => SwitchToPackages(project.Path);
            lblName.MouseEnter += OnMouseEnter;

            // Path
            var lblPath = new Label
            {
                Text = project.Path == "__GLOBAL__" ? "System-wide" : project.Path,
                Font = new Font("Segoe UI", 8),
                ForeColor = _cTextDim,
                AutoEllipsis = true,
                Width = 250,
                Location = new Point(15, 75),
                BackColor = Color.Transparent
            };
            lblPath.Click += (s, e) => SwitchToPackages(project.Path);
            lblPath.MouseEnter += OnMouseEnter;

            // Stats Badge Area
            var pnlStats = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = Color.Transparent 
            };
            pnlStats.Click += (s, e) => SwitchToPackages(project.Path);
            pnlStats.MouseEnter += OnMouseEnter;

            // Package Count Label
            var lblCount = new Label
            {
                Text = $"{totalCount} packages",
                ForeColor = _cTextDim,
                AutoSize = true,
                Location = new Point(15, 10),
                Font = new Font("Segoe UI", 9)
            };
            lblCount.Click += (s, e) => SwitchToPackages(project.Path);
            lblCount.MouseEnter += OnMouseEnter;

            // Updates Label
            var lblUpdates = new Label
            {
                Text = outdatedCount > 0 ? $"{outdatedCount} updates" : "Up to date",
                ForeColor = outdatedCount > 0 ? _cWarning : _cSuccess,
                AutoSize = true,
                Location = new Point(150, 10), // Push to right
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            lblUpdates.Click += (s, e) => SwitchToPackages(project.Path);
            lblUpdates.MouseEnter += OnMouseEnter;

            pnlStats.Controls.Add(lblCount);
            pnlStats.Controls.Add(lblUpdates);

            card.Controls.Add(lblIcon);
            card.Controls.Add(lblName);
            card.Controls.Add(lblPath);
            card.Controls.Add(pnlStats);

            return card;
        }

        private void ApplyFilters()
        {
            if (_currentView == ViewMode.Projects)
            {
                RenderProjects();
                return;
            }

            var searchTerm = _txtSearch.Text.ToLower();
            var showOutdatedOnly = _chkOutdatedOnly.Checked;
            
            _filteredPackages = _allPackages.Where(p =>
            {
                // Strict Project Filter in Drill-down mode
                if (_currentProjectFilter != null && p.ProjectPath != _currentProjectFilter)
                    return false;

                // Search filter
                if (!string.IsNullOrEmpty(searchTerm) && 
                    !p.Name.ToLower().Contains(searchTerm))
                    return false;
                
                // Outdated filter
                if (showOutdatedOnly && !p.IsOutdated)
                    return false;
                
                return true;
            }).ToList();
            
            RefreshListView();
        }

        private void EcosystemChanged(object? sender, EventArgs e)
        {
            string selected = _cmbEcosystem.SelectedItem?.ToString() ?? "All";
            
            // All ecosystems now support scanning
            _contentPanel.Visible = true;
            _statsPanel.Visible = true;
            _btnScanDirectory.Enabled = true;
            _btnExport.Enabled = true;
            _btnRefresh.Enabled = true;
            
            // NPM-specific buttons: visible for "All" or "NPM"
            bool showNpmButtons = selected == "All" || selected == "NPM";
            _btnScanGlobal.Visible = showNpmButtons; // Only NPM has global packages
            _btnCheckUpdates.Visible = showNpmButtons; // Update checking for NPM packages
            _btnUpdateAll.Visible = showNpmButtons;
            
            _currentView = ViewMode.Projects;
            SwitchToProjects();
            LoadData();
        }
        
        /// <summary>
        /// Gets the currently selected ecosystem from the UI.
        /// Returns null when "All" is selected.
        /// </summary>
        private Ecosystem? GetSelectedEcosystem()
        {
            string selected = _cmbEcosystem.SelectedItem?.ToString() ?? "All";
            return selected switch
            {
                "NPM" => Ecosystem.NPM,
                "Python" => Ecosystem.Python,
                "C++" => Ecosystem.Cpp,
                _ => null // "All" or unknown = no filter
            };
        }

        private void RefreshListView()
        {
            _packageListView.BeginUpdate();
            _packageListView.Items.Clear();
            
            foreach (var package in _filteredPackages)
            {
                var item = new ListViewItem(package.Name);
                item.SubItems.Add(package.Version);
                item.SubItems.Add(package.LatestVersion ?? "-");
                item.SubItems.Add(package.DisplayProjectPath);
                item.SubItems.Add(package.StatusText);
                item.SubItems.Add(package.IsDev ? "Dev" : "Prod");
                item.Tag = package;
                
                // Colorize outdated
                if (package.IsOutdated) item.ForeColor = _cWarning;
                
                _packageListView.Items.Add(item);
            }
            
            _packageListView.EndUpdate();
        }

        private void UpdateStatsPanel()
        {
            _statsPanel.Controls.Clear();
            
            var totalPackages = _allPackages.Count;
            var outdatedPackages = _allPackages.Count(p => p.IsOutdated);
            var projectCount = _allProjects.Count;
            
            AddStatCard(_statsPanel, "Total Packages", totalPackages.ToString(), _cAccent, 0);
            AddStatCard(_statsPanel, "Outdated", outdatedPackages.ToString(), outdatedPackages > 0 ? _cWarning : _cSuccess, 160);
            AddStatCard(_statsPanel, "Projects", projectCount.ToString(), _cAccent, 320);
        }

        private void AddStatCard(Panel parent, string label, string value, Color accentColor, int left)
        {
            var card = new Panel
            {
                Width = 150,
                Height = 50,
                Left = left,
                Top = 0,
                BackColor = _cGridHeader
            };
            
            var accent = new Panel
            {
                Width = 4,
                Height = 50,
                Left = 0,
                Top = 0,
                BackColor = accentColor
            };
            
            var lblValue = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = _cText,
                AutoSize = true,
                Location = new Point(15, 5)
            };
            
            var lblLabel = new Label
            {
                Text = label,
                Font = new Font("Segoe UI", 9),
                ForeColor = _cTextDim,
                AutoSize = true,
                Location = new Point(15, 30)
            };
            
            card.Controls.Add(accent);
            card.Controls.Add(lblValue);
            card.Controls.Add(lblLabel);
            parent.Controls.Add(card);
        }

        #endregion

        #region Event Handlers

        private void FilterChanged(object? sender, EventArgs e)
        {
            ApplyFilters();
        }

        private async void BtnScanDirectory_Click(object? sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select a directory to scan for npm projects",
                UseDescriptionForTitle = true
            };
            
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                await ScanDirectoryAsync(dialog.SelectedPath);
            }
        }

        private async void BtnAddFolder_Click(object? sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select a specific project folder to add",
                UseDescriptionForTitle = true
            };
            
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                await AddSingleProjectAsync(dialog.SelectedPath);
            }
        }

        private async void BtnScanGlobal_Click(object? sender, EventArgs e)
        {
            await ScanGlobalPackagesAsync();
        }

        private async void BtnCheckUpdates_Click(object? sender, EventArgs e)
        {
            await CheckForUpdatesAsync();
        }

        private void BtnUpdateAll_Click(object? sender, EventArgs e)
        {
            // If in a specific local project, run Project Update
            if (_currentView == ViewMode.Packages && 
                _currentProjectFilter != null && 
                _currentProjectFilter != "__GLOBAL__")
            {
                UpdateProjectAsync(_currentProjectFilter);
            }
            else
            {
                // Fallback to standard "Update All Outdated" (legacy/global behavior)
                UpdateAllOutdatedAsync();
            }
        }

        private void BtnExport_Click(object? sender, EventArgs e)
        {
            ExportData();
        }

        private void BtnRefresh_Click(object? sender, EventArgs e)
        {
            LoadData();
        }

        private void BtnClearAll_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear ALL data?\n\nThis will remove all tracked projects and packages from the database. This action cannot be undone.",
                "Confirm Clear All Data",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            
            if (result == DialogResult.Yes)
            {
                _database?.ClearAllData();
                LoadData();
                UpdateStatus("All data cleared. Use 'Scan Directory' to add projects.");
            }
        }

        private void ListView_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && _packageListView.SelectedItems.Count > 0)
            {
                // Hide "Update Package" for local projects as we enforce Project-level updates
                // Global packages still allow individual updates
                bool isGlobal = _currentProjectFilter == "__GLOBAL__";
                
                foreach (ToolStripItem item in _contextMenu.Items)
                {
                    if (item.Text == "Update Package")
                    {
                        item.Visible = isGlobal;
                    }
                }
                
                _contextMenu.Show(_packageListView, e.Location);
            }
            else if (e.Button == MouseButtons.Left && _packageListView.SelectedItems.Count == 1)
            {
                // Show package details panel (like NPM Handler's PackageDrawer)
                var selectedPackages = GetSelectedPackages();
                if (selectedPackages.Count > 0)
                {
                    _packageDetailsPanel?.ShowPackage(selectedPackages[0]);
                }
            }
        }

        private void ListView_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            var selectedPackages = GetSelectedPackages();
            if (selectedPackages.Count > 0)
            {
                var package = selectedPackages[0];
                // Show details panel on double-click as well
                _packageDetailsPanel?.ShowPackage(package);
            }
        }

        private void ListView_ColumnClick(object? sender, ColumnClickEventArgs e)
        {
            // Toggle sort direction if same column
            if (_sortColumn == e.Column)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _sortColumn = e.Column;
                _sortAscending = true;
            }
            
            // Sort by column
            IOrderedEnumerable<NpmPackage> sorted = _sortColumn switch
            {
                0 => _sortAscending ? _filteredPackages.OrderBy(p => p.Name) : _filteredPackages.OrderByDescending(p => p.Name),
                1 => _sortAscending ? _filteredPackages.OrderBy(p => p.Version) : _filteredPackages.OrderByDescending(p => p.Version),
                2 => _sortAscending ? _filteredPackages.OrderBy(p => p.LatestVersion) : _filteredPackages.OrderByDescending(p => p.LatestVersion),
                3 => _sortAscending ? _filteredPackages.OrderBy(p => p.ProjectPath) : _filteredPackages.OrderByDescending(p => p.ProjectPath),
                4 => _sortAscending ? _filteredPackages.OrderBy(p => p.StatusText) : _filteredPackages.OrderByDescending(p => p.StatusText),
                5 => _sortAscending ? _filteredPackages.OrderBy(p => p.IsDev) : _filteredPackages.OrderByDescending(p => p.IsDev),
                _ => _filteredPackages.OrderBy(p => p.Name)
            };
            
            _filteredPackages = sorted.ToList();
            RefreshListView();
        }

        #endregion

        #region Context Menu Handlers

        private async void ContextMenu_UpdatePackage(object? sender, EventArgs e)
        {
            var selectedPackages = GetSelectedPackages();
            if (selectedPackages.Count == 0) return;
            
            var outdated = selectedPackages.Where(p => p.IsOutdated).ToList();
            if (outdated.Count == 0)
            {
                MessageBox.Show("Selected packages are already up to date.", "Info", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            SetLoading(true);
            UpdateStatus($"Updating {outdated.Count} package(s)...");
            
            try
            {
                foreach (var package in outdated)
                {
                    var result = await _updater!.UpdatePackageAsync(package);
                    if (result.Success)
                    {
                        // Sync database with new version
                        if (!string.IsNullOrEmpty(result.NewVersion))
                        {
                            _database?.MarkPackageAsUpdated(package.ProjectPath, package.Name, result.NewVersion);
                        }
                        UpdateStatus($"Updated {package.Name} to {result.NewVersion}");
                    }
                    else
                    {
                        UpdateStatus($"Failed to update {package.Name}: {result.ErrorMessage}");
                    }
                }
                
                LoadData();
            }
            finally
            {
                SetLoading(false);
            }
        }

        private async void ContextMenu_UninstallPackage(object? sender, EventArgs e)
        {
            var selectedPackages = GetSelectedPackages();
            if (selectedPackages.Count == 0) return;
            
            var result = MessageBox.Show(
                $"Are you sure you want to uninstall {selectedPackages.Count} package(s)?\n\nThis will run 'npm uninstall' for each selected package.",
                "Confirm Uninstall",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            
            if (result != DialogResult.Yes) return;
            
            SetLoading(true);
            UpdateStatus($"Uninstalling {selectedPackages.Count} package(s)...");
            
            try
            {
                foreach (var package in selectedPackages)
                {
                    var success = await _updater!.UninstallPackageAsync(package);
                    if (success)
                    {
                        _database?.DeletePackage(package.ProjectPath, package.Name);
                        UpdateStatus($"Uninstalled {package.Name}");
                    }
                    else
                    {
                        UpdateStatus($"Failed to uninstall {package.Name}");
                    }
                }
                
                LoadData();
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void ContextMenu_RemoveFromCatalog(object? sender, EventArgs e)
        {
            var selectedPackages = GetSelectedPackages();
            if (selectedPackages.Count == 0) return;
            
            var result = MessageBox.Show(
                $"Remove {selectedPackages.Count} package(s) from the catalog?\n\nThis only removes them from tracking, not from your projects.",
                "Confirm Remove",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            
            if (result != DialogResult.Yes) return;
            
            foreach (var package in selectedPackages)
            {
                _database?.DeletePackage(package.ProjectPath, package.Name);
            }
            
            LoadData();
            UpdateStatus($"Removed {selectedPackages.Count} package(s) from catalog");
        }

        private void ContextMenu_DeleteProject(object? sender, EventArgs e)
        {
            var selectedPackages = GetSelectedPackages();
            if (selectedPackages.Count == 0) return;
            
            // Get unique projects from selected packages
            var projectPaths = selectedPackages.Select(p => p.ProjectPath).Distinct().ToList();
            
            var result = MessageBox.Show(
                $"Delete {projectPaths.Count} project(s) and all their packages from the catalog?\n\nThis only removes them from tracking, not from disk.",
                "Confirm Delete Project",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            
            if (result != DialogResult.Yes) return;
            
            foreach (var projectPath in projectPaths)
            {
                _database?.DeleteProject(projectPath);
            }
            
            LoadData();
            UpdateStatus($"Deleted {projectPaths.Count} project(s) from catalog");
        }

        private void ContextMenu_OpenFolder(object? sender, EventArgs e)
        {
            var selectedPackages = GetSelectedPackages();
            if (selectedPackages.Count == 0) return;
            
            var package = selectedPackages[0];
            var path = package.ProjectPath == "__GLOBAL__" 
                ? GetGlobalNodeModulesPath() 
                : package.ProjectPath;
            
            if (Directory.Exists(path))
            {
                System.Diagnostics.Process.Start("explorer.exe", path);
            }
            else
            {
                MessageBox.Show("Folder not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private List<NpmPackage> GetSelectedPackages()
        {
            var packages = new List<NpmPackage>();
            foreach (ListViewItem item in _packageListView.SelectedItems)
            {
                if (item.Tag is NpmPackage package)
                {
                    packages.Add(package);
                }
            }
            return packages;
        }

        #endregion

        #region Operations

        private async Task ScanDirectoryAsync(string path)
        {
            var ecosystem = GetSelectedEcosystem();
            SetLoading(true);
            
            var ecosystemLabel = ecosystem.HasValue ? ecosystem.Value.ToString() : "all";
            UpdateStatus($"Scanning {path} for {ecosystemLabel} projects...");
            
            try
            {
                var progress = new Progress<string>(msg => UpdateStatus(msg));
                int totalPackages = 0;
                int totalProjects = 0;
                TimeSpan totalDuration = TimeSpan.Zero;
                
                // If "All" is selected (ecosystem is null), scan all ecosystems
                var ecosystemsToScan = ecosystem.HasValue 
                    ? new[] { ecosystem.Value }
                    : new[] { Ecosystem.NPM, Ecosystem.Python, Ecosystem.Cpp };
                
                foreach (var eco in ecosystemsToScan)
                {
                    UpdateStatus($"Scanning for {eco} projects...");
                    var result = await _scanner!.ScanDirectoryAsync(path, eco, progress);
                    
                    // Save to database
                    foreach (var project in result.Projects)
                    {
                        _database?.UpsertProject(project);
                    }
                    
                    foreach (var package in result.Packages)
                    {
                        _database?.UpsertPackage(package);
                    }
                    
                    totalPackages += result.PackagesFound;
                    totalProjects += result.Projects.Count;
                    totalDuration += result.ScanDuration;
                }
                
                LoadData();
                UpdateStatus($"Scan complete: Found {totalPackages} packages in {totalProjects} projects ({totalDuration.TotalSeconds:F1}s)");
                AddLog(LogLevel.Info, $"Scan complete: {path}", 
                    $"Found {totalPackages} packages in {totalProjects} projects ({ecosystemLabel})");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Scan failed: {ex.Message}");
                AddLog(LogLevel.Error, $"Scan failed: {path}", ex.Message + "\n" + ex.StackTrace);
            }
            finally
            {
                SetLoading(false);
            }
        }

        private async Task ScanGlobalPackagesAsync()
        {
            SetLoading(true);
            UpdateStatus("Scanning global npm packages...");
            
            try
            {
                var progress = new Progress<string>(msg => UpdateStatus(msg));
                var packages = await _scanner!.ScanGlobalPackagesAsync(progress);
                
                // Create/update global project
                var globalProject = new NpmProject
                {
                    Name = "Global Packages",
                    Path = "__GLOBAL__",
                    LastScanned = DateTime.Now,
                    PackageCount = packages.Count,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _database?.UpsertProject(globalProject);
                
                foreach (var package in packages)
                {
                    _database?.UpsertPackage(package);
                }
                
                LoadData();
                UpdateStatus($"Scan complete: Found {packages.Count} global packages");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Global scan failed: {ex.Message}");
            }
            finally
            {
                SetLoading(false);
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            SetLoading(true);
            UpdateStatus("Checking for updates...");
            
            try
            {
                var progress = new Progress<(int current, int total, string packageName)>(val => 
                    UpdateStatus($"Checking updates: {val.packageName} ({val.current}/{val.total})"));
                var updates = await _updater!.CheckForUpdatesAsync(_allPackages, progress);
                
                // Persist results to database (matching NPM Handler's behavior)
                int outdatedCount = 0;
                foreach (var (package, latestVersion, isOutdated, notFound) in updates)
                {
                    _database?.UpdatePackageVersionInfo(
                        package.ProjectPath, 
                        package.Name, 
                        latestVersion, 
                        isOutdated, 
                        notFound);
                    
                    if (isOutdated) outdatedCount++;
                }
                
                LoadData();
                
                if (outdatedCount > 0)
                {
                    UpdateStatus($"Update check complete: {outdatedCount} outdated packages found");
                }
                else
                {
                    UpdateStatus("Update check complete: All packages are up to date");
                }
                
                AddLog(LogLevel.Info, "Update check complete", 
                    $"Checked {updates.Count} packages, {outdatedCount} outdated");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Update check failed: {ex.Message}");
                AddLog(LogLevel.Error, "Update check failed", ex.Message);
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void UpdateAllOutdatedAsync()
        {
            // Use filtered packages to respect current filter state (e.g., "Outdated Only" checkbox)
            var outdated = _filteredPackages.Where(p => p.IsOutdated).ToList();
            if (outdated.Count == 0)
            {
                MessageBox.Show("No outdated packages found.\n\nRun 'Check Updates' first to discover outdated packages.", 
                    "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            // Show update dialog with version selection (matching NPM Handler's UpdateDialog)
            using var dialog = new UpdateDialog(outdated, _updater!, _database!);
            dialog.ShowDialog();
            
            // Log results
            if (dialog.Results.Count > 0)
            {
                var successCount = dialog.Results.Count(r => r.Success);
                var failCount = dialog.Results.Count - successCount;
                
                foreach (var result in dialog.Results)
                {
                    if (result.Success)
                    {
                        AddLog(LogLevel.Info, $"Updated {result.PackageName}", 
                            $"From {result.OldVersion} to {result.NewVersion}");
                    }
                    else if (!string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        AddLog(LogLevel.Error, $"Failed to update {result.PackageName}", result.ErrorMessage);
                    }
                }
                
                UpdateStatus($"Update complete: {successCount} succeeded, {failCount} failed");
            }
            
            LoadData();
        }

        #endregion

        #region Project Update Logic
        
        private async void UpdateProjectAsync(string projectPath)
        {
            var confirm = MessageBox.Show(
                $"This will run 'npm update' in:\n{projectPath}\n\nThis updates all packages to the latest versions allowed by package.json and updates the lockfile.\n\nContinue?",
                "Confirm Project Update",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            SetLoading(true);
            UpdateStatus($"Updating project: {projectPath}...");
            
            try
            {
                var result = await _updater!.UpdateProjectAsync(projectPath);
                
                if (result.Success)
                {
                    UpdateStatus("Project updated successfully.");
                    AddLog(LogLevel.Info, "Project Update Success", result.Details);
                    MessageBox.Show("Project updated successfully!\nCheck logs for details.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    // Re-scan directory to reflect changes
                    await ScanDirectoryAsync(projectPath); 
                }
                else
                {
                    UpdateStatus("Project update failed.");
                    AddLog(LogLevel.Error, "Project Update Failed", result.ErrorMessage);
                    MessageBox.Show($"Project update failed:\n{result.ErrorMessage}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
                AddLog(LogLevel.Error, "Project Update Error", ex.Message);
            }
            finally
            {
                SetLoading(false);
            }
        }
        
        #endregion

        private void ExportData()
        {
            using var dialog = new SaveFileDialog
            {
                Title = "Export NPM Package Data",
                Filter = "JSON Files (*.json)|*.json|CSV Files (*.csv)|*.csv",
                DefaultExt = "json",
                FileName = $"npm-packages-export-{DateTime.Now:yyyy-MM-dd}"
            };
            
            if (dialog.ShowDialog() != DialogResult.OK)
                return;
            
            try
            {
                var extension = System.IO.Path.GetExtension(dialog.FileName).ToLower();
                string content;
                
                if (extension == ".csv")
                {
                    content = ExportToCsv();
                }
                else
                {
                    content = ExportToJson();
                }
                
                File.WriteAllText(dialog.FileName, content);
                UpdateStatus($"Exported data to {dialog.FileName}");
                MessageBox.Show($"Successfully exported {_allPackages.Count} packages to:\n{dialog.FileName}", 
                    "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private string ExportToJson()
        {
            var exportData = new
            {
                packages = _allPackages.Select(p => new
                {
                    name = p.Name,
                    version = p.Version,
                    latestVersion = p.LatestVersion,
                    projectPath = p.ProjectPath,
                    path = p.Path,
                    isOutdated = p.IsOutdated,
                    notFound = p.NotFound,
                    lastChecked = p.LastChecked?.ToString("o"),
                    description = p.Description,
                    author = p.Author,
                    license = p.License,
                    homepage = p.Homepage,
                    repository = p.Repository,
                    isDev = p.IsDev,
                    createdAt = p.CreatedAt.ToString("o"),
                    updatedAt = p.UpdatedAt.ToString("o")
                }),
                projects = _allProjects.Select(p => new
                {
                    name = p.Name,
                    path = p.Path,
                    lastScanned = p.LastScanned?.ToString("o"),
                    packageCount = p.PackageCount,
                    createdAt = p.CreatedAt.ToString("o"),
                    updatedAt = p.UpdatedAt.ToString("o")
                }),
                exportDate = DateTime.Now.ToString("o")
            };
            
            return JsonConvert.SerializeObject(exportData, Formatting.Indented);
        }
        
        private string ExportToCsv()
        {
            var lines = new List<string>();
            
            // Header
            lines.Add("Name,Version,Latest Version,Project Path,Path,Is Outdated,Not Found,Last Checked,Description,Author,License,Is Dev");
            
            // Data rows
            foreach (var pkg in _allPackages)
            {
                var row = string.Join(",", 
                    EscapeCsvField(pkg.Name),
                    EscapeCsvField(pkg.Version),
                    EscapeCsvField(pkg.LatestVersion ?? ""),
                    EscapeCsvField(pkg.ProjectPath),
                    EscapeCsvField(pkg.Path),
                    pkg.IsOutdated ? "Yes" : "No",
                    pkg.NotFound ? "Yes" : "No",
                    pkg.LastChecked?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never",
                    EscapeCsvField(pkg.Description ?? ""),
                    EscapeCsvField(pkg.Author ?? ""),
                    EscapeCsvField(pkg.License ?? ""),
                    pkg.IsDev ? "Dev" : "Prod"
                );
                lines.Add(row);
            }
            
            return string.Join(Environment.NewLine, lines);
        }
        
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return "\"\"";
            
            // Escape quotes and wrap in quotes if contains comma, quote, or newline
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }

        private void UpdateStatus(string status)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateStatus(status)));
                return;
            }
            _lblStatus.Text = status;
        }

        private void SetLoading(bool loading)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetLoading(loading)));
                return;
            }
            SetLoadingInternal(loading);
        }

        private void SetLoadingInternal(bool loading)
        {
            _progressBar.Visible = loading;
            _btnScanDirectory.Enabled = !loading;
            // ... disable other buttons
        }

        private string GetGlobalNodeModulesPath()
        {
            // Simplified for brevity
            return Environment.ExpandEnvironmentVariables("%APPDATA%\\npm\\node_modules");
        }

        private void ListView_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.Graphics.FillRectangle(new SolidBrush(_cGridHeader), e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, _packageListView.Font, e.Bounds, _cTextDim, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        private void ListView_DrawItem(object? sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void ListView_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private async Task AddSingleProjectAsync(string path)
        {
            var ecosystem = GetSelectedEcosystem();
            SetLoading(true);
            UpdateStatus($"Adding project folder: {path}...");

            try
            {
                var progress = new Progress<string>(msg => UpdateStatus(msg));
                
                // Force scan this specific folder
                var result = await _scanner!.ScanSingleProjectAsync(path, ecosystem, progress);
                
                // Save to database
                if (result.Projects.Count > 0)
                {
                    foreach (var project in result.Projects)
                    {
                        _database?.UpsertProject(project);
                    }
                    
                    foreach (var package in result.Packages)
                    {
                        _database?.UpsertPackage(package);
                    }
                    
                    AddLog(LogLevel.Info, "Added project folder", 
                        $"Added {result.Projects.Count} project(s) and {result.Packages.Count} package(s) from {path}");
                    
                    UpdateStatus($"Added project: {Path.GetFileName(path)}");
                }
                else
                {
                     UpdateStatus("No valid project found in selected folder.");
                     MessageBox.Show("Could not detect a valid project in the selected folder.\n\nMake sure it contains package.json (NPM), requirements.txt (Python), or CMakeLists.txt (C++).", 
                         "Project Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                
                LoadData();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to add project: {ex.Message}");
                AddLog(LogLevel.Error, $"Add folder failed: {path}", ex.Message);
            }
            finally
            {
                SetLoading(false);
            }
        }
    }
}
