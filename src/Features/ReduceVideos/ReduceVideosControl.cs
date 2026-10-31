using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.ReduceVideos;

/// <summary>
/// The Reduce Size - Videos view. Scan a folder: every video's facts are read (ffprobe, or Windows when ffmpeg
/// is not installed; cached, so a rescan only reads new or changed files) and each video is advised -
/// Recommended (pre-ticked), Your call, or Skip with the reason and the estimated new size. Changing the
/// engine, quality or minimum saving re-advises instantly. "Reduce selected" makes the copies one by one,
/// verifies each and swaps it in, keeping the original in a review folder (or the Recycle Bin); "Undo last
/// run" puts the originals back. All logic lives in the engine classes; this control only wires the UI.
/// </summary>
public sealed partial class ReduceVideosControl : UserControl
{
    private const string Title = "Reduce Size - Videos";

    private readonly IFeatureContext _context;
    private readonly ReduceVideosSettingsStore _settingsStore;
    private readonly ReduceVideosSettings _settings;
    private readonly string _lastBatchFile;
    private readonly SavingsLedger _ledger;

    private readonly TextBox _folder = new() { Dock = DockStyle.Fill };
    private readonly Button _scan;
    private readonly Button _pauseResume;
    private readonly Button _stop;
    private readonly ComboBox _engine;
    private readonly ComboBox _quality;
    private readonly NumericUpDown _minSaving = new() { Minimum = 5, Maximum = 90, Increment = 5, Width = 52, Margin = new Padding(0, 3, 0, 0) };
    private readonly CheckBox _checkQuality;
    private readonly CheckBox _skipHdr;
    private readonly ComboBox _action;
    private readonly Label _help = new() { Dock = DockStyle.Top, Height = 40, ForeColor = Color.FromArgb(40, 70, 120), UseMnemonic = false };
    private readonly Label _tools = new() { AutoSize = true, Margin = new Padding(0, 6, 8, 0), UseMnemonic = false };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Top, Height = 8, Minimum = 0, Maximum = 100 };
    private readonly Label _status = new() { Dock = DockStyle.Top, Height = 22, ForeColor = Color.Gray, Text = "Idle.", UseMnemonic = false };
    private readonly Label _summary = new() { Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold), Text = "No folder scanned yet.", UseMnemonic = false };
    private readonly ListView _list;
    private readonly Label _selection = new() { AutoSize = true, Margin = new Padding(8, 8, 8, 0), UseMnemonic = false };
    private readonly Label _saved = new() { AutoSize = true, Margin = new Padding(8, 8, 0, 0), ForeColor = Color.FromArgb(0, 110, 60), UseMnemonic = false };
    private readonly Button _reduce;
    private readonly Button _undo;
    private readonly Button _saveReport;
    private readonly TextBox _log = new() { Dock = DockStyle.Bottom, Height = 64, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9f), WordWrap = false };

    private VideoFactsCache _cache = new();
    private string _cacheRoot = "";
    private IReadOnlyList<VideoFacts> _facts = [];
    private List<(VideoFacts Facts, VideoAdvice Advice)> _rows = [];
    private MediaToolPaths? _toolPaths;
    private GpuEncoder? _gpu;
    private bool _windowsHevc = true;
    private bool _isSample;
    private bool _filling;
    private PauseToken? _pause;
    private CancellationTokenSource? _cts;

    public ReduceVideosControl(IFeatureContext context)
    {
        _context = context;
        _settingsStore = new ReduceVideosSettingsStore(context.DataDirectory);
        _settings = _settingsStore.Load();
        _lastBatchFile = Path.Combine(context.DataDirectory, "last_run.json");
        _ledger = new SavingsLedger(Path.Combine(context.DataDirectory, "history.jsonl"));

        Dock = DockStyle.Fill;
        BackColor = Color.White;
        _folder.Text = _settings.Root;
        _scan = MakeButton("Scan", OnScan);
        _pauseResume = MakeButton("Pause", OnPauseResume);
        _stop = MakeButton("Stop", () => { _cts?.Cancel(); _pause?.Resume(); });
        _reduce = MakeButton("Reduce selected", OnReduce);
        _undo = MakeButton("Undo last run", OnUndo);
        _saveReport = MakeButton("Save report...", OnSaveReport);
        _pauseResume.Enabled = _stop.Enabled = _reduce.Enabled = _saveReport.Enabled = false;
        _undo.Enabled = ShrinkSwap.LoadLastBatch(_lastBatchFile) is not null;

        _engine = MakeChoice(ReduceVideosText.EngineChoices.Select(c => c.Text), Array.FindIndex(ReduceVideosText.EngineChoices, c => c.Value == _settings.Engine));
        _quality = MakeChoice(ReduceVideosText.QualityChoices.Select(c => c.Text), Array.FindIndex(ReduceVideosText.QualityChoices, c => c.Value == _settings.Quality));
        _minSaving.Value = Math.Clamp(_settings.MinSavingPercent, 5, 90);
        _checkQuality = MakeCheck("Check picture quality of each copy", _settings.CheckQuality);
        _skipHdr = MakeCheck("Leave HDR videos alone", _settings.SkipHdr);
        _action = MakeChoice(["a review folder (undo-able)", "the Recycle Bin"], _settings.Action == RemoveAction.RecycleBin ? 1 : 0);
        _list = BuildList();

        BuildLayout();
        WireEvents();
        RefreshTools();
        if (_toolPaths is not null)
        {
            AppendLog("ffmpeg found in " + _toolPaths.Folder);
        }
        ShowSaved();
        _ = DetectWindowsEncoderAsync();
    }

    /// <summary>
    /// Shows already-read videos without scanning (no reduce possible). Used by the documentation screenshots
    /// (with <see cref="SampleVideoFacts"/>, so no real file appears) and by QA to drive the view.
    /// </summary>
    public void ShowFacts(string root, IReadOnlyList<VideoFacts> facts)
    {
        _isSample = true;
        _log.Clear(); // the start-up log names this PC's ffmpeg folder - keep it out of screenshots
        _folder.Text = root;
        _cacheRoot = root;
        _facts = facts;
        Readvise();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Dispose();
            _pause?.Dispose();
        }
        base.Dispose(disposing);
    }

    // ----------------------------------------------------------------- advice

    private VideoAdviceOptions Options() => _settings.ToAdviceOptions(_windowsHevc);

    /// <summary>Advises every known video with the current options and refills the list (no rescan).</summary>
    private void Readvise()
    {
        var options = Options();
        _rows = [.. _facts.Select(f => (f, VideoAdvisor.Advise(f, options)))
            .OrderBy(r => r.Item2.Verdict)
            .ThenByDescending(r => r.Item2.SavingFrom(r.f.Size))];
        _filling = true;
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var (facts, advice) in _rows)
        {
            var item = new ListViewItem(ReduceVideosText.Cells(facts, advice)) { Tag = (facts, advice), Checked = advice.Verdict == Verdict.Recommended };
            if (advice.Verdict == Verdict.Skip)
            {
                item.ForeColor = Color.Gray;
            }
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        _filling = false;
        _summary.Text = _rows.Count == 0 ? "No videos found." : ReduceVideosText.Summary(_rows);
        _saveReport.Enabled = _rows.Count > 0;
        UpdateSelection();
    }

    private List<(VideoFacts Facts, VideoAdvice Advice)> Selected()
        => [.. _list.CheckedItems.Cast<ListViewItem>().Select(i => ((VideoFacts, VideoAdvice))i.Tag!)];

    private void UpdateSelection()
    {
        var selected = Selected();
        _selection.Text = ReduceVideosText.Selection(selected);
        _reduce.Enabled = selected.Count > 0 && _scan.Enabled && !_isSample;
    }

    private void SelectWhere(Func<Verdict, bool> rule)
    {
        _filling = true;
        foreach (ListViewItem item in _list.Items)
        {
            var (_, advice) = ((VideoFacts, VideoAdvice))item.Tag!;
            item.Checked = advice.Verdict != Verdict.Skip && rule(advice.Verdict);
        }
        _filling = false;
        UpdateSelection();
    }

    private void OnOptionsChanged()
    {
        _settings.Engine = ReduceVideosText.EngineChoices[Math.Max(0, _engine.SelectedIndex)].Value;
        _settings.Quality = ReduceVideosText.QualityChoices[Math.Max(0, _quality.SelectedIndex)].Value;
        _settings.MinSavingPercent = (int)_minSaving.Value;
        _settings.CheckQuality = _checkQuality.Checked;
        _settings.SkipHdr = _skipHdr.Checked;
        _settings.Action = _action.SelectedIndex == 1 ? RemoveAction.RecycleBin : RemoveAction.ReviewFolder;
        SaveSettings();
        UpdateHelp();
        if (_facts.Count > 0)
        {
            Readvise();
        }
    }

    private void UpdateHelp()
    {
        var gpu = _gpu ?? GpuEncoder.Amd;
        _help.Text = ReduceVideosText.EngineHelp(_settings.Engine, _toolPaths is not null, _gpu is null ? gpu : _gpu.Value, _windowsHevc)
            + "  " + ReduceVideosText.QualityHelp(_settings.Quality);
        _checkQuality.Enabled = _settings.Engine != VideoEngineKind.Windows;
    }

    private void RefreshTools()
    {
        _toolPaths = FfmpegLocator.Find(_settings.FfmpegFolder);
        // The folder goes to the log, not the label: a user-profile path must never appear in screenshots (no PII in the docs).
        _tools.Text = _toolPaths is null ? "ffmpeg: not installed" : "ffmpeg: installed";
        _tools.ForeColor = _toolPaths is null ? Color.FromArgb(170, 70, 0) : Color.FromArgb(0, 110, 60);
        UpdateHelp();
    }

    private async Task DetectWindowsEncoderAsync()
    {
        var hevc = await Task.Run(() => MfTranscoder.HasHevcEncoder);
        if (IsDisposed)
        {
            return;
        }
        _windowsHevc = hevc;
        UpdateHelp();
        if (_facts.Count > 0 && _settings.Engine == VideoEngineKind.Windows)
        {
            Readvise();
        }
    }

    private void ShowSaved() => _saved.Text = ReduceVideosText.SavedSoFar(SavingsLedger.Total(_ledger.Load()));
}
