using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using DigitalSecretary.App.Hosting;
using DigitalSecretary.App.Settings;
using DigitalSecretary.App.UI;

namespace DigitalSecretary.App;

/// <summary>
/// The pluggable shell: a menu bar built from the installed feature manifests, plus a content
/// area that hosts the dashboard or whichever feature view the user opens.
/// </summary>
public sealed class MainForm : Form
{
    private readonly IReadOnlyList<LoadedFeature> _features;
    private readonly AppSettings _settings;
    private readonly Panel _content;
    private ToolStripMenuItem _featuresMenu = null!;
    private DashboardControl? _dashboard;

    public MainForm(IReadOnlyList<LoadedFeature> features, AppSettings settings)
    {
        _features = features;
        _settings = settings;

        Text = "Digital Secretary";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1000, 660);
        MinimumSize = new Size(820, 560);
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.White;

        _content = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        Controls.Add(_content);

        var menu = BuildMenu();
        Controls.Add(menu);
        MainMenuStrip = menu;

        ShowDashboard();
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip { Dock = DockStyle.Top };

        // File
        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add("E&xit", null, (_, _) => Close());

        // Features  ->  Category  ->  Feature   (menus and submenus, built from manifests)
        _featuresMenu = new ToolStripMenuItem("Fe&atures");
        RebuildFeaturesMenu();

        // View
        var view = new ToolStripMenuItem("&View");
        view.DropDownItems.Add("&Home", null, (_, _) => ShowDashboard());
        view.DropDownItems.Add("&Configure Features…", null, (_, _) => ConfigureFeatures());

        // Help
        var help = new ToolStripMenuItem("&Help");
        help.DropDownItems.Add("&About", null, (_, _) => ShowAbout());

        menu.Items.AddRange(new ToolStripItem[] { file, _featuresMenu, view, help });
        return menu;
    }

    /// <summary>(Re)builds the Features menu, honouring which features are hidden from the menu.</summary>
    private void RebuildFeaturesMenu()
    {
        _featuresMenu.DropDownItems.Clear();

        var visible = _features
            .Where(f => !_settings.HiddenOnMenu.Contains(f.Manifest.Id, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var grouped = visible
            .GroupBy(f => f.Manifest.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            var categoryItem = new ToolStripMenuItem(group.Key);
            foreach (var feature in group.OrderBy(f => f.Manifest.Order))
            {
                var item = new ToolStripMenuItem(feature.Manifest.Title)
                {
                    ToolTipText = feature.Manifest.Description,
                    Tag = feature
                };
                item.Click += (_, _) => ActivateFeature(feature);
                categoryItem.DropDownItems.Add(item);
            }
            _featuresMenu.DropDownItems.Add(categoryItem);
        }

        if (_featuresMenu.DropDownItems.Count == 0)
        {
            var hint = _features.Count == 0
                ? "(no features installed)"
                : "(all features hidden — see View ▸ Configure Features…)";
            _featuresMenu.DropDownItems.Add(new ToolStripMenuItem(hint) { Enabled = false });
        }
    }

    private void ActivateFeature(LoadedFeature feature)
    {
        try
        {
            var view = feature.GetView();
            ShowControl(view);
            Text = $"Digital Secretary  —  {feature.Manifest.Title}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Could not open '{feature.Manifest.Title}'.\n\n{ex.Message}",
                "Feature failed to load", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowDashboard()
    {
        _dashboard ??= new DashboardControl(_features, _settings, ActivateFeature);
        _dashboard.RefreshTiles();
        ShowControl(_dashboard);
        Text = "Digital Secretary";
    }

    private void ConfigureFeatures()
    {
        using var dlg = new ConfigureFeaturesForm(_features, _settings);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            SettingsStore.Save(_settings);
            RebuildFeaturesMenu();
            ShowDashboard();
        }
    }

    private void ShowAbout()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0";
        var loaded = _features.Count(f => f.IsActivated);
        MessageBox.Show(this,
            $"Digital Secretary\nVersion {version}\n\n" +
            $"Installed features: {_features.Count}\n" +
            $"Loaded this session: {loaded}\n\n" +
            "A pluggable personal assistant. Each feature is an independent plugin, loaded only when opened.",
            "About Digital Secretary", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowControl(Control control)
    {
        _content.SuspendLayout();
        // Detach the current view without disposing it (feature views are cached and reused).
        _content.Controls.Clear();
        control.Dock = DockStyle.Fill;
        _content.Controls.Add(control);
        _content.ResumeLayout();
    }
}
