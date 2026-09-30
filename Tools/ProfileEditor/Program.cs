namespace ModbusExt.ProfileEditor;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        MessageFilter.Register();
        try { Application.Run(new MainForm()); }
        finally { MessageFilter.Revoke(); }
    }
}