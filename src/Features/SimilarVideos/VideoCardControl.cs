using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace DigitalSecretary.Features.SimilarVideos;

/// <summary>
/// One video on the review screen: a film strip of four frames, a KEEP / MOVE / keep badge and the facts
/// needed to choose (length, picture size, bit rate, file size, date, how it matches). Clicking a non-keeper
/// toggles its mark; double-click plays it; right-click offers "Keep this one instead", Play and Explorer.
/// Paint and wiring only.
/// </summary>
public sealed class VideoCardControl : Panel
{
    /// <summary>Width of the film-strip area (four 16:9 frames, scaled to fit; two cards sit side by side).</summary>
    public const int StripWidth = 344;

    private static readonly Color KeepColor = Color.FromArgb(0, 120, 60);
    private static readonly Color MoveColor = Color.FromArgb(180, 40, 30);
    private static readonly Color NeutralColor = Color.FromArgb(110, 110, 110);

    private readonly PictureBox _strip;
    private readonly Label _badge;
    private readonly bool _isKeeper;
    private bool _marked;

    public VideoCardControl(VideoRecord video, VideoMember? member, bool marked)
    {
        Video = video;
        _isKeeper = member is null;
        _marked = marked && !_isKeeper;
        Width = StripWidth + 16;
        Height = 158;
        Margin = new Padding(6);
        Padding = new Padding(6);
        BorderStyle = BorderStyle.FixedSingle;
        Cursor = _isKeeper ? Cursors.Default : Cursors.Hand;

        _strip = new PictureBox { Dock = DockStyle.Top, Height = 50, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(30, 30, 30) };
        _badge = new Label { Dock = DockStyle.Top, Height = 24, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold) };
        var info = new Label { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.25f), Text = Describe(video, member), UseMnemonic = false };

        Controls.Add(info);
        Controls.Add(_badge);
        Controls.Add(_strip);
        foreach (Control c in new Control[] { this, _strip, _badge, info })
        {
            c.MouseClick += OnAnyClick;
            c.DoubleClick += (_, _) => PlayRequested?.Invoke(Video);
        }
        Restyle();
    }

    public VideoRecord Video { get; }

    /// <summary>The user toggled this card's mark (never raised for the keeper).</summary>
    public event Action<VideoCardControl, bool>? MarkChanged;

    /// <summary>"Keep this one instead" was chosen.</summary>
    public event Action<VideoRecord>? KeeperRequested;

    public event Action<VideoRecord>? PlayRequested;
    public event Action<VideoRecord>? ExplorerRequested;

    /// <summary>Shows the film strip once it has loaded (the card owns and disposes it).</summary>
    public void SetStrip(Image? image)
    {
        var old = _strip.Image;
        _strip.Image = image;
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
            _strip.Image?.Dispose();
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
            menu.Items.Add("Keep this one instead", null, (_, _) => KeeperRequested?.Invoke(Video));
        }
        menu.Items.Add("Play", null, (_, _) => PlayRequested?.Invoke(Video));
        menu.Items.Add("Show in Explorer", null, (_, _) => ExplorerRequested?.Invoke(Video));
        menu.Closed += (_, _) => BeginInvoke(menu.Dispose);
        menu.Show(screen);
    }

    private static string Describe(VideoRecord v, VideoMember? member)
    {
        var match = member is null ? "Best of the group" : VideoReportText.MatchText(member.Match);
        var rate = VideoReportText.Bitrate(v.BitrateKbps) is { Length: > 0 } r ? "  ·  " + r : "";
        return Path.GetFileName(v.Path) + "\n"
            + VideoReportText.Duration(v.DurationMs) + "  ·  " + VideoReportText.Dimensions(v) + rate + "  ·  " + ByteSize.Format(v.Size) + "\n"
            + "Saved " + v.ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "  ·  " + match + "\n"
            + Path.GetDirectoryName(v.Path);
    }
}
