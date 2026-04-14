using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using OPTools.Core;
using OPTools.Utils;

namespace OPTools.Forms
{
    public class AddProjectDialog : Form
    {
        // Theme Colors
        private readonly Color _cBackground = Color.FromArgb(30, 30, 30);
        private readonly Color _cCardBg = Color.FromArgb(40, 40, 40);
        private readonly Color _cAccent = Color.FromArgb(0, 122, 204);
        private readonly Color _cText = Color.FromArgb(241, 241, 241);
        private readonly Color _cTextDim = Color.FromArgb(150, 150, 150);

        public ProjectInfo? ResultProject { get; private set; }
        private readonly GitService _gitService;

        private TextBox _txtPath = null!;
        private TextBox _txtName = null!;
        private TextBox _txtRepoUrl = null!;
        private TextBox _txtBranch = null!;
        private ComboBox _cmbStrategy = null!;
        private Label _lblStatus = null!;

        public AddProjectDialog(GitService gitService, ProjectInfo? existingProject = null)
        {
            _gitService = gitService;
            ResultProject = existingProject;
            InitializeComponents();

            if (existingProject != null)
            {
                this.Text = "Edit Project";
                _txtPath.Text = existingProject.Path;
                _txtName.Text = existingProject.Name;
                _txtRepoUrl.Text = existingProject.GitHubUrl;
                _txtBranch.Text = existingProject.DefaultBranch;
                _cmbStrategy.SelectedItem = existingProject.VersionStrategy;
                _lblStatus.Text = "Editing existing project.";
            }
        }

        private void InitializeComponents()
        {
            this.Text = "Add Project";
            this.Size = new Size(500, 450);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = _cBackground;
            this.ForeColor = _cText;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int y = 20;
            int labelWidth = 100;
            int inputWidth = 340;

            // Path
            AddLabel("Project Path:", 20, y);
            _txtPath = AddTextBox(120, y, inputWidth - 40);
            var btnBrowse = new ModernButton
            {
                Text = "...",
                Width = 35,
                Height = 28,
                Location = new Point(460 - 35, y),
                BackColor = _cCardBg
            };
            btnBrowse.Click += BtnBrowse_Click;
            this.Controls.Add(btnBrowse);
            y += 40;

            // Name
            AddLabel("Project Name:", 20, y);
            _txtName = AddTextBox(120, y, inputWidth);
            y += 40;

            // Repo URL
            AddLabel("GitHub URL:", 20, y);
            _txtRepoUrl = AddTextBox(120, y, inputWidth);
            y += 40;

            // Branch
            AddLabel("Default Branch:", 20, y);
            _txtBranch = AddTextBox(120, y, inputWidth);
            _txtBranch.Text = "main";
            y += 40;

            // Strategy
            AddLabel("Version Strategy:", 20, y);
            _cmbStrategy = new ComboBox
            {
                Location = new Point(120, y),
                Width = inputWidth,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = _cCardBg,
                ForeColor = _cText,
                FlatStyle = FlatStyle.Flat
            };
            _cmbStrategy.Items.Add(VersionStrategy.Tag);
            _cmbStrategy.Items.Add(VersionStrategy.Commit);
            _cmbStrategy.Items.Add(VersionStrategy.File);
            _cmbStrategy.SelectedIndex = 0; // Default to Tag
            this.Controls.Add(_cmbStrategy);
            y += 40;

            // Status
            _lblStatus = new Label
            {
                Text = "Ready to detect...",
                ForeColor = _cTextDim,
                Location = new Point(20, y),
                AutoSize = true
            };
            this.Controls.Add(_lblStatus);

            // Buttons
            var btnCancel = new ModernButton
            {
                Text = "Cancel",
                Width = 100,
                Height = 36,
                Location = new Point(this.Width - 130, this.Height - 80),
                BackColor = _cCardBg
            };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            var btnSave = new ModernButton
            {
                Text = "Save",
                Width = 100,
                Height = 36,
                Location = new Point(this.Width - 240, this.Height - 80),
                BackColor = _cAccent
            };
            btnSave.Click += BtnSave_Click;

            this.Controls.Add(btnCancel);
            this.Controls.Add(btnSave);
        }

        private void AddLabel(string text, int x, int y)
        {
            var lbl = new Label
            {
                Text = text,
                Location = new Point(x, y + 3),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = _cText
            };
            this.Controls.Add(lbl);
        }

        private TextBox AddTextBox(int x, int y, int width)
        {
            var txt = new TextBox
            {
                Location = new Point(x, y),
                Width = width,
                BackColor = _cCardBg,
                ForeColor = _cText,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10)
            };
            this.Controls.Add(txt);
            return txt;
        }

        private async void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _txtPath.Text = dialog.SelectedPath;
                    await AutoDetect(dialog.SelectedPath);
                }
            }
        }

        private async Task AutoDetect(string path)
        {
            _lblStatus.Text = "Detecting...";
            _txtName.Text = Path.GetFileName(path);

            var scanner = new PackageScanner();
            var fileVersion = scanner.GetProjectLocalVersion(path, Ecosystem.NPM) ?? 
                              scanner.GetProjectLocalVersion(path, Ecosystem.Python) ?? 
                              scanner.GetProjectLocalVersion(path, Ecosystem.Cpp);

            if (_gitService.IsGitRepository(path))
            {
                var url = await _gitService.GetGitHubUrlAsync(path);
                if (!string.IsNullOrEmpty(url))
                {
                    _txtRepoUrl.Text = url;
                    _lblStatus.Text = "Detected Git repository and GitHub URL.";
                }
                else
                {
                    _lblStatus.Text = "Detected Git repository (no remote found).";
                }
                
                if (!string.IsNullOrEmpty(fileVersion))
                {
                    _lblStatus.Text += $" Version: {fileVersion}";
                }
            }
            else
            {
                _lblStatus.Text = "Not a Git repository.";
                if (!string.IsNullOrEmpty(fileVersion))
                {
                    _lblStatus.Text += $" Found local version: {fileVersion}";
                    _cmbStrategy.SelectedItem = VersionStrategy.File;
                }
            }
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_txtPath.Text) || string.IsNullOrWhiteSpace(_txtName.Text))
            {
                MessageBox.Show("Path and Name are required.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ResultProject = new ProjectInfo
            {
                Id = ResultProject?.Id ?? 0, // Preserve ID if editing
                Name = _txtName.Text,
                Path = _txtPath.Text,
                GitHubUrl = _txtRepoUrl.Text,
                DefaultBranch = _txtBranch.Text,
                VersionStrategy = (VersionStrategy)_cmbStrategy.SelectedItem,
                IsGitRepo = _gitService.IsGitRepository(_txtPath.Text),
                CreatedAt = ResultProject?.CreatedAt ?? DateTime.Now,
                UpdatedAt = DateTime.Now,
                // Preserve other fields
                LastScanned = ResultProject?.LastScanned,
                PackageCount = ResultProject?.PackageCount ?? 0,
                Ecosystem = ResultProject?.Ecosystem ?? Ecosystem.NPM,
                LocalVersion = ResultProject?.LocalVersion,
                RemoteVersion = ResultProject?.RemoteVersion,
                UpdateAvailable = ResultProject?.UpdateAvailable ?? false,
                LastCheckedAt = ResultProject?.LastCheckedAt
            };

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
