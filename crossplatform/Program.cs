using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace AssetRaider.Desktop;

internal static class Program
{
    public static string? UiCheckDirectory { get; private set; }
    [STAThread]
    public static int Main(string[] args)
    {
        if (OperatingSystem.IsMacOS())
        {
            var resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources"));
            if (Directory.Exists(Path.Combine(resources, ".playwright"))) Environment.SetEnvironmentVariable("PLAYWRIGHT_DRIVER_SEARCH_PATH", resources);
        }
        if (args.Contains("--launch-check")) return CrossCheck.Run(args.Last());
        if (args.Contains("--audio-check")) return CrossCheck.AudioAsync(args.Last()).GetAwaiter().GetResult();
        if (args.Contains("--ui-check")) UiCheckDirectory = args.Last();
        using var mutex = new Mutex(true, UiCheckDirectory == null ? "AssetRaiderDesktop" : "AssetRaiderUiCheck-" + Guid.NewGuid().ToString("N"), out var first);
        if (!first) return 0;
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args);
        return 0;
    }
}
public sealed class App : Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark; }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow(); desktop.MainWindow = window;
            if (Program.UiCheckDirectory is { } directory)
                window.Opened += (_, _) => DispatcherTimer.RunOnce(() => {
                    try
                    {
                        Directory.CreateDirectory(directory);
                        using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Width, (int)window.Height), new Vector(96, 96));
                        bitmap.Render(window); bitmap.Save(Path.Combine(directory, "app.png"));
                        File.WriteAllText(Path.Combine(directory, "ui-check.json"), "{\"Passed\":true,\"Rendered\":\"Main window and recording controls\"}");
                        desktop.Shutdown(0);
                    }
                    catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "ui-check.json"), ex.ToString()); desktop.Shutdown(1); }
                }, TimeSpan.FromSeconds(1));
        }
        base.OnFrameworkInitializationCompleted();
    }
}
