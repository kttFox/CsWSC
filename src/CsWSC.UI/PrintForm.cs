
namespace CsWSC.UI;

/// <summary>Print() の出力窓。閉じても破棄せず隠すだけ。</summary>
public partial class PrintForm : Form, ILogWindow
{
    // LogPrint(false) で隠されたら、Print しても自動表示しない
    private bool _suppressed;

    public PrintForm()
    {
        InitializeComponent();
    }

    public void Clear() => OnUi(outputTextBox.Clear);

    /// <summary>スクリプト開始時の初期化。内容を消し、LogPrint の非表示指定も解除する。</summary>
    public void Reset()
    {
        _suppressed = false;
        outputTextBox.Clear();
    }

    public void AppendLine(string text)
    {
        if (!Visible && !_suppressed) Show(Owner);
        outputTextBox.AppendText(text + Environment.NewLine);
    }

    void ILogWindow.SetVisible(bool visible) => OnUi(() =>
    {
        _suppressed = !visible;
        if (visible && !Visible) Show(Owner);
        else if (!visible && Visible) Hide();
    });

    void ILogWindow.SetBounds(int? x, int? y, int? width, int? height) => OnUi(() =>
        SetBounds(x ?? Left, y ?? Top, width ?? Width, height ?? Height));

    // スクリプトのスレッドから呼ばれるので UI スレッドで同期実行する
    private void OnUi(Action action)
    {
        if (InvokeRequired) Invoke(action);
        else action();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnFormClosing(e);
    }
}
