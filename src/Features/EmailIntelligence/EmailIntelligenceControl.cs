using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// UI for Email Intelligence: pick the archive folder + output folder, run the offline analysis
/// with phased progress, then open the generated HTML report. UI only — all logic lives in
/// <see cref="IntelligencePipeline"/> and friends.
/// </summary>
public sealed class EmailIntelligenceControl : UserControl
{
    private readonly EmailIntelligenceSettingsStore _settingsStore;

    private readonly TextBox _input;
    private readonly TextBox _output;
    private readonly TextBox _owner;
    private readonly Button _browseInput;
    private readonly Button _browseOutput;
    private readonly Button _start;
    private readonly Button _cancel;
    private readonly Button _open;
    private readonly CheckBox _append;
    private readonly ProgressBar _progress;
    private readonly Label _status;
    private readonly TextBox _log;

    private CancellationTokenSource? _cts;
    private string? _reportPath;

    public EmailIntelligenceControl(IFeatureContext context)
    {
        _settingsStore = new EmailIntelligenceSettingsStore(context.DataDirectory);
        var saved = _settingsStore.Load();

        Dock = DockStyle.Fill;
        BackColor = Color.White;

        var title = new Label
        {
            Text = "Email Intelligence",
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 37, 43),
        };

        var note = new Label
        {
            Dock = DockStyle.Top,
            Height = 60,
            ForeColor = Color.FromArgb(90, 90, 90),
            Text = "Point this at the folder where you downloaded your emails (.eml / .txt + attachments).\n" +
                   "It builds a contact list, relationships, timelines and a network graph — fully offline,\n" +
                   "no cloud and no AI. Results are written as Excel-ready CSV, JSON, vCard and an HTML report.",
        };

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 120,
            ColumnCount = 3,
            RowCount = 3,
            Padding = new Padding(0, 4, 0, 4),
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        for (var r = 0; r < 3; r++)
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        var defaultInput = string.IsNullOrWhiteSpace(saved.InputDir)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DigitalSecretary_Email")
            : saved.InputDir;
        var defaultOutput = string.IsNullOrWhiteSpace(saved.OutputDir)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DigitalSecretary_EmailIntelligence")
            : saved.OutputDir;

        _input = new TextBox { Dock = DockStyle.Fill, Text = defaultInput };
        _output = new TextBox { Dock = DockStyle.Fill, Text = defaultOutput };
        _owner = new TextBox { Dock = DockStyle.Fill, Text = saved.OwnerAddress };
        _browseInput = MakeBrowse(() => Browse(_input, "Choose your downloaded-email folder"));
        _browseOutput = MakeBrowse(() => Browse(_output, "Choose where to write the results"));

        form.Controls.Add(MakeLabel("Email archive folder:"), 0, 0);
        form.Controls.Add(_input, 1, 0);
        form.Controls.Add(_browseInput, 2, 0);
        form.Controls.Add(MakeLabel("Output folder:"), 0, 1);
        form.Controls.Add(_output, 1, 1);
        form.Controls.Add(_browseOutput, 2, 1);
        form.Controls.Add(MakeLabel("Your email (optional):"), 0, 2);
        form.Controls.Add(_owner, 1, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(0, 4, 0, 4) };
        _start = MakeButton("Analyze", OnStart);
        _cancel = MakeButton("Cancel", OnCancel);
        _open = MakeButton("Open report", OnOpenReport);
        _cancel.Enabled = false;
        _open.Enabled = false;
        buttons.Controls.Add(_start);
        buttons.Controls.Add(_cancel);
        buttons.Controls.Add(_open);

        _append = new CheckBox
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "Append to existing results (merge & de-duplicate across archives — keep the same output folder)",
            Checked = saved.Append,
        };

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
            WordWrap = false,
        };

        Controls.Add(_log);
        Controls.Add(_status);
        Controls.Add(_progress);
        Controls.Add(buttons);
        Controls.Add(_append);
        Controls.Add(form);
        Controls.Add(note);
        Controls.Add(title);
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
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
            Cursor = Cursors.Hand,
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Button MakeBrowse(Action onClick)
    {
        var btn = new Button { Text = "Browse...", Dock = DockStyle.Fill, FlatStyle = FlatStyle.System, Cursor = Cursors.Hand };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static void Browse(TextBox target, string description)
    {
        using var dlg = new FolderBrowserDialog { Description = description };
        if (Directory.Exists(target.Text))
            dlg.SelectedPath = target.Text;
        if (dlg.ShowDialog() == DialogResult.OK)
            target.Text = dlg.SelectedPath;
    }

    private async void OnStart()
    {
        var input = _input.Text.Trim();
        var output = _output.Text.Trim();

        if (!Directory.Exists(input))
        {
            MessageBox.Show("Please choose an existing folder containing your downloaded emails.",
                "Folder not found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (output.Length == 0)
        {
            MessageBox.Show("Please choose an output folder for the results.",
                "Missing output folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _settingsStore.Save(new EmailIntelligenceSettings
        {
            InputDir = input,
            OutputDir = output,
            OwnerAddress = _owner.Text.Trim(),
            Append = _append.Checked,
        });

        _log.Clear();
        _progress.Value = 0;
        _open.Enabled = false;
        SetRunning(true);
        _cts = new CancellationTokenSource();

        var progress = new Progress<AnalysisProgress>(OnProgress);
        var options = new EmailIntelligenceOptions
        {
            InputDir = input,
            OutputDir = output,
            OwnerAddress = _owner.Text.Trim(),
            Mode = _append.Checked ? AnalysisMode.Append : AnalysisMode.Overwrite,
        };

        try
        {
            var pipeline = new IntelligencePipeline();
            var outDir = await Task.Run(() => pipeline.RunAndExport(options, progress, _cts.Token), _cts.Token);
            _reportPath = Path.Combine(outDir, "index.html");
            _open.Enabled = File.Exists(_reportPath);
            _status.Text = "Completed.";
            AppendLog("Report: " + _reportPath);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Cancelled.";
            AppendLog("Analysis cancelled by user.");
        }
        catch (Exception ex)
        {
            _status.Text = "Error.";
            AppendLog("ERROR: " + ex.Message);
            MessageBox.Show(ex.Message, "Analysis failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        _status.Text = "Cancelling...";
        _cts?.Cancel();
    }

    private void OnOpenReport()
    {
        if (_reportPath is null || !File.Exists(_reportPath))
            return;
        Process.Start(new ProcessStartInfo(_reportPath) { UseShellExecute = true });
    }

    private void OnProgress(AnalysisProgress p)
    {
        _progress.Value = Math.Clamp(p.Percent, 0, 100);
        _status.Text = $"{p.Phase}: {p.Detail}";
        AppendLog($"[{p.Percent,3}%] {p.Phase} — {p.Detail}");
    }

    private void SetRunning(bool running)
    {
        _start.Enabled = !running;
        _cancel.Enabled = running;
        _input.Enabled = !running;
        _output.Enabled = !running;
        _owner.Enabled = !running;
        _browseInput.Enabled = !running;
        _browseOutput.Enabled = !running;
        _append.Enabled = !running;
        if (running)
            _status.Text = "Working...";
    }

    private void AppendLog(string line) =>
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
}
