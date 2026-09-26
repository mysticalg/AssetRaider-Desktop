using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;

namespace AssetRaider.Setup;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Contains("--verify-install")) return Verify(args.Last());
        ApplicationConfiguration.Initialize();
        Application.Run(new SetupForm(args.Contains("--uninstall")));
        return 0;
    }
    static int Verify(string report)
    {
        var root = Path.Combine(Path.GetTempPath(), "AssetRaider-Installer-QA");
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, "AssetRaider-" + Guid.NewGuid().ToString("N"));
        try
        {
            InstallCore.Extract(target);
            if (!File.Exists(Path.Combine(target, ".playwright", "node", "win32_x64", "node.exe"))) throw new IOException("Missing browser driver.");
            var checkDir = Path.Combine(root, "checks-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(checkDir);
            var start = new ProcessStartInfo(Path.Combine(target, "AssetRaider.exe")) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--launch-check"); start.ArgumentList.Add(checkDir);
            using var check = Process.Start(start)!;
            if (!check.WaitForExit(30000) || check.ExitCode != 0) throw new IOException("Installed app failed its launch/data checks.");
            try { InstallCore.DeleteOwnedDirectory(root, root); throw new Exception("Root directory deletion was accepted."); } catch (InvalidOperationException) { }
            InstallCore.DeleteOwnedDirectory(target, root);
            if (Directory.Exists(target)) throw new IOException("Uninstall cleanup failed.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);
            File.WriteAllText(report, JsonSerializer.Serialize(new { Passed = true, Checks = new[] { "Embedded ZIP extracted", "Hidden Playwright driver present", "Installed app data checks passed", "Deletion boundary enforced", "Owned files removed" } }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex) { File.WriteAllText(report, ex.ToString()); return 1; }
    }
}

internal static class InstallCore
{
    const string Marker = ".assetraider-install";
    const string MarkerValue = "AssetRaider-Desktop-873bcc60-5de0-45dc-b49c-30b6b0a516fb";
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\AssetRaiderDesktop";
    public static string ProgramsRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");
    public static string Target => Path.Combine(ProgramsRoot, "AssetRaider");
    static string InstallerHome => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AssetRaiderInstaller");
    static string Shortcut => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "AssetRaider.lnk");

    public static void Extract(string target)
    {
        if (Directory.Exists(target)) throw new IOException("The destination already exists.");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, Marker), MarkerValue);
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip") ?? throw new IOException("Installer payload is missing.");
        using var archive = new ZipArchive(resource, ZipArchiveMode.Read);
        archive.ExtractToDirectory(target, false);
        if (!File.Exists(Path.Combine(target, "AssetRaider.exe"))) throw new IOException("The app was not extracted.");
    }
    public static void DeleteOwnedDirectory(string path, string root)
    {
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        var boundary = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(boundary, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(Path.Combine(full, Marker)) || File.ReadAllText(Path.Combine(full, Marker)) != MarkerValue)
            throw new InvalidOperationException("Refusing to remove a directory that this installer does not own.");
        if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Refusing to remove a linked installation folder.");
        Directory.Delete(full, true);
    }
    public static void EnsureStopped()
    {
        if (Process.GetProcessesByName("AssetRaider").Length > 0) throw new IOException("Close AssetRaider before installing or uninstalling it.");
    }
    public static void Install()
    {
        EnsureStopped();
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348) || !Environment.Is64BitOperatingSystem) throw new PlatformNotSupportedException("AssetRaider requires Windows 11 x64 (or Windows build 20348+).");
        Directory.CreateDirectory(ProgramsRoot);
        if (Directory.Exists(Target) && (!File.Exists(Path.Combine(Target, Marker)) || File.ReadAllText(Path.Combine(Target, Marker)) != MarkerValue)) throw new IOException("The install folder contains files not owned by this installer. Move that folder before installing.");
        var stage = Target + ".new-" + Guid.NewGuid().ToString("N");
        var backup = Target + ".backup-" + Guid.NewGuid().ToString("N");
        Extract(stage);
        try
        {
            if (Directory.Exists(Target)) Directory.Move(Target, backup);
            Directory.Move(stage, Target);
            Directory.CreateDirectory(InstallerHome);
            var helper = Path.Combine(InstallerHome, "AssetRaider.Setup.exe");
            if (!string.Equals(Environment.ProcessPath, helper, StringComparison.OrdinalIgnoreCase)) File.Copy(Environment.ProcessPath!, helper, true);
            using var key = Registry.CurrentUser.CreateSubKey(UninstallKey);
            key.SetValue("DisplayName", "AssetRaider Desktop"); key.SetValue("DisplayVersion", "0.3.1 beta");
            key.SetValue("Publisher", "mysticalg"); key.SetValue("InstallLocation", Target);
            key.SetValue("DisplayIcon", Path.Combine(Target, "AssetRaider.exe"));
            key.SetValue("UninstallString", $"\"{helper}\" --uninstall");
            key.SetValue("NoModify", 1); key.SetValue("NoRepair", 1);
            key.SetValue("URLInfoAbout", "https://mysticalg.github.io/AssetRaider-Desktop/");
            CreateShortcut();
        }
        catch
        {
            if (Directory.Exists(backup))
            {
                if (Directory.Exists(Target)) DeleteOwnedDirectory(Target, ProgramsRoot);
                Directory.Move(backup, Target);
            }
            throw;
        }
        finally { if (Directory.Exists(stage)) DeleteOwnedDirectory(stage, ProgramsRoot); }
        if (Directory.Exists(backup)) DeleteOwnedDirectory(backup, ProgramsRoot);
    }
    static void CreateShortcut()
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new IOException("Windows shortcut support is unavailable.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic link = shell.CreateShortcut(Shortcut);
        link.TargetPath = Path.Combine(Target, "AssetRaider.exe"); link.WorkingDirectory = Target;
        link.Description = "Udio + Suno playback to WAV"; link.Save();
        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);
        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
    }
    public static void Uninstall()
    {
        EnsureStopped();
        if (Directory.Exists(Target)) DeleteOwnedDirectory(Target, ProgramsRoot);
        if (File.Exists(Shortcut)) File.Delete(Shortcut);
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
        // Keep the helper outside the app folder; no delayed self-deleting shell command is used.
        // Browser profiles and recordings belong to the user and are never removed here.
    }
}

internal sealed class SetupForm : Form
{
    public SetupForm(bool uninstall)
    {
        Text = uninstall ? "Uninstall AssetRaider" : "Install AssetRaider";
        ClientSize = new Size(600, 360); StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
        Font = new Font("Segoe UI", 10); BackColor = Color.FromArgb(246, 247, 252);
        var heading = new Label { Text = "AssetRaider", Font = new Font("Segoe UI", 25, FontStyle.Bold), Location = new Point(28, 24), AutoSize = true };
        var detail = new Label { Location = new Point(30, 88), Size = new Size(535, 118), Text = uninstall ?
            "Remove the installed desktop app and its Start menu shortcut.\n\nYour saved recordings and browser sign-ins will be kept." :
            "Udio + Suno playback to WAV · Version 0.3.1 public beta\n\nInstalls for your Windows account. No administrator access needed.\nRequires Windows 11 x64 and Google Chrome.\nThis beta has not completed full signed-in site testing." };
        var location = new Label { Text = "Location: " + InstallCore.Target, Location = new Point(30, 210), Size = new Size(535, 45) };
        var status = new Label { Location = new Point(30, 263), Size = new Size(535, 40) };
        var action = new Button { Text = uninstall ? "Uninstall" : "Install", Location = new Point(438, 310), Size = new Size(130, 34), BackColor = Color.FromArgb(68, 60, 195), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var completed = false;
        action.Click += async (_, _) => {
            if (completed) { Close(); return; }
            action.Enabled = false; status.Text = uninstall ? "Removing app files…" : "Installing…";
            try
            {
                await Task.Run(() => { if (uninstall) InstallCore.Uninstall(); else InstallCore.Install(); });
                status.Text = uninstall ? "Uninstalled. Your recordings and sign-ins are kept." : "Installed. Open AssetRaider from the Start menu.";
                action.Text = "Close";
                completed = true;
            }
            catch (Exception ex) { status.Text = ex.Message; }
            finally { action.Enabled = true; }
        };
        Controls.AddRange([heading, detail, location, status, action]);
    }
}
