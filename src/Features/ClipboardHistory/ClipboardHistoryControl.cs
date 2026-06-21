using System.Drawing;
using System.Windows.Forms;

namespace DigitalSecretary.Features.ClipboardHistory;

/// <summary>Watches the clipboard and keeps a history of copied text so you can re-paste older items.</summary>
public sealed class ClipboardHistoryControl : UserControl
{
    private const int MaxItems = 50;

    private readonly ListBox _list;
    private readonly List<string> _items = new();
    private readonly System.Windows.Forms.Timer _timer;
    private string _last = "";

    public ClipboardHistoryControl()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.White;

        var title = new Label
        {
            Text = "Clipboard History",
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 37, 43)
        };
        var hint = new Label
        {
            Text = "Text you copy anywhere is captured here. Double-click an entry to copy it again.",
            Dock = DockStyle.Top,
            Height = 26,
            ForeColor = Color.Gray
        };

        _list = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f),
            IntegralHeight = false
        };
        _list.DoubleClick += (_, _) => CopySelected();

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(0, 6, 0, 6) };
        bar.Controls.Add(MakeButton("Copy", CopySelected));
        bar.Controls.Add(MakeButton("Delete", DeleteSelected));
        bar.Controls.Add(MakeButton("Clear All", ClearAll));

        Controls.Add(_list);
        Controls.Add(bar);
        Controls.Add(hint);
        Controls.Add(title);

        _timer = new System.Windows.Forms.Timer { Interval = 800 };
        _timer.Tick += (_, _) => Poll();
        _timer.Start();
    }

    private static Button MakeButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 32,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(8, 2, 8, 2),
            FlatStyle = FlatStyle.System,
            Cursor = Cursors.Hand
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private void Poll()
    {
        try
        {
            if (!Clipboard.ContainsText())
                return;

            var text = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text) || text == _last)
                return;

            _last = text;
            _items.Remove(text);
            _items.Insert(0, text);
            if (_items.Count > MaxItems)
                _items.RemoveRange(MaxItems, _items.Count - MaxItems);

            RefreshList();
        }
        catch
        {
            // Clipboard can be briefly locked by another app — ignore and try again next tick.
        }
    }

    private void RefreshList()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var item in _items)
            _list.Items.Add(ClipboardPreview.Format(item));
        _list.EndUpdate();
    }

    private void CopySelected()
    {
        var i = _list.SelectedIndex;
        if (i < 0 || i >= _items.Count)
            return;
        try
        {
            _last = _items[i];
            Clipboard.SetText(_items[i]);
        }
        catch
        {
            // ignore transient clipboard errors
        }
    }

    private void DeleteSelected()
    {
        var i = _list.SelectedIndex;
        if (i < 0 || i >= _items.Count)
            return;
        _items.RemoveAt(i);
        RefreshList();
    }

    private void ClearAll()
    {
        _items.Clear();
        RefreshList();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _timer.Dispose();
        base.Dispose(disposing);
    }
}
