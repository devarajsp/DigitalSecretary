using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.StorageStudio;

/// <summary>
/// The Storage Studio view (phase 1: Space Map). Pick a folder and Scan; the window then shows a
/// clickable size map of the focused folder, bar charts of space by type and by age, and tabs with
/// the largest files and folders, clean-up candidates and what changed since the previous scan.
/// Clicking a folder (on the map, in the breadcrumb or a list) re-focuses everything on it.
/// <b>Read-only:</b> nothing is deleted or moved; "Open in Explorer" hands off to Windows.
/// All analysis lives in the pure engine classes - this control only wires them to the UI.
/// </summary>
public sealed class StorageStudioControl : UserControl
{
    private const int ListRows = 100;

    private readonly IFeatureContext _context;
    private readonly StorageSettingsStore _settingsStore;
    private readonly SnapshotStore _snapshots;
    private readonly StorageSettings _settings;

    private readonly TextBox _folder = new() { Dock = DockStyle.Fill };
    private readonly Button _scan;
    private readonly Button _pauseResume;
    private readonly Button _stop;
    private readonly Button _saveReport;
    private readonly ComboBox _depth;
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Top, Height = 6, Style = ProgressBarStyle.Marquee, Visible = false };
    private readonly Label _status = new() { Dock = DockStyle.Top, Height = 22, ForeColor = Color.Gray, Text = "Idle." };
    private readonly FlowLayoutPanel _breadcrumb = new() { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = false };
    private readonly Label _summary = new() { Dock = DockStyle.Right, Width = 380, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold) };
    private readonly TreemapControl _map = new() { Dock = DockStyle.Fill };
    private readonly BarChartControl _byType = new("Space by type") { Dock = DockStyle.Top };
    private readonly BarChartControl _byAge = new("Space by age (last changed)") { Dock = DockStyle.Top };
    private readonly ListView _files = MakeList(("Size", 90, true), ("File", 520, false), ("Type", 130, false), ("Changed", 90, false));
    private readonly ListView _folders = MakeList(("Size", 90, true), ("Folder", 600, false), ("Files", 80, true));
    private readonly ListView _cleanup = MakeList(("Size", 90, true), ("Item", 620, false), ("Changed", 90, false));
    private readonly ListView _growth = MakeList(("Change", 100, true), ("Folder", 560, false), ("Before", 90, true), ("Now", 90, true));
    private readonly TextBox _log = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9f), WordWrap = false };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    private SpaceNode? _root;
    private SpaceNode? _focus;
    private SpaceReport? _report;
    private IReadOnlyList<GrowthEntry>? _growthEntries;
    private DateTime? _previousScanUtc;
    private PauseToken? _pause;
    private CancellationTokenSource? _cts;
    private int _refreshVersion;

    public StorageStudioControl(IFeatureContext context)
    {
        _context = context;
        _settingsStore = new StorageSettingsStore(context.DataDirectory);
        _snapshots = new SnapshotStore(context.DataDirectory);
        _settings = _settingsStore.Load();

        Dock = DockStyle.Fill;
        BackColor = Color.White;
        _folder.Text = _settings.Root;
        _scan = MakeButton("Scan", OnScan);
        _pauseResume = MakeButton("Pause", OnPauseResume);
        _stop = MakeButton("Stop", OnStop);
        _saveReport = MakeButton("Save report...", OnSaveReport);
        _pauseResume.Enabled = _stop.Enabled = _saveReport.Enabled = false;
        _depth = MakeDepthChoice(_settings.MapDepth);
        _map.MapDepth = _settings.MapDepth;
        _byType.ColorFor = (_, item) => CategoryPalette.For(CategoryForLabel(item.Label));
        _byAge.ColorFor = (i, _) => CategoryPalette.Tint(Color.FromArgb(0x1F, 0x4E, 0x79), i * 0.2);

        WireEvents();
        BuildLayout();
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

    // ----------------------------------------------------------------- layout

    private void BuildLayout()
    {
        _tabs.TabPages.Add(Page("Largest files", _files));
        _tabs.TabPages.Add(Page("Largest folders", _folders));
        _tabs.TabPages.Add(Page("Clean-up candidates", _cleanup));
        _tabs.TabPages.Add(Page("Changes since last scan", _growth));
        _tabs.TabPages.Add(Page("Log", _log));

        var side = new Panel { Dock = DockStyle.Right, Width = 430, Padding = new Padding(12, 4, 4, 4), AutoScroll = true };
        side.Controls.Add(_byAge);
        side.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12 });
        side.Controls.Add(_byType);

        // Map + charts above, lists below. The map gets ~60% of the height the first time the split
        // is sized; after that the user's own splitter position is kept.
        var body = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 5 };
        body.Panel1.Controls.Add(_map);
        body.Panel1.Controls.Add(side);
        body.Panel2.Controls.Add(_tabs);
        var sized = false;
        body.SizeChanged += (_, _) =>
        {
            if (!sized && body.Height > 200)
            {
                sized = true;
                body.SplitterDistance = (int)(body.Height * 0.6);
            }
        };

        Controls.Add(body);
        Controls.Add(BuildBreadcrumbRow());
        Controls.Add(_status);
        Controls.Add(_progress);
        Controls.Add(BuildFolderRow());
        Controls.Add(BuildNote());
        Controls.Add(BuildTitle());
    }

    private static Label BuildTitle() => new()
    {
        Text = "Storage Studio",
        Dock = DockStyle.Top,
        Height = 38,
        Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
        ForeColor = Color.FromArgb(33, 37, 43)
    };

    private static Label BuildNote() => new()
    {
        Dock = DockStyle.Top,
        Height = 40,
        ForeColor = Color.FromArgb(90, 90, 90),
        Text = "See what is using the space. Each rectangle is a folder or file, sized by how much it takes and coloured by type. "
            + "Click a folder to open it; right-click for Explorer. Nothing is deleted or moved - this view only looks."
    };

    private TableLayoutPanel BuildFolderRow()
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Top, Height = 40, ColumnCount = 5, RowCount = 1, Padding = new Padding(0, 4, 0, 0) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Controls.Add(new Label { Text = "Folder:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        row.Controls.Add(_folder, 1, 0);
        row.Controls.Add(MakeButton("Browse...", BrowseFolder), 2, 0);

        var commands = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(8, 0, 0, 0) };
        commands.Controls.AddRange([_scan, _pauseResume, _stop, _saveReport]);
        row.Controls.Add(commands, 3, 0);

        var depth = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        depth.Controls.Add(new Label { Text = "Map levels:", AutoSize = true, Margin = new Padding(8, 7, 2, 0) });
        depth.Controls.Add(_depth);
        row.Controls.Add(depth, 4, 0);
        return row;
    }

    private Panel BuildBreadcrumbRow()
    {
        var row = new Panel { Dock = DockStyle.Top, Height = 30, Padding = new Padding(0, 2, 0, 2) };
        row.Controls.Add(_breadcrumb);
        row.Controls.Add(_summary);
        return row;
    }

    private static TabPage Page(string title, Control content)
    {
        var page = new TabPage(title);
        page.Controls.Add(content);
        return page;
    }

    private static ListView MakeList(params (string Title, int Width, bool Right)[] columns)
    {
        var list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, Font = new Font("Segoe UI", 9f) };
        foreach (var (title, width, right) in columns)
        {
            list.Columns.Add(title, width, right ? HorizontalAlignment.Right : HorizontalAlignment.Left);
        }
        return list;
    }

    private static Button MakeButton(string text, Action onClick)
    {
        var button = new Button { Text = text, AutoSize = true, Padding = new Padding(10, 2, 10, 2), Margin = new Padding(0, 0, 6, 0), FlatStyle = FlatStyle.System, Cursor = Cursors.Hand };
        button.Click += (_, _) => onClick();
        return button;
    }

    private static ComboBox MakeDepthChoice(int selected)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 48, Margin = new Padding(0, 3, 0, 0) };
        combo.Items.AddRange(["1", "2", "3"]);
        combo.SelectedIndex = Math.Clamp(selected, 1, 3) - 1;
        return combo;
    }

    private void WireEvents()
    {
        _map.FolderOpened += SetFocus;
        _map.NodeMenuRequested += ShowNodeMenu;
        _depth.SelectedIndexChanged += (_, _) =>
        {
            _settings.MapDepth = _depth.SelectedIndex + 1;
            _map.MapDepth = _settings.MapDepth;
            SaveSettings();
        };
        foreach (var list in new[] { _files, _folders, _cleanup })
        {
            list.DoubleClick += (_, _) => OnListActivate(list);
            list.MouseClick += (_, e) => OnListRightClick(list, e);
        }
    }

    // ----------------------------------------------------------------- scan

    private void BrowseFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = "Choose the folder to map" };
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
            MessageBox.Show(FindForm(), "Please choose a folder that exists.", "Storage Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _settings.Root = root;
        SaveSettings();
        BeginRun();
        try
        {
            var request = new SpaceScanRequest
            {
                Root = root,
                Progress = new Progress<SpaceScanProgress>(ShowScanProgress),
                Log = new Progress<string>(AppendLog),
                Pause = _pause,
                Cancel = _cts!.Token
            };
            var result = await Task.Run(() => SpaceScanner.Run(request));
            AcceptScan(result);
        }
        catch (Exception ex)
        {
            _status.Text = "Scan failed: " + ex.Message;
            AppendLog("Scan failed: " + ex);
            _context.Log("Storage Studio scan failed: " + ex.Message);
        }
        finally
        {
            EndRun();
        }
    }

    /// <summary>Takes a finished (or stopped) scan: compares and saves the snapshot, then shows it.</summary>
    private void AcceptScan(SpaceScanResult result)
    {
        _root = result.Root;
        var now = DateTime.UtcNow;
        _growthEntries = null;
        _previousScanUtc = null;

        if (!result.Cancelled)
        {
            var snapshot = SpaceSnapshot.From(result.Root, now);
            if (_snapshots.Load(result.Root.Name) is { } previous)
            {
                _growthEntries = GrowthComparer.Compare(previous, snapshot, 1L << 20, ListRows);
                _previousScanUtc = previous.TakenUtc;
            }
            TrySaveSnapshot(snapshot);
        }

        var words = result.Cancelled ? "Stopped - showing what was found so far" : "Done";
        _status.Text = string.Format(CultureInfo.InvariantCulture, "{0}: {1} files, {2} folders, {3} in {4:0.0}s. {5} unreadable folder(s), {6} link(s) skipped.",
            words, N(result.Root.FileCount), N(result.Root.FolderCount), ByteSize.Format(result.Root.Size),
            result.Elapsed.TotalSeconds, result.Errors, result.SkippedLinks);
        AppendLog(_status.Text);
        FillGrowth();
        SetFocus(result.Root);
    }

    private void TrySaveSnapshot(SpaceSnapshot snapshot)
    {
        try
        {
            _snapshots.Save(snapshot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppendLog("Could not save the snapshot for next time: " + ex.Message);
        }
    }

    private void ShowScanProgress(SpaceScanProgress p)
        => _status.Text = string.Format(CultureInfo.InvariantCulture, "Scanning... {0} files, {1} folders, {2}  {3}",
            N(p.Files), N(p.Folders), ByteSize.Format(p.Bytes), p.Current);

    private void BeginRun()
    {
        _cts?.Dispose();
        _pause?.Dispose();
        _cts = new CancellationTokenSource();
        _pause = new PauseToken();
        _scan.Enabled = _saveReport.Enabled = false;
        _pauseResume.Enabled = _stop.Enabled = true;
        _pauseResume.Text = "Pause";
        _progress.Visible = true;
        _status.Text = "Scanning...";
    }

    private void EndRun()
    {
        _scan.Enabled = true;
        _pauseResume.Enabled = _stop.Enabled = false;
        _pauseResume.Text = "Pause";
        _progress.Visible = false;
        _saveReport.Enabled = _root is not null;
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
            _progress.Style = ProgressBarStyle.Marquee;
        }
        else
        {
            _pause.Pause();
            _pauseResume.Text = "Resume";
            _progress.Style = ProgressBarStyle.Continuous;
            _status.Text = "Paused.";
        }
    }

    private void OnStop()
    {
        _cts?.Cancel();
        _pause?.Resume();
    }

    // ----------------------------------------------------------------- focus + views

    /// <summary>
    /// Shows an already-built tree without scanning or snapshotting. Used by the documentation
    /// screenshots with <see cref="SampleSpaceTree"/>, and by QA to drive the view.
    /// </summary>
    public void ShowTree(SpaceNode root)
    {
        _root = root;
        _growthEntries = null;
        _previousScanUtc = null;
        _folder.Text = root.FullPath;
        _status.Text = "Showing " + ByteSize.Format(root.Size) + " in " + N(root.FileCount) + " files.";
        _saveReport.Enabled = true;
        FillGrowth();
        SetFocus(root);
    }

    /// <summary>Makes <paramref name="folder"/> the focus of the map, charts and lists.</summary>
    private async void SetFocus(SpaceNode folder)
    {
        _focus = folder;
        _map.FocusNode = folder;
        BuildBreadcrumb(folder);
        _summary.Text = ByteSize.Format(folder.Size) + "  ·  " + N(folder.FileCount) + " files  ·  " + N(folder.FolderCount) + " folders";

        // Re-asking the questions walks the subtree; do it off the UI thread and drop stale answers.
        var version = ++_refreshVersion;
        var report = await Task.Run(() => SpaceReport.For(folder, DateTime.UtcNow, _growthEntries, _previousScanUtc, ListRows));
        if (version != _refreshVersion)
        {
            return;
        }
        _report = report;
        _byType.Items = report.ByType;
        _byAge.Items = report.ByAge;
        FillFiles(report.LargestFiles);
        FillFolders(report.LargestFolders);
        FillCleanup(report.Cleanup);
    }

    private void BuildBreadcrumb(SpaceNode folder)
    {
        _breadcrumb.SuspendLayout();
        _breadcrumb.Controls.Clear();
        var chain = new List<SpaceNode>();
        for (var node = folder; node is not null; node = node.Parent)
        {
            chain.Insert(0, node);
        }
        if (folder.Parent is { } parent)
        {
            _breadcrumb.Controls.Add(MakeButton("Up", () => SetFocus(parent)));
        }
        foreach (var node in chain)
        {
            var link = new LinkLabel { Text = node.Name, AutoSize = true, Margin = new Padding(2, 6, 0, 0), LinkColor = Color.FromArgb(0, 90, 160) };
            link.LinkClicked += (_, _) => SetFocus(node);
            _breadcrumb.Controls.Add(link);
            if (node != folder)
            {
                _breadcrumb.Controls.Add(new Label { Text = ">", AutoSize = true, Margin = new Padding(2, 6, 0, 0), ForeColor = Color.Gray });
            }
        }
        _breadcrumb.ResumeLayout();
    }

    private void FillFiles(IReadOnlyList<SpaceNode> files)
        => Fill(_files, files.Select(f => Row(f, ByteSize.Format(f.Size), f.FullPath, FileTypeClassifier.Label(f.Category), Date(f.LastWriteUtc))));

    private void FillFolders(IReadOnlyList<SpaceNode> folders)
        => Fill(_folders, folders.Select(f => Row(f, ByteSize.Format(f.Size), f.FullPath, N(f.FileCount))));

    private void FillCleanup(IReadOnlyList<CleanupGroup> groups)
    {
        _cleanup.BeginUpdate();
        _cleanup.Items.Clear();
        _cleanup.Groups.Clear();
        foreach (var group in groups)
        {
            var header = new ListViewGroup(group.Title + " - " + ByteSize.Format(group.Bytes) + " in " + N(group.Items.Count) + " item(s).  " + group.Why);
            _cleanup.Groups.Add(header);
            foreach (var item in group.Items.Take(ListRows))
            {
                var row = Row(item, ByteSize.Format(item.Size), item.FullPath, Date(item.LastWriteUtc));
                row.Group = header;
                _cleanup.Items.Add(row);
            }
        }
        if (groups.Count == 0)
        {
            _cleanup.Items.Add(new ListViewItem(["", "No common clean-up candidates found here."]));
        }
        _cleanup.EndUpdate();
    }

    private void FillGrowth()
    {
        IEnumerable<ListViewItem> rows = _growthEntries is null
            ? [new ListViewItem(["", "No earlier scan of this folder to compare with - scan it again later to see what grew."])]
            : _growthEntries.Count == 0
                ? [new ListViewItem(["", "Nothing changed by 1 MB or more since " + Date(_previousScanUtc!.Value) + "."])]
                : _growthEntries.Select(g => new ListViewItem([SpaceReportText.WithSign(g.Delta), g.RelativePath, ByteSize.Format(g.OldBytes), ByteSize.Format(g.NewBytes)])
                    { ForeColor = g.Delta > 0 ? Color.FromArgb(170, 40, 30) : Color.FromArgb(20, 120, 60) });
        Fill(_growth, rows);
    }

    private static void Fill(ListView list, IEnumerable<ListViewItem> rows)
    {
        list.BeginUpdate();
        list.Items.Clear();
        list.Items.AddRange([.. rows]);
        list.EndUpdate();
    }

    private static ListViewItem Row(SpaceNode node, params string[] cells) => new(cells) { Tag = node };

    // ----------------------------------------------------------------- actions

    private void OnListActivate(ListView list)
    {
        if (list.SelectedItems.Count == 0 || list.SelectedItems[0].Tag is not SpaceNode node)
        {
            return;
        }
        var folder = node.IsFolder ? node : node.Parent;
        if (folder is not null)
        {
            SetFocus(folder);
        }
    }

    private void OnListRightClick(ListView list, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right && list.GetItemAt(e.X, e.Y)?.Tag is SpaceNode node)
        {
            ShowNodeMenu(node, list.PointToScreen(e.Location));
        }
    }

    private void ShowNodeMenu(SpaceNode node, Point screen)
    {
        var menu = new ContextMenuStrip();
        if (node.IsFolder)
        {
            menu.Items.Add("Open this folder in the map", null, (_, _) => SetFocus(node));
        }
        menu.Items.Add("Show in Explorer", null, (_, _) => ShowInExplorer(node.FullPath));
        menu.Items.Add("Copy path", null, (_, _) => Clipboard.SetText(node.FullPath));
        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
        menu.Show(screen);
    }

    private void ShowInExplorer(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppendLog("Could not open Explorer: " + ex.Message);
        }
    }

    private void OnSaveReport()
    {
        if (_report is null)
        {
            return;
        }
        using var dlg = new SaveFileDialog { Filter = "Text file (*.txt)|*.txt", FileName = "storage-report.txt", Title = "Save the Storage Studio report" };
        if (dlg.ShowDialog() != DialogResult.OK)
        {
            return;
        }
        try
        {
            File.WriteAllText(dlg.FileName, SpaceReportText.Render(_report));
            AppendLog("Report saved: " + dlg.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(FindForm(), "Could not save the report: " + ex.Message, "Storage Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ----------------------------------------------------------------- helpers

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

    private void AppendLog(string line) => _log.AppendText(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + line + Environment.NewLine);

    private static FileCategory CategoryForLabel(string label)
        => Enum.GetValues<FileCategory>().FirstOrDefault(c => FileTypeClassifier.Label(c) == label, FileCategory.Other);

    private static string Date(DateTime utc) => utc == default ? "" : utc.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string N(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
