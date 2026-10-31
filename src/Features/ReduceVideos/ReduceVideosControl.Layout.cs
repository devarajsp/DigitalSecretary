using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace DigitalSecretary.Features.ReduceVideos;

/// <summary>Layout, events and small helpers of <see cref="ReduceVideosControl"/>.</summary>
public sealed partial class ReduceVideosControl
{
    private void BuildLayout()
    {
        Controls.Add(_list);
        Controls.Add(BuildSelectBar());
        Controls.Add(_log);
        Controls.Add(BuildActionBar());
        Controls.Add(_summary);
        Controls.Add(_status);
        Controls.Add(_progress);
        Controls.Add(_help);
        Controls.Add(BuildOptionsRow2());
        Controls.Add(BuildOptionsRow1());
        Controls.Add(BuildFolderRow());
        Controls.Add(BuildNote());
        Controls.Add(BuildTitle());
    }

    private static Label BuildTitle() => new()
    {
        Text = Title,
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
        Text = "Phones and cameras record far more data than the picture needs. Scan shows which videos can shrink without a visible difference, and by how much. "
            + "Nothing changes until you click Reduce selected - every copy is checked first, and originals are kept until you delete them."
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

    private FlowLayoutPanel BuildOptionsRow1()
    {
        var row = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(0, 4, 0, 2) };
        row.Controls.Add(new Label { Text = "Engine:", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        _engine.Width = 270;
        row.Controls.Add(_engine);
        row.Controls.Add(new Label { Text = "Quality:", AutoSize = true, Margin = new Padding(12, 6, 4, 0) });
        _quality.Width = 230;
        row.Controls.Add(_quality);
        row.Controls.Add(new Label { Text = "", AutoSize = true, Margin = new Padding(12, 0, 0, 0) });
        row.Controls.Add(_tools);
        row.Controls.Add(MakeButton("Find ffmpeg", LocateFfmpeg));
        row.Controls.Add(MakeButton("How to install", CopyInstallCommand));
        return row;
    }

    private FlowLayoutPanel BuildOptionsRow2()
    {
        var row = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new Padding(0, 2, 0, 2) };
        row.Controls.Add(new Label { Text = "Offer a video only if it saves at least", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        row.Controls.Add(_minSaving);
        row.Controls.Add(new Label { Text = "%", AutoSize = true, Margin = new Padding(2, 6, 0, 0) });
        row.Controls.Add(_checkQuality);
        row.Controls.Add(_skipHdr);
        row.Controls.Add(new Label { Text = "Originals go to:", AutoSize = true, Margin = new Padding(14, 6, 4, 0) });
        _action.Width = 190;
        row.Controls.Add(_action);
        return row;
    }

    private FlowLayoutPanel BuildActionBar()
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, WrapContents = false, Padding = new Padding(0, 6, 0, 2) };
        bar.Controls.AddRange([_reduce, _undo, MakeButton("Open originals folder", OpenOriginals), _saveReport, _saved]);
        return bar;
    }

    private FlowLayoutPanel BuildSelectBar()
    {
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 34, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
        bar.Controls.Add(MakeButton("Select recommended", () => SelectWhere(v => v == Verdict.Recommended)));
        bar.Controls.Add(MakeButton("Select all worth it", () => SelectWhere(_ => true)));
        bar.Controls.Add(MakeButton("Select none", () => SelectWhere(_ => false)));
        bar.Controls.Add(_selection);
        return bar;
    }

    private static ListView BuildList()
    {
        var list = new ListView { Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true, FullRowSelect = true, HideSelection = false, MultiSelect = false, Font = new Font("Segoe UI", 9f) };
        list.Columns.Add("Video", 230);
        list.Columns.Add("Size", 75, HorizontalAlignment.Right);
        list.Columns.Add("Length", 60, HorizontalAlignment.Right);
        list.Columns.Add("Picture", 90);
        list.Columns.Add("Codec", 90);
        list.Columns.Add("Bit rate", 80, HorizontalAlignment.Right);
        list.Columns.Add("Advice", 95);
        list.Columns.Add("New size", 80, HorizontalAlignment.Right);
        list.Columns.Add("Saving", 115, HorizontalAlignment.Right);
        list.Columns.Add("Why", 520);
        return list;
    }

    private void WireEvents()
    {
        _engine.SelectedIndexChanged += (_, _) => OnOptionsChanged();
        _quality.SelectedIndexChanged += (_, _) => OnOptionsChanged();
        _minSaving.ValueChanged += (_, _) => OnOptionsChanged();
        _checkQuality.CheckedChanged += (_, _) => OnOptionsChanged();
        _skipHdr.CheckedChanged += (_, _) => OnOptionsChanged();
        _action.SelectedIndexChanged += (_, _) => OnOptionsChanged();
        _list.ItemCheck += (_, e) =>
        {
            // A "Skip" video can never be ticked: it is not worth it, or not safe.
            if (!_filling && e.NewValue == CheckState.Checked && _list.Items[e.Index].Tag is (VideoFacts _, VideoAdvice { Verdict: Verdict.Skip }))
            {
                e.NewValue = CheckState.Unchecked;
            }
        };
        _list.ItemChecked += (_, _) =>
        {
            if (!_filling)
            {
                UpdateSelection();
            }
        };
        _list.DoubleClick += (_, _) => OpenSelectedVideo();
    }

    private void BeginRun()
    {
        _cts?.Dispose();
        _pause?.Dispose();
        _cts = new CancellationTokenSource();
        _pause = new PauseToken();
        _scan.Enabled = _reduce.Enabled = _undo.Enabled = false;
        _pauseResume.Enabled = _stop.Enabled = true;
        _pauseResume.Text = "Pause";
        _progress.Value = 0;
    }

    private void EndRun()
    {
        _scan.Enabled = true;
        _pauseResume.Enabled = _stop.Enabled = false;
        _undo.Enabled = ShrinkSwap.LoadLastBatch(_lastBatchFile) is not null;
        UpdateSelection();
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
            _status.Text = "Pausing after the current video...";
        }
    }

    private static Button MakeButton(string text, Action onClick)
    {
        var button = new Button { Text = text, AutoSize = true, Padding = new Padding(10, 2, 10, 2), Margin = new Padding(0, 0, 6, 0), FlatStyle = FlatStyle.System, Cursor = Cursors.Hand };
        button.Click += (_, _) => onClick();
        return button;
    }

    private static CheckBox MakeCheck(string text, bool on) => new() { Text = text, Checked = on, AutoSize = true, Margin = new Padding(14, 5, 0, 0) };

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

    private void Inform(string text) => MessageBox.Show(FindForm(), text, Title, MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void Fail(string what, Exception ex)
    {
        _status.Text = what + ": " + ex.Message;
        AppendLog(what + ": " + ex);
        _context.Log(Title + " - " + what + ": " + ex.Message);
    }

    private void AppendLog(string line) => _log.AppendText(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + line + Environment.NewLine);
}
