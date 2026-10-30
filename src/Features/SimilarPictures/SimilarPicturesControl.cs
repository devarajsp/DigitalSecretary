using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.SimilarPictures;

/// <summary>
/// The Similar Pictures view. Pick a folder and Scan: every picture is fingerprinted (cached, so a
/// rescan only reads new or changed files) and look-alikes are grouped. The left list shows the
/// groups; the right side shows one group's pictures side by side with KEEP / MOVE badges. Exact copies
/// and re-saved copies are pre-marked; "Similar - check" groups never are - the user decides. "Move
/// marked" puts the marked pictures in a review folder (or the Recycle Bin) and "Undo last move" puts
/// them back. All matching logic lives in the engine classes; this control only wires the UI.
/// </summary>
public sealed class SimilarPicturesControl : UserControl
{
    private const int CardThumbSize = 170;

    private static readonly (Sensitivity Value, string Text)[] SensitivityChoices =
    [
        (Sensitivity.Strict, "Strict - only very close matches"),
        (Sensitivity.Balanced, "Balanced (recommended)"),
        (Sensitivity.Loose, "Loose - more matches to check")
    ];

    private readonly IFeatureContext _context;
    private readonly SimilarPicturesSettingsStore _settingsStore;
    private readonly SimilarPicturesSettings _settings;
    private readonly string _lastBatchFile;

    private readonly TextBox _folder = new() { Dock = DockStyle.Fill };
    private readonly Button _scan;
    private readonly Button _pauseResume;
    private readonly Button _stop;
    private readonly ComboBox _sensitivity;
    private readonly CheckBox _rotated;
    private readonly CheckBox _similar;
    private readonly ComboBox _action;
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Top, Height = 8, Minimum = 0, Maximum = 100 };
    private readonly Label _status = new() { Dock = DockStyle.Top, Height = 22, ForeColor = Color.Gray, Text = "Idle." };
    private readonly Label _summary = new() { Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold), Text = "No folder scanned yet." };
    private readonly ListView _groupList;
    private readonly Label _groupTitle = new() { Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold), UseMnemonic = false };
    private readonly FlowLayoutPanel _cards = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(248, 248, 248) };
    private readonly Button _move;
    private readonly Button _undo;
    private readonly Button _openReview;
    private readonly Button _saveReport;
    private readonly TextBox _log = new() { Dock = DockStyle.Bottom, Height = 56, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9f), WordWrap = false };

    private PictureCache _cache = new();
    private string _cacheRoot = "";
    private IReadOnlyList<PictureRecord> _records = [];
    private List<SimilarGroup> _groups = [];
    private readonly HashSet<string> _marked = new(StringComparer.OrdinalIgnoreCase);
    private int _current = -1;
    private int _showVersion;
    private int _thumbVersion;
    private bool _isSample;
    private Func<string, Bitmap?> _thumbnails = DefaultThumbnail;
    private PauseToken? _pause;
    private CancellationTokenSource? _cts;

    public SimilarPicturesControl(IFeatureContext context)
    {
        _context = context;
        _settingsStore = new SimilarPicturesSettingsStore(context.DataDirectory);
        _settings = _settingsStore.Load();
        _lastBatchFile = Path.Combine(context.DataDirectory, "last_move.json");

        Dock = DockStyle.Fill;
        BackColor = Color.White;
        _folder.Text = _settings.Root;
        _scan = MakeButton("Scan", OnScan);
        _pauseResume = MakeButton("Pause", OnPauseResume);
        _stop = MakeButton("Stop", () => { _cts?.Cancel(); _pause?.Resume(); });
        _move = MakeButton("Move marked", OnMove);
        _undo = MakeButton("Undo last move", OnUndo);
        _openReview = MakeButton("Open review folder", OpenReviewFolder);
        _saveReport = MakeButton("Save report...", OnSaveReport);
        _pauseResume.Enabled = _stop.Enabled = _move.Enabled = _saveReport.Enabled = false;
        _undo.Enabled = ReviewMover.LoadLastBatch(_lastBatchFile) is not null;

        _sensitivity = MakeChoice(SensitivityChoices.Select(c => c.Text), Array.FindIndex(SensitivityChoices, c => c.Value == _settings.Sensitivity));
        _rotated = MakeCheck("Turned / mirrored copies", _settings.IncludeRotated);
        _similar = MakeCheck("Similar scenes and bursts (you check each one)", _settings.IncludeSimilar);
        _action = MakeChoice(["the review folder (undo-able)", "the Recycle Bin"], _settings.Action == RemoveAction.RecycleBin ? 1 : 0);
        _groupList = BuildGroupList();

        WireEvents();
        BuildLayout();
    }

    /// <summary>
    /// Shows already-fingerprinted pictures without scanning, with thumbnails from
    /// <paramref name="thumbnails"/>. Used by the documentation screenshots (with
    /// <see cref="SamplePictureSet"/>, so no real photo appears) and by QA to drive the view.
    /// </summary>
    public void ShowRecords(string root, IReadOnlyList<PictureRecord> records, Func<string, Bitmap?> thumbnails)
    {
        _thumbnails = thumbnails;
        _isSample = true;
        _folder.Text = root;
        _cacheRoot = root;
        _records = records;
        Regroup(resetMarks: true);
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

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData is (Keys.Control | Keys.Right) or Keys.PageDown)
        {
            ShowGroup(Math.Min(_current + 1, _groups.Count - 1));
            return true;
        }
        if (keyData is (Keys.Control | Keys.Left) or Keys.PageUp)
        {
            ShowGroup(Math.Max(_current - 1, 0));
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ----------------------------------------------------------------- layout

    private void BuildLayout()
    {
        var review = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 0, 0) };
        review.Controls.Add(_cards);
        review.Controls.Add(BuildGroupButtons());
        review.Controls.Add(_groupTitle);

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterWidth = 5, FixedPanel = FixedPanel.Panel1 };
        split.Panel1.Controls.Add(_groupList);
        split.Panel2.Controls.Add(review);
        split.HandleCreated += (_, _) => split.SplitterDistance = Math.Min(330, Math.Max(200, split.Width / 3));

        Controls.Add(split);
        Controls.Add(_log);
        Controls.Add(BuildActionBar());
        Controls.Add(_summary);
        Controls.Add(_status);
        Controls.Add(_progress);
        Controls.Add(BuildOptionsRow());
        Controls.Add(BuildFolderRow());
        Controls.Add(BuildNote());
        Controls.Add(BuildTitle());
    }

    private static Label BuildTitle() => new()
    {
        Text = "Similar Pictures",
        Dock = DockStyle.Top,
        Height = 38,
        Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
        ForeColor = Color.FromArgb(33, 37, 43)
    };

    private static Label BuildNote() => new()
    {
        Dock = DockStyle.Top,
        Height = 40,
        ForeColor = Color.FromArgb(150, 80, 0),
        Text = "Finds pictures that look the same: copies, re-saved or resized versions, turned or mirrored ones, and burst shots. "
            + "Nothing moves until you click Move marked - and moved pictures go to a review folder you can undo. \"Similar - check\" groups are never pre-marked."
    };

    private TableLayoutPanel BuildFolderRow()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Top, Height = 38, ColumnCount = 4, RowCount = 1, Padding = new Padding(0, 4, 0, 0) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Controls.Add(new Label { Text = "Folder:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        row.Controls.Add(_folder, 1, 0);
        row.Controls.Add(MakeButton("Browse...", BrowseFolder), 2, 0);
        var commands = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(8, 0, 0, 0) };
        commands.Controls.AddRange([_scan, _pauseResume, _stop]);
        row.Controls.Add(commands, 3, 0);
        return row;
    }

    private FlowLayoutPanel BuildOptionsRow()
    {
        var row = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
        row.Controls.Add(new Label { Text = "Matching:", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        _sensitivity.Width = 220;
        row.Controls.Add(_sensitivity);
        row.Controls.Add(_rotated);
        row.Controls.Add(_similar);
        row.Controls.Add(new Label { Text = "Move to:", AutoSize = true, Margin = new Padding(10, 6, 4, 0) });
        _action.Width = 190;
        row.Controls.Add(_action);
        return row;
    }

    private FlowLayoutPanel BuildGroupButtons()
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, WrapContents = false };
        bar.Controls.Add(MakeButton("< Previous", () => ShowGroup(Math.Max(_current - 1, 0))));
        bar.Controls.Add(MakeButton("Next >", () => ShowGroup(Math.Min(_current + 1, _groups.Count - 1))));
        bar.Controls.Add(MakeButton("Mark all but the keeper", () => MarkCurrent(all: true)));
        bar.Controls.Add(MakeButton("Keep all in this group", () => MarkCurrent(all: false)));
        return bar;
    }

    private FlowLayoutPanel BuildActionBar()
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, WrapContents = false, Padding = new Padding(0, 6, 0, 2) };
        bar.Controls.AddRange([_move, _undo, _openReview, _saveReport]);
        return bar;
    }

    private static ListView BuildGroupList()
    {
        var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, Font = new Font("Segoe UI", 9f) };
        list.Columns.Add("#", 40, HorizontalAlignment.Right);
        list.Columns.Add("Match", 110);
        list.Columns.Add("Pics", 45, HorizontalAlignment.Right);
        list.Columns.Add("Marked", 70, HorizontalAlignment.Right);
        return list;
    }

    private static Button MakeButton(string text, Action onClick)
    {
        var button = new Button { Text = text, AutoSize = true, Padding = new Padding(10, 2, 10, 2), Margin = new Padding(0, 0, 6, 0), FlatStyle = FlatStyle.System, Cursor = Cursors.Hand };
        button.Click += (_, _) => onClick();
        return button;
    }

    private static CheckBox MakeCheck(string text, bool on) => new() { Text = text, Checked = on, AutoSize = true, Margin = new Padding(12, 5, 0, 0) };

    private static ComboBox MakeChoice(IEnumerable<string> items, int selected)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 2, 0, 0) };
        foreach (var item in items)
        {
            combo.Items.Add(item);
        }
        combo.SelectedIndex = Math.Max(0, selected);
        return combo;
    }

    private void WireEvents()
    {
        _groupList.SelectedIndexChanged += (_, _) =>
        {
            if (_groupList.SelectedIndices.Count > 0 && _groupList.SelectedIndices[0] != _current)
            {
                ShowGroup(_groupList.SelectedIndices[0]);
            }
        };
        _sensitivity.SelectedIndexChanged += (_, _) => OnOptionsChanged();
        _rotated.CheckedChanged += (_, _) => OnOptionsChanged();
        _similar.CheckedChanged += (_, _) => OnOptionsChanged();
        _action.SelectedIndexChanged += (_, _) =>
        {
            _settings.Action = _action.SelectedIndex == 1 ? RemoveAction.RecycleBin : RemoveAction.ReviewFolder;
            SaveSettings();
            UpdateMoveButton();
        };
    }

    // ----------------------------------------------------------------- scan

    private void BrowseFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = "Choose the folder with your pictures" };
        if (Directory.Exists(_folder.Text))
        {
            dlg.SelectedPath = _folder.Text;
        }
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _folder.Text = dlg.SelectedPath;
        }
    }

    private async void OnScan()
    {
        var root = _folder.Text.Trim();
        if (root.Length == 0 || !Directory.Exists(root))
        {
            Inform("Please choose a folder that exists.");
            return;
        }
        _settings.Root = root;
        SaveSettings();
        _thumbnails = DefaultThumbnail;
        _isSample = false;
        var cacheFile = PictureCache.PathFor(_context.DataDirectory, root);
        if (!string.Equals(_cacheRoot, root, StringComparison.OrdinalIgnoreCase))
        {
            _cache = PictureCache.Load(cacheFile);
            _cacheRoot = root;
        }

        BeginRun();
        try
        {
            var reader = PictureReader.Default();
            var request = new PictureScanRequest
            {
                Root = root,
                Cache = _cache,
                Read = reader.Read,
                ExcludeFolder = ReviewMover.ReviewFolderFor(root),
                Progress = new Progress<PictureScanProgress>(p =>
                {
                    _progress.Value = Math.Clamp(p.Percent, 0, 100);
                    _status.Text = string.Format(CultureInfo.InvariantCulture, "Scanning... {0:N0} of {1:N0} ({2:N0} read, {3:N0} already known)  {4}", p.Processed, p.Total, p.Read, p.Reused, p.Current);
                }),
                Log = new Progress<string>(AppendLog),
                Pause = _pause,
                Cancel = _cts!.Token,
                AutoSave = c => TrySave(c, cacheFile)
            };
            var result = await Task.Run(() => PictureScanner.Run(request));
            TrySave(_cache, cacheFile);
            _status.Text = string.Format(CultureInfo.InvariantCulture, "{0}: {1:N0} pictures ({2:N0} read, {3:N0} already known, {4:N0} unreadable).",
                result.Cancelled ? "Stopped" : "Done", result.Pictures, result.Read, result.Reused, result.Unreadable);
            AppendLog(_status.Text);
            _records = [.. _cache.Records];
            Regroup(resetMarks: true);
        }
        catch (Exception ex)
        {
            _status.Text = "Scan failed: " + ex.Message;
            AppendLog("Scan failed: " + ex);
            _context.Log("Similar Pictures scan failed: " + ex.Message);
        }
        finally
        {
            EndRun();
        }
    }

    private void TrySave(PictureCache cache, string file)
    {
        try
        {
            cache.Save(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            BeginInvoke(() => AppendLog("Could not save fingerprints: " + ex.Message));
        }
    }

    private void BeginRun()
    {
        _cts?.Dispose();
        _pause?.Dispose();
        _cts = new CancellationTokenSource();
        _pause = new PauseToken();
        _scan.Enabled = _move.Enabled = _undo.Enabled = false;
        _pauseResume.Enabled = _stop.Enabled = true;
        _pauseResume.Text = "Pause";
        _progress.Value = 0;
    }

    private void EndRun()
    {
        _scan.Enabled = true;
        _pauseResume.Enabled = _stop.Enabled = false;
        _undo.Enabled = ReviewMover.LoadLastBatch(_lastBatchFile) is not null;
        UpdateMoveButton();
    }

    private void OnPauseResume()
    {
        if (_pause is null)
        {
            return;
        }
        if (_pause.IsPaused)
        {
            _pause.Resume();
            _pauseResume.Text = "Pause";
        }
        else
        {
            _pause.Pause();
            _pauseResume.Text = "Resume";
            _status.Text = "Paused.";
        }
    }

    // ----------------------------------------------------------------- grouping + review

    private void OnOptionsChanged()
    {
        _settings.Sensitivity = SensitivityChoices[Math.Max(0, _sensitivity.SelectedIndex)].Value;
        _settings.IncludeRotated = _rotated.Checked;
        _settings.IncludeSimilar = _similar.Checked;
        SaveSettings();
        if (_records.Count > 0)
        {
            Regroup(resetMarks: true);
        }
    }

    /// <summary>Re-runs grouping from the cached fingerprints (no rescan) and shows the result.</summary>
    private async void Regroup(bool resetMarks)
    {
        var records = _records;
        var options = _settings.ToFindOptions();
        var version = ++_showVersion;
        var exact = !_isSample ? FileBytes.AreIdentical : (Func<string, string, bool>?)null;
        _summary.Text = "Grouping " + records.Count.ToString("N0", CultureInfo.InvariantCulture) + " pictures...";
        var groups = await Task.Run(() => SimilarityFinder.Find(records, options, exact));
        if (version != _showVersion)
        {
            return;
        }
        _groups = [.. groups];
        if (resetMarks)
        {
            _marked.Clear();
            foreach (var m in _groups.SelectMany(g => g.Members).Where(m => m.Suggested))
            {
                _marked.Add(m.Picture.Path);
            }
        }
        FillGroupList();
        ShowGroup(_groups.Count > 0 ? 0 : -1);
    }

    private void FillGroupList()
    {
        _groupList.BeginUpdate();
        _groupList.Items.Clear();
        foreach (var g in _groups)
        {
            var item = new ListViewItem([g.Id.ToString(CultureInfo.InvariantCulture), SimilarReportText.Label(g.Kind), g.Count.ToString(CultureInfo.InvariantCulture), MarkedIn(g).ToString(CultureInfo.InvariantCulture)])
            {
                ForeColor = g.Kind == MatchKind.Similar ? Color.FromArgb(150, 80, 0) : Color.FromArgb(33, 37, 43)
            };
            _groupList.Items.Add(item);
        }
        _groupList.EndUpdate();
        UpdateSummary();
    }

    private void ShowGroup(int index)
    {
        _current = index;
        ClearCards();
        if (index < 0 || index >= _groups.Count)
        {
            _groupTitle.Text = _records.Count == 0 ? "" : "No similar pictures found with these settings.";
            return;
        }
        var g = _groups[index];
        if (_groupList.Items.Count > index && !_groupList.Items[index].Selected)
        {
            _groupList.Items[index].Selected = true;
            _groupList.EnsureVisible(index);
        }
        _groupTitle.Text = string.Format(CultureInfo.InvariantCulture, "Group {0} of {1} - {2} - {3} pictures{4}",
            g.Id, _groups.Count, SimilarReportText.Label(g.Kind), g.Count,
            g.Kind == MatchKind.Similar ? "  (look closely: these may be different moments)" : "");

        var maxSharp = g.Members.Select(m => m.Picture.Sharpness).Append(g.Keeper.Sharpness).Max();
        _cards.SuspendLayout();
        AddCard(g.Keeper, null, maxSharp);
        foreach (var m in g.Members)
        {
            AddCard(m.Picture, m, maxSharp);
        }
        _cards.ResumeLayout();
        LoadThumbnails(++_thumbVersion);
    }

    private void AddCard(PictureRecord picture, GroupMember? member, double maxSharp)
    {
        var card = new PictureCardControl(picture, member, _marked.Contains(picture.Path), maxSharp);
        card.MarkChanged += (c, on) =>
        {
            if (on)
            {
                _marked.Add(c.Picture.Path);
            }
            else
            {
                _marked.Remove(c.Picture.Path);
            }
            RefreshCurrentRow();
        };
        card.KeeperRequested += p => ChangeKeeper(p.Path);
        card.OpenRequested += p => Shell(p.Path, select: false);
        card.ExplorerRequested += p => Shell(p.Path, select: true);
        _cards.Controls.Add(card);
    }

    private async void LoadThumbnails(int version)
    {
        var source = _thumbnails;
        foreach (var card in _cards.Controls.OfType<PictureCardControl>().ToList())
        {
            var path = card.Picture.Path;
            var image = await Task.Run(() => source(path));
            if (version != _thumbVersion || card.IsDisposed)
            {
                image?.Dispose();
                return;
            }
            card.SetThumbnail(image);
        }
    }

    private void ClearCards()
    {
        var old = _cards.Controls.OfType<Control>().ToList();
        _cards.Controls.Clear();
        foreach (var c in old)
        {
            c.Dispose();
        }
    }

    private void ChangeKeeper(string path)
    {
        if (_current < 0)
        {
            return;
        }
        _groups[_current] = GroupEdits.WithKeeper(_groups[_current], path);
        _marked.Remove(path);
        RefreshCurrentRow();
        ShowGroup(_current);
    }

    private void MarkCurrent(bool all)
    {
        if (_current < 0)
        {
            return;
        }
        foreach (var m in _groups[_current].Members)
        {
            if (all)
            {
                _marked.Add(m.Picture.Path);
            }
            else
            {
                _marked.Remove(m.Picture.Path);
            }
        }
        foreach (var card in _cards.Controls.OfType<PictureCardControl>())
        {
            card.SetMarked(_marked.Contains(card.Picture.Path));
        }
        RefreshCurrentRow();
    }

    private void RefreshCurrentRow()
    {
        if (_current >= 0 && _current < _groupList.Items.Count)
        {
            _groupList.Items[_current].SubItems[3].Text = MarkedIn(_groups[_current]).ToString(CultureInfo.InvariantCulture);
        }
        UpdateSummary();
    }

    private int MarkedIn(SimilarGroup g) => g.Members.Count(m => _marked.Contains(m.Picture.Path));

    private void UpdateSummary()
    {
        var plan = ReviewMover.Plan(_groups, _marked);
        _summary.Text = SimilarReportText.Summary(_records.Count, _groups, plan.Count, plan.Sum(p => p.Size));
        _saveReport.Enabled = _records.Count > 0;
        UpdateMoveButton();
    }

    private void UpdateMoveButton()
    {
        var plan = ReviewMover.Plan(_groups, _marked);
        _move.Text = string.Format(CultureInfo.InvariantCulture, "Move {0:N0} marked ({1}) to {2}", plan.Count, ByteSize.Format(plan.Sum(p => p.Size)),
            _settings.Action == RemoveAction.RecycleBin ? "the Recycle Bin" : "the review folder");
        _move.Enabled = plan.Count > 0 && _scan.Enabled && !_isSample;
    }

    // ----------------------------------------------------------------- move / undo / report

    private async void OnMove()
    {
        var plan = ReviewMover.Plan(_groups, _marked);
        var where = _settings.Action == RemoveAction.RecycleBin ? "the Recycle Bin" : "the review folder:\n" + ReviewMover.ReviewFolderFor(_cacheRoot);
        var text = string.Format(CultureInfo.InvariantCulture, "Move {0:N0} picture(s) ({1}) to {2}\n\nThe kept picture of every group stays where it is.", plan.Count, ByteSize.Format(plan.Sum(p => p.Size)), where);
        if (plan.Count == 0 || MessageBox.Show(FindForm(), text, "Similar Pictures", MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.OK)
        {
            return;
        }

        BeginRun();
        try
        {
            var request = new RemoveRequest { Root = _cacheRoot, Groups = _groups, Marked = _marked, Action = _settings.Action, Log = new Progress<string>(AppendLog), Pause = _pause, Cancel = _cts!.Token };
            var outcome = await Task.Run(() => ReviewMover.Run(request));
            if (outcome.Batch is { } batch)
            {
                ReviewMover.SaveLastBatch(_lastBatchFile, batch);
            }
            _status.Text = string.Format(CultureInfo.InvariantCulture, "Moved {0:N0} picture(s), {1}. Skipped {2}, failed {3}.", outcome.Removed, ByteSize.Format(outcome.Bytes), outcome.Skipped, outcome.Failed);
            AppendLog(_status.Text);
            ForgetMoved(plan.Select(p => p.Path));
        }
        catch (Exception ex)
        {
            Fail("Move failed", ex);
        }
        finally
        {
            EndRun();
        }
    }

    /// <summary>Drops pictures that are no longer in place from the cache and regroups what is left.</summary>
    private void ForgetMoved(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(p => !File.Exists(p)))
        {
            _cache.Remove(path);
            _marked.Remove(path);
        }
        TrySave(_cache, PictureCache.PathFor(_context.DataDirectory, _cacheRoot));
        _records = [.. _cache.Records];
        Regroup(resetMarks: false);
    }

    private void OnUndo()
    {
        if (ReviewMover.LoadLastBatch(_lastBatchFile) is not { } batch)
        {
            return;
        }
        var text = string.Format(CultureInfo.InvariantCulture, "Put back the {0:N0} picture(s) moved on {1}?", batch.Entries.Count, batch.WhenUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        if (MessageBox.Show(FindForm(), text, "Similar Pictures", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
        {
            return;
        }
        var (restored, skipped) = ReviewMover.Undo(batch, new Progress<string>(AppendLog));
        File.Delete(_lastBatchFile);
        _undo.Enabled = false;
        _status.Text = string.Format(CultureInfo.InvariantCulture, "Put back {0:N0} picture(s); {1:N0} could not be. Scan again to include them.", restored, skipped);
        AppendLog(_status.Text);
    }

    private void OpenReviewFolder()
    {
        var root = _cacheRoot.Length > 0 ? _cacheRoot : _folder.Text.Trim();
        var folder = ReviewMover.ReviewFolderFor(root);
        if (!Directory.Exists(folder))
        {
            Inform("There is no review folder yet - nothing has been moved from this folder.");
            return;
        }
        Shell(folder, select: false);
    }

    private void OnSaveReport()
    {
        using var dlg = new SaveFileDialog { Filter = "Text file (*.txt)|*.txt", FileName = "similar-pictures-report.txt", Title = "Save the Similar Pictures report" };
        if (dlg.ShowDialog() != DialogResult.OK)
        {
            return;
        }
        try
        {
            File.WriteAllText(dlg.FileName, SimilarReportText.Render(_cacheRoot, DateTime.UtcNow, _records.Count, _groups, _marked));
            AppendLog("Report saved: " + dlg.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Fail("Could not save the report", ex);
        }
    }

    // ----------------------------------------------------------------- helpers

    private static Bitmap? DefaultThumbnail(string path)
        => new ShellThumbnailProvider().GetThumbnail(path, CardThumbSize) ?? new GdiThumbnailProvider().GetThumbnail(path, CardThumbSize);

    private void Shell(string path, bool select)
    {
        try
        {
            var start = select
                ? new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true }
                : new ProcessStartInfo(path) { UseShellExecute = true };
            Process.Start(start);
        }
        catch (Exception ex)
        {
            AppendLog("Could not open " + path + ": " + ex.Message);
        }
    }

    private void SaveSettings()
    {
        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppendLog("Could not save settings: " + ex.Message);
        }
    }

    private void Inform(string text) => MessageBox.Show(FindForm(), text, "Similar Pictures", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void Fail(string what, Exception ex)
    {
        _status.Text = what + ": " + ex.Message;
        AppendLog(what + ": " + ex);
        _context.Log("Similar Pictures - " + what + ": " + ex.Message);
    }

    private void AppendLog(string line) => _log.AppendText(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + line + Environment.NewLine);
}
