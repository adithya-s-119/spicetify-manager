using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm(args));
    }
}

internal sealed class MainForm : Form
{
    private const string InstallerUrl = "https://raw.githubusercontent.com/spicetify/cli/main/install.ps1";

    private static readonly Color Canvas = Color.FromArgb(245, 246, 248);
    private static readonly Color Ink = Color.FromArgb(24, 26, 30);
    private static readonly Color Muted = Color.FromArgb(110, 116, 126);
    private static readonly Color Green = Color.FromArgb(29, 185, 84);
    private static readonly Color GreenHover = Color.FromArgb(30, 215, 96);
    private static readonly Color GreenDown = Color.FromArgb(21, 160, 70);
    private static readonly Color Slate = Color.FromArgb(43, 45, 49);
    private static readonly Color SlateHover = Color.FromArgb(64, 67, 73);
    private static readonly Color SlateDown = Color.FromArgb(32, 34, 37);
    private static readonly Color LogBack = Color.FromArgb(30, 30, 32);
    private static readonly Color LogText = Color.FromArgb(214, 214, 214);
    private static readonly Color LogDim = Color.FromArgb(136, 142, 152);
    private static readonly Color LogGreen = Color.FromArgb(106, 220, 150);
    private static readonly Color LogRed = Color.FromArgb(244, 135, 113);
    private static readonly Color LogYellow = Color.FromArgb(220, 220, 170);
    private static readonly Color LogCyan = Color.FromArgb(86, 182, 240);

    private static readonly Regex AnsiCodes =
        new Regex("\u001B\\[[0-9;?]*[ -/]*[@-~]", RegexOptions.Compiled);

    private readonly string[] _startupArgs;
    private readonly RichTextBox _log = new RichTextBox();
    private readonly Label _status = new Label();
    private readonly Label _version = new Label();
    private Label _applyHint;
    private readonly Button _apply = new Button();
    private readonly Button _update = new Button();
    private readonly Button _install = new Button();
    private readonly Button _stop = new Button();
    private readonly Timer _clock = new Timer();

    private Process _current;
    private bool _running;
    private bool _missing;
    private bool _installing;
    private bool _stoppedByUser;
    private DateTime _startedAt;
    private string _currentName = string.Empty;

    public MainForm(string[] args)
    {
        _startupArgs = args ?? new string[0];

        BuildUi();

        _clock.Interval = 1000;
        _clock.Tick += delegate { UpdateElapsedStatus(); };

        _apply.Click += delegate { OnApplyClicked(); };
        _update.Click += delegate { StartCommand("update"); };
        _install.Click += delegate { OnInstallClicked(); };
        _stop.Click += delegate { StopCommand(); };

        Load += delegate { DetectEnvironment(); };
        Shown += delegate
        {
            if (_startupArgs.Length > 0)
            {
                StartCommand(JoinArgs(_startupArgs));
            }
        };
        FormClosing += OnFormClosing;
    }

    // ---------------------------------------------------------------- layout

    private void BuildUi()
    {
        Text = "Spicetify Control";
        BackColor = Canvas;
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(880, 660);
        MinimumSize = new Size(780, 560);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // Keep the default icon if the executable icon cannot be read.
        }

        TableLayoutPanel root = new TableLayoutPanel();
        root.Dock = DockStyle.Fill;
        root.BackColor = Canvas;
        root.Padding = new Padding(22, 18, 22, 14);
        root.ColumnCount = 1;
        root.RowCount = 5;
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 106f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        Controls.Add(root);

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildActions(), 0, 1);
        root.Controls.Add(BuildLogHeader(), 0, 2);
        root.Controls.Add(BuildLog(), 0, 3);
        root.Controls.Add(BuildFooter(), 0, 4);
    }

    private Control BuildHeader()
    {
        Panel header = new Panel();
        header.Dock = DockStyle.Fill;
        header.Margin = new Padding(0);

        Label title = new Label();
        title.Text = "Spicetify Control";
        title.Font = new Font("Segoe UI Semibold", 18f);
        title.ForeColor = Ink;
        title.AutoSize = true;
        title.Location = new Point(0, 0);

        Label subtitle = new Label();
        subtitle.Text = "Back up and apply your setup, or update spicetify.";
        subtitle.Font = new Font("Segoe UI", 9.5f);
        subtitle.ForeColor = Muted;
        subtitle.AutoSize = true;
        subtitle.Location = new Point(2, 42);

        Panel right = new Panel();
        right.Dock = DockStyle.Right;
        right.Width = 300;
        right.BackColor = Canvas;

        _version.Text = "checking spicetify...";
        _version.Font = new Font("Segoe UI", 9f);
        _version.ForeColor = Muted;
        _version.TextAlign = ContentAlignment.MiddleRight;
        _version.AutoSize = false;
        _version.Dock = DockStyle.Fill;

        StylePrimary(_install, "Install Spicetify");
        _install.Font = new Font("Segoe UI Semibold", 9.5f);
        _install.Size = new Size(150, 34);
        _install.Location = new Point(150, 25);
        _install.Visible = false;

        right.Controls.Add(_version);
        right.Controls.Add(_install);

        header.Controls.Add(right);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        return header;
    }

    private Control BuildActions()
    {
        TableLayoutPanel grid = new TableLayoutPanel();
        grid.Dock = DockStyle.Fill;
        grid.Margin = new Padding(0);
        grid.BackColor = Canvas;
        grid.ColumnCount = 2;
        grid.RowCount = 2;
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 60f));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        StylePrimary(_apply, "Backup && Apply");
        _apply.Dock = DockStyle.Fill;
        _apply.Margin = new Padding(0, 0, 5, 8);

        StyleSecondary(_update, "Update Spicetify");
        _update.Dock = DockStyle.Fill;
        _update.Margin = new Padding(5, 0, 0, 8);

        _applyHint = MakeHint("Creates backup, then applies");
        _applyHint.Margin = new Padding(2, 0, 5, 0);

        Label updateHint = MakeHint("Runs  spicetify update");
        updateHint.Margin = new Padding(7, 0, 0, 0);

        grid.Controls.Add(_apply, 0, 0);
        grid.Controls.Add(_update, 1, 0);
        grid.Controls.Add(_applyHint, 0, 1);
        grid.Controls.Add(updateHint, 1, 1);
        return grid;
    }

    private Control BuildLogHeader()
    {
        Panel bar = new Panel();
        bar.Dock = DockStyle.Fill;
        bar.Margin = new Padding(0);

        Label caption = new Label();
        caption.Text = "Output";
        caption.Font = new Font("Segoe UI Semibold", 9f);
        caption.ForeColor = Muted;
        caption.Dock = DockStyle.Left;
        caption.Width = 120;
        caption.TextAlign = ContentAlignment.MiddleLeft;

        Panel buttons = new Panel();
        buttons.Dock = DockStyle.Right;
        buttons.Width = 140;

        Button copy = MakeSmallButton("Copy", 62);
        copy.Location = new Point(0, 4);
        copy.Click += delegate
        {
            if (_log.TextLength > 0)
            {
                try
                {
                    Clipboard.SetText(_log.Text);
                }
                catch
                {
                    // Clipboard can be locked by another app; ignore.
                }
            }
        };

        Button clear = MakeSmallButton("Clear", 62);
        clear.Location = new Point(68, 4);
        clear.Click += delegate { _log.Clear(); };

        buttons.Controls.Add(copy);
        buttons.Controls.Add(clear);

        bar.Controls.Add(buttons);
        bar.Controls.Add(caption);
        return bar;
    }

    private Control BuildLog()
    {
        Panel host = new Panel();
        host.Dock = DockStyle.Fill;
        host.Margin = new Padding(0);
        host.BackColor = LogBack;
        host.Padding = new Padding(10, 8, 4, 8);

        _log.Dock = DockStyle.Fill;
        _log.BorderStyle = BorderStyle.None;
        _log.BackColor = LogBack;
        _log.ForeColor = LogText;
        _log.Font = new Font("Consolas", 9.5f);
        _log.ReadOnly = true;
        _log.WordWrap = true;
        _log.ScrollBars = RichTextBoxScrollBars.Vertical;
        _log.DetectUrls = false;
        _log.HideSelection = false;
        _log.TabStop = false;

        host.Controls.Add(_log);
        return host;
    }

    private Control BuildFooter()
    {
        Panel footer = new Panel();
        footer.Dock = DockStyle.Fill;
        footer.Margin = new Padding(0);
        footer.Padding = new Padding(0, 5, 0, 3);

        _status.Text = "Ready.";
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.Font = new Font("Segoe UI", 9f);
        _status.ForeColor = Muted;

        _stop.Text = "Stop";
        _stop.Dock = DockStyle.Right;
        _stop.Width = 92;
        _stop.Enabled = false;
        _stop.Visible = false;
        _stop.FlatStyle = FlatStyle.Flat;
        _stop.FlatAppearance.BorderSize = 1;
        _stop.FlatAppearance.BorderColor = Color.FromArgb(200, 204, 210);
        _stop.BackColor = Color.White;
        _stop.ForeColor = Color.FromArgb(190, 70, 60);
        _stop.FlatAppearance.MouseOverBackColor = Color.FromArgb(252, 240, 238);
        _stop.Cursor = Cursors.Hand;
        _stop.UseVisualStyleBackColor = false;

        footer.Controls.Add(_status);
        footer.Controls.Add(_stop);
        return footer;
    }

    private static Label MakeHint(string text)
    {
        Label hint = new Label();
        hint.Text = text;
        hint.Dock = DockStyle.Fill;
        hint.TextAlign = ContentAlignment.MiddleLeft;
        hint.Font = new Font("Segoe UI", 8.75f);
        hint.ForeColor = Muted;
        return hint;
    }

    private static Button MakeSmallButton(string text, int width)
    {
        Button button = new Button();
        button.Text = text;
        button.Size = new Size(width, 26);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = Color.FromArgb(231, 234, 238);
        button.ForeColor = Ink;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(218, 222, 228);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(205, 210, 217);
        button.Font = new Font("Segoe UI", 8.75f);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
        return button;
    }

    private static void StylePrimary(Button button, string text)
    {
        button.Text = text;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = Green;
        button.ForeColor = Color.White;
        button.FlatAppearance.MouseOverBackColor = GreenHover;
        button.FlatAppearance.MouseDownBackColor = GreenDown;
        button.Font = new Font("Segoe UI Semibold", 10.5f);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
    }

    private static void StyleSecondary(Button button, string text)
    {
        button.Text = text;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = Slate;
        button.ForeColor = Color.White;
        button.FlatAppearance.MouseOverBackColor = SlateHover;
        button.FlatAppearance.MouseDownBackColor = SlateDown;
        button.Font = new Font("Segoe UI Semibold", 10.5f);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
    }

    // --------------------------------------------------------------- commands

    private void OnApplyClicked()
    {
        if (!HasBackup())
        {
            StartCommand("backup apply");
            return;
        }

        AppendLine("A backup already exists  -  skipping the backup step and running spicetify apply.", LogDim);
        StartCommand("apply");
    }

    private void UpdateBackupHints()
    {
        bool hasBackup = HasBackup();

        _applyHint.Text = hasBackup
            ? "Backup found  -  runs spicetify apply"
            : "Creates backup, then applies";
        _applyHint.ForeColor = Muted;
    }

    private static bool HasBackup()
    {
        string[] roots =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        foreach (string root in roots)
        {
            try
            {
                string folder = Path.Combine(root, "spicetify", "Backup");
                if (Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length > 0)
                {
                    return true;
                }
            }
            catch
            {
                // Ignore unreadable locations and keep looking.
            }
        }

        return false;
    }

    private void OnInstallClicked()
    {
        if (_running)
        {
            return;
        }

        DialogResult answer = MessageBox.Show(
            this,
            "Install spicetify now?\n\n" +
            "The official installer opens in its own window and downloads the latest spicetify CLI from GitHub:\n\n" +
            "    " + InstallerUrl + "\n\n" +
            "It installs to %LOCALAPPDATA%\\spicetify and may ask whether you want the Marketplace too. " +
            "Follow its prompts - this window will detect spicetify as soon as it closes.",
            "Install spicetify?",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        string powerShell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        if (!File.Exists(powerShell))
        {
            powerShell = "powershell.exe";
        }

        string command = "$ProgressPreference='SilentlyContinue'; iwr -useb '" + InstallerUrl + "' | iex";

        _installing = true;
        _running = true;
        _stoppedByUser = false;
        _startedAt = DateTime.Now;
        _currentName = "spicetify installer";

        SetBusy(true);
        _stop.Visible = false; // never offer to kill the installer halfway through
        _install.Visible = false;
        _clock.Start();
        UpdateElapsedStatus();

        AppendLine(string.Empty, LogText);
        AppendLine("> official spicetify installer", LogCyan);
        AppendLine("A separate installer window is open - follow its prompts.", LogDim);
        AppendLine("This app will check for spicetify as soon as the installer closes.", LogDim);

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(
                powerShell,
                "-NoProfile -ExecutionPolicy Bypass -Command \"" + command + "\"");
            startInfo.UseShellExecute = true;
            startInfo.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            Process process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("The installer process could not be started.");
            }

            _current = process;

            Task.Factory.StartNew(delegate
            {
                process.WaitForExit();

                int exitCode;
                try
                {
                    exitCode = process.ExitCode;
                }
                catch
                {
                    exitCode = -1;
                }

                try
                {
                    process.Dispose();
                }
                catch
                {
                    // Nothing useful to do if disposal fails.
                }

                RunOnUi(delegate { Finish(exitCode); });
            });
        }
        catch (Exception ex)
        {
            AppendLine("Could not start the installer: " + ex.Message, LogRed);
            _installing = false;
            Finish(-1);
        }
    }

    private void StartCommand(string arguments)
    {
        if (_running || _missing)
        {
            return;
        }

        string executable = FindSpicetify();
        if (executable == null)
        {
            ShowMissingSpicetify();
            return;
        }

        _running = true;
        _stoppedByUser = false;
        _startedAt = DateTime.Now;
        _currentName = "spicetify " + arguments;

        SetBusy(true);
        _clock.Start();
        UpdateElapsedStatus();

        AppendLine(string.Empty, LogText);
        AppendLine("> " + _currentName, LogCyan);
        AppendLine(new string('-', 76), LogDim);

        ProcessStartInfo startInfo = new ProcessStartInfo(executable, arguments);
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardOutputEncoding = new UTF8Encoding(false);
        startInfo.StandardErrorEncoding = new UTF8Encoding(false);

        Process process = new Process();
        process.StartInfo = startInfo;
        process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                AppendFromWorker(e.Data);
            }
        };
        process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                AppendFromWorker(e.Data);
            }
        };

        _current = process;

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            AppendLine("Could not start spicetify: " + ex.Message, LogRed);
            _current = null;
            process.Dispose();
            Finish(-1);
            return;
        }

        Task.Factory.StartNew(delegate
        {
            process.WaitForExit();
            int exitCode = process.ExitCode;
            try
            {
                process.Dispose();
            }
            catch
            {
                // Nothing useful to do if disposal fails.
            }

            RunOnUi(delegate { Finish(exitCode); });
        });
    }

    private void Finish(int exitCode)
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        _current = null;
        _clock.Stop();
        SetBusy(false);

        string elapsed = ElapsedText(DateTime.Now - _startedAt);

        if (_installing)
        {
            // The official installer is interactive, so its exit code is not a
            // reliable success signal. Just look for spicetify again.
            _installing = false;
            _stop.Visible = false;

            AppendLine(string.Empty, LogText);
            AppendLine("Installer closed after " + elapsed + ".", LogDim);

            DetectEnvironment();

            if (!_missing)
            {
                _status.Text = "spicetify is installed. Choose an action above.";
                _status.ForeColor = Color.FromArgb(18, 140, 66);
            }

            return;
        }

        if (exitCode == 0)
        {
            AppendLine(string.Empty, LogText);
            AppendLine("Finished in " + elapsed + "  -  success.", LogGreen);
            _status.Text = "Finished in " + elapsed + "  -  spicetify completed successfully.";
            _status.ForeColor = Color.FromArgb(18, 140, 66);

            // "spicetify update" may have installed a new CLI version,
            // and a rebuild may have changed the backup state.
            UpdateBackupHints();
            string executable = FindSpicetify();
            if (executable != null)
            {
                RefreshVersionLabel(executable);
            }
        }
        else if (_stoppedByUser)
        {
            AppendLine(string.Empty, LogText);
            AppendLine("Stopped by user after " + elapsed + ".", LogYellow);
            _status.Text = "Stopped after " + elapsed + ".";
            _status.ForeColor = Color.FromArgb(150, 110, 30);
        }
        else
        {
            AppendLine(string.Empty, LogText);
            AppendLine("Failed with exit code " + exitCode + " after " + elapsed + ".", LogRed);
            _status.Text = "Failed in " + elapsed + "  -  exit code " + exitCode + ". See the output above.";
            _status.ForeColor = Color.FromArgb(196, 62, 48);
        }
    }

    private void StopCommand()
    {
        if (_current == null)
        {
            return;
        }

        DialogResult answer = MessageBox.Show(
            this,
            "Stop the running command?\n\nStopping in the middle of an apply can leave Spotify" +
            "\npartially patched. Running Backup & Apply again fixes that.",
            "Stop spicetify",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        try
        {
            if (!_current.HasExited)
            {
                _current.Kill();
                _stoppedByUser = true;
                AppendLine("Stopped by user.", LogYellow);
            }
        }
        catch (Exception ex)
        {
            AppendLine("Could not stop spicetify: " + ex.Message, LogRed);
        }
    }

    private void SetBusy(bool busy)
    {
        _apply.Enabled = !busy;
        _update.Enabled = !busy;
        _stop.Enabled = busy;
        _stop.Visible = busy;

        _apply.BackColor = busy ? Color.FromArgb(151, 199, 169) : Green;
        _update.BackColor = busy ? Color.FromArgb(186, 189, 194) : Slate;
        _apply.ForeColor = busy ? Color.FromArgb(238, 244, 240) : Color.White;
        _update.ForeColor = busy ? Color.FromArgb(246, 247, 248) : Color.White;
        Cursor = busy ? Cursors.AppStarting : Cursors.Default;
    }

    private void UpdateElapsedStatus()
    {
        if (!_running)
        {
            return;
        }

        _status.Text = "Running " + _currentName + "...   " + ElapsedText(DateTime.Now - _startedAt);
        _status.ForeColor = Muted;
    }

    private static string ElapsedText(TimeSpan span)
    {
        if (span.TotalHours >= 1)
        {
            return string.Format("{0}:{1:00}:{2:00}", (int)span.TotalHours, span.Minutes, span.Seconds);
        }

        return string.Format("{0}:{1:00}", (int)span.TotalMinutes, span.Seconds);
    }

    // ----------------------------------------------------------------- output

    private void AppendFromWorker(string line)
    {
        RunOnUi(delegate { AppendStreamLine(line); });
    }

    private void AppendStreamLine(string raw)
    {
        string line = AnsiCodes.Replace(raw, string.Empty).TrimEnd('\r');
        string lower = line.ToLowerInvariant();

        Color color = LogText;
        if (lower.Contains("error") || lower.Contains("failed") || lower.Contains("cannot") ||
            lower.Contains("exception"))
        {
            color = LogRed;
        }
        else if (lower.Contains("warn") || lower.Contains("deprecat"))
        {
            color = LogYellow;
        }
        else if (lower.Contains("success") || lower.Contains("applied") || lower.Contains("updated"))
        {
            color = LogGreen;
        }
        else if (line.TrimStart().StartsWith(">"))
        {
            color = LogCyan;
        }

        AppendLine(line, color);
    }

    private void AppendLine(string text, Color color)
    {
        if (_log.IsDisposed)
        {
            return;
        }

        // Keep the buffer bounded for very long sessions.
        if (_log.TextLength > 400000)
        {
            _log.Clear();
        }

        _log.SelectionStart = _log.TextLength;
        _log.SelectionLength = 0;
        _log.SelectionColor = color;
        _log.AppendText(text + "\n");
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private void RunOnUi(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        try
        {
            BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
            // The window is closing; drop late output.
        }
    }

    // -------------------------------------------------------------- discovery

    private void DetectEnvironment()
    {
        string executable = FindSpicetify();
        if (executable == null)
        {
            ShowMissingSpicetify();
            return;
        }

        if (_missing)
        {
            AppendLine(string.Empty, LogText);
            AppendLine("spicetify is now installed.", LogGreen);
        }

        _missing = false;
        _install.Visible = false;
        _version.Visible = true;

        AppendLine("spicetify found: " + executable, LogDim);
        AppendLine(string.Empty, LogText);

        SetBusy(false);
        UpdateBackupHints();
        RefreshVersionLabel(executable);
    }

    private void RefreshVersionLabel(string executable)
    {
        Task.Factory.StartNew(delegate
        {
            string version = null;
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(executable, "--version");
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.StandardOutputEncoding = new UTF8Encoding(false);

                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(5000);
                    version = AnsiCodes.Replace(output, string.Empty).Trim();
                    int newline = version.IndexOf('\n');
                    if (newline >= 0)
                    {
                        version = version.Substring(0, newline).Trim();
                    }
                }
            }
            catch
            {
                version = null;
            }

            RunOnUi(delegate
            {
                if (!string.IsNullOrEmpty(version))
                {
                    _version.Text = "spicetify v" + version.TrimStart('v', 'V');
                    _version.ForeColor = Color.FromArgb(18, 140, 66);
                }
                else
                {
                    _version.Text = "spicetify ready";
                }
            });
        });
    }

    private void ShowMissingSpicetify()
    {
        _missing = true;
        _version.Visible = false;   // replaced by the Install button
        _install.Visible = true;

        _status.Text = "spicetify is not installed. Use Install Spicetify to set it up.";
        _status.ForeColor = LogRed;

        _apply.Enabled = false;
        _update.Enabled = false;
        _apply.BackColor = Color.FromArgb(151, 199, 169);
        _update.BackColor = Color.FromArgb(186, 189, 194);

        AppendLine("spicetify.exe was not found.", LogRed);
        AppendLine("Looked in your PATH, %LOCALAPPDATA%\\spicetify and %APPDATA%\\spicetify.", LogDim);
        AppendLine("Click Install Spicetify to run the official installer, or get it from https://spicetify.app.", LogDim);
    }

    private static string FindSpicetify()
    {
        string fromPath = SearchPath("spicetify.exe");
        if (fromPath != null)
        {
            return fromPath;
        }

        string local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "spicetify",
            "spicetify.exe");
        if (File.Exists(local))
        {
            return local;
        }

        string roaming = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "spicetify",
            "spicetify.exe");
        if (File.Exists(roaming))
        {
            return roaming;
        }

        return null;
    }

    private static string SearchPath(string fileName)
    {
        string path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (string directory in path.Split(Path.PathSeparator))
        {
            if (directory.Trim().Length == 0)
            {
                continue;
            }

            try
            {
                string candidate = Path.Combine(directory.Trim().Trim('"'), fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                // Skip malformed PATH entries.
            }
        }

        return null;
    }

    private static string JoinArgs(string[] args)
    {
        string[] quoted = new string[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            quoted[i] = args[i].IndexOf(' ') >= 0 ? "\"" + args[i] + "\"" : args[i];
        }

        return string.Join(" ", quoted);
    }

    private void OnFormClosing(object sender, FormClosingEventArgs e)
    {
        if (!_running)
        {
            return;
        }

        if (_installing)
        {
            DialogResult installAnswer = MessageBox.Show(
                this,
                "The spicetify installer is still running in its own window.\n\n" +
                "Close this window and leave the installer running?",
                "Spicetify Control",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);

            if (installAnswer != DialogResult.Yes)
            {
                e.Cancel = true;
            }

            // Never kill the installer halfway through.
            return;
        }

        DialogResult answer = MessageBox.Show(
            this,
            "spicetify is still running.\n\nStop it and close the window?",
            "Spicetify Control",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        try
        {
            if (_current != null && !_current.HasExited)
            {
                _current.Kill();
            }
        }
        catch
        {
            // Closing anyway.
        }
    }
}
