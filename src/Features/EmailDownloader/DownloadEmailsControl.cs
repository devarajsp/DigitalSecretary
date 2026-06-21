using System.Drawing;
using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.EmailDownloader;

/// <summary>Downloads a local copy of all Yahoo Mail folders, emails (.txt + .eml) and attachments.</summary>
public sealed class DownloadEmailsControl : UserControl
{
    private readonly EmailSettingsStore _settingsStore;

    private readonly TextBox _email;
    private readonly TextBox _password;
    private readonly TextBox _folder;
    private readonly Button _browse;
    private readonly Button _start;
    private readonly Button _cancel;
    private readonly ProgressBar _progress;
    private readonly Label _status;
    private readonly TextBox _log;

    private CancellationTokenSource? _cts;

    public DownloadEmailsControl(IFeatureContext context)
    {
        _settingsStore = new EmailSettingsStore(context.DataDirectory);

        Dock = DockStyle.Fill;
        BackColor = Color.White;

        var saved = _settingsStore.Load();
        var defaultDir = string.IsNullOrWhiteSpace(saved.DownloadDir)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DigitalSecretary_Email")
            : saved.DownloadDir;

        var title = new Label
        {
            Text = "Download Emails (Yahoo)",
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 37, 43)
        };

        var note = new Label
        {
            Dock = DockStyle.Top,
            Height = 76,
            ForeColor = Color.FromArgb(150, 80, 0),
            Text = "Yahoo requires an APP PASSWORD for IMAP — your normal password will not work.\n" +
                   "Create one at: Yahoo Account → Account Security → \"Generate app password\".\n" +
                   "Each email is saved as both .txt and .eml, with all attachments (including inline images).\n" +
                   "This downloads a COPY only; your emails stay in Yahoo Mail, untouched."
        };

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 120,
            ColumnCount = 3,
            RowCount = 3,
            Padding = new Padding(0, 4, 0, 4)
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        for (var r = 0; r < 3; r++)
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        _email = new TextBox { Dock = DockStyle.Fill, Text = saved.Email };
        _password = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        _folder = new TextBox { Dock = DockStyle.Fill, Text = defaultDir };
        _browse = new Button { Text = "Browse…", Dock = DockStyle.Fill, FlatStyle = FlatStyle.System, Cursor = Cursors.Hand };
        _browse.Click += (_, _) => BrowseFolder();

        form.Controls.Add(MakeLabel("Yahoo email:"), 0, 0);
        form.Controls.Add(_email, 1, 0);
        form.Controls.Add(MakeLabel("App password:"), 0, 1);
        form.Controls.Add(_password, 1, 1);
        form.Controls.Add(MakeLabel("Save to folder:"), 0, 2);
        form.Controls.Add(_folder, 1, 2);
        form.Controls.Add(_browse, 2, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(0, 4, 0, 4) };
        _start = MakeButton("Start Download", OnStart);
        _cancel = MakeButton("Cancel", OnCancel);
        _cancel.Enabled = false;
        buttons.Controls.Add(_start);
        buttons.Controls.Add(_cancel);

        _progress = new ProgressBar { Dock = DockStyle.Top, Height = 22, Minimum = 0, Maximum = 100 };

        _status = new Label { Dock = DockStyle.Top, Height = 24, ForeColor = Color.Gray, Text = "Idle." };

        _log = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(250, 250, 250),
            Font = new Font("Consolas", 9f),
            WordWrap = false
        };

        Controls.Add(_log);
        Controls.Add(_status);
        Controls.Add(_progress);
        Controls.Add(buttons);
        Controls.Add(form);
        Controls.Add(note);
        Controls.Add(title);
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static Button MakeButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            Padding = new Padding(12, 4, 12, 4),
            Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.System,
            Cursor = Cursors.Hand
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private void BrowseFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = "Choose where to save downloaded emails" };
        if (Directory.Exists(_folder.Text))
            dlg.SelectedPath = _folder.Text;
        if (dlg.ShowDialog() == DialogResult.OK)
            _folder.Text = dlg.SelectedPath;
    }

    private async void OnStart()
    {
        var email = _email.Text.Trim();
        var password = _password.Text;
        var dir = _folder.Text.Trim();

        if (email.Length == 0 || password.Length == 0)
        {
            MessageBox.Show("Please enter your Yahoo email and app password.", "Missing details",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (dir.Length == 0)
        {
            MessageBox.Show("Please choose a folder to save the emails to.", "Missing details",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _settingsStore.Save(new EmailSettings { Email = email, DownloadDir = dir });

        _log.Clear();
        _progress.Value = 0;
        SetRunning(true);
        _cts = new CancellationTokenSource();

        var percent = new Progress<int>(v => _progress.Value = Math.Clamp(v, 0, 100));
        var log = new Progress<string>(AppendLog);

        try
        {
            var downloader = new EmailDownloader();
            await Task.Run(() => downloader.RunAsync(email, password, dir, percent, log, _cts.Token), _cts.Token);
            _status.Text = "Completed.";
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Cancelled.";
            AppendLog("Download cancelled by user.");
        }
        catch (Exception ex)
        {
            _status.Text = "Error.";
            AppendLog("ERROR: " + ex.Message);
            MessageBox.Show(ex.Message, "Download failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetRunning(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void OnCancel()
    {
        _status.Text = "Cancelling…";
        _cts?.Cancel();
    }

    private void SetRunning(bool running)
    {
        _start.Enabled = !running;
        _cancel.Enabled = running;
        _email.Enabled = !running;
        _password.Enabled = !running;
        _folder.Enabled = !running;
        _browse.Enabled = !running;
        if (running)
            _status.Text = "Working…";
    }

    private void AppendLog(string line)
    {
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
        _status.Text = line;
    }
}
