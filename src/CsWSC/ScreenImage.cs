using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CsWSC;

/// <summary>画面キャプチャと画像検索 (UWSC の SAVEIMG / CHKIMG)。</summary>
internal static class ScreenImage
{
    public static Bitmap Capture(Rectangle area)
    {
        var bmp = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(area.Location, Point.Empty, area.Size);
        return bmp;
    }

    public static void Save(string path, Rectangle area)
    {
        using var bmp = Capture(area);
        var format = System.IO.Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ImageFormat.Jpeg,
            ".bmp" => ImageFormat.Bmp,
            ".gif" => ImageFormat.Gif,
            _ => ImageFormat.Png,
        };
        bmp.Save(path, format);
    }

    /// <summary>
    /// area 内で画像を探し、見つかれば左上のスクリーン座標を返す。
    /// tolerance は RGB 各成分の許容差 (0 で完全一致)。
    /// </summary>
    public static Point? Find(string imagePath, Rectangle area, int tolerance)
    {
        using var file = new Bitmap(imagePath);
        using var needle = file.Clone(new Rectangle(Point.Empty, file.Size), PixelFormat.Format32bppArgb);
        using var hay = Capture(area);
        var n = Pixels(needle);
        var h = Pixels(hay);
        int nw = needle.Width, nh = needle.Height, hw = hay.Width, hh = hay.Height;

        for (var y = 0; y <= hh - nh; y++)
            for (var x = 0; x <= hw - nw; x++)
                if (Match(h, hw, x, y, n, nw, nh, tolerance)) return new Point(area.X + x, area.Y + y);
        return null;
    }

    private static bool Match(int[] h, int hw, int ox, int oy, int[] n, int nw, int nh, int tol)
    {
        for (var y = 0; y < nh; y++)
        {
            var hi = (oy + y) * hw + ox;
            var ni = y * nw;
            for (var x = 0; x < nw; x++)
            {
                var a = n[ni + x];
                if ((a >>> 24) < 128) continue; // 透明ピクセルは比較しない
                var b = h[hi + x];
                if (tol == 0 ? (a & 0xFFFFFF) != (b & 0xFFFFFF) :
                    Math.Abs(((a >> 16) & 0xFF) - ((b >> 16) & 0xFF)) > tol ||
                    Math.Abs(((a >> 8) & 0xFF) - ((b >> 8) & 0xFF)) > tol ||
                    Math.Abs((a & 0xFF) - (b & 0xFF)) > tol) return false;
            }
        }
        return true;
    }

    private static int[] Pixels(Bitmap bmp)
    {
        var data = bmp.LockBits(new Rectangle(Point.Empty, bmp.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var result = new int[bmp.Width * bmp.Height];
            for (var y = 0; y < bmp.Height; y++)
                Marshal.Copy(data.Scan0 + y * data.Stride, result, y * bmp.Width, bmp.Width);
            return result;
        }
        finally { bmp.UnlockBits(data); }
    }
}
