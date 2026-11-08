using Microsoft.Win32;
using System.Diagnostics;
using System.Text.RegularExpressions;
namespace StellarDrive.Installer;

public static class GameLocator {
    public static List<string> FindSteamGames(string appId) {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var steam = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        var home = steam?.GetValue("SteamPath") as string;
        if (!string.IsNullOrWhiteSpace(home)) {
            roots.Add(Path.GetFullPath(home));
            var vdf = Path.Combine(home, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf)) foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                roots.Add(Path.GetFullPath(m.Groups[1].Value.Replace(@"\\", @"\")));
        }
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots) {
            var acf = Path.Combine(root, "steamapps", $"appmanifest_{appId}.acf");
            if (!File.Exists(acf)) continue;
            var m = Regex.Match(File.ReadAllText(acf), "\"installdir\"\\s+\"([^\"]+)\"");
            if (!m.Success) continue;
            var dir = Path.GetFullPath(Path.Combine(root, "steamapps", "common", m.Groups[1].Value));
            if (File.Exists(Path.Combine(dir, "StellarDrive.exe"))) result.Add(dir.TrimEnd(Path.DirectorySeparatorChar));
        }
        return result.ToList();
    }
    public static void AssertClosed(string gameDir) {
        foreach (var process in Process.GetProcessesByName("StellarDrive")) using (process) {
            // Any active copy shares the same save directory; do not mutate while it runs.
            throw new InvalidOperationException("Zamknij StellarDrive przed instalowaniem lub przywracaniem plików.");
        }
    }
    public static void Launch(string gameDir) {
        var games = FindSteamGames("4689730");
        if (!games.Contains(Path.GetFullPath(gameDir).TrimEnd('\\'), StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Uruchom tę kopię przez jej własny skrót. Przycisk obsługuje instalację rozpoznaną w Steam.");
        Process.Start(new ProcessStartInfo("steam://rungameid/4689730") { UseShellExecute = true });
    }
}
