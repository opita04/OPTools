using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using OPTools.Core;
using OPTools.Utils;

namespace OPTools.Forms
{
    public class PackageListControl : Panel
    {
        // Theme Colors
        private readonly Color _cBackground = Color.FromArgb(30, 30, 30);
        private readonly Color _cAccent = Color.FromArgb(0, 122, 204);
        private readonly Color _cSuccess = Color.FromArgb(92, 184, 92);
        private readonly Color _cWarning = Color.FromArgb(240, 173, 78);
        private readonly Color _cText = Color.FromArgb(241, 241, 241);
        private readonly Color _cTextDim = Color.FromArgb(150, 150, 150);
        private readonly Color _cGridHeader = Color.FromArgb(45, 45, 48);

        // Core Components
        private readonly PackageDatabase _database;
        private readonly PackageUpdater _updater;
        private readonly PackageScanner _scanner;

        // UI Components
        private ListView _packageListView = null!;
        private TextBox _txtSearch = null!;
        private CheckBox _chkOutdatedOnly = null!;
        private Label _lblStatus = null!;
        private ProgressBar _progressBar = null!;
        private PackageDetailsPanel _packageDetailsPanel = null!;
        private ContextMenuStrip _contextMenu = null!;

        // Data
        private List<PackageInfo> _allPackages = new();
        private List<PackageInfo> _filteredPackages = new();
        private string? _projectPathFilter;
        private Ecosystem? _ecosystemFilter;
        private bool _onlyGlobal;
        private bool _onlyLocal;

        // Sorting state
        private int _sortColumn = -1;
        private bool _sortAscending = true;

        public event Action<string>? StatusUpdated;

        public PackageListControl()
        {
            _database = new PackageDatabase();
            _updater = new PackageUpdater();
            _scanner = new PackageScanner();
            
            InitializeComponents();
            InitializeContextMenu();
        }

        public string? ProjectPathFilter
        {
            get => _projectPathFilter;
            set
            {
                _projectPathFilter = value;
                _onlyGlobal = false;
                _onlyLocal = false;
                _ = LoadDataAsync();
            }
        }

        public bool OnlyGlobal
        {
            get => _onlyGlobal;
            set
            {
                _onlyGlobal = value;
                if (_onlyGlobal) _onlyLocal = false;
                _ = LoadDataAsync();
            }
        }

        public bool OnlyLocal
        {
            get => _onlyLocal;
            set
            {
                _onlyLocal = value;
                if (_onlyLocal) _onlyGlobal = false;
                _ = LoadDataAsync();
            }
        }

        public Ecosystem? EcosystemFilter
        {
            get => _ecosystemFilter;
            set
            {
                _ecosystemFilter = value;
                _ = LoadDataAsync();
            }
        }

        private void InitializeComponents()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = _cBackground;

            // Filter Panel (Search & Checkbox)
            var filterPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 45,
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
                Location = new Point(0, 5),
                PlaceholderText = "Search packages..."
            };
            _txtSearch.TextChanged += (s, e) => ApplyFilters();

            _chkOutdatedOnly = new CheckBox
            {
                Text = "Show outdated only",
                ForeColor = _cText,
                AutoSize = true,
                Font = new Font("Segoe UI", 10),
                Location = new Point(270, 8)
            };
            _chkOutdatedOnly.CheckedChanged += (s, e) => ApplyFilters();

            filterPanel.Controls.Add(_txtSearch);
            filterPanel.Controls.Add(_chkOutdatedOnly);

            // Status Panel
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

            // ListView
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
                OwnerDraw = true
            };

            _packageListView.Columns.Add("Package Name", 200);
            _packageListView.Columns.Add("Version", 100);
            _packageListView.Columns.Add("Latest", 100);
            _packageListView.Columns.Add("Location", 200);
            _packageListView.Columns.Add("Status", 100);
            _packageListView.Columns.Add("Type", 80);

            _packageListView.DrawColumnHeader += ListView_DrawColumnHeader;
            _packageListView.DrawItem += ListView_DrawItem;
            _packageListView.DrawSubItem += ListView_DrawSubItem;
            _packageListView.ColumnClick += ListView_ColumnClick;
            _packageListView.MouseClick += ListView_MouseClick;
            _packageListView.MouseDoubleClick += ListView_MouseDoubleClick;

            // Details Panel
            _packageDetailsPanel = new PackageDetailsPanel();
            _packageDetailsPanel.CloseRequested += (s, e) => _packageDetailsPanel.HidePanel();
            _packageDetailsPanel.UpdateRequested += async (s, pkg) => await UpdateSinglePackageAsync(pkg);
            _packageDetailsPanel.UninstallRequested += async (s, pkg) => await UninstallPackageAsync(pkg);

            this.Controls.Add(_packageListView);
            this.Controls.Add(_packageDetailsPanel);
            this.Controls.Add(filterPanel);
            this.Controls.Add(statusPanel);
        }

        private void InitializeContextMenu()
        {
            _contextMenu = new ContextMenuStrip
            {
                BackColor = Color.FromArgb(25, 25, 26),
                ForeColor = _cText,
                RenderMode = ToolStripRenderMode.System
            };

            var updateItem = new ToolStripMenuItem("Update Package");
            updateItem.Click += async (s, e) => await UpdateSelectedPackagesAsync();

            var uninstallItem = new ToolStripMenuItem("Uninstall Package");
            uninstallItem.Click += async (s, e) => await UninstallSelectedPackagesAsync();

            var openFolderItem = new ToolStripMenuItem("Open Folder");
            openFolderItem.Click += (s, e) => OpenSelectedPackageFolder();

            _contextMenu.Items.Add(updateItem);
            _contextMenu.Items.Add(uninstallItem);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(openFolderItem);
        }

        public async Task LoadDataAsync()
        {
            SetLoading(true);
            UpdateStatus("Loading packages...");

            try
            {
                _allPackages = await Task.Run(() => 
                {
                    List<PackageInfo> results;
                    if (!string.IsNullOrEmpty(_projectPathFilter))
                    {
                        results = _database.GetPackagesByProject(_projectPathFilter);
                    }
                    else if (_onlyGlobal)
                    {
                        results = _database.GetAllPackages().Where(p => p.ProjectPath.StartsWith("__GLOBAL")).ToList();
                    }
                    else if (_onlyLocal)
                    {
                        results = _database.GetAllPackages().Where(p => !p.ProjectPath.StartsWith("__GLOBAL")).ToList();
                    }
                    else
                    {
                        results = new List<PackageInfo>();
                    }

                    if (_ecosystemFilter.HasValue)
                    {
                        results = results.Where(p => p.Ecosystem == _ecosystemFilter.Value).ToList();
                    }
                    return results;
                });

                ApplyFilters();
                UpdateStatus($"Loaded {_allPackages.Count} packages");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void ApplyFilters()
        {
            var searchTerm = _txtSearch.Text.ToLower();
            var showOutdatedOnly = _chkOutdatedOnly.Checked;

            _filteredPackages = _allPackages.Where(p =>
            {
                if (!string.IsNullOrEmpty(searchTerm) && !p.Name.ToLower().Contains(searchTerm))
                    return false;
                if (showOutdatedOnly && !p.IsOutdated)
                    return false;
                return true;
            }).ToList();

            RefreshListView();
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

                if (package.IsOutdated) item.ForeColor = _cWarning;
                _packageListView.Items.Add(item);
            }

            _packageListView.EndUpdate();
        }

        private void SetLoading(bool loading)
        {
            if (InvokeRequired) { Invoke(new Action(() => SetLoading(loading))); return; }
            _progressBar.Visible = loading;
            _txtSearch.Enabled = !loading;
            _chkOutdatedOnly.Enabled = !loading;
        }

        private void UpdateStatus(string status)
        {
            if (InvokeRequired) { Invoke(new Action(() => UpdateStatus(status))); return; }
            _lblStatus.Text = status;
            StatusUpdated?.Invoke(status);
        }

        private async Task UpdateSinglePackageAsync(PackageInfo package)
        {
            SetLoading(true);
            UpdateStatus($"Updating {package.Name}...");
            try
            {
                var result = await _updater.UpdatePackageAsync(package);
                if (result.Success)
                {
                    _database.MarkPackageAsUpdated(package.ProjectPath, package.Name, result.NewVersion);
                    UpdateStatus($"Updated {package.Name} to {result.NewVersion}");
                    await LoadDataAsync();
                }
                else
                {
                    MessageBox.Show($"Failed to update {package.Name}: {result.ErrorMessage}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                SetLoading(false);
            }
        }

        private async Task UpdateSelectedPackagesAsync()
        {
            var selected = GetSelectedPackages();
            var outdated = selected.Where(p => p.IsOutdated).ToList();
            if (outdated.Count == 0) return;

            SetLoading(true);
            foreach (var pkg in outdated)
            {
                await UpdateSinglePackageAsync(pkg);
            }
            SetLoading(false);
        }

        private async Task UninstallPackageAsync(PackageInfo package)
        {
            if (MessageBox.Show($"Uninstall {package.Name}?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

            SetLoading(true);
            UpdateStatus($"Uninstalling {package.Name}...");
            try
            {
                var success = await _updater.UninstallPackageAsync(package);
                if (success)
                {
                    _database.DeletePackage(package.ProjectPath, package.Name);
                    UpdateStatus($"Uninstalled {package.Name}");
                    _packageDetailsPanel.HidePanel();
                    await LoadDataAsync();
                }
            }
            finally
            {
                SetLoading(false);
            }
        }

        private async Task UninstallSelectedPackagesAsync()
        {
            var selected = GetSelectedPackages();
            if (selected.Count == 0) return;

            foreach (var pkg in selected)
            {
                await UninstallPackageAsync(pkg);
            }
        }

        private void OpenSelectedPackageFolder()
        {
            var selected = GetSelectedPackages();
            if (selected.Count == 0) return;
            var path = selected[0].Path;
            if (System.IO.Directory.Exists(path))
                System.Diagnostics.Process.Start("explorer.exe", path);
        }

        private List<PackageInfo> GetSelectedPackages()
        {
            return _packageListView.SelectedItems.Cast<ListViewItem>().Select(i => (PackageInfo)i.Tag).ToList();
        }

        public async Task CheckUpdatesAsync()
        {
            SetLoading(true);
            UpdateStatus("Checking for updates...");
            try
            {
                var progress = new Progress<(int current, int total, string packageName)>(val =>
                    UpdateStatus($"Checking: {val.packageName} ({val.current}/{val.total})"));
                
                var results = await _updater.CheckForUpdatesAsync(_allPackages, progress);
                foreach (var (pkg, latest, outdated, notFound) in results)
                {
                    _database.UpdatePackageVersionInfo(pkg.ProjectPath, pkg.Name, latest, outdated, notFound);
                }
                await LoadDataAsync();
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void ListView_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.Graphics.FillRectangle(new SolidBrush(_cGridHeader), e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? string.Empty, _packageListView.Font, e.Bounds, _cTextDim, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }

        private void ListView_DrawItem(object? sender, DrawListViewItemEventArgs e) => e.DrawDefault = true;
        private void ListView_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e) => e.DrawDefault = true;

        private void ListView_ColumnClick(object? sender, ColumnClickEventArgs e)
        {
            if (_sortColumn == e.Column) _sortAscending = !_sortAscending;
            else { _sortColumn = e.Column; _sortAscending = true; }

            _filteredPackages = (_sortColumn switch
            {
                0 => _sortAscending ? _filteredPackages.OrderBy(p => p.Name) : _filteredPackages.OrderByDescending(p => p.Name),
                1 => _sortAscending ? _filteredPackages.OrderBy(p => p.Version) : _filteredPackages.OrderByDescending(p => p.Version),
                _ => _filteredPackages.OrderBy(p => p.Name)
            }).ToList();
            RefreshListView();
        }

        private void ListView_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && _packageListView.SelectedItems.Count > 0)
                _contextMenu.Show(_packageListView, e.Location);
            else if (e.Button == MouseButtons.Left && _packageListView.SelectedItems.Count == 1)
                _packageDetailsPanel.ShowPackage((PackageInfo)_packageListView.SelectedItems[0].Tag);
        }

        private void ListView_MouseDoubleClick(object? sender, MouseEventArgs e)
        {
            if (_packageListView.SelectedItems.Count == 1)
                _packageDetailsPanel.ShowPackage((PackageInfo)_packageListView.SelectedItems[0].Tag);
        }
    }
}
