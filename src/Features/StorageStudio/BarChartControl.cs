using System.Drawing;
using System.Windows.Forms;

namespace DigitalSecretary.Features.StorageStudio;

/// <summary>
/// A titled horizontal bar chart of <see cref="BreakdownItem"/>s: label, a bar scaled to the
/// largest item, and "size · share" text. Each bar's colour comes from <see cref="ColorFor"/>, so the
/// "by type" chart doubles as the size map's colour legend. Paint-only; the data is computed elsewhere.
/// </summary>
public sealed class BarChartControl : Control
{
    private const int RowHeight = 24;
    private const int TitleHeight = 26;
    private const int LabelWidth = 140;
    private const int ValueWidth = 112;

    private IReadOnlyList<BreakdownItem> _items = [];

    public BarChartControl(string title)
    {
        Text = title;
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9f);
        Height = TitleHeight + RowHeight;
    }

    /// <summary>Colour for the bar at an index; defaults to one neutral blue.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<int, BreakdownItem, Color> ColorFor { get; set; } = (_, _) => Color.FromArgb(0x00, 0x72, 0xB2);

    /// <summary>The bars to draw; the control resizes its height to fit them.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<BreakdownItem> Items
    {
        get => _items;
        set
        {
            _items = value;
            Height = TitleHeight + (Math.Max(1, _items.Count) * RowHeight) + 6;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        using var titleFont = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        TextRenderer.DrawText(g, Text, titleFont, new Rectangle(0, 2, Width, TitleHeight - 4),
            Color.FromArgb(33, 37, 43), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

        if (_items.Count == 0)
        {
            TextRenderer.DrawText(g, "Nothing to show yet.", Font, new Rectangle(0, TitleHeight, Width, RowHeight),
                Color.Gray, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            return;
        }

        var max = Math.Max(1, _items.Max(i => i.Bytes));
        var barArea = Math.Max(10, Width - LabelWidth - ValueWidth - 8);
        for (var i = 0; i < _items.Count; i++)
        {
            DrawRow(g, i, _items[i], max, barArea);
        }
    }

    private void DrawRow(Graphics g, int index, BreakdownItem item, long max, int barArea)
    {
        var y = TitleHeight + (index * RowHeight);
        TextRenderer.DrawText(g, item.Label, Font, new Rectangle(0, y, LabelWidth - 6, RowHeight),
            Color.FromArgb(50, 50, 50), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        var barWidth = item.Bytes <= 0 ? 0 : Math.Max(2, (int)Math.Round(barArea * (double)item.Bytes / max));
        using (var brush = new SolidBrush(ColorFor(index, item)))
        {
            g.FillRectangle(brush, LabelWidth, y + 5, barWidth, RowHeight - 10);
        }

        var value = ByteSize.Format(item.Bytes) + " · " + SpaceReportText.Percent(item.Share);
        TextRenderer.DrawText(g, value, Font, new Rectangle(LabelWidth + barWidth + 6, y, ValueWidth + 40, RowHeight),
            Color.FromArgb(70, 70, 70), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
