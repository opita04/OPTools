using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OPTools.Forms
{
    /// <summary>
    /// Log entry for the error logs system
    /// </summary>
    public class LogEntry
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
    }

    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// A modal dialog that displays application logs
    /// Matches the LogsModal from NPM Handler
    /// </summary>
    public class LogsDialog : Form
    {
        // Theme Colors
        private readonly Color _cBackground = Color.FromArgb(30, 30, 30);
        private readonly Color _cText = Color.FromArgb(241, 241, 241);
        private readonly Color _cTextDim = Color.FromArgb(150, 150, 150);
        private readonly Color _cGridHeader = Color.FromArgb(45, 45, 48);
        private readonly Color _cDanger = Color.FromArgb(217, 83, 79);
        private readonly Color _cWarning = Color.FromArgb(240, 173, 78);
        private readonly Color _cInfo = Color.FromArgb(91, 192, 222);

        private readonly List<LogEntry> _logs;
        private Panel _logsPanel = null!;
        private ToolTip _toolTip = null!;

        public event EventHandler? LogsCleared;

        public LogsDialog(List<LogEntry> logs)
        {
            _logs = logs;
            InitializeDialog();
            BuildLogsList();
        }

        private void InitializeDialog()
        {
            _toolTip = new ToolTip();
            _toolTip.AutoPopDelay = 5000;
            _toolTip.InitialDelay = 1000;
            _toolTip.ReshowDelay = 500;
            _toolTip.ShowAlways = true;

            this.Text = $"Application Logs ({_logs.Count} entries)";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = _cBackground;
            this.ForeColor = _cText;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(600, 400);

            // Header Panel
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = _cGridHeader,
                Padding = new Padding(20, 15, 20, 15)
            };

            var lblTitle = new Label
            {
                Text = "📋 Application Logs",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = _cText,
                AutoSize = true,
                Location = new Point(20, 18)
            };
            headerPanel.Controls.Add(lblTitle);

            var lblCount = new Label
            {
                Text = $"({_logs.Count} entries)",
                Font = new Font("Segoe UI", 10),
                ForeColor = _cTextDim,
                AutoSize = true,
                Location = new Point(200, 22)
            };
            headerPanel.Controls.Add(lblCount);

            // Action buttons in header
            var btnCopyAll = new Button
            {
                Text = "📋 Copy All",
                Width = 100,
                Height = 30,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = _cText,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(headerPanel.Width - 220, 15),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnCopyAll.Click += BtnCopyAll_Click;
            _toolTip.SetToolTip(btnCopyAll, "Copy all logs to clipboard");
            headerPanel.Controls.Add(btnCopyAll);

            var btnClear = new Button
            {
                Text = "🗑 Clear",
                Width = 80,
                Height = 30,
                BackColor = _cDanger,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(headerPanel.Width - 110, 15),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnClear.Click += BtnClear_Click;
            _toolTip.SetToolTip(btnClear, "Clear all logs");
            headerPanel.Controls.Add(btnClear);

            this.Controls.Add(headerPanel);

            // Logs Panel (scrollable)
            _logsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = _cBackground,
                Padding = new Padding(20)
            };
            this.Controls.Add(_logsPanel);

            // Footer
            var footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = _cGridHeader
            };

            var btnClose = new Button
            {
                Text = "Close",
                Width = 100,
                Height = 35,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = _cText,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(footerPanel.Width - 120, 8),
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            btnClose.Click += (s, e) => this.Close();
            _toolTip.SetToolTip(btnClose, "Close this dialog");
            footerPanel.Controls.Add(btnClose);

            this.Controls.Add(footerPanel);
        }

        private void BuildLogsList()
        {
            _logsPanel.Controls.Clear();

            if (_logs.Count == 0)
            {
                var lblEmpty = new Label
                {
                    Text = "📋 No logs available\n\nError logs will appear here when operations fail",
                    Font = new Font("Segoe UI", 12),
                    ForeColor = _cTextDim,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill
                };
                _logsPanel.Controls.Add(lblEmpty);
                return;
            }

            int yPos = 10;
            foreach (var log in _logs.OrderByDescending(l => l.Timestamp))
            {
                var card = CreateLogCard(log, ref yPos);
                _logsPanel.Controls.Add(card);
            }
        }

        private Panel CreateLogCard(LogEntry log, ref int yPos)
        {
            var (bgColor, borderColor, levelText) = log.Level switch
            {
                LogLevel.Error => (Color.FromArgb(40, 217, 83, 79), _cDanger, "ERROR"),
                LogLevel.Warning => (Color.FromArgb(40, 240, 173, 78), _cWarning, "WARNING"),
                _ => (Color.FromArgb(40, 91, 192, 222), _cInfo, "INFO")
            };

            var card = new Panel
            {
                Width = _logsPanel.Width - 60,
                BackColor = bgColor,
                Location = new Point(10, yPos),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Padding = new Padding(15)
            };

            // Left border accent
            var accent = new Panel
            {
                Width = 4,
                Dock = DockStyle.Left,
                BackColor = borderColor
            };
            card.Controls.Add(accent);

            int innerY = 10;

            // Level badge and timestamp
            var lblLevel = new Label
            {
                Text = levelText,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = borderColor,
                AutoSize = true,
                Location = new Point(20, innerY)
            };
            card.Controls.Add(lblLevel);

            var lblTime = new Label
            {
                Text = log.Timestamp.ToString("g"),
                Font = new Font("Segoe UI", 8),
                ForeColor = _cTextDim,
                AutoSize = true,
                Location = new Point(80, innerY)
            };
            card.Controls.Add(lblTime);

            // Copy button
            var btnCopy = new Label
            {
                Text = "📋",
                Font = new Font("Segoe UI", 10),
                ForeColor = _cTextDim,
                AutoSize = true,
                Location = new Point(card.Width - 50, innerY),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            btnCopy.Click += (s, e) => CopyLogToClipboard(log);
            card.Controls.Add(btnCopy);

            innerY += 25;

            // Message
            var lblMessage = new Label
            {
                Text = log.Message,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = _cText,
                AutoSize = true,
                MaximumSize = new Size(card.Width - 60, 0),
                Location = new Point(20, innerY)
            };
            card.Controls.Add(lblMessage);
            innerY += lblMessage.Height + 10;

            // Details (if present)
            if (!string.IsNullOrEmpty(log.Details))
            {
                var txtDetails = new TextBox
                {
                    Text = log.Details,
                    Font = new Font("Consolas", 9),
                    ForeColor = _cText,
                    BackColor = Color.FromArgb(25, 25, 26),
                    Width = card.Width - 60,
                    Multiline = true,
                    ReadOnly = true,
                    BorderStyle = BorderStyle.None,
                    Location = new Point(20, innerY),
                    ScrollBars = ScrollBars.Vertical
                };
                
                // Calculate height based on content
                var lineCount = log.Details.Split('\n').Length;
                txtDetails.Height = Math.Min(lineCount * 16 + 10, 150);
                
                card.Controls.Add(txtDetails);
                innerY += txtDetails.Height + 10;
            }

            card.Height = innerY + 10;
            yPos += card.Height + 10;

            return card;
        }

        private void BtnCopyAll_Click(object? sender, EventArgs e)
        {
            var text = string.Join("\n\n", _logs.Select(log =>
                $"[{log.Timestamp:g}] {log.Level.ToString().ToUpper()}: {log.Message}" +
                (string.IsNullOrEmpty(log.Details) ? "" : $"\n{log.Details}")
            ));

            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show("No logs to copy.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Clipboard.SetText(text);
            MessageBox.Show("All logs copied to clipboard.", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnClear_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show("Clear all logs?", "Confirm Clear",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                _logs.Clear();
                BuildLogsList();
                LogsCleared?.Invoke(this, EventArgs.Empty);
            }
        }

        private void CopyLogToClipboard(LogEntry log)
        {
            var text = $"[{log.Timestamp:g}] {log.Level.ToString().ToUpper()}: {log.Message}" +
                (string.IsNullOrEmpty(log.Details) ? "" : $"\n{log.Details}");
            Clipboard.SetText(text);
        }
    }
}
