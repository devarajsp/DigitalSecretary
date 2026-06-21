using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.Launcher;

/// <summary>Save and one-click launch apps, files, folders, and URLs.</summary>
public sealed class LauncherControl : UserControl
{
    private readonly LauncherStore _store;
    private readonly ListView _list;
    private readonly List<LauncherItem> _items;

    public LauncherControl(IFeatureContext context)
    {
        _store = new LauncherStore(context.DataDirectory);
        _items = _store.Load();

        Dock = DockStyle.Fill;
        BackColor = Color.White;

        var title = new Label
        {
            Text = "Launcher & Favorites",
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 37, 43)
        };
        var hint = new Label
        {
            Text = "Double-click an item to open it. Add your favorite apps, files, folders, and links.",
            Dock = DockStyle.Top,
            Height = 26,
            ForeColor = Color.Gray
        };

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            HideSelection = false
        };
        _list.Columns.Add("Name", 200);
        _list.Columns.Add("Target", 460);
        _list.DoubleClick += (_, _) => LaunchSelected();

        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(0, 6, 0, 6) };
        bar.Controls.Add(MakeButton("Add App / File", AddFile));
        bar.Controls.Add(MakeButton("Add Folder", AddFolder));
        bar.Controls.Add(MakeButton("Add URL", AddUrl));
        bar.Controls.Add(MakeButton("Edit", EditSelected));
        bar.Controls.Add(MakeButton("Remove", RemoveSelected));
        bar.Controls.Add(MakeButton("Launch", LaunchSelected));

        Controls.Add(_list);
        Controls.Add(bar);
        Controls.Add(hint);
        Controls.Add(title);

        RefreshList();
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

    private void RefreshList()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var s in _items)
        {
            var item = new ListViewItem(s.Name) { Tag = s };
            item.SubItems.Add(s.Target);
            _list.Items.Add(item);
        }
        _list.EndUpdate();
    }

    private void Persist() => _store.Save(_items);

    private void AddFile()
    {
        using var dlg = new OpenFileDialog { Title = "Choose an app or file", CheckFileExists = true };
        if (dlg.ShowDialog() != DialogResult.OK)
            return;

        var name = Prompt.Show("Add Shortcut", "Name for this shortcut:", Path.GetFileNameWithoutExtension(dlg.FileName));
        if (name is null)
            return;

        _items.Add(new LauncherItem { Name = name, Target = dlg.FileName });
        Persist();
        RefreshList();
    }

    private void AddFolder()
    {
        using var dlg = new FolderBrowserDialog { Description = "Choose a folder" };
        if (dlg.ShowDialog() != DialogResult.OK)
            return;

        var name = Prompt.Show("Add Folder", "Name for this folder shortcut:", new DirectoryInfo(dlg.SelectedPath).Name);
        if (name is null)
            return;

        _items.Add(new LauncherItem { Name = name, Target = dlg.SelectedPath });
        Persist();
        RefreshList();
    }

    private void AddUrl()
    {
        var url = Prompt.Show("Add URL", "Web address (e.g. https://example.com):", "https://");
        if (string.IsNullOrWhiteSpace(url))
            return;

        var name = Prompt.Show("Add URL", "Name for this link:", url);
        if (name is null)
            return;

        _items.Add(new LauncherItem { Name = name, Target = url });
        Persist();
        RefreshList();
    }

    private LauncherItem? Selected =>
        _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as LauncherItem : null;

    private void EditSelected()
    {
        if (Selected is not { } s)
            return;

        var name = Prompt.Show("Edit Shortcut", "Name:", s.Name);
        if (name is null)
            return;
        var target = Prompt.Show("Edit Shortcut", "Target (path or URL):", s.Target);
        if (target is null)
            return;

        s.Name = name;
        s.Target = target;
        Persist();
        RefreshList();
    }

    private void RemoveSelected()
    {
        if (Selected is not { } s)
            return;

        if (MessageBox.Show($"Remove \"{s.Name}\"?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _items.Remove(s);
        Persist();
        RefreshList();
    }

    private void LaunchSelected()
    {
        if (Selected is not { } s)
            return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = s.Target,
                UseShellExecute = true
            };
            if (!string.IsNullOrWhiteSpace(s.Arguments))
                psi.Arguments = s.Arguments;

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open \"{s.Name}\".\n\n{ex.Message}", "Launch failed",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
