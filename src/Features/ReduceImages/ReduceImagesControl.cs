using System.Drawing;
using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.ReduceImages;

/// <summary>
/// The Reduce Size - Images view. Scan a folder: every picture's facts are read (cached; a rescan only reads
/// new or changed files) and each picture is advised - Recommended (pre-ticked), Your call, or Skip with the
/// reason and the estimated new size. Changing an option re-advises instantly. "Reduce selected" makes the
/// copies, checks each (opens, smaller, picture quality) and swaps it in, keeping the original in a review
/// folder (or the Recycle Bin); "Undo last run" puts the originals back. This control only wires the UI.
/// </summary>
public sealed partial class ReduceImagesControl : UserControl
{
    private const string Title = "Reduce Size - Images";

    private readonly IFeatureContext _context;
    private readonly ReduceImagesSettingsStore _settingsStore;
    private readonly ReduceImagesSettings _settings;
    private readonly string _lastBatchFile;
    private readonly SavingsLedger _ledger;

    private readonly TextBox _folder = new() { Dock = DockStyle.Fill };
    private readonly Button _scan;
    private readonly Button _pauseResume;
    private readonly Button _stop;
    private readonly CheckBox _recompress;
    private readonly CheckBox _photosToJpeg;
    private readonly CheckBox _toPng;
    private readonly CheckBox _downscale;
    private readonly NumericUpDown _maxSide = new() { Minimum = 640, Maximum = 20_000, Increment = 160, Width = 70, Margin = new Padding(0, 3, 0, 0) };
    private readonly ComboBox _modern;
    private readonly ComboBox _quality;
    private readonly NumericUpDown _minSaving = new() { Minimum = 5, Maximum = 90, Increment = 5, Width = 52, Margin = new Padding(0, 3, 0, 0) };
    private readonly ComboBox _action;
    private readonly Label _help = new() { Dock = DockStyle.Top, Height = 44, ForeColor = Color.FromArgb(40, 70, 120), UseMnemonic = false };
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

    private ImageFactsCache _cache = new();
    private string _cacheRoot = "";
    private IReadOnlyList<ImageFacts> _facts = [];
    private List<(ImageFacts Facts, ImageAdvice Advice)> _rows = [];
    private readonly MediaToolPaths? _tools = FfmpegLocator.Find();
    private bool _isSample;
    private bool _filling;
    private PauseToken? _pause;
    private CancellationTokenSource? _cts;

    public ReduceImagesControl(IFeatureContext context)
    {
        _context = context;
        _settingsStore = new ReduceImagesSettingsStore(context.DataDirectory);
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

        _recompress = MakeCheck("Re-compress big JPEGs", _settings.RecompressJpegs);
        _photosToJpeg = MakeCheck("Photos in PNG/BMP/TIFF to JPEG", _settings.PhotosToJpeg);
        _toPng = MakeCheck("BMP/TIFF graphics to PNG (no loss)", _settings.LosslessToPng);
        _downscale = MakeCheck("Shrink pictures larger than", _settings.Downscale);
        _maxSide.Value = Math.Clamp(_settings.MaxLongSide, 640, 20_000);
        _modern = MakeChoice(ReduceImagesText.ModernChoices.Select(c => c.Text), Array.FindIndex(ReduceImagesText.ModernChoices, c => c.Value == _settings.Modern));
        _quality = MakeChoice(ReduceImagesText.QualityChoices.Select(c => c.Text), Array.FindIndex(ReduceImagesText.QualityChoices, c => c.Value == _settings.Quality));
        _minSaving.Value = Math.Clamp(_settings.MinSavingPercent, 5, 90);
        _action = MakeChoice(["a review folder (undo-able)", "the Recycle Bin"], _settings.Action == RemoveAction.RecycleBin ? 1 : 0);
        _list = BuildList();

        BuildLayout();
        WireEvents();
        UpdateHelp();
        ShowSaved();
    }

    /// <summary>Shows already-read pictures without scanning (no reduce possible): documentation screenshots and QA.</summary>
    public void ShowFacts(string root, IReadOnlyList<ImageFacts> facts)
    {
        _isSample = true;
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

    private void Readvise()
    {
        var options = _settings.ToAdviceOptions();
        _rows = [.. _facts.Select(f => (f, ImageAdvisor.Advise(f, options)))
            .OrderBy(r => r.Item2.Verdict)
            .ThenByDescending(r => r.Item2.SavingFrom(r.f.Size))];
        _filling = true;
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var (facts, advice) in _rows)
        {
            var item = new ListViewItem(ReduceImagesText.Cells(facts, advice)) { Tag = (facts, advice), Checked = advice.Verdict == Verdict.Recommended };
            if (advice.Verdict == Verdict.Skip)
            {
                item.ForeColor = Color.Gray;
            }
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        _filling = false;
        _summary.Text = _rows.Count == 0 ? "No pictures found." : ReduceImagesText.Summary(_rows);
        _saveReport.Enabled = _rows.Count > 0;
        UpdateSelection();
    }

    private List<(ImageFacts Facts, ImageAdvice Advice)> Selected()
        => [.. _list.CheckedItems.Cast<ListViewItem>().Select(i => ((ImageFacts, ImageAdvice))i.Tag!)];

    private void UpdateSelection()
    {
        var selected = Selected();
        _selection.Text = ReduceImagesText.Selection(selected);
        _reduce.Enabled = selected.Count > 0 && _scan.Enabled && !_isSample;
    }

    private void SelectWhere(Func<Verdict, bool> rule)
    {
        _filling = true;
        foreach (ListViewItem item in _list.Items)
        {
            var (_, advice) = ((ImageFacts, ImageAdvice))item.Tag!;
            item.Checked = advice.Verdict != Verdict.Skip && rule(advice.Verdict);
        }
        _filling = false;
        UpdateSelection();
    }

    private void OnOptionsChanged()
    {
        _settings.RecompressJpegs = _recompress.Checked;
        _settings.PhotosToJpeg = _photosToJpeg.Checked;
        _settings.LosslessToPng = _toPng.Checked;
        _settings.Downscale = _downscale.Checked;
        _settings.MaxLongSide = (int)_maxSide.Value;
        _settings.Modern = ReduceImagesText.ModernChoices[Math.Max(0, _modern.SelectedIndex)].Value;
        _settings.Quality = ReduceImagesText.QualityChoices[Math.Max(0, _quality.SelectedIndex)].Value;
        _settings.MinSavingPercent = (int)_minSaving.Value;
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
        _help.Text = ReduceImagesText.Help(_settings.ToAdviceOptions(), _tools is not null);
        _maxSide.Enabled = _downscale.Checked;
    }

    private void ShowSaved() => _saved.Text = ReduceImagesText.SavedSoFar(SavingsLedger.Total(_ledger.Load()));
}
