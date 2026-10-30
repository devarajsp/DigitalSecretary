using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace DigitalSecretary.Features.SimilarPictures;

/// <summary>
/// One picture on the review screen: the thumbnail, a KEEP / MOVE / keep badge and the facts needed to
/// choose (size, dimensions, date, sharpness, how it matches). Clicking a non-keeper toggles its mark;
/// right-click offers "Keep this one instead", open and Explorer. Paint and wiring only.
/// </summary>
public sealed class PictureCardControl : Panel
{
    private const int ThumbSize = 170;

    private static readonly Color KeepColor = Color.FromArgb(0, 120, 60);
    private static readonly Color MoveColor = Color.FromArgb(180, 40, 30);
    private static readonly Color NeutralColor = Color.FromArgb(110, 110, 110);

    private readonly PictureBox _thumb;
    private readonly Label _badge;
    private readonly bool _isKeeper;
    private bool _marked;

    public PictureCardControl(PictureRecord picture, GroupMember? member, bool marked, double groupMaxSharpness)
    {
        Picture = picture;
        _isKeeper = member is null;
        _marked = marked && !_isKeeper;
        Width = ThumbSize + 16;
        Height = ThumbSize + 128;
        Margin = new Padding(6);
        Padding = new Padding(6);
        BorderStyle = BorderStyle.FixedSingle;
        Cursor = _isKeeper ? Cursors.Default : Cursors.Hand;

        _thumb = new PictureBox { Dock = DockStyle.Top, Height = ThumbSize, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(240, 240, 240) };
        _badge = new Label { Dock = DockStyle.Top, Height = 24, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold) };
        var info = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.25f), Text = Describe(picture, member, groupMaxSharpness), UseMnemonic = false };

        Controls.Add(info);
        Controls.Add(_badge);
        Controls.Add(_thumb);
        foreach (Control c in new Control[] { this, _thumb, _badge, info })
        {
            c.MouseClick += OnAnyClick;
            c.DoubleClick += (_, _) => OpenRequested?.Invoke(Picture);
        }
        Restyle();
    }

    public PictureRecord Picture { get; }

    /// <summary>The user toggled this card's mark (never raised for the keeper).</summary>
    public event Action<PictureCardControl, bool>? MarkChanged;

    /// <summary>"Keep this one instead" was chosen.</summary>
    public event Action<PictureRecord>? KeeperRequested;

    public event Action<PictureRecord>? OpenRequested;
    public event Action<PictureRecord>? ExplorerRequested;

    /// <summary>Shows the thumbnail once it has loaded (the card owns and disposes it).</summary>
    public void SetThumbnail(Image? image)
    {
        var old = _thumb.Image;
        _thumb.Image = image;
        old?.Dispose();
    }

    public void SetMarked(bool marked)
    {
        if (_isKeeper)
        {
            return;
        }
        _marked = marked;
        Restyle();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _thumb.Image?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void Restyle()
    {
        if (_isKeeper)
        {
            _badge.Text = "KEEP";
            _badge.BackColor = KeepColor;
        }
        else if (_marked)
        {
            _badge.Text = "MOVE  (click to keep)";
            _badge.BackColor = MoveColor;
        }
        else
        {
            _badge.Text = "keep  (click to move)";
            _badge.BackColor = NeutralColor;
        }
        BackColor = _marked ? Color.FromArgb(253, 236, 234) : Color.White;
    }

    private void OnAnyClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            ShowMenu(((Control)sender!).PointToScreen(e.Location));
            return;
        }
        if (e.Button == MouseButtons.Left && !_isKeeper)
        {
            SetMarked(!_marked);
            MarkChanged?.Invoke(this, _marked);
        }
    }

    private void ShowMenu(Point screen)
    {
        var menu = new ContextMenuStrip();
        if (!_isKeeper)
        {
            menu.Items.Add("Keep this one instead", null, (_, _) => KeeperRequested?.Invoke(Picture));
        }
        menu.Items.Add("Open", null, (_, _) => OpenRequested?.Invoke(Picture));
        menu.Items.Add("Show in Explorer", null, (_, _) => ExplorerRequested?.Invoke(Picture));
        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
        menu.Show(screen);
    }

    private static string Describe(PictureRecord p, GroupMember? member, double maxSharpness)
    {
        var sharp = maxSharpness > 0 ? (int)Math.Round(100 * p.Sharpness / maxSharpness) : 0;
        var match = member is null
            ? "Best of the group"
            : SimilarReportText.Label(member.Kind) + (SimilarReportText.Describe(member.Orientation) is { Length: > 0 } d ? ", " + d : "");
        return Path.GetFileName(p.Path) + "\n"
            + SimilarReportText.Dimensions(p) + "  ·  " + ByteSize.Format(p.Size) + "\n"
            + p.BestTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\n"
            + "Sharpness " + sharp.ToString(CultureInfo.InvariantCulture) + "%  ·  " + match + "\n"
            + Path.GetDirectoryName(p.Path);
    }
}
