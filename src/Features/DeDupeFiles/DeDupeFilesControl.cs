using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.DeDupeFiles;

/// <summary>
/// The DeDupe Files view: pick a folder, build or refresh its MD5 checksum list, see what is
/// duplicated and how much space the duplicates cost, then remove them - keeping one copy of each.
/// All the thinking lives in the engine classes; this control only wires them to the UI, keeps the
/// checksum index in memory between steps, and owns the Pause / Stop controls.
/// </summary>
public sealed class DeDupeFilesControl : UserControl
{
    private const int MaxDisplayRows = 5000;

    private static readonly (ExistingListAction Value, string Text)[] ExistingChoices =
    [
        (ExistingListAction.Ask, "Ask me each time"),
        (ExistingListAction.Update, "Update it (re-scan changed files)"),
        (ExistingListAction.UseAsIs, "Use it as it is (no scan)")
    ];

    private static readonly (KeeperPolicy Value, string Text)[] KeeperChoices =
    [
        (KeeperPolicy.ShortestPath, "The copy with the shortest path"),
        (KeeperPolicy.OldestModified, "The oldest copy"),
        (KeeperPolicy.NewestModified, "The newest copy"),
        (KeeperPolicy.FirstAlphabetical, "The first in alphabetical order")
    ];

    private readonly IFeatureContext _context;
    private readonly DeDupeSettingsStore _settingsStore;

    private readonly TextBox _folder;
    private readonly Button _browse;
    private readonly ComboBox _onExisting;
    private readonly ComboBox _keeper;
    private readonly CheckBox _ignoreEmpty;
    private readonly CheckBox _verifyBeforeDelete;
    private readonly CheckBox _recycleBin;
    private readonly CheckBox _dryRun;
    private readonly Button _scan;
    private readonly Button _analyze;
    private readonly Button _delete;
    private readonly Button _pauseResume;
    private readonly Button _stop;
    private readonly Button _saveReport;
    private readonly ProgressBar _progress;
    private readonly Label _status;
    private readonly Label _summary;
    private readonly ListView _duplicates;
    private readonly TextBox _log;

    private ChecksumIndex _index = new();
    private DuplicateReport? _report;
    private DeleteOutcome? _outcome;
    private string _indexRoot = "";
    private PauseToken? _pause;
    private CancellationTokenSource? _cts;

    public DeDupeFilesControl(IFeatureContext context)
    {
        _context = context;
        _settingsStore = new DeDupeSettingsStore(context.DataDirectory);
        var saved = _settingsStore.Load();

        Dock = DockStyle.Fill;
        BackColor = Color.White;

        _folder = new TextBox { Dock = DockStyle.Fill, Text = saved.Root };
        _browse = MakeButton("Browse...", BrowseFolder);
        _onExisting = MakeChoice(ExistingChoices.Select(c => c.Text),
            Array.FindIndex(ExistingChoices, c => c.Value == saved.OnExistingList));
        _keeper = MakeChoice(KeeperChoices.Select(c => c.Text),
            Array.FindIndex(KeeperChoices, c => c.Value == saved.Keeper));

        _ignoreEmpty = MakeCheck("Ignore empty files", saved.IgnoreEmptyFiles);
        _verifyBeforeDelete = MakeCheck("Verify byte-for-byte before deleting", saved.VerifyBeforeDelete);
        _recycleBin = MakeCheck("Send to Recycle Bin", saved.RecycleBin);
        _dryRun = MakeCheck("Dry run (delete nothing)", saved.DryRun);

        _scan = MakeButton("Scan", OnScan);
        _analyze = MakeButton("Analyze", OnAnalyze);
        _delete = MakeButton("Delete duplicates", OnDelete);
        _pauseResume = MakeButton("Pause", OnPauseResume);
        _stop = MakeButton("Stop", OnStop);
        _saveReport = MakeButton("Save report...", OnSaveReport);
        _analyze.Enabled = false;
        _delete.Enabled = false;
        _saveReport.Enabled = false;
        _pauseResume.Enabled = false;
        _stop.Enabled = false;

        _progress = new ProgressBar { Dock = DockStyle.Top, Height = 22, Minimum = 0, Maximum = 100 };
        _status = new Label { Dock = DockStyle.Top, Height = 24, ForeColor = Color.Gray, Text = "Idle." };
        _summary = new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 37, 43),
            Text = "No folder scanned yet."
        };
        _duplicates = BuildDuplicateList();
        _log = BuildLog();

        Controls.Add(BuildResults());
        Controls.Add(_summary);
        Controls.Add(_status);
        Controls.Add(_progress);
        Controls.Add(BuildCommandBar());
        Controls.Add(BuildOptions());
        Controls.Add(BuildFolderRow());
        Controls.Add(BuildNote());
        Controls.Add(BuildTitle());
    }

    private ExistingListAction SelectedExisting
        => ExistingChoices[Math.Max(_onExisting.SelectedIndex, 0)].Value;

    private KeeperPolicy SelectedKeeper
        => KeeperChoices[Math.Max(_keeper.SelectedIndex, 0)].Value;

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

    private static Label BuildTitle() => new()
    {
        Text = "DeDupe Files",
        Dock = DockStyle.Top,
        Height = 38,
        Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
        ForeColor = Color.FromArgb(33, 37, 43)
    };

    private static Label BuildNote() => new()
    {
        Dock = DockStyle.Top,
        Height = 76,
        ForeColor = Color.FromArgb(150, 80, 0),
        Text = "Finds files whose contents are identical and frees the space the extra copies take.\n"
            + "1) Scan writes an MD5 checksum list (" + ChecksumListFile.FileName + ") into the folder; "
            + "a second scan only re-reads files that changed.\n"
            + "2) Analyze shows each set of duplicates and what you would save.\n"
            + "3) Delete removes the extra copies - one copy of every set is always kept. "
            + "Dry run is on until you turn it off."
    };

    private TableLayoutPanel BuildFolderRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(0, 4, 0, 0)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        row.Controls.Add(MakeLabel("Folder to scan:"), 0, 0);
        row.Controls.Add(_folder, 1, 0);
        row.Controls.Add(_browse, 2, 0);
        return row;
    }

    private TableLayoutPanel BuildOptions()
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 76,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(0, 4, 0, 0)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        grid.Controls.Add(MakeLabel("When a list exists:"), 0, 0);
        grid.Controls.Add(_onExisting, 1, 0);
        grid.Controls.Add(MakeLabel("Keep:"), 2, 0);
        grid.Controls.Add(_keeper, 3, 0);

        var checks = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(0) };
        checks.Controls.Add(_dryRun);
        checks.Controls.Add(_recycleBin);
        checks.Controls.Add(_verifyBeforeDelete);
        checks.Controls.Add(_ignoreEmpty);
        grid.Controls.Add(checks, 0, 1);
        grid.SetColumnSpan(checks, 4);
        return grid;
    }

    private FlowLayoutPanel BuildCommandBar()
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 46, Padding = new Padding(0, 6, 0, 4) };
        bar.Controls.Add(_scan);
        bar.Controls.Add(_analyze);
        bar.Controls.Add(_delete);
        bar.Controls.Add(_pauseResume);
        bar.Controls.Add(_stop);
        bar.Controls.Add(_saveReport);
        return bar;
    }

    /// <summary>
    /// Duplicate grid above, log below. The log is docked to a fixed height rather than sharing a
    /// split, so it cannot be squeezed out of sight when the window is short; the splitter between
    /// them still lets the user trade space either way.
    /// </summary>
    private Panel BuildResults()
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        panel.Controls.Add(_duplicates);
        panel.Controls.Add(new Splitter { Dock = DockStyle.Bottom, Height = 4, MinExtra = 80, MinSize = 60 });
        panel.Controls.Add(_log);
        return panel;
    }

    private static ListView BuildDuplicateList()
    {
        var list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            ShowGroups = true,
            HideSelection = false,
            Font = new Font("Segoe UI", 9f)
        };
        list.Columns.Add("Action", 70);
        list.Columns.Add("Size", 90, HorizontalAlignment.Right);
        list.Columns.Add("File", 620);
        list.Columns.Add("MD5", 240);
        return list;
    }

    private static TextBox BuildLog() => new()
    {
        Dock = DockStyle.Bottom,
        Height = 140,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        BackColor = Color.FromArgb(250, 250, 250),
        Font = new Font("Consolas", 9f),
        WordWrap = false
    };

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static CheckBox MakeCheck(string text, bool checkedState) => new()
    {
        Text = text,
        Checked = checkedState,
        AutoSize = true,
        Margin = new Padding(0, 6, 18, 0)
    };

    private static ComboBox MakeChoice(IEnumerable<string> items, int selected)
    {
        var combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var item in items)
        {
            combo.Items.Add(item);
        }
        combo.SelectedIndex = selected < 0 ? 0 : selected;
        return combo;
    }

    private static Button MakeButton(string text, Action onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Padding = new Padding(12, 4, 12, 4),
            Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.System,
            Cursor = Cursors.Hand
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    // ----------------------------------------------------------------- step 1: scan

    private void BrowseFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = "Choose the folder to de-duplicate" };
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

        SaveSettings();
        var listPath = ChecksumListFile.PathIn(root);
        LoadIndex(root, listPath);
        var action = SelectedExisting;

        if (_index.Count > 0 && File.Exists(listPath))
        {
            if (action == ExistingListAction.Ask)
            {
                var answer = ExistingListPrompt.Ask(FindForm(), listPath, _index.Count);
                if (answer is null)
                {
                    AppendLog("Cancelled - the list was left untouched.");
                    return;
                }
                action = answer.Value;
            }

            if (action == ExistingListAction.UseAsIs)
            {
                AppendLog("Using the saved list: " + Count(_index.Count) + " checksum(s), nothing re-read.");
                ShowLoadedSummary();
                _analyze.Enabled = _index.Count > 0;
                return;
            }
        }

        await RunScanAsync(root, listPath);
    }

    private void LoadIndex(string root, string listPath)
    {
        if (string.Equals(_indexRoot, root, StringComparison.OrdinalIgnoreCase) && _index.Count > 0)
        {
            return;
        }

        try
        {
            _index = ChecksumIndex.FromEntries(ChecksumListFile.Read(listPath));
        }
        catch (IOException ex)
        {
            AppendLog("Could not read the existing list (" + ex.Message + ") - starting a fresh one.");
            _index = new ChecksumIndex();
        }

        _indexRoot = root;
        _report = null;
        _outcome = null;
    }

    private async Task RunScanAsync(string root, string listPath)
    {
        BeginRun("Scanning...");
        try
        {
            var engine = new ScanEngine(new Md5FileHasher());
            var request = new ScanRequest
            {
                Root = root,
                Index = _index,
                Progress = new Progress<ScanProgress>(ShowScanProgress),
                Log = new Progress<string>(AppendLog),
                Pause = _pause,
                Cancel = _cts!.Token,
                AutoSave = index => ChecksumListFile.Write(listPath, root, index.Entries)
            };

            var result = await Task.Run(() => engine.Run(request), _cts.Token);
            ChecksumListFile.Write(listPath, root, _index.Entries);
            ReportScanResult(result, listPath);
            _analyze.Enabled = _index.Count > 0;
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Stopped.";
            AppendLog("Scan stopped. The checksum list keeps the work done so far.");
        }
        catch (Exception ex)
        {
            Fail("Scan failed", ex);
        }
        finally
        {
            EndRun();
        }
    }

    private void ShowScanProgress(ScanProgress progress)
    {
        _progress.Value = Math.Clamp(progress.Percent, 0, 100);
        _status.Text = "Scanning " + Count(progress.Processed) + " / " + Count(progress.Total)
            + "  (hashed " + Count(progress.Hashed) + ", reused " + Count(progress.Reused) + ")";
    }

    private void ReportScanResult(ScanResult result, string listPath)
    {
        _status.Text = result.Cancelled ? "Scan stopped." : "Scan complete.";
        _summary.Text = "Files: " + Count(result.FilesSeen) + "  (" + ByteSize.Format(result.TotalBytes)
            + ")   hashed " + Count(result.Hashed) + "   reused " + Count(result.Reused)
            + "   pruned " + Count(result.Removed)
            + (result.Errors > 0 ? "   unreadable " + Count(result.Errors) : "");
        AppendLog("Checksum list saved: " + listPath + " (" + Count(_index.Count) + " entries).");
        _context.Log("DeDupe Files: scanned " + result.FilesSeen + " file(s) in " + _indexRoot);
    }

    private void ShowLoadedSummary()
        => _summary.Text = "Loaded " + Count(_index.Count) + " checksum(s) from the saved list  ("
            + ByteSize.Format(_index.TotalBytes) + ").";

    // ----------------------------------------------------------------- step 2: analyze

    private async void OnAnalyze()
    {
        if (_index.Count == 0)
        {
            Inform("Scan a folder first so there are checksums to compare.");
            return;
        }

        BeginRun("Analyzing...");
        try
        {
            var index = _index;
            var policy = SelectedKeeper;
            var ignoreEmpty = _ignoreEmpty.Checked;
            var report = await Task.Run(() => DuplicateAnalyzer.Analyze(index, policy, ignoreEmpty));

            _report = report;
            _outcome = null;
            ShowDuplicates(report);
            _delete.Enabled = report.DuplicateFiles > 0;
            _saveReport.Enabled = true;
            _status.Text = report.IsClean ? "No duplicates found." : "Analysis complete.";
            AppendLog(report.IsClean
                ? "No duplicate files in " + _indexRoot + "."
                : Count(report.GroupCount) + " duplicate set(s), " + Count(report.DuplicateFiles)
                  + " removable copy(ies), " + ByteSize.Format(report.ReclaimableBytes) + " reclaimable.");
        }
        catch (Exception ex)
        {
            Fail("Analysis failed", ex);
        }
        finally
        {
            EndRun();
        }
    }

    /// <summary>
    /// Fills the grid: one group per set of identical files, the kept copy first and the removable
    /// copies under it. Very large results are capped so the window stays responsive - the saved
    /// report always holds every row.
    /// </summary>
    private void ShowDuplicates(DuplicateReport report)
    {
        _duplicates.BeginUpdate();
        try
        {
            _duplicates.Items.Clear();
            _duplicates.Groups.Clear();

            var rows = 0;
            foreach (var group in report.Groups)
            {
                if (rows + group.Copies > MaxDisplayRows)
                {
                    AppendLog("Showing the first " + Count(rows) + " row(s) only - use Save report for the full list.");
                    break;
                }

                var header = group.Copies + " copies - " + ByteSize.Format(group.Length) + " each - reclaim "
                    + ByteSize.Format(group.Reclaimable);
                var lvGroup = new ListViewGroup(header);
                _duplicates.Groups.Add(lvGroup);
                _duplicates.Items.Add(MakeRow("KEEP", group.Keeper, group.Md5, lvGroup, keep: true));
                foreach (var duplicate in group.Duplicates)
                {
                    _duplicates.Items.Add(MakeRow("delete", duplicate, group.Md5, lvGroup, keep: false));
                }
                rows += group.Copies;
            }
        }
        finally
        {
            _duplicates.EndUpdate();
        }

        ShowAnalysisSummary(report);
    }

    private static ListViewItem MakeRow(
        string action, DuplicateFile file, string md5, ListViewGroup group, bool keep)
    {
        var item = new ListViewItem([action, ByteSize.Format(file.Length), file.Path, md5])
        {
            Group = group,
            ForeColor = keep ? Color.FromArgb(0, 110, 40) : Color.FromArgb(150, 40, 40),
            ToolTipText = file.Path
        };
        return item;
    }

    private void ShowAnalysisSummary(DuplicateReport report)
        => _summary.Text = "Files: " + Count(report.TotalFiles) + " (" + ByteSize.Format(report.TotalBytes)
            + ")   duplicate sets: " + Count(report.GroupCount)
            + "   removable copies: " + Count(report.DuplicateFiles)
            + "   duplicate size: " + ByteSize.Format(report.DuplicateBytes)
            + "   you can save: " + ByteSize.Format(report.ReclaimableBytes);

    // ----------------------------------------------------------------- step 3: delete

    private async void OnDelete()
    {
        if (_report is null || _report.DuplicateFiles == 0)
        {
            Inform("Run Analyze first so there is something to remove.");
            return;
        }

        var dryRun = _dryRun.Checked;
        if (!dryRun && !ConfirmDeletion(_report))
        {
            return;
        }

        SaveSettings();
        BeginRun(dryRun ? "Dry run..." : "Deleting...");
        try
        {
            var engine = new DeleteEngine(
                _recycleBin.Checked ? new RecycleBinFileDeleter() : new PermanentFileDeleter(),
                _verifyBeforeDelete.Checked ? FileComparer.AreIdentical : null);
            var request = new DeleteRequest
            {
                Groups = _report.Groups,
                DryRun = dryRun,
                Index = _index,
                Progress = new Progress<DeleteProgress>(ShowDeleteProgress),
                Log = new Progress<string>(AppendLog),
                Pause = _pause,
                Cancel = _cts!.Token
            };

            var outcome = await Task.Run(() => engine.Run(request), _cts.Token);
            _outcome = outcome;
            ReportDeleteOutcome(outcome);
            if (!dryRun)
            {
                await AfterDeletionAsync();
            }
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Stopped.";
        }
        catch (Exception ex)
        {
            Fail("Deleting failed", ex);
        }
        finally
        {
            EndRun();
        }
    }

    private bool ConfirmDeletion(DuplicateReport report)
    {
        var where = _recycleBin.Checked
            ? "They go to the Recycle Bin, so you can get them back."
            : "They are deleted permanently and CANNOT be recovered.";
        var answer = MessageBox.Show(
            "Delete " + Count(report.DuplicateFiles) + " duplicate file(s) and free about "
            + ByteSize.Format(report.ReclaimableBytes) + "?\n\n"
            + "One copy of every set is kept.\n" + where
            + (_verifyBeforeDelete.Checked
                ? "\nEach file is checked byte-for-byte against the copy being kept first."
                : "\nByte-for-byte verification is OFF - only the checksums are compared."),
            "Delete duplicates?",
            MessageBoxButtons.OKCancel,
            _recycleBin.Checked ? MessageBoxIcon.Question : MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        return answer == DialogResult.OK;
    }

    private void ShowDeleteProgress(DeleteProgress progress)
    {
        _progress.Value = Math.Clamp(progress.Percent, 0, 100);
        _status.Text = Count(progress.Processed) + " / " + Count(progress.Total)
            + "  freed " + ByteSize.Format(progress.BytesFreed);
    }

    private void ReportDeleteOutcome(DeleteOutcome outcome)
    {
        var verb = outcome.DryRun ? "Would delete" : "Deleted";
        _status.Text = outcome.Cancelled ? "Stopped." : outcome.DryRun ? "Dry run complete." : "Done.";
        _summary.Text = verb + ": " + Count(outcome.Deleted)
            + "   " + (outcome.DryRun ? "would free" : "freed") + ": " + ByteSize.Format(outcome.BytesFreed)
            + "   skipped: " + Count(outcome.Skipped)
            + "   failed: " + Count(outcome.Failed);
        AppendLog(_summary.Text);
        if (outcome.DryRun)
        {
            AppendLog("Nothing was removed. Untick \"Dry run\" and click Delete duplicates to do it for real.");
        }
        else
        {
            _context.Log("DeDupe Files: deleted " + outcome.Deleted + " duplicate(s), freed "
                + outcome.BytesFreed + " byte(s)");
        }
    }

    /// <summary>Rewrites the checksum list and refreshes the grid so it reflects what is left.</summary>
    private async Task AfterDeletionAsync()
    {
        try
        {
            ChecksumListFile.Write(ChecksumListFile.PathIn(_indexRoot), _indexRoot, _index.Entries);
            var index = _index;
            var policy = SelectedKeeper;
            var ignoreEmpty = _ignoreEmpty.Checked;
            var refreshed = await Task.Run(() => DuplicateAnalyzer.Analyze(index, policy, ignoreEmpty));
            _report = refreshed;
            ShowDuplicates(refreshed);
            _delete.Enabled = refreshed.DuplicateFiles > 0;
            AppendLog("Checksum list updated; " + Count(refreshed.DuplicateFiles)
                + " duplicate copy(ies) remain.");
        }
        catch (IOException ex)
        {
            AppendLog("Could not update the checksum list: " + ex.Message);
        }
    }

    // ----------------------------------------------------------------- pause / stop / report

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
            _status.Text = "Resumed.";
            AppendLog("Resumed.");
        }
        else
        {
            _pause.Pause();
            _pauseResume.Text = "Resume";
            _status.Text = "Paused - click Resume to carry on.";
            AppendLog("Paused.");
        }
    }

    private void OnStop()
    {
        _status.Text = "Stopping...";
        _pause?.Resume();
        _pauseResume.Text = "Pause";
        _cts?.Cancel();
    }

    private void OnSaveReport()
    {
        if (_report is null)
        {
            Inform("Run Analyze first so there is a report to save.");
            return;
        }

        using var dlg = new SaveFileDialog
        {
            Title = "Save the duplicate report",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = "dedupe_report.txt"
        };
        if (Directory.Exists(_indexRoot))
        {
            dlg.InitialDirectory = _indexRoot;
        }
        if (dlg.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        try
        {
            File.WriteAllText(dlg.FileName,
                DeDupeReportText.Render(_indexRoot, _report, SelectedKeeper, _outcome));
            AppendLog("Report saved: " + dlg.FileName);
        }
        catch (Exception ex)
        {
            Fail("Could not save the report", ex);
        }
    }

    // ----------------------------------------------------------------- shared helpers

    private void BeginRun(string status)
    {
        _progress.Value = 0;
        _status.Text = status;
        _pause?.Dispose();
        _cts?.Dispose();
        _pause = new PauseToken();
        _cts = new CancellationTokenSource();
        _pauseResume.Text = "Pause";
        SetRunning(true);
    }

    private void EndRun()
    {
        SetRunning(false);
        _cts?.Dispose();
        _cts = null;
        _pause?.Dispose();
        _pause = null;
    }

    private void SetRunning(bool running)
    {
        _scan.Enabled = !running;
        _analyze.Enabled = !running && _index.Count > 0;
        _delete.Enabled = !running && _report is not null && _report.DuplicateFiles > 0;
        _saveReport.Enabled = !running && _report is not null;
        _pauseResume.Enabled = running;
        _stop.Enabled = running;
        _folder.Enabled = !running;
        _browse.Enabled = !running;
        _onExisting.Enabled = !running;
        _keeper.Enabled = !running;
        _ignoreEmpty.Enabled = !running;
        _verifyBeforeDelete.Enabled = !running;
        _recycleBin.Enabled = !running;
        _dryRun.Enabled = !running;
    }

    private void SaveSettings()
        => _settingsStore.Save(new DeDupeSettings
        {
            Root = _folder.Text.Trim(),
            OnExistingList = SelectedExisting,
            Keeper = SelectedKeeper,
            IgnoreEmptyFiles = _ignoreEmpty.Checked,
            VerifyBeforeDelete = _verifyBeforeDelete.Checked,
            RecycleBin = _recycleBin.Checked,
            DryRun = _dryRun.Checked
        });

    private void AppendLog(string line)
        => _log.AppendText("[" + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "] "
            + line + Environment.NewLine);

    private void Fail(string title, Exception ex)
    {
        _status.Text = "Error.";
        AppendLog("ERROR: " + ex.Message);
        _context.Log("DeDupe Files: " + title + " - " + ex.Message);
        MessageBox.Show(ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static void Inform(string message)
        => MessageBox.Show(message, "DeDupe Files", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private static string Count(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
