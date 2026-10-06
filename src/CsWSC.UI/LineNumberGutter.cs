namespace CsWSC.UI;

/// <summary>複数行 TextBox の左に置く行番号表示。</summary>
internal sealed class LineNumberGutter : Control
{
    private const int WM_PAINT = 0x000F;
    private readonly TextBox _target;

    public LineNumberGutter(TextBox target)
    {
        _target = target;
        Dock = DockStyle.Left;
        Font = target.Font;
        ForeColor = SystemColors.GrayText;
        BackColor = SystemColors.Control;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

        // TextBox にはスクロールイベントが無いため、再描画 (スクロール時にも発生) を監視して追従する
        new PaintListener(target, Invalidate);
        target.TextChanged += (_, _) => UpdateWidth();
        target.KeyUp += (_, _) => Invalidate();
        target.MouseUp += (_, _) => Invalidate();
        UpdateWidth();
    }

    private int LineCount => _target.GetLineFromCharIndex(_target.TextLength) + 1;

    private void UpdateWidth()
    {
        var digits = Math.Max(3, LineCount.ToString().Length);
        var width = TextRenderer.MeasureText(new string('0', digits), Font).Width + 8;
        if (Width != width) Width = width;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        var first = _target.GetLineFromCharIndex(_target.GetCharIndexFromPosition(new Point(1, 1)));
        var firstChar = _target.GetFirstCharIndexFromLine(first);
        if (firstChar < 0) return;
        // 末尾の空行は GetPositionFromCharIndex が座標を返さないため、行の高さから計算する
        var top = _target.GetPositionFromCharIndex(firstChar).Y;
        var lineHeight = TextRenderer.MeasureText("0", _target.Font).Height;
        var caretLine = _target.GetLineFromCharIndex(_target.SelectionStart);
        var count = LineCount;

        using var bold = new Font(Font, FontStyle.Bold);
        for (var line = first; line < count; line++)
        {
            var y = top + (line - first) * lineHeight;
            if (y > Height) break;
            var rect = new Rectangle(0, y, Width - 4, lineHeight);
            var current = line == caretLine;
            TextRenderer.DrawText(e.Graphics, (line + 1).ToString(), current ? bold : Font, rect,
                current ? SystemColors.ControlText : ForeColor, TextFormatFlags.Right | TextFormatFlags.NoPadding);
        }
    }

    private sealed class PaintListener : NativeWindow
    {
        private readonly Action _onPaint;

        public PaintListener(Control control, Action onPaint)
        {
            _onPaint = onPaint;
            if (control.IsHandleCreated) AssignHandle(control.Handle);
            control.HandleCreated += (_, _) => AssignHandle(control.Handle);
            control.HandleDestroyed += (_, _) => ReleaseHandle();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT) _onPaint();
        }
    }
}
