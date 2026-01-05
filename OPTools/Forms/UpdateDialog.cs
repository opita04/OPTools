using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using OPTools.Core;

namespace OPTools.Forms
{
    /// <summary>
    /// Dialog for updating packages with version selection
    /// Matches the UpdateDialog from NPM Handler
    /// </summary>
    public class UpdateDialog : Form
    {
        // Theme Colors
        private readonly Color _cBackground = Color.FromArgb(30, 30, 30);
        private readonly Color _cSidebar = Color.FromArgb(25, 25, 26);
        private readonly Color _cAccent = Color.FromArgb(0, 122, 204);
        private readonly Color _cSuccess = Color.FromArgb(92, 184, 92);
        private readonly Color _cWarning = Color.FromArgb(240, 173, 78);
        private readonly Color _cDanger = Color.FromArgb(217, 83, 79);
        private readonly Color _cText = Color.FromArgb(241, 241, 241);
        private readonly Color _cTextDim = Color.FromArgb(150, 150, 150);
        private readonly Color _cGridHeader = Color.FromArgb(45, 45, 48);

        private readonly List<NpmPackage> _packages;
        private readonly NpmUpdater _updater;
        private readonly NpmDatabase _database;
        
        private Panel _contentPanel = null!;
        private Panel _footerPanel = null!;
        private Button _btnUpdate = null!;
        private Button _btnClose = null!;
        private Label _lblStatus = null!;
        private ProgressBar _progressBar = null!;
        
        private List<(NpmPackage package, TextBox versionInput)> _packageInputs = new();
        private List<NpmUpdateResult> _results = new();
        private bool _isUpdating = false;
        private bool _updateCompleted = false;

        public List<NpmUpdateResult> Results => _results;

        public UpdateDialog(List<NpmPackage> packages, NpmUpdater updater, NpmDatabase database)
        {
            _packages = packages;
            _updater = updater;
            _database = database;
            InitializeDialog();
            BuildPackageList();
        }

        private void InitializeDialog()
        {
            this.Text = $"Update Packages ({_packages.Count})";
            this.Size = new Size(700, 550);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = _cBackground;
            this.ForeColor = _cText;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // IMPORTANT: For WinForms docking, add controls in REVERSE order
            // (Bottom/Fill first, then Top panels last so they stack correctly)

            // Footer Panel (add first for Dock.Bottom)
            _footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = _cGridHeader,
                Padding = new Padding(20, 15, 20, 15)
            };

            _btnClose = new Button
            {
                Text = "Cancel",
                Width = 100,
                Height = 40,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = _cText,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            _btnClose.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            _btnClose.Click += (s, e) => this.Close();
            _btnClose.Location = new Point(_footerPanel.Width - 240, 15);

            _btnUpdate = new Button
            {
                Text = "⬆ Update Packages",
                Width = 130,
                Height = 40,
                BackColor = _cAccent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            _btnUpdate.FlatAppearance.BorderSize = 0;
            _btnUpdate.Click += BtnUpdate_Click;
            _btnUpdate.Location = new Point(_footerPanel.Width - 130, 15);

            _lblStatus = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9),
                ForeColor = _cTextDim,
                AutoSize = true,
                Location = new Point(20, 25)
            };

            _progressBar = new ProgressBar
            {
                Width = 200,
                Height = 5,
                Location = new Point(20, 45),
                Visible = false,
                Style = ProgressBarStyle.Continuous
            };

            _footerPanel.Controls.Add(_lblStatus);
            _footerPanel.Controls.Add(_progressBar);
            _footerPanel.Controls.Add(_btnClose);
            _footerPanel.Controls.Add(_btnUpdate);
            this.Controls.Add(_footerPanel);

            // Content Panel (add second for Dock.Fill)
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = _cBackground,
                Padding = new Padding(20)
            };
            this.Controls.Add(_contentPanel);

            // Info Banner (add third for Dock.Top - will appear below header)
            var infoPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(30, 60, 114, 173),
                Padding = new Padding(15)
            };

            var lblInfo = new Label
            {
                Text = "⚠ Review packages and versions before updating. Major version updates may contain breaking changes.",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(173, 214, 255),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            infoPanel.Controls.Add(lblInfo);
            this.Controls.Add(infoPanel);

            // Header (add last for Dock.Top - will appear at very top)
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = _cSidebar,
                Padding = new Padding(20, 15, 20, 15)
            };

            var lblTitle = new Label
            {
                Text = $"Update {_packages.Count} Package(s)",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = _cText,
                AutoSize = true
            };
            headerPanel.Controls.Add(lblTitle);
            this.Controls.Add(headerPanel);

            // Reposition buttons on resize
            this.Resize += (s, e) =>
            {
                _btnUpdate.Location = new Point(_footerPanel.Width - 150, 15);
                _btnClose.Location = new Point(_footerPanel.Width - 260, 15);
            };
        }

        private void BuildPackageList()
        {
            _contentPanel.Controls.Clear();
            _packageInputs.Clear();

            int yPos = 10;
            foreach (var pkg in _packages)
            {
                var card = CreatePackageCard(pkg, ref yPos);
                _contentPanel.Controls.Add(card);
            }
        }

        private Panel CreatePackageCard(NpmPackage pkg, ref int yPos)
        {
            var card = new Panel
            {
                Width = _contentPanel.Width - 60,
                Height = 80,
                BackColor = _cGridHeader,
                Location = new Point(10, yPos),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Package name
            var lblName = new Label
            {
                Text = pkg.Name,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = _cText,
                AutoSize = true,
                Location = new Point(15, 10)
            };
            card.Controls.Add(lblName);

            // Project path
            var lblPath = new Label
            {
                Text = pkg.DisplayProjectPath,
                Font = new Font("Segoe UI", 8),
                ForeColor = _cTextDim,
                AutoSize = true,
                Location = new Point(15, 35)
            };
            card.Controls.Add(lblPath);

            // Version info
            var currentVersion = pkg.Version;
            var latestVersion = pkg.LatestVersion ?? "latest";
            var isMajorUpdate = IsMajorUpdate(currentVersion, latestVersion);

            var lblVersions = new Label
            {
                Text = $"{currentVersion} → {latestVersion}",
                Font = new Font("Segoe UI", 10),
                ForeColor = isMajorUpdate ? _cWarning : _cSuccess,
                AutoSize = true,
                Location = new Point(card.Width - 200, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            card.Controls.Add(lblVersions);

            // Major update badge
            if (isMajorUpdate)
            {
                var lblMajor = new Label
                {
                    Text = "MAJOR",
                    Font = new Font("Segoe UI", 7, FontStyle.Bold),
                    ForeColor = _cWarning,
                    BackColor = Color.FromArgb(60, 240, 173, 78),
                    AutoSize = true,
                    Padding = new Padding(4, 2, 4, 2),
                    Location = new Point(card.Width - 200, 35),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right
                };
                card.Controls.Add(lblMajor);
            }

            // Version input
            var lblTarget = new Label
            {
                Text = "Target:",
                Font = new Font("Segoe UI", 9),
                ForeColor = _cTextDim,
                AutoSize = true,
                Location = new Point(15, 55)
            };
            card.Controls.Add(lblTarget);

            var txtVersion = new TextBox
            {
                Text = latestVersion,
                Width = 120,
                Height = 25,
                BackColor = Color.FromArgb(50, 50, 52),
                ForeColor = _cText,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9),
                Location = new Point(70, 52)
            };
            card.Controls.Add(txtVersion);

            _packageInputs.Add((pkg, txtVersion));

            yPos += 90;
            return card;
        }

        private bool IsMajorUpdate(string current, string? latest)
        {
            if (string.IsNullOrEmpty(latest)) return false;

            try
            {
                var currentMajor = int.Parse(current.TrimStart('v', '^', '~').Split('.')[0]);
                var latestMajor = int.Parse(latest.TrimStart('v', '^', '~').Split('.')[0]);
                return latestMajor > currentMajor;
            }
            catch
            {
                return false;
            }
        }

        private async void BtnUpdate_Click(object? sender, EventArgs e)
        {
            // If updates already completed, clicking "Done" should close the dialog
            if (_updateCompleted)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
                return;
            }
            
            if (_isUpdating) return;
            
            _isUpdating = true;
            _btnUpdate.Enabled = false;
            _btnClose.Text = "Close";
            _progressBar.Visible = true;
            _progressBar.Maximum = _packages.Count;
            _progressBar.Value = 0;
            _results.Clear();

            try
            {
                for (int i = 0; i < _packageInputs.Count; i++)
                {
                    var (pkg, versionInput) = _packageInputs[i];
                    var targetVersion = versionInput.Text.Trim();
                    
                    if (string.IsNullOrEmpty(targetVersion))
                        targetVersion = pkg.LatestVersion ?? "latest";

                    _lblStatus.Text = $"Updating {pkg.Name}... ({i + 1}/{_packages.Count})";
                    _progressBar.Value = i + 1;
                    Application.DoEvents();

                    var result = await _updater.UpdatePackageAsync(pkg, targetVersion);
                    _results.Add(result);

                    // If update succeeded, sync the database immediately
                    if (result.Success && !string.IsNullOrEmpty(result.NewVersion))
                    {
                        _database.MarkPackageAsUpdated(pkg.ProjectPath, pkg.Name, result.NewVersion);
                    }

                    // Visual feedback on the card
                    UpdateCardStatus(i, result);
                }

                var successCount = _results.Count(r => r.Success);
                var failCount = _results.Count - successCount;
                _lblStatus.Text = $"Complete: {successCount} succeeded, {failCount} failed";
                _lblStatus.ForeColor = failCount == 0 ? _cSuccess : _cWarning;
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"Error: {ex.Message}";
                _lblStatus.ForeColor = _cDanger;
            }
            finally
            {
                _isUpdating = false;
                _updateCompleted = true;
                _btnUpdate.Text = "Done";
                _btnUpdate.BackColor = _cGridHeader;
                _btnUpdate.Enabled = true;
            }
        }

        private void UpdateCardStatus(int index, NpmUpdateResult result)
        {
            if (index >= _contentPanel.Controls.Count) return;
            
            var card = _contentPanel.Controls[index] as Panel;
            if (card == null) return;

            var statusLabel = new Label
            {
                Text = result.Success ? "✓ Updated" : "✗ Failed",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = result.Success ? _cSuccess : _cDanger,
                AutoSize = true,
                Location = new Point(card.Width - 80, 55),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            card.Controls.Add(statusLabel);

            if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
            {
                // Add tooltip or expand error
                statusLabel.Tag = result.ErrorMessage;
                var tooltip = new ToolTip();
                tooltip.SetToolTip(statusLabel, result.ErrorMessage);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isUpdating)
            {
                e.Cancel = true;
                MessageBox.Show("Please wait for the update to complete.", "Update in Progress",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            base.OnFormClosing(e);
        }
    }
}
