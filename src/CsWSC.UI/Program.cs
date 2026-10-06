namespace CsWSC.UI;

static class Program
{
    /// <summary>
    ///  CsWSC.exe [スクリプト.csws]  … 引数を渡すと読み込んで即実行します。
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args.FirstOrDefault()));
    }
}

public class Form : System.Windows.Forms.Form {
	public Form() {
	    this.Icon = System.Drawing.SystemIcons.Application;
	}
}
