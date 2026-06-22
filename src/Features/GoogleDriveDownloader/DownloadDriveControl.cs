using System.Drawing;
using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.GoogleDriveDownloader;

/// <summary>Downloads a local copy of Google Drive: regular files as-is, Google docs exported to Office + PDF.</summary>
public sealed class DownloadDriveControl : UserControl
{
    private readonly DriveSettingsStore _settingsStore;
    private readonly string _tokenDir;

    private readonly TextBox _credentials;
    private readonly Button _browseCreds;
    private readonly TextBox _folder;
    private readonly Button _browseFolder;
    private readonly Button _start;
    private readonly Button _cancel;
    private readonly ProgressBar _progress;
    private readonly Label _status;
    private readonly TextBox _log;

    private CancellationTokenSource? _cts;

    public DownloadDriveControl(IFeatureContext context)
    {
        _settingsStore = new DriveSettingsStore(context.DataDirectory);
        _tokenDir = Path.Combine(context.DataDirectory, "token");

        Dock = DockStyle.Fill;
        BackColor = Color.White;

        var saved = _settingsStore.Load();
        var defaultDir = string.IsNullOrWhiteSpace(saved.DownloadDir)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DigitalSecretary_Drive")
            : saved.DownloadDir;

        var title = new Label
        {
            Text = "Download Google Drive",
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 37, 43)
        };

        var note = new Label
        {
            Dock = DockStyle.Top,
            Height = 92,
            ForeColor = Color.FromArgb(150, 80, 0),
            Text = "Google Drive needs a one-time OAuth setup (no app password exists for Drive):\n" +
                   "1) In Google Cloud Console create an OAuth client ID of type \"Desktop app\".\n" +
                   "2) Download its JSON and select it below as the credentials file.\n" +
                   "3) Click Start — a browser opens once to authorise read-only access; the token is cached.\n" +
                   "Google Docs/Sheets/Slides are exported to both Office formats and PDF. Read-only: your Drive is untouched."
        };

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 84,
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(0, 4, 0, 4)
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        for (var r = 0; r < 2; r++)
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        _credentials = new TextBox { Dock = DockStyle.Fill, Text = saved.CredentialsPath };
        _browseCreds = new Button { Text = "Browse…", Dock = DockStyle.Fill, FlatStyle = FlatStyle.System, Cursor = Cursors.Hand };
        _browseCreds.Click += (_, _) => BrowseCredentials();

        _folder = new TextBox { Dock = DockStyle.Fill, Text = defaultDir };
        _browseFolder = new Button { Text = "Browse…", Dock = DockStyle.Fill, FlatStyle = FlatStyle.System, Cursor = Cursors.Hand };
        _browseFolder.Click += (_, _) => BrowseFolder();

        form.Controls.Add(MakeLabel("Credentials (.json):"), 0, 0);
        form.Controls.Add(_credentials, 1, 0);
        form.Controls.Add(_browseCreds, 2, 0);
        form.Controls.Add(MakeLabel("Save to folder:"), 0, 1);
        form.Controls.Add(_folder, 1, 1);
        form.Controls.Add(_browseFolder, 2, 1);

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

    private void BrowseCredentials()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Select your Google OAuth client credentials JSON",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
        };
        if (File.Exists(_credentials.Text))
            dlg.FileName = _credentials.Text;
        if (dlg.ShowDialog() == DialogResult.OK)
            _credentials.Text = dlg.FileName;
    }

    private void BrowseFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = "Choose where to save downloaded Drive files" };
        if (Directory.Exists(_folder.Text))
            dlg.SelectedPath = _folder.Text;
        if (dlg.ShowDialog() == DialogResult.OK)
            _folder.Text = dlg.SelectedPath;
    }

    private async void OnStart()
    {
        var credentials = _credentials.Text.Trim();
        var dir = _folder.Text.Trim();

        if (credentials.Length == 0 || !File.Exists(credentials))
        {
            MessageBox.Show("Please select your Google OAuth client credentials JSON file.", "Missing details",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (dir.Length == 0)
        {
            MessageBox.Show("Please choose a folder to save the files to.", "Missing details",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _settingsStore.Save(new DriveSettings { CredentialsPath = credentials, DownloadDir = dir });

        _log.Clear();
        _progress.Value = 0;
        SetRunning(true);
        _cts = new CancellationTokenSource();

        var percent = new Progress<int>(v => _progress.Value = Math.Clamp(v, 0, 100));
        var log = new Progress<string>(AppendLog);

        try
        {
            var downloader = new GoogleDriveDownloader();
            await Task.Run(() => downloader.RunAsync(credentials, _tokenDir, dir, percent, log, _cts.Token), _cts.Token);
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
        _credentials.Enabled = !running;
        _browseCreds.Enabled = !running;
        _folder.Enabled = !running;
        _browseFolder.Enabled = !running;
        if (running)
            _status.Text = "Working…";
    }

    private void AppendLog(string line)
    {
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
        _status.Text = line;
    }
}
