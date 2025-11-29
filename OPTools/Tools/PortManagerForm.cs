using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OPTools.Tools;

public partial class PortManagerForm : Form
{
    private ListView _listView = null!;
    private Button _btnRefresh = null!;
    private Button _btnKill = null!;
    private TextBox _txtPid = null!;
    private Label _lblStatus = null!;
    private ProgressBar _progressBar = null!;

    public PortManagerForm()
    {
        InitializeComponent();
        LoadPorts();
    }

    private void InitializeComponent()
    {
        this.Text = "Kill Process by Port";
        this.Size = new Size(700, 500);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;

        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };

        _listView.Columns.Add("Protocol", 80);
        _listView.Columns.Add("Port", 80);
        _listView.Columns.Add("PID", 80);
        _listView.Columns.Add("Process Name", 200);
        _listView.Columns.Add("State", 100);

        _listView.SelectedIndexChanged += ListView_SelectedIndexChanged;

        Panel topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50
        };

        Label lblInstructions = new Label
        {
            Text = "Select a port or enter PID:",
            Dock = DockStyle.Left,
            Width = 150,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };

        _txtPid = new TextBox
        {
            Dock = DockStyle.Left,
            Width = 100,
            TextAlign = HorizontalAlignment.Center
        };

        _btnKill = new Button
        {
            Text = "Kill Process",
            Dock = DockStyle.Left,
            Width = 120,
            Enabled = false
        };
        _btnKill.Click += BtnKill_Click;

        _btnRefresh = new Button
        {
            Text = "Refresh",
            Dock = DockStyle.Right,
            Width = 100
        };
        _btnRefresh.Click += BtnRefresh_Click;

        topPanel.Controls.Add(_btnRefresh);
        topPanel.Controls.Add(_btnKill);
        topPanel.Controls.Add(_txtPid);
        topPanel.Controls.Add(lblInstructions);

        _lblStatus = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 25,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.LightGray
        };

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Bottom,
            Height = 20,
            Style = ProgressBarStyle.Marquee,
            Visible = false
        };

        Panel mainPanel = new Panel
        {
            Dock = DockStyle.Fill
        };
        mainPanel.Controls.Add(_listView);
        mainPanel.Controls.Add(_progressBar);
        mainPanel.Controls.Add(_lblStatus);

        this.Controls.Add(mainPanel);
        this.Controls.Add(topPanel);
    }

    private void ListView_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_listView.SelectedItems.Count > 0)
        {
            var item = _listView.SelectedItems[0];
            if (item.SubItems.Count > 2 && int.TryParse(item.SubItems[2].Text, out int pid))
            {
                _txtPid.Text = pid.ToString();
                _btnKill.Enabled = pid > 0;
            }
        }
    }

    private async void LoadPorts()
    {
        _progressBar.Visible = true;
        _lblStatus.Text = "Loading active ports...";
        _listView.Items.Clear();
        _btnRefresh.Enabled = false;

        try
        {
            var ports = await PortManager.GetActivePorts();

            foreach (var port in ports)
            {
                ListViewItem item = new ListViewItem(port.Protocol);
                item.SubItems.Add(port.Port.ToString());
                item.SubItems.Add(port.ProcessId.ToString());
                item.SubItems.Add(port.ProcessName);
                item.SubItems.Add(port.State);
                item.Tag = port;
                _listView.Items.Add(item);
            }

            _lblStatus.Text = $"Found {ports.Count} active port(s)";
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Error loading ports: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _progressBar.Visible = false;
            _btnRefresh.Enabled = true;
        }
    }

    private void BtnRefresh_Click(object? sender, EventArgs e)
    {
        LoadPorts();
    }

    private async void BtnKill_Click(object? sender, EventArgs e)
    {
        if (!int.TryParse(_txtPid.Text, out int pid) || pid <= 0)
        {
            MessageBox.Show("Please enter a valid PID", "Invalid PID",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult confirm = MessageBox.Show(
            $"Kill process with PID {pid}?",
            "Confirm Kill",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm == DialogResult.Yes)
        {
            _progressBar.Visible = true;
            _btnKill.Enabled = false;
            _lblStatus.Text = $"Killing process {pid}...";

            try
            {
                var result = await ProcessKiller.KillProcessById(pid);

                if (result.Success)
                {
                    _lblStatus.Text = $"Successfully killed process {pid}";
                    MessageBox.Show($"Process {pid} killed successfully!", "Success",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadPorts();
                }
                else
                {
                    _lblStatus.Text = $"Failed to kill process {pid}";
                    MessageBox.Show($"Failed to kill process {pid}:\n\n{string.Join("\n", result.Errors)}",
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
                _btnKill.Enabled = true;
            }
        }
    }
}

