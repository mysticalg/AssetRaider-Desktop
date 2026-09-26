namespace AssetRaider.Desktop;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Contains("--self-test")) return SelfTest.RunAsync(args.Last()).GetAwaiter().GetResult();
        if (args.Contains("--launch-check")) return SelfTest.CheckLaunch(args.Last());
        using var mutex = new Mutex(true, "Local\\AssetRaiderDesktop", out var first);
        if (!first) { MessageBox.Show("AssetRaider is already running."); return 0; }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}
