using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace DigitalSecretary.Features.StorageStudio;

/// <summary>
/// The size map: draws <see cref="TreemapLayout.Build"/> cells for the focused folder, coloured by
/// file type, with a hover tooltip. Clicking a folder tile raises <see cref="FolderOpened"/> so the
/// owner can drill in; right-click raises <see cref="NodeMenuRequested"/>. Geometry and colours
/// come from the pure <see cref="TreemapLayout"/> and <see cref="CategoryPalette"/>; this control only
/// paints and hit-tests.
/// </summary>
public sealed class TreemapControl : Control
{
    private readonly ToolTip _tip = new() { InitialDelay = 300, ReshowDelay = 100, AutoPopDelay = 15000 };
    private IReadOnlyList<TreemapCell> _cells = [];
    private SpaceNode? _focus;
    private TreemapCell? _hover;
    private int _depth = 2;

    public TreemapControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 8.5f);
        Cursor = Cursors.Hand;
    }

    /// <summary>A folder tile was clicked: the owner should make it the new focus.</summary>
    public event Action<SpaceNode>? FolderOpened;

    /// <summary>A tile was right-clicked at the given screen point.</summary>
    public event Action<SpaceNode, Point>? NodeMenuRequested;

    /// <summary>The folder whose contents are drawn. Null clears the map.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public SpaceNode? FocusNode
    {
        get => _focus;
        set
        {
            _focus = value;
            Relayout();
        }
    }

    /// <summary>How many levels to nest (1 - 3).</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int MapDepth
    {
        get => _depth;
        set
        {
            _depth = Math.Clamp(value, 1, 3);
            Relayout();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Relayout();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tip.Dispose();
        }
        base.Dispose(disposing);
    }

    private void Relayout()
    {
        _hover = null;
        _cells = _focus is null || Width < 4 || Height < 4
            ? []
            : TreemapLayout.Build(_focus, new RectangleF(1, 1, Width - 2, Height - 2), _depth);
        Invalidate();
    }

    // ----------------------------------------------------------------- painting

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        if (_cells.Count == 0)
        {
            DrawEmpty(g);
            return;
        }

        foreach (var cell in _cells)
        {
            DrawCell(g, cell);
        }
        if (_hover is { } hover)
        {
            using var pen = new Pen(Color.FromArgb(20, 20, 20), 2f);
            g.DrawRectangle(pen, Rectangle.Round(hover.Rect));
        }
    }

    private void DrawEmpty(Graphics g)
    {
        var text = _focus is null ? "Choose a folder and click Scan to see its size map." : "This folder is empty.";
        TextRenderer.DrawText(g, text, Font, ClientRectangle, Color.Gray,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
    }

    private void DrawCell(Graphics g, TreemapCell cell)
    {
        var rect = Rectangle.Round(cell.Rect);
        if (rect.Width < 1 || rect.Height < 1)
        {
            return;
        }

        var fill = FillFor(cell);
        using (var brush = new SolidBrush(fill))
        {
            g.FillRectangle(brush, rect);
        }
        g.DrawRectangle(Pens.White, rect);

        if (cell.Expanded)
        {
            var header = new Rectangle(rect.X + 3, rect.Y, rect.Width - 6, (int)TreemapLayout.HeaderHeight);
            DrawLabel(g, cell.Label + "  " + ByteSize.Format(cell.Bytes), header, fill, bold: true);
            return;
        }
        if (rect.Width >= 46 && rect.Height >= 30)
        {
            var inner = Rectangle.Inflate(rect, -4, -3);
            DrawLabel(g, cell.Label + "\n" + ByteSize.Format(cell.Bytes), inner, fill, bold: false);
        }
    }

    private void DrawLabel(Graphics g, string text, Rectangle bounds, Color background, bool bold)
    {
        using var font = bold ? new Font(Font, FontStyle.Bold) : null;
        TextRenderer.DrawText(g, text, font ?? Font, bounds, CategoryPalette.TextOn(background),
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix
            | (bold ? TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine : TextFormatFlags.WordBreak));
    }

    private static Color FillFor(TreemapCell cell)
    {
        if (cell.Node is null)
        {
            return Color.FromArgb(205, 205, 205);
        }
        var color = CategoryPalette.Fill(cell.Node.DominantCategory, cell.Depth);
        // An expanded folder is mostly covered by its children; a light shade keeps its header readable.
        return cell.Expanded ? CategoryPalette.Tint(color, 0.55) : color;
    }

    // ----------------------------------------------------------------- interaction

    /// <summary>The top-most cell under <paramref name="point"/>, or null.</summary>
    private TreemapCell? HitTest(Point point)
    {
        for (var i = _cells.Count - 1; i >= 0; i--)
        {
            if (_cells[i].Rect.Contains(point))
            {
                return _cells[i];
            }
        }
        return null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hit = HitTest(e.Location);
        if (ReferenceEquals(hit, _hover))
        {
            return;
        }
        _hover = hit;
        Invalidate();
        _tip.SetToolTip(this, hit is null ? "" : TooltipFor(hit));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = null;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (HitTest(e.Location)?.Node is not { } node)
        {
            return;
        }
        if (e.Button == MouseButtons.Right)
        {
            NodeMenuRequested?.Invoke(node, PointToScreen(e.Location));
        }
        else if (e.Button == MouseButtons.Left)
        {
            // Clicking a file drills into the folder that holds it, so every click moves closer.
            var folder = node.IsFolder ? node : node.Parent;
            if (folder is not null && folder != _focus)
            {
                FolderOpened?.Invoke(folder);
            }
        }
    }

    private string TooltipFor(TreemapCell cell)
    {
        var share = SpaceReportText.Percent(SpaceAnalyzer.Share(cell.Bytes, _focus?.Size ?? 0));
        if (cell.Node is not { } node)
        {
            return cell.Label + "\n" + ByteSize.Format(cell.Bytes) + "  (" + share + " of this folder)";
        }
        var kind = node.IsFolder
            ? node.FileCount.ToString("N0", CultureInfo.InvariantCulture) + " files - mostly "
                + FileTypeClassifier.Label(node.DominantCategory).ToLowerInvariant()
            : FileTypeClassifier.Label(node.Category) + " - changed "
                + node.LastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return node.Name + "\n" + ByteSize.Format(node.Size) + "  (" + share + " of this folder)\n"
            + kind + "\n" + node.FullPath + (node.IsFolder ? "\nClick to open" : "");
    }
}
