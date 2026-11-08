using System.Text.Json;
namespace StellarDrive.Installer;

public static class Json {
    public static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    public static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? throw new InvalidDataException("Pusty plik JSON.");
    public static void Save<T>(string path, T value) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        File.Move(temp, path, true);
    }
}
public class Pack {
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string GameVersion { get; set; } = "";
    public string SteamAppId { get; set; } = "4689730";
    public Dictionary<string,string> GameHashes { get; set; } = new();
    public string LoaderVersion { get; set; } = "";
    public string LoaderArchiveId { get; set; } = "";
    public string Changelog { get; set; } = "";
    public List<ArchiveSpec> Archives { get; set; } = new();
    public List<ModSpec> Mods { get; set; } = new();
}
public class ArchiveSpec {
    public string Id { get; set; } = "";
    public string Url { get; set; } = "";
    public string Sha256 { get; set; } = "";
}
public class ModSpec {
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string Compatibility { get; set; } = "";
    public string License { get; set; } = "";
    public bool Redistributable { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public List<FileSpec> Files { get; set; } = new();
}
public class FileSpec {
    public string Target { get; set; } = "";
    public string ArchiveId { get; set; } = "";
    public string Entry { get; set; } = "";
    public string? DownloadUrl { get; set; }
    public string Sha256 { get; set; } = "";
    public bool PreserveExisting { get; set; }
}
public class OwnedFile {
    public string Target { get; set; } = "";
    public string? InstalledHash { get; set; }
    public string? OriginalBackup { get; set; }
    public string? OriginalHash { get; set; }
}
public class InstallState {
    public string PackId { get; set; } = "";
    public string PackVersion { get; set; } = "";
    public string GameDir { get; set; } = "";
    public List<OwnedFile> Files { get; set; } = new();
    public Dictionary<string,string> Mods { get; set; } = new();
}
public class Journal {
    public string GameDir { get; set; } = "";
    public string Status { get; set; } = "pending";
    public List<JournalEntry> Entries { get; set; } = new();
}
public class JournalEntry {
    public string Target { get; set; } = "";
    public string? Backup { get; set; }
    public string? BeforeHash { get; set; }
    public string? AfterHash { get; set; }
}
public record ProgressInfo(double Percent, string Message);
public class UserSettings {
    public string GameDir { get; set; } = "";
    public string UpdateUrl { get; set; } = "https://stella-drive-mods-odyssey.apttide20.chatgpt.site/api/updates";
    public string SkippedVersion { get; set; } = "";
    public bool Offline { get; set; }
}
