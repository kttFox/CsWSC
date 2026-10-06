using System.Diagnostics;
using System.Windows.Automation;

namespace CsWSC;

/// <summary>UI Automation によるコントロールの特定と操作。</summary>
internal static class UiAutomation
{
    /// <summary>画面上の点にあるコントロールの名前と種類。取得できなければ null。</summary>
    public static (string Name, string ControlType)? ElementAt(int x, int y)
    {
        try
        {
            var el = AutomationElement.FromPoint(new System.Windows.Point(x, y));
            var name = el.Current.Name;
            if (string.IsNullOrWhiteSpace(name)) return null;
            return (name, el.Current.ControlType.ProgrammaticName.Replace("ControlType.", ""));
        }
        catch (Exception) { return null; }
    }

    public static bool ClickItem(IntPtr window, string name, double timeout)
    {
        var sw = Stopwatch.StartNew();
        do
        {
            var el = Find(window, name);
            if (el != null)
            {
                Invoke(el);
                return true;
            }
            Thread.Sleep(100);
        } while (sw.Elapsed.TotalSeconds < timeout);
        return false;
    }

    private static AutomationElement? Find(IntPtr window, string name)
    {
        try
        {
            var root = AutomationElement.FromHandle(window);
            var cond = new AndCondition(
                new PropertyCondition(AutomationElement.NameProperty, name),
                new PropertyCondition(AutomationElement.IsEnabledProperty, true));
            return root.FindFirst(TreeScope.Descendants, cond);
        }
        catch (ElementNotAvailableException) { return null; }
    }

    // ボタンは Invoke、チェックボックスは Toggle、リスト項目は Select。どれも無ければ中心をクリック
    private static void Invoke(AutomationElement el)
    {
        if (el.TryGetCurrentPattern(InvokePattern.Pattern, out var p) && p is InvokePattern invoke) invoke.Invoke();
        else if (el.TryGetCurrentPattern(TogglePattern.Pattern, out p) && p is TogglePattern toggle) toggle.Toggle();
        else if (el.TryGetCurrentPattern(SelectionItemPattern.Pattern, out p) && p is SelectionItemPattern sel) sel.Select();
        else if (el.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out p) && p is ExpandCollapsePattern ec)
        {
            if (ec.Current.ExpandCollapseState == ExpandCollapseState.Collapsed) ec.Expand(); else ec.Collapse();
        }
        else
        {
            var r = el.Current.BoundingRectangle;
            Input.Click((int)(r.X + r.Width / 2), (int)(r.Y + r.Height / 2), MouseButton.Left);
        }
    }

    // ---- CHKBTN / GETITEM / GETSLCTLST / GETSLIDER / SETSLIDER / POSACC ----

    /// <summary>ボタン類の状態。1: オン, 0: オフ, 2: 不定, -1: 見つからない / 状態を持たない。</summary>
    public static int CheckState(IntPtr window, string name)
    {
        try
        {
            var el = AutomationElement.FromHandle(window).FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, name));
            if (el == null) return -1;
            if (el.TryGetCurrentPattern(TogglePattern.Pattern, out var p) && p is TogglePattern t)
                return t.Current.ToggleState switch { ToggleState.On => 1, ToggleState.Off => 0, _ => 2 };
            if (el.TryGetCurrentPattern(SelectionItemPattern.Pattern, out p) && p is SelectionItemPattern s)
                return s.Current.IsSelected ? 1 : 0;
            return el.Current.ControlType == ControlType.Button ? 0 : -1;
        }
        catch (ElementNotAvailableException) { return -1; }
    }

    private static readonly (ItemKind Kind, ControlType Type)[] KindMap =
    [
        (ItemKind.Button, ControlType.Button), (ItemKind.CheckBox, ControlType.CheckBox),
        (ItemKind.RadioButton, ControlType.RadioButton), (ItemKind.Text, ControlType.Text),
        (ItemKind.Edit, ControlType.Edit), (ItemKind.ListItem, ControlType.ListItem),
        (ItemKind.ComboBox, ControlType.ComboBox), (ItemKind.Tab, ControlType.TabItem),
        (ItemKind.Menu, ControlType.MenuItem), (ItemKind.TreeItem, ControlType.TreeItem),
        (ItemKind.DataItem, ControlType.DataItem), (ItemKind.Link, ControlType.Hyperlink),
        (ItemKind.Group, ControlType.Group),
    ];

    /// <summary>指定種類のコントロールの表示文字列を列挙する。エディット・コンボは値を返す。</summary>
    public static string[] GetItems(IntPtr window, ItemKind kinds)
    {
        var types = KindMap.Where(k => kinds.HasFlag(k.Kind))
            .Select(k => (Condition)new PropertyCondition(AutomationElement.ControlTypeProperty, k.Type)).ToArray();
        if (types.Length == 0) return [];
        try
        {
            var cond = types.Length == 1 ? types[0] : new OrCondition(types);
            return AutomationElement.FromHandle(window).FindAll(TreeScope.Descendants, cond).Cast<AutomationElement>()
                .Select(TextOf).Where(s => !string.IsNullOrEmpty(s)).ToArray();
        }
        catch (ElementNotAvailableException) { return []; }
    }

    private static string TextOf(AutomationElement el)
    {
        var ct = el.Current.ControlType;
        if ((ct == ControlType.Edit || ct == ControlType.ComboBox) &&
            el.TryGetCurrentPattern(ValuePattern.Pattern, out var p) && p is ValuePattern v) return v.Current.Value;
        return el.Current.Name;
    }

    private static Condition TypeIn(params ControlType[] types) =>
        new OrCondition(types.Select(t => (Condition)new PropertyCondition(AutomationElement.ControlTypeProperty, t)).ToArray());

    private static readonly Condition SelectableCond =
        TypeIn(ControlType.List, ControlType.ComboBox, ControlType.DataGrid, ControlType.Tree, ControlType.Table);

    /// <summary>n 番目 (1 から) のリスト / コンボ / リストビュー / ツリーで選択中の項目名。</summary>
    public static string[] GetSelected(IntPtr window, int index)
    {
        try
        {
            var lists = AutomationElement.FromHandle(window).FindAll(TreeScope.Descendants, SelectableCond);
            if (index < 1 || index > lists.Count) return [];
            var list = lists[index - 1];
            if (list.TryGetCurrentPattern(SelectionPattern.Pattern, out var p) && p is SelectionPattern sel)
                return sel.Current.GetSelection().Select(e => e.Current.Name).ToArray();
            // SelectionPattern を持たないコンボは値で代用
            return list.TryGetCurrentPattern(ValuePattern.Pattern, out p) && p is ValuePattern v ? [v.Current.Value] : [];
        }
        catch (ElementNotAvailableException) { return []; }
    }

    private static readonly Condition RangeCond =
        TypeIn(ControlType.Slider, ControlType.ScrollBar, ControlType.Spinner, ControlType.ProgressBar);

    private static RangeValuePattern? Range(IntPtr window, int index)
    {
        var els = AutomationElement.FromHandle(window).FindAll(TreeScope.Descendants, RangeCond).Cast<AutomationElement>()
            .Where(e => e.TryGetCurrentPattern(RangeValuePattern.Pattern, out _)).ToArray();
        return index >= 1 && index <= els.Length ? (RangeValuePattern)els[index - 1].GetCurrentPattern(RangeValuePattern.Pattern) : null;
    }

    /// <summary>n 番目のスライダー類の値。見つからなければ null。</summary>
    public static SliderInfo? GetSlider(IntPtr window, int index)
    {
        try
        {
            var r = Range(window, index);
            return r == null ? null : new SliderInfo(r.Current.Value, r.Current.Minimum, r.Current.Maximum, r.Current.SmallChange, r.Current.LargeChange);
        }
        catch (ElementNotAvailableException) { return null; }
    }

    public static bool SetSlider(IntPtr window, int index, double value)
    {
        try
        {
            var r = Range(window, index);
            if (r == null || r.Current.IsReadOnly) return false;
            r.SetValue(Math.Clamp(value, r.Current.Minimum, r.Current.Maximum));
            return true;
        }
        catch (Exception e) when (e is ElementNotAvailableException or InvalidOperationException or ArgumentOutOfRangeException) { return false; }
    }

    /// <summary>画面上の点にあるコントロールの文字列 (名前、無ければ値)。</summary>
    public static string TextAt(int x, int y)
    {
        try
        {
            var el = AutomationElement.FromPoint(new System.Windows.Point(x, y));
            var name = el.Current.Name;
            if (!string.IsNullOrEmpty(name)) return name;
            return el.TryGetCurrentPattern(ValuePattern.Pattern, out var p) && p is ValuePattern v ? v.Current.Value : "";
        }
        catch (Exception) { return ""; }
    }
}

/// <summary>GETITEM で取得するコントロールの種類 (組み合わせ可)。</summary>
[Flags]
public enum ItemKind
{
    Button = 1, CheckBox = 2, RadioButton = 4, Text = 8, Edit = 16, ListItem = 32, ComboBox = 64,
    Tab = 128, Menu = 256, TreeItem = 512, DataItem = 1024, Link = 2048, Group = 4096,
    Buttons = Button | CheckBox | RadioButton,
    All = 0x1FFF,
}

/// <summary>スライダー類の値と範囲。</summary>
public sealed record SliderInfo(double Value, double Minimum, double Maximum, double SmallChange, double LargeChange);
