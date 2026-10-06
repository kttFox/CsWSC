using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// 64x64 で描いて 20x20 (ボタン) / 16,32,48 (アプリアイコン) に縮小する
var outDir = args[0];
Directory.CreateDirectory(outDir);

Save("Load", DrawLoad);
Save("Save", DrawSave);
Save("Play", DrawPlay);
Save("Stop", DrawStop);
Save("Record", DrawRecord);
Save("Settings", DrawSettings);
SaveIco(Path.Combine(outDir, "CsWSC.ico"), DrawAppIcon, [16, 24, 32, 48, 256]);
using (var vsIcon = Render(DrawAppIcon, 128)) vsIcon.Save(Path.Combine(outDir, "icon.png"), ImageFormat.Png);

void Save(string name, Action<Graphics> draw)
{
    using var big = Render(draw, 64);
    using var small = Resize(big, 20);
    small.Save(Path.Combine(outDir, name + ".png"), ImageFormat.Png);
}

Bitmap Render(Action<Graphics> draw, int size)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
    g.ScaleTransform(size / 64f, size / 64f);
    draw(g);
    return bmp;
}

Bitmap Resize(Bitmap src, int size)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.DrawImage(src, 0, 0, size, size);
    return bmp;
}

void SaveIco(string path, Action<Graphics> draw, int[] sizes)
{
    var pngs = sizes.Select(s =>
    {
        using var bmp = Render(draw, s);
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }).ToList();
    using var w = new BinaryWriter(File.Create(path));
    w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
    int offset = 6 + 16 * sizes.Length;
    for (int i = 0; i < sizes.Length; i++)
    {
        w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        w.Write((byte)0); w.Write((byte)0);
        w.Write((short)1); w.Write((short)32);
        w.Write(pngs[i].Length); w.Write(offset);
        offset += pngs[i].Length;
    }
    foreach (var p in pngs) w.Write(p);
}

// ---- 部品 ----

void Page(Graphics g, float x, float y, float w, float h)
{
    // 右上が折れた書類
    float fold = w * 0.3f;
    var pts = new[] { new PointF(x, y), new PointF(x + w - fold, y), new PointF(x + w, y + fold), new PointF(x + w, y + h), new PointF(x, y + h) };
    g.FillPolygon(Brushes.White, pts);
    using var pen = new Pen(Color.FromArgb(70, 70, 70), 3f) { LineJoin = LineJoin.Round };
    g.DrawPolygon(pen, pts);
    g.DrawLines(pen, [new PointF(x + w - fold, y), new PointF(x + w - fold, y + fold), new PointF(x + w, y + fold)]);
    using var line = new Pen(Color.FromArgb(150, 150, 150), 2.5f);
    for (float ly = y + h * 0.4f; ly < y + h - 6; ly += 7) g.DrawLine(line, x + 6, ly, x + w - 7, ly);
}

void Arrow(Graphics g, Color color, PointF from, PointF to, float width)
{
    using var pen = new Pen(color, width) { CustomEndCap = new AdjustableArrowCap(2.2f, 2.2f, true), StartCap = LineCap.Round };
    g.DrawLine(pen, from, to);
}

// 読込み: 書類に青い矢印が入る
void DrawLoad(Graphics g)
{
    Page(g, 18, 6, 38, 52);
    Arrow(g, Color.FromArgb(30, 100, 220), new PointF(4, 34), new PointF(34, 34), 7);
}

// 保存: 書類から緑の矢印が出る (重ねた書類)
void DrawSave(Graphics g)
{
    Page(g, 14, 4, 34, 46);
    Page(g, 22, 12, 34, 46);
    Arrow(g, Color.FromArgb(30, 150, 60), new PointF(39, 30), new PointF(39, 60), 7);
}

// 再生: モニターに再生マーク
void DrawPlay(Graphics g)
{
    Monitor(g);
    using var b = new SolidBrush(Color.FromArgb(40, 170, 70));
    g.FillPolygon(b, [new PointF(26, 15), new PointF(26, 37), new PointF(43, 26)]);
}

// 停止: 再生・記録どちらの停止にも使う汎用の停止マーク (白い枠に赤い四角)
void DrawStop(Graphics g)
{
    using var pen = new Pen(Color.FromArgb(60, 60, 70), 4f);
    using var frame = Rounded(new RectangleF(5, 5, 54, 54), 10);
    g.FillPath(Brushes.White, frame);
    g.DrawPath(pen, frame);
    using var b = new SolidBrush(Color.FromArgb(215, 40, 35));
    g.FillRectangle(b, 18, 18, 28, 28);
}

void Monitor(Graphics g)
{
    using var frame = new SolidBrush(Color.FromArgb(90, 90, 100));
    using var screen = new LinearGradientBrush(new PointF(0, 8), new PointF(0, 44), Color.FromArgb(225, 240, 255), Color.FromArgb(170, 205, 240));
    using var pen = new Pen(Color.FromArgb(50, 50, 60), 3f);
    g.FillRectangle(frame, 26, 46, 12, 8);
    g.FillRectangle(frame, 16, 52, 32, 6);
    var r = new RectangleF(6, 6, 52, 40);
    using (var path = Rounded(r, 4)) { g.FillPath(frame, path); g.DrawPath(pen, path); }
    g.FillRectangle(screen, 11, 11, 42, 30);
}

// 記録: ビデオカメラと赤い録画ランプ
void DrawRecord(Graphics g)
{
    using var body = new LinearGradientBrush(new PointF(0, 22), new PointF(0, 54), Color.FromArgb(110, 110, 120), Color.FromArgb(40, 40, 50));
    using var pen = new Pen(Color.FromArgb(30, 30, 35), 3f);
    // フィルムリール
    g.FillEllipse(body, 8, 4, 20, 20); g.DrawEllipse(pen, 8, 4, 20, 20);
    g.FillEllipse(body, 26, 4, 20, 20); g.DrawEllipse(pen, 26, 4, 20, 20);
    // 本体
    using (var path = Rounded(new RectangleF(6, 24, 40, 30), 5)) { g.FillPath(body, path); g.DrawPath(pen, path); }
    // レンズ
    var lens = new[] { new PointF(46, 33), new PointF(60, 25), new PointF(60, 53), new PointF(46, 45) };
    g.FillPolygon(body, lens); g.DrawPolygon(pen, lens);
    // 録画ランプ
    g.FillEllipse(Brushes.Red, 12, 30, 10, 10);
}

// 設定: スパナとドライバーの交差
void DrawSettings(Graphics g)
{
    // ドライバー (黄色の柄)
    var state = g.Save();
    g.TranslateTransform(32, 32); g.RotateTransform(45);
    using var shaft = new SolidBrush(Color.FromArgb(160, 160, 170));
    using var pen = new Pen(Color.FromArgb(40, 40, 40), 2.5f);
    g.FillRectangle(shaft, -3, -28, 6, 30); g.DrawRectangle(pen, -3, -28, 6, 30);
    using (var handle = Rounded(new RectangleF(-7, 2, 14, 26), 5))
    {
        using var yb = new SolidBrush(Color.FromArgb(240, 190, 30));
        g.FillPath(yb, handle); g.DrawPath(pen, handle);
    }
    g.Restore(state);

    // スパナ (グレー)
    state = g.Save();
    g.TranslateTransform(32, 32); g.RotateTransform(-45);
    using var wrench = new LinearGradientBrush(new PointF(-13, -32), new PointF(13, 26), Color.FromArgb(200, 200, 210), Color.FromArgb(120, 120, 130));
    // 口の部分は塗らずに切り抜く (下のドライバーが見えるように)
    var mouth = new RectangleF(-5, -34, 10, 14);
    g.SetClip(mouth, CombineMode.Exclude);
    using (var bar = Rounded(new RectangleF(-5, -16, 10, 42), 4)) { g.FillPath(wrench, bar); g.DrawPath(pen, bar); }
    g.FillEllipse(wrench, -13, -32, 26, 24); g.DrawEllipse(pen, -13, -32, 26, 24);
    g.ResetClip();
    // 口の内側の輪郭
    using (var head = new GraphicsPath())
    {
        head.AddEllipse(-13, -32, 26, 24);
        g.SetClip(head);
        g.DrawLines(pen, [new PointF(mouth.Left, mouth.Top), new PointF(mouth.Left, mouth.Bottom), new PointF(mouth.Right, mouth.Bottom), new PointF(mouth.Right, mouth.Top)]);
        g.ResetClip();
    }
    g.Restore(state);
}

// アプリ: 青地にロボットの顔 (赤いアンテナ)
void DrawAppIcon(Graphics g)
{
    var blue = Color.FromArgb(43, 111, 224);
    using var bg = new SolidBrush(blue);
    using (var path = Rounded(new RectangleF(2, 2, 60, 60), 12)) g.FillPath(bg, path);
    // アンテナ
    using var antenna = new Pen(Color.White, 3f);
    g.DrawLine(antenna, 32, 20, 32, 10);
    using var red = new SolidBrush(Color.FromArgb(230, 50, 40));
    g.FillEllipse(red, 28.5f, 5.5f, 7, 7);
    // 顔
    using (var path = Rounded(new RectangleF(14, 20, 36, 28), 7)) g.FillPath(Brushes.White, path);
    g.FillEllipse(bg, 21, 29, 8, 8);
    g.FillEllipse(bg, 35, 29, 8, 8);
    using (var path = Rounded(new RectangleF(25, 40, 14, 3), 1.5f)) g.FillPath(bg, path);
}

GraphicsPath Rounded(RectangleF r, float radius)
{
    var p = new GraphicsPath();
    float d = radius * 2;
    p.AddArc(r.X, r.Y, d, d, 180, 90);
    p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
    p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
    p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
    p.CloseFigure();
    return p;
}
