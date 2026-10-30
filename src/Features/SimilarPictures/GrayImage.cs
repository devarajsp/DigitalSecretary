using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DigitalSecretary.Features.SimilarPictures;

/// <summary>The eight ways a picture can be turned or mirrored (the symmetries of a rectangle).</summary>
public enum PictureOrientation
{
    AsIs,
    Rotate90,
    Rotate180,
    Rotate270,
    MirrorLeftRight,
    MirrorTopBottom,
    MirrorDiagonal,
    MirrorAntiDiagonal
}

/// <summary>
/// A small greyscale picture (brightness 0 - 255, row by row) - the only form the hashing and
/// sharpness maths ever sees. Immutable and pure apart from <see cref="FromBitmap"/>, which reads a
/// GDI+ bitmap; everything else is plain arithmetic, so it is fully unit-testable.
/// </summary>
public sealed class GrayImage
{
    private readonly float[] _pixels;

    public GrayImage(int width, int height, float[] pixels)
    {
        if (width <= 0 || height <= 0 || pixels.Length != width * height)
        {
            throw new ArgumentException("Pixel count must equal width x height, both positive.");
        }
        Width = width;
        Height = height;
        _pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }

    public float this[int x, int y] => _pixels[(y * Width) + x];

    /// <summary>
    /// Converts a bitmap to greyscale with the Rec. 601 luma weights. Transparent pixels are
    /// composited over white so a logo on a clear background reads the same as on paper.
    /// </summary>
    public static GrayImage FromBitmap(Bitmap bitmap)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var row = new byte[data.Stride];
            var pixels = new float[bitmap.Width * bitmap.Height];
            for (var y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(data.Scan0 + (y * data.Stride), row, 0, data.Stride);
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var i = x * 4;
                    float b = row[i], g = row[i + 1], r = row[i + 2], a = row[i + 3] / 255f;
                    var luma = (0.299f * r) + (0.587f * g) + (0.114f * b);
                    pixels[(y * bitmap.Width) + x] = (luma * a) + (255f * (1 - a));
                }
            }
            return new GrayImage(bitmap.Width, bitmap.Height, pixels);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    /// <summary>
    /// Resamples to <paramref name="width"/> x <paramref name="height"/> by area averaging: each target
    /// pixel is the mean of the source area it covers, so shrinking is smooth and stable (the property
    /// perceptual hashes rely on). Growing an axis falls back to the nearest source pixel.
    /// </summary>
    public GrayImage Resize(int width, int height)
    {
        var result = new float[width * height];
        var sx = (double)Width / width;
        var sy = (double)Height / height;
        for (var ty = 0; ty < height; ty++)
        {
            var y0 = ty * sy;
            var y1 = Math.Max(y0 + 1e-9, (ty + 1) * sy);
            for (var tx = 0; tx < width; tx++)
            {
                var x0 = tx * sx;
                var x1 = Math.Max(x0 + 1e-9, (tx + 1) * sx);
                result[(ty * width) + tx] = (float)AreaMean(x0, x1, y0, y1);
            }
        }
        return new GrayImage(width, height, result);
    }

    /// <summary>The picture turned or mirrored as <paramref name="orientation"/> says.</summary>
    public GrayImage Transform(PictureOrientation orientation)
    {
        var swap = orientation is PictureOrientation.Rotate90 or PictureOrientation.Rotate270
            or PictureOrientation.MirrorDiagonal or PictureOrientation.MirrorAntiDiagonal;
        var w = swap ? Height : Width;
        var h = swap ? Width : Height;
        var result = new float[w * h];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var (srcX, srcY) = Source(orientation, x, y);
                result[(y * w) + x] = this[srcX, srcY];
            }
        }
        return new GrayImage(w, h, result);
    }

    /// <summary>Mean brightness, 0 - 255.</summary>
    public double Mean() => _pixels.Average(p => (double)p);

    /// <summary>
    /// Variance of the 4-neighbour Laplacian - a standard focus measure. Sharp pictures have strong
    /// edges and score high; blurred or shaken shots score low. 0 for pictures under 3 x 3.
    /// </summary>
    public double LaplacianVariance()
    {
        if (Width < 3 || Height < 3)
        {
            return 0;
        }
        double sum = 0, sumSq = 0;
        var n = 0;
        for (var y = 1; y < Height - 1; y++)
        {
            for (var x = 1; x < Width - 1; x++)
            {
                double lap = this[x - 1, y] + this[x + 1, y] + this[x, y - 1] + this[x, y + 1] - (4 * this[x, y]);
                sum += lap;
                sumSq += lap * lap;
                n++;
            }
        }
        var mean = sum / n;
        return (sumSq / n) - (mean * mean);
    }

    private (int X, int Y) Source(PictureOrientation orientation, int x, int y) => orientation switch
    {
        PictureOrientation.Rotate90 => (y, Height - 1 - x),
        PictureOrientation.Rotate180 => (Width - 1 - x, Height - 1 - y),
        PictureOrientation.Rotate270 => (Width - 1 - y, x),
        PictureOrientation.MirrorLeftRight => (Width - 1 - x, y),
        PictureOrientation.MirrorTopBottom => (x, Height - 1 - y),
        PictureOrientation.MirrorDiagonal => (y, x),
        PictureOrientation.MirrorAntiDiagonal => (Width - 1 - y, Height - 1 - x),
        _ => (x, y)
    };

    /// <summary>Mean over the source rectangle [x0,x1) x [y0,y1), weighting partly covered pixels.</summary>
    private double AreaMean(double x0, double x1, double y0, double y1)
    {
        double total = 0, weight = 0;
        var yStart = (int)Math.Floor(y0);
        var yEnd = Math.Min(Height - 1, (int)Math.Ceiling(y1) - 1);
        var xStart = (int)Math.Floor(x0);
        var xEnd = Math.Min(Width - 1, (int)Math.Ceiling(x1) - 1);
        for (var y = yStart; y <= yEnd; y++)
        {
            var wy = Math.Min(y + 1, y1) - Math.Max(y, y0);
            for (var x = xStart; x <= xEnd; x++)
            {
                var w = wy * (Math.Min(x + 1, x1) - Math.Max(x, x0));
                total += this[x, y] * w;
                weight += w;
            }
        }
        return weight > 0 ? total / weight : this[Math.Min(xStart, Width - 1), Math.Min(yStart, Height - 1)];
    }
}
