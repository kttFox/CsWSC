using System.Drawing.Drawing2D;

namespace CsWSC;

/// <summary>吹き出しの「しっぽ」の位置。指定座標をしっぽの先が指す。</summary>
public enum BalloonTail { None, TopLeft, TopRight, BottomLeft, BottomRight }

/// <summary>
/// 表示中の吹き出し。<c>Fukidasi(...)</c> が返す。複数同時に表示できる。
/// </summary>
/// <remarks>
/// <see cref="Close"/> (または using) で消す。スクリプトが終わると自動で消える。
/// </remarks>
public sealed class Balloon : IDisposable
{
    private readonly BalloonHost _host;
    private readonly BalloonHost.BalloonForm _form;
    private string _message;
    private Point _target;
    private readonly BalloonTail _tail;
    private readonly float _fontSize;
    private readonly Color _back, _fore;

    internal Balloon(BalloonHost host, string message, Point target, BalloonTail tail, float fontSize, Color back, Color fore)
    {
        _host = host;
        _message = message;
        _target = target;
        _tail = tail;
        _fontSize = fontSize;
        _back = back;
        _fore = fore;
        _form = host.CreateForm();
        Refresh();
    }

    /// <summary>表示中の文字列。</summary>
    public string Message => _message;

    /// <summary>しっぽの先が指す位置 (画面座標。MouseOrg の影響を受けない)。</summary>
    public Point Target => _target;

    /// <summary><see cref="Close"/> 済み、またはスクリプト終了で消えていれば true。</summary>
    public bool IsClosed => !_host.IsOpen(_form);

    /// <summary>
    /// 文字列や位置を変えて表示し直す。省略した値はそのまま。
    /// </summary>
    /// <param name="message">新しい文字列。省略すると変えない。</param>
    /// <param name="point">しっぽの先が指す座標 (画面座標。MouseOrg の影響を受けない)。省略すると変えない。</param>
    /// <returns>この吹き出し自身。</returns>
    public Balloon Update(string? message = null, Point? point = null)
    {
        if (message != null) _message = BalloonHost.Normalize(message);
        _target = point ?? _target;
        Refresh();
        return this;
    }

    /// <summary>吹き出しを消す。消した後の <see cref="Update"/> は何もしない。</summary>
    public void Close() => _host.CloseForm(_form);

    /// <summary><see cref="Close"/> と同じ。using で一時的に表示するときに使う。</summary>
    public void Dispose() => Close();

    private void Refresh()
    {
        var (message, target) = (_message, _target);
        _host.Run(_form, () => _form.Display(message, target, _tail, _fontSize, _back, _fore));
    }
}

/// <summary>
/// 吹き出し窓をまとめて持つ UI スレッド。スクリプトのスレッドとは別に 1 本だけ作る。
/// 窓は前面に出ず、クリックも透過する。
/// </summary>
internal sealed class BalloonHost : IDisposable
{
    private Control? _invoker;
    private readonly List<BalloonForm> _forms = [];
    private readonly object _lock = new();

    internal static string Normalize(string message) => message.ReplaceLineEndings("\r\n");

    internal BalloonForm CreateForm()
    {
        var invoker = EnsureThread();
        var form = (BalloonForm)invoker.Invoke(() =>
        {
            var f = new BalloonForm();
            _ = f.Handle;
            return f;
        });
        lock (_lock) _forms.Add(form);
        return form;
    }

    internal bool IsOpen(BalloonForm form)
    {
        lock (_lock) return _forms.Contains(form);
    }

    internal void Run(BalloonForm form, Action action)
    {
        if (!IsOpen(form)) return;
        try { form.Invoke(action); }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) when (form.IsDisposed) { }
    }

    internal void CloseForm(BalloonForm form)
    {
        lock (_lock) _forms.Remove(form);
        if (form is { IsDisposed: false, IsHandleCreated: true }) form.BeginInvoke(form.Close);
    }

    /// <summary>表示中の吹き出しをすべて消す。</summary>
    public void CloseAll()
    {
        BalloonForm[] forms;
        lock (_lock) forms = [.. _forms];
        foreach (var f in forms) CloseForm(f);
    }

    public void Dispose()
    {
        CloseAll();
        lock (_lock)
        {
            if (_invoker is { IsDisposed: false } inv) inv.BeginInvoke(Application.ExitThread);
            _invoker = null;
        }
    }

    private Control EnsureThread()
    {
        lock (_lock)
        {
            if (_invoker is { IsDisposed: false }) return _invoker;
            using var ready = new ManualResetEventSlim();
            var thread = new System.Threading.Thread(() =>
            {
                var invoker = new Control();
                _ = invoker.Handle; // Invoke できるようハンドルを作る
                _invoker = invoker;
                ready.Set();
                Application.Run();
                invoker.Dispose();
            }) { IsBackground = true, Name = "CsWSC FUKIDASI" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.Wait();
            return _invoker!;
        }
    }

    internal sealed class BalloonForm : Form
    {
        private const int TextMargin = 8, TailSize = 12, Radius = 8;
        private const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;

        private string _message = "";
        private BalloonTail _tail;
        private GraphicsPath? _path;

        public BalloonForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            DoubleBuffered = true;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        public void Display(string message, Point target, BalloonTail tail, float fontSize, Color back, Color fore)
        {
            _message = message;
            _tail = tail;
            Font = new Font(SystemFonts.MessageBoxFont?.FontFamily ?? FontFamily.GenericSansSerif, fontSize);
            BackColor = back;
            ForeColor = fore;

            var text = TextRenderer.MeasureText(message, Font, Size.Empty, TextFormatFlags.NoPrefix);
            var body = new Rectangle(0, 0, text.Width + TextMargin * 2, text.Height + TextMargin * 2);
            var hasTail = tail != BalloonTail.None;
            var top = tail is BalloonTail.TopLeft or BalloonTail.TopRight;
            var left = tail is BalloonTail.TopLeft or BalloonTail.BottomLeft;
            if (hasTail && top) body.Offset(0, TailSize);
            var size = new Size(body.Width, body.Height + (hasTail ? TailSize : 0));

            // しっぽの先 (フォーム内座標)
            var tipX = left ? Radius : size.Width - Radius;
            var tip = new Point(tipX, top ? 0 : size.Height);

            _path?.Dispose();
            _path = BuildPath(body, hasTail ? tip : null, left, top);
            Region = new Region(_path);

            var location = hasTail ? new Point(target.X - tip.X, target.Y - tip.Y) : target;
            Bounds = new Rectangle(KeepOnScreen(location, size), size);
            Invalidate();
            Visible = true;
        }

        private static Point KeepOnScreen(Point p, Size size)
        {
            var area = Screen.FromPoint(p).WorkingArea;
            return new Point(
                Math.Clamp(p.X, area.Left, Math.Max(area.Left, area.Right - size.Width)),
                Math.Clamp(p.Y, area.Top, Math.Max(area.Top, area.Bottom - size.Height)));
        }

        private static GraphicsPath BuildPath(Rectangle r, Point? tip, bool left, bool top)
        {
            var path = new GraphicsPath();
            const int d = Radius * 2;
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d - 1, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d - 1, d, d, 90, 90);
            path.CloseFigure();
            if (tip is { } t)
            {
                var baseY = top ? r.Top + 1 : r.Bottom - 2;
                var x1 = left ? r.Left + Radius : r.Right - Radius - TailSize;
                path.AddPolygon([new Point(x1, baseY), new Point(x1 + TailSize, baseY), t]);
            }
            path.FillMode = FillMode.Winding;
            return path;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_path == null) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var border = new Pen(Color.FromArgb(160, ForeColor));
            e.Graphics.DrawPath(border, _path);
            var bodyTop = _tail is BalloonTail.TopLeft or BalloonTail.TopRight ? TailSize : 0;
            TextRenderer.DrawText(e.Graphics, _message, Font, new Point(TextMargin, bodyTop + TextMargin), ForeColor, TextFormatFlags.NoPrefix);
        }
    }
}
