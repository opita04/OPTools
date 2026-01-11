using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using OPTools.Core;

namespace OPTools.Forms
{
    /// <summary>
    /// A slide-in panel that displays detailed package information
    /// Similar to the PackageDrawer in NPM Handler
    /// </summary>
    public class PackageDetailsPanel : Panel
    {
        // Theme Colors (matching OPTools theme)
        private readonly Color _cBackground = Color.FromArgb(25, 25, 26);
        private readonly Color _cAccent = Color.FromArgb(0, 122, 204);
        private readonly Color _cText = Color.FromArgb(241, 241, 241);
        private readonly Color _cTextDim = Color.FromArgb(150, 150, 150);
        private readonly Color _cWarning = Color.FromArgb(240, 173, 78);
        private readonly Color _cSuccess = Color.FromArgb(92, 184, 92);
        private readonly Color _cBorder = Color.FromArgb(60, 60, 60);

        private PackageInfo? _currentPackage;
        private Panel _contentPanel = null!;
        private ToolTip _toolTip = null!;
        
        public event EventHandler? CloseRequested;
        public event EventHandler<PackageInfo>? UpdateRequested;
        public event EventHandler<PackageInfo>? UninstallRequested;

        public PackageDetailsPanel()
        {
            InitializePanel();
        }

        private void InitializePanel()
        {
            _toolTip = new ToolTip();
            _toolTip.AutoPopDelay = 5000;
            _toolTip.InitialDelay = 1000;
            _toolTip.ReshowDelay = 500;
            _toolTip.ShowAlways = true;

            this.Width = 400;
            this.Dock = DockStyle.Right;
            this.BackColor = _cBackground;
            this.Visible = false;
            this.Padding = new Padding(1);

            // Border effect
            var borderPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 1,
                BackColor = _cBorder
            };
            this.Controls.Add(borderPanel);

            // Content panel with scrolling
            _contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = _cBackground,
                AutoScroll = true,
                Padding = new Padding(20)
            };
            this.Controls.Add(_contentPanel);
        }

        public void ShowPackage(PackageInfo package)
        {
            _currentPackage = package;
            BuildContent();
            this.Visible = true;
        }

        public void HidePanel()
        {
            this.Visible = false;
            _currentPackage = null;
        }

        private void BuildContent()
        {
            // Dispose old controls to prevent memory leaks
            foreach (Control c in _contentPanel.Controls)
            {
                c.Dispose();
            }
            _contentPanel.Controls.Clear();
            
            if (_currentPackage == null) return;

            var pkg = _currentPackage;
            int yPos = 10;

            // Close button
            var btnClose = new Label
            {
                Text = "✕",
                Font = new Font("Segoe UI", 14),
                ForeColor = _cTextDim,
                AutoSize = true,
                Location = new Point(_contentPanel.Width - 50, 10),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnClose.Click += (s, e) => { HidePanel(); CloseRequested?.Invoke(this, EventArgs.Empty); };
            btnClose.MouseEnter += (s, e) => btnClose.ForeColor = _cText;
            btnClose.MouseLeave += (s, e) => btnClose.ForeColor = _cTextDim;
            _contentPanel.Controls.Add(btnClose);

            // Package Name
            var lblName = new Label
            {
                Text = pkg.Name,
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = _cText,
                AutoSize = true,
                Location = new Point(10, yPos),
                MaximumSize = new Size(_contentPanel.Width - 80, 0)
            };
            _contentPanel.Controls.Add(lblName);
            yPos += lblName.Height + 10;

            // Version Badge
            var pnlVersion = CreateBadge($"v{pkg.Version}", _cAccent);
            pnlVersion.Location = new Point(10, yPos);
            _contentPanel.Controls.Add(pnlVersion);

            // Outdated Badge
            if (pkg.IsOutdated && !string.IsNullOrEmpty(pkg.LatestVersion))
            {
                var pnlOutdated = CreateBadge($"Update: v{pkg.LatestVersion}", _cWarning);
                pnlOutdated.Location = new Point(pnlVersion.Width + 20, yPos);
                _contentPanel.Controls.Add(pnlOutdated);
            }
            yPos += 40;

            // Separator
            AddSeparator(ref yPos);

            // Description Section
            if (!string.IsNullOrEmpty(pkg.Description))
            {
                AddSection("About", pkg.Description, ref yPos);
            }

            // Details Grid
            AddSeparator(ref yPos);
            AddDetailRow("Author", pkg.Author ?? "—", ref yPos);
            AddDetailRow("License", pkg.License ?? "—", ref yPos);
            AddDetailRow("Location", pkg.DisplayProjectPath, ref yPos);
            AddDetailRow("Last Checked", pkg.LastChecked?.ToString("g") ?? "Never", ref yPos);
            AddDetailRow("Type", pkg.IsDev ? "Dev Dependency" : "Production", ref yPos);
            AddDetailRow("Status", pkg.StatusText, ref yPos, 
                pkg.IsOutdated ? _cWarning : (pkg.NotFound ? Color.Red : _cSuccess));

            // Links Section
            AddSeparator(ref yPos);
            AddSection("Links", null, ref yPos);

            // NPM Link
            AddLinkButton("View on npm Registry", pkg.NpmUrl, ref yPos);

            // Homepage Link
            if (!string.IsNullOrEmpty(pkg.Homepage))
            {
                AddLinkButton("Homepage", pkg.Homepage, ref yPos);
            }

            // Repository Link
            if (!string.IsNullOrEmpty(pkg.Repository))
            {
                AddLinkButton("Repository", pkg.Repository, ref yPos);
            }

            // Action Buttons
            AddSeparator(ref yPos);
            yPos += 10;

            // Update Button (if outdated)
            if (pkg.IsOutdated)
            {
                var btnUpdate = CreateActionButton("Update Package", _cSuccess, "Update this package to the latest version");
                btnUpdate.Location = new Point(10, yPos);
                btnUpdate.Click += (s, e) => UpdateRequested?.Invoke(this, pkg);
                _contentPanel.Controls.Add(btnUpdate);
                yPos += 50;
            }

            // Uninstall Button
            var btnUninstall = CreateActionButton("Uninstall Package", Color.FromArgb(217, 83, 79), "Uninstall this package from the project");
            btnUninstall.Location = new Point(10, yPos);
            btnUninstall.Click += (s, e) => UninstallRequested?.Invoke(this, pkg);
            _contentPanel.Controls.Add(btnUninstall);
        }

        private Panel CreateBadge(string text, Color bgColor)
        {
            var badge = new Panel
            {
                BackColor = bgColor,
                Height = 26,
                AutoSize = true,
                Padding = new Padding(8, 4, 8, 4)
            };

            var lbl = new Label
            {
                Text = text,
                Font = new Font("Consolas", 10),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(8, 4)
            };
            badge.Controls.Add(lbl);
            badge.Width = lbl.Width + 16;
            
            return badge;
        }

        private void AddSection(string title, string? content, ref int yPos)
        {
            var lblTitle = new Label
            {
                Text = title.ToUpper(),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = _cTextDim,
                AutoSize = true,
                Location = new Point(10, yPos)
            };
            _contentPanel.Controls.Add(lblTitle);
            yPos += 25;

            if (!string.IsNullOrEmpty(content))
            {
                var lblContent = new Label
                {
                    Text = content,
                    Font = new Font("Segoe UI", 10),
                    ForeColor = _cText,
                    AutoSize = true,
                    MaximumSize = new Size(_contentPanel.Width - 40, 0),
                    Location = new Point(10, yPos)
                };
                _contentPanel.Controls.Add(lblContent);
                yPos += lblContent.Height + 10;
            }
        }

        private void AddDetailRow(string label, string value, ref int yPos, Color? valueColor = null)
        {
            var lblLabel = new Label
            {
                Text = label,
                Font = new Font("Segoe UI", 9),
                ForeColor = _cTextDim,
                Width = 100,
                Location = new Point(10, yPos)
            };
            _contentPanel.Controls.Add(lblLabel);

            var lblValue = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 10),
                ForeColor = valueColor ?? _cText,
                AutoSize = true,
                MaximumSize = new Size(_contentPanel.Width - 130, 0),
                Location = new Point(115, yPos)
            };
            _contentPanel.Controls.Add(lblValue);
            yPos += Math.Max(25, lblValue.Height + 5);
        }

        private void AddLinkButton(string text, string url, ref int yPos)
        {
            var btn = new Panel
            {
                Width = _contentPanel.Width - 40,
                Height = 45,
                BackColor = Color.FromArgb(40, 40, 42),
                Location = new Point(10, yPos),
                Cursor = Cursors.Hand
            };

            var lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 10),
                ForeColor = _cText,
                AutoSize = true,
                Location = new Point(15, 12)
            };
            btn.Controls.Add(lbl);

            var arrow = new Label
            {
                Text = "→",
                Font = new Font("Segoe UI", 12),
                ForeColor = _cTextDim,
                AutoSize = true,
                Location = new Point(btn.Width - 35, 10),
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btn.Controls.Add(arrow);

            btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(55, 55, 58);
            btn.MouseLeave += (s, e) => btn.BackColor = Color.FromArgb(40, 40, 42);
            btn.Click += (s, e) => OpenUrl(url);
            lbl.Click += (s, e) => OpenUrl(url);
            arrow.Click += (s, e) => OpenUrl(url);
            
            _toolTip.SetToolTip(btn, url);
            _toolTip.SetToolTip(lbl, url);
            _toolTip.SetToolTip(arrow, url);

            _contentPanel.Controls.Add(btn);
            yPos += 55;
        }

        private void AddSeparator(ref int yPos)
        {
            yPos += 10;
            var sep = new Panel
            {
                Width = _contentPanel.Width - 40,
                Height = 1,
                BackColor = _cBorder,
                Location = new Point(10, yPos)
            };
            _contentPanel.Controls.Add(sep);
            yPos += 15;
        }

        private Button CreateActionButton(string text, Color bgColor, string tooltipText = "")
        {
            var btn = new Button
            {
                Text = text,
                Width = _contentPanel.Width - 40,
                Height = 40,
                BackColor = bgColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            
            if (!string.IsNullOrEmpty(tooltipText))
            {
                _toolTip.SetToolTip(btn, tooltipText);
            }
            
            return btn;
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open URL: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
