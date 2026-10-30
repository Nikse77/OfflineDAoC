namespace OfflineDaoc.Launcher;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--prepare-portable-account")
        {
            // the new client's launcher keeps its account with the game it plays (server_root.txt)
            PortableCredentials.ReadOrCreate(MainForm.ServerRootFor(AppContext.BaseDirectory));
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) =>
        {
            // A known WinForms bug: a tooltip timer fires for a tool that was just replaced. Harmless, so no crash box.
            if (e.Exception is KeyNotFoundException && e.Exception.StackTrace?.Contains("KeyboardToolTipStateMachine") == true)
                return;
            using ThreadExceptionDialog dialog = new(e.Exception);
            if (dialog.ShowDialog() == DialogResult.Abort)
                Application.Exit();
        };
        Application.Run(new MainForm());
    }
}
