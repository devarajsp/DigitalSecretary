using System.Drawing;
using System.Windows.Forms;
using DigitalSecretary.App.Hosting;
using DigitalSecretary.App.Settings;

namespace DigitalSecretary.App.UI;

/// <summary>The home screen: a tile for each feature the user has chosen to show.</summary>
public sealed class DashboardControl : UserControl
{
    private readonly IReadOnlyList<LoadedFeature> _features;
    private readonly AppSettings _settings;
    private readonly Action<LoadedFeature> _activate;
    private readonly FlowLayoutPanel _tiles;

    public DashboardControl(IReadOnlyList<LoadedFeature> features, AppSettings settings, Action<LoadedFeature> activate)
    {
        _features = features;
        _settings = settings;
        _activate = activate;

        Dock = DockStyle.Fill;
        BackColor = Color.White;

        var title = new Label
        {
            Text = "Digital Secretary",
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(16, 8, 0, 0),
            Font = new Font("Segoe UI Semibold", 18f, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 37, 43)
        };
        var subtitle = new Label
        {
            Text = "Your features at a glance. Use View → Configure Features… to choose what appears here.",
            Dock = DockStyle.Top,
            Height = 28,
            Padding = new Padding(18, 0, 0, 0),
            ForeColor = Color.Gray
        };

        _tiles = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(12)
        };

        Controls.Add(_tiles);
        Controls.Add(subtitle);
        Controls.Add(title);

        RefreshTiles();
    }

    /// <summary>Rebuilds the tiles to reflect the current show/hide settings.</summary>
    public void RefreshTiles()
    {
        _tiles.SuspendLayout();
        _tiles.Controls.Clear();

        var visible = _features
            .Where(f => !_settings.HiddenOnDashboard.Contains(f.Manifest.Id, StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => f.Manifest.Order)
            .ToList();

        if (visible.Count == 0)
        {
            _tiles.Controls.Add(new Label
            {
                Text = _features.Count == 0
                    ? "No features are installed. Drop a feature into the 'plugins' folder and restart."
                    : "Nothing is shown on the dashboard yet. Use View → Configure Features… to add features.",
                AutoSize = true,
                Margin = new Padding(8),
                ForeColor = Color.Gray
            });
        }
        else
        {
            foreach (var feature in visible)
                _tiles.Controls.Add(CreateTile(feature));
        }

        _tiles.ResumeLayout();
    }

    private Button CreateTile(LoadedFeature feature)
    {
        var tile = new Button
        {
            Width = 210,
            Height = 96,
            Margin = new Padding(10),
            Padding = new Padding(12),
            TextAlign = ContentAlignment.TopLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(247, 248, 250),
            Cursor = Cursors.Hand,
            Tag = feature,
            Text = feature.Manifest.Title + Environment.NewLine + Environment.NewLine + feature.Manifest.Description,
            Font = new Font("Segoe UI", 9.5f)
        };
        tile.FlatAppearance.BorderColor = Color.FromArgb(220, 223, 228);
        tile.Click += (_, _) => _activate(feature);
        return tile;
    }
}
