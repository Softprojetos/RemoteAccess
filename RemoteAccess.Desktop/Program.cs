using RemoteAccess.Desktop.Forms;

namespace RemoteAccess.Desktop;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
