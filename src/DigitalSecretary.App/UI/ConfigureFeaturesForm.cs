using System.Drawing;
using System.Windows.Forms;
using DigitalSecretary.App.Hosting;
using DigitalSecretary.App.Settings;

namespace DigitalSecretary.App.UI;

/// <summary>
/// Lets the user choose, per feature, whether it appears on the dashboard and/or in the
/// Features menu — independently.
/// </summary>
public sealed class ConfigureFeaturesForm : Form
{
    private const int DashboardColumn = 1;
    private const int MenuColumn = 2;

    private readonly DataGridView _grid;
    private readonly AppSettings _settings;

    public ConfigureFeaturesForm(IReadOnlyList<LoadedFeature> features, AppSettings settings)
    {
        _settings = settings;

        Text = "Configure Features";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(480, 360);
        Font = new Font("Segoe UI", 9.5f);

        var header = new Label
        {
            Text = "Choose where each feature appears:",
            Dock = DockStyle.Top,
            Height = 30,
            Padding = new Padding(6, 7, 0, 0)
        };

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            MultiSelect = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
        };

        var nameCol = new DataGridViewTextBoxColumn
        {
            HeaderText = "Feature",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        var dashCol = new DataGridViewCheckBoxColumn
        {
            HeaderText = "On Dashboard",
            Width = 110,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        var menuCol = new DataGridViewCheckBoxColumn
        {
            HeaderText = "In Menu",
            Width = 80,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        _grid.Columns.AddRange(nameCol, dashCol, menuCol);

        foreach (var feature in features.OrderBy(f => f.Manifest.Order))
        {
            var onDashboard = !settings.HiddenOnDashboard.Contains(feature.Manifest.Id, StringComparer.OrdinalIgnoreCase);
            var inMenu = !settings.HiddenOnMenu.Contains(feature.Manifest.Id, StringComparer.OrdinalIgnoreCase);

            var index = _grid.Rows.Add($"{feature.Manifest.Title}  —  {feature.Manifest.Category}", onDashboard, inMenu);
            _grid.Rows[index].Tag = feature;
        }

        // Commit checkbox toggles immediately so OK reads the latest values.
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 48,
            Padding = new Padding(8)
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, Padding = new Padding(10, 2, 10, 2) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, Padding = new Padding(10, 2, 10, 2) };
        ok.Click += (_, _) => Apply();
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        Controls.Add(_grid);
        Controls.Add(buttons);
        Controls.Add(header);

        AcceptButton = ok;
        CancelButton = cancel;
    }

    private void Apply()
    {
        _grid.EndEdit();

        _settings.HiddenOnDashboard.Clear();
        _settings.HiddenOnMenu.Clear();

        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is not LoadedFeature feature)
                continue;

            if (!Convert.ToBoolean(row.Cells[DashboardColumn].Value ?? false))
                _settings.HiddenOnDashboard.Add(feature.Manifest.Id);

            if (!Convert.ToBoolean(row.Cells[MenuColumn].Value ?? false))
                _settings.HiddenOnMenu.Add(feature.Manifest.Id);
        }
    }
}
