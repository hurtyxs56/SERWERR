using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace StellarDrive.Installer;

public sealed class InstallerEngine {
    public const string StateFile = ".stellardrive-modpack.json";
    public string DataDir { get; }
    public string CacheDir => Path.Combine(DataDir, "Cache");
    public string LogFile { get; }
    public string? LocalPayloadDir { get; set; }
    public bool Offline { get; set; }
    public Action<string>? OnLog { get; set; }
    public Action<int>? TestAfterWrite { get; set; }
    private readonly Action<string> assertGameClosed;
    private readonly SemaphoreSlim gate = new(1,1);
    private static readonly HttpClient client = CreateClient();
    private static HttpClient CreateClient() {
        var result = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(10) };
        result.DefaultRequestHeaders.UserAgent.ParseAdd("StellarDriveModpackInstaller/1.0"); return result;
    }
    public InstallerEngine(string? dataDir = null, Action<string>? gameClosedCheck = null) {
        assertGameClosed = gameClosedCheck ?? GameLocator.AssertClosed;
        DataDir = Path.GetFullPath(dataDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StellarDriveModpack"));
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(CacheDir);
        LogFile = Path.Combine(DataDir, "Logs", DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
        Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
    }
    public void Log(string text) { var line = $"[{DateTime.Now:HH:mm:ss}] {text}"; File.AppendAllText(LogFile, line + Environment.NewLine); OnLog?.Invoke(line); }
    public static string Hash(string path) { using var f = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(f)); }
    public static void CheckHash(string path, string expected) {
        if (!File.Exists(path) || !Hash(path).Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Niezgodna suma SHA-256 lub brak pliku: " + path);
    }
    public static string SafePath(string root, string relative) {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':')) throw new InvalidDataException("Niedozwolona ścieżka: " + relative);
        var parts = relative.Replace('\\','/').Split('/');
        if (parts.Any(p => p is ".." or "." or "" || p.EndsWith('.') || p.EndsWith(' ') || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)) throw new InvalidDataException("Nieprawidłowa ścieżka: " + relative);
        var basePath = Path.GetFullPath(root).TrimEnd('\\') + '\\';
        var full = Path.GetFullPath(Path.Combine(basePath, relative));
        if (!full.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Ścieżka wykracza poza folder.");
        for (var p = full; p != null; p = Path.GetDirectoryName(p)) {
            if ((File.Exists(p) || Directory.Exists(p)) && File.GetAttributes(p).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Dowiązanie w ścieżce nie jest obsługiwane: " + p);
        }
        return full;
    }
    private static bool ModTarget(string p) => new[] { "Mods/", "Plugins/", "UserLibs/", "UserData/" }.Any(x => p.Replace('\\','/').StartsWith(x, StringComparison.OrdinalIgnoreCase));
    private static bool LoaderTarget(string p) => p.Equals("version.dll", StringComparison.OrdinalIgnoreCase) || p.Replace('\\','/').StartsWith("MelonLoader/", StringComparison.OrdinalIgnoreCase);
    private static bool LegacyTarget(string p) => p.Replace('\\','/').StartsWith("BepInEx/", StringComparison.OrdinalIgnoreCase) || new[] { "winhttp.dll", "winhttp.dll.melty-off", "doorstop_config.ini", ".doorstop_version" }.Contains(p, StringComparer.OrdinalIgnoreCase);
    public static void ValidateUrl(string value, bool source = true) {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var u) || u.Scheme != "https" || u.UserInfo != "" || !u.IsDefaultPort) throw new InvalidDataException("Wymagany jest prawidłowy adres HTTPS.");
        if (source && !new[] { "github.com", "raw.githubusercontent.com", "codeload.github.com", "release-assets.githubusercontent.com", "objects.githubusercontent.com", "stella-drive-mods-odyssey.tera78326.chatgpt.site", "stella-drive-mods-odyssey.apttide20.chatgpt.site" }.Contains(u.Host.ToLowerInvariant()))
            throw new InvalidDataException("Źródło poza listą autorów i oficjalnego portalu paczki: " + u.Host);
    }
    public static void ValidatePack(Pack pack) {
        if (pack.SchemaVersion != 1 || pack.Id != "stellardrive-odyssey" || pack.SteamAppId != "4689730" || !Version.TryParse(pack.Version, out _)) throw new InvalidDataException("Nieobsługiwany manifest modpacka.");
        if (!pack.GameHashes.ContainsKey("StellarDrive.exe") || !pack.GameHashes.ContainsKey("StellarDrive_Data/Managed/Assembly-CSharp.dll")) throw new InvalidDataException("Brak kontroli wersji gry.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in pack.Archives) { if (!ids.Add(a.Id) || !IsHash(a.Sha256)) throw new InvalidDataException("Błędny opis archiwum."); ValidateUrl(a.Url); }
        if (!ids.Contains(pack.LoaderArchiveId)) throw new InvalidDataException("Brak archiwum MelonLoader.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in pack.Mods) {
            if (!names.Add(m.Id) || m.Files.Count == 0 || !Version.TryParse(m.Version, out _)) throw new InvalidDataException("Błędny opis moda: " + m.Id);
            if (!m.Compatibility.Contains(pack.GameVersion,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Mod nie deklaruje zgodności z wybraną grą: "+m.Name);
            foreach (var f in m.Files) {
                SafePath(Path.GetTempPath(), f.Target);
                if (!ModTarget(f.Target) || !targets.Add(f.Target.Replace('\\','/')) || !IsHash(f.Sha256)) throw new InvalidDataException("Błędny plik moda: " + f.Target);
                if (f.DownloadUrl != null) ValidateUrl(f.DownloadUrl);
                else { if (!ids.Contains(f.ArchiveId)) throw new InvalidDataException("Brak źródła pliku: " + f.Target); SafePath(Path.GetTempPath(), f.Entry); }
            }
        }
        foreach (var m in pack.Mods) foreach (var d in m.Dependencies) if (!names.Contains(d)) throw new InvalidDataException($"Brak zależności {d} wymaganej przez {m.Name}. Dodaj ją do manifestu (np. SDModKit).");
        foreach (var pair in pack.GameHashes) { SafePath(Path.GetTempPath(), pair.Key); if (!IsHash(pair.Value)) throw new InvalidDataException("Błędna suma gry."); }
        Resolve(pack, pack.Mods.Select(x=>x.Id));
    }
    private static bool IsHash(string s) => Regex.IsMatch(s ?? "", "^[a-fA-F0-9]{64}$");
    public static List<ModSpec> Resolve(Pack pack, IEnumerable<string> ids) {
        var result = new List<ModSpec>(); var active = new HashSet<string>(); var done = new HashSet<string>();
        void Visit(string id) {
            if (done.Contains(id)) return;
            if (!active.Add(id)) throw new InvalidDataException("Cykl zależności: " + id);
            var m = pack.Mods.SingleOrDefault(x=>x.Id == id) ?? throw new InvalidDataException("Nieznana zależność: " + id);
            foreach (var d in m.Dependencies) Visit(d);
            active.Remove(id); done.Add(id); result.Add(m);
        }
        foreach (var id in ids) Visit(id);
        return result;
    }
    public static void VerifyGame(Pack pack, string gameDir) {
        if (!Directory.Exists(gameDir)) throw new DirectoryNotFoundException("Wskaż folder z plikiem StellarDrive.exe.");
        foreach (var pair in pack.GameHashes) try { CheckHash(SafePath(gameDir, pair.Key), pair.Value); }
        catch (InvalidDataException) { throw new InvalidDataException($"Gra nie odpowiada sprawdzonej wersji {pack.GameVersion}. Brak lub inna zawartość: {pair.Key}. Nie zmieniono gry."); }
    }
    public List<string> LegacyFiles(string gameDir) {
        var result = new List<string>();
        var bep = SafePath(gameDir, "BepInEx");
        if (Directory.Exists(bep)) foreach (var file in EnumerateSafe(bep)) result.Add(Path.GetRelativePath(gameDir,file));
        foreach (var n in new[] { "winhttp.dll", "winhttp.dll.melty-off", "doorstop_config.ini", ".doorstop_version" }) if (File.Exists(SafePath(gameDir,n))) result.Add(n);
        return result;
    }
    private static IEnumerable<string> EnumerateSafe(string root) {
        foreach (var item in Directory.EnumerateFileSystemEntries(root)) {
            if (File.GetAttributes(item).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Dowiązanie w folderze modów: " + item);
            if (Directory.Exists(item)) { foreach (var f in EnumerateSafe(item)) yield return f; } else yield return item;
        }
    }
    public InstallState? ReadState(string gameDir) {
        var path = SafePath(gameDir, StateFile);
        if (!File.Exists(path)) return null;
        var state = Json.Read<InstallState>(path);
        if (state.PackId != "stellardrive-odyssey" || !Path.GetFullPath(state.GameDir).Equals(Path.GetFullPath(gameDir),StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Nieprawidłowy rejestr instalacji.");
        foreach (var f in state.Files) {
            SafePath(gameDir,f.Target);
            if (!(ModTarget(f.Target) || LoaderTarget(f.Target) || LegacyTarget(f.Target))) throw new InvalidDataException("Rejestr zawiera plik spoza zakresu modów.");
            if (f.OriginalBackup != null) BackupPath(f.OriginalBackup);
        }
        return state;
    }
    private string BackupPath(string relative) => SafePath(Path.Combine(DataDir,"Backups"),relative);
    public async Task<Pack> FetchManifest(string url, CancellationToken ct) {
        if (Offline) throw new IOException("Tryb offline: sprawdzanie aktualizacji jest wyłączone.");
        ValidateUrl(url, false);
        using var response = await Request(url, false, ct);
        if (response.Content.Headers.ContentLength > 2_000_000) throw new InvalidDataException("Manifest jest zbyt duży.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var ms = new MemoryStream(); var buffer = new byte[8192]; int n;
        while ((n = await stream.ReadAsync(buffer,ct)) > 0) { if (ms.Length+n > 2_000_000) throw new InvalidDataException("Manifest jest zbyt duży."); ms.Write(buffer,0,n); }
        var pack = JsonSerializer.Deserialize<Pack>(ms.ToArray(),Json.Options) ?? throw new InvalidDataException("Pusty manifest."); ValidatePack(pack); return pack;
    }
    private static async Task<HttpResponseMessage> Request(string url, bool source, CancellationToken ct) {
        // Disable automatic redirects so every destination is checked, including HTTPS downgrade.
        for (var i=0;i<6;i++) {
            ValidateUrl(url,source);
            var response = await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,ct);
            if ((int)response.StatusCode is >=300 and <=399 && response.Headers.Location != null) {
                url = new Uri(new Uri(url),response.Headers.Location).AbsoluteUri; response.Dispose(); continue;
            }
            if (!response.IsSuccessStatusCode) { var status=response.StatusCode; response.Dispose(); throw new HttpRequestException("Błąd pobierania HTTP "+(int)status); } return response;
        }
        throw new HttpRequestException("Zbyt wiele przekierowań.");
    }
    private async Task<string> Download(string url, string hash, IProgress<ProgressInfo>? progress, double start, double span, CancellationToken ct) {
        var path = SafePath(CacheDir,hash.ToUpperInvariant()+".bin");
        if (File.Exists(path)) { try { CheckHash(path,hash); progress?.Report(new(start+span,"Sprawdzono plik z pamięci podręcznej")); return path; } catch (InvalidDataException) { File.Delete(path); } }
        if (LocalPayloadDir != null) {
            var local = SafePath(LocalPayloadDir,hash.ToUpperInvariant()+".bin");
            if (File.Exists(local)) { CheckHash(local,hash); File.Copy(local,path); return path; }
        }
        if (Offline) throw new IOException("Brak pliku w lokalnym magazynie. Wyłącz tryb offline, aby pobrać: " + url);
        ValidateUrl(url); Log("Pobieranie " + url);
        var temp = path + ".partial-" + Guid.NewGuid().ToString("N");
        try {
            using var response = await Request(url,true,ct);
            var total = response.Content.Headers.ContentLength;
            if (total > 2_000_000_000) throw new IOException("Plik przekracza limit 2 GB.");
            await using (var input = await response.Content.ReadAsStreamAsync(ct))
            await using (var output = File.Create(temp)) {
                var buffer = new byte[81920]; long received=0; int count;
                while ((count = await input.ReadAsync(buffer,ct))>0) {
                    await output.WriteAsync(buffer.AsMemory(0,count),ct); received += count;
                    if (received>2_000_000_000) throw new IOException("Plik przekracza limit 2 GB.");
                    progress?.Report(new(start+(total>0 ? span*received/total.Value : 0),$"Pobieranie: {received/1048576d:F1} MB"+(total>0?$" / {total/1048576d:F1} MB":" (rozmiar nieznany)")));
                }
            }
            CheckHash(temp,hash); File.Move(temp,path,true); return path;
        } finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private sealed record Pending(string Target,string? Source,string? Hash,bool Preserve=false);
    public async Task Install(Pack pack,string gameDir,IEnumerable<string> selected,bool migrate,IProgress<ProgressInfo>? progress,CancellationToken ct) {
        await gate.WaitAsync(ct);
        try {
            using var appLock = AcquireGameLock(gameDir);
            ValidatePack(pack); VerifyGame(pack,gameDir); assertGameClosed(gameDir); EnsureNoPending(gameDir);
            var mods = Resolve(pack,selected); if (mods.Count==0) throw new InvalidOperationException("Wybierz przynajmniej jeden mod.");
            var legacy = LegacyFiles(gameDir); if (legacy.Count>0 && !migrate) throw new InvalidOperationException("Wykryto BepInEx. Zaznacz zgodę na przeniesienie poprzednich modów do kopii zapasowej.");
            var state = ReadState(gameDir) ?? new InstallState { PackId=pack.Id,GameDir=Path.GetFullPath(gameDir) };
            var needed = mods.SelectMany(m=>m.Files).Where(f=>f.DownloadUrl==null).Select(f=>f.ArchiveId).Append(pack.LoaderArchiveId).Distinct().ToList();
            var archives = new Dictionary<string,string>(); int done=0;
            foreach (var id in needed) {
                var a=pack.Archives.Single(x=>x.Id==id);
                archives[id] = await Download(a.Url,a.Sha256,progress,5+50d*done/needed.Count,50d/needed.Count,ct); done++;
            }
            var stage=Path.Combine(DataDir,"Staging",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
            var changes = new List<Pending>();
            using (var zip = ZipFile.OpenRead(archives[pack.LoaderArchiveId])) {
                long bytes=0;
                foreach (var entry in zip.Entries.Where(e=>e.Name!="")) {
                    if (!LoaderTarget(entry.FullName)) throw new InvalidDataException("Nieoczekiwany plik w archiwum loadera: "+entry.FullName);
                    bytes += entry.Length; if(bytes>1_000_000_000) throw new InvalidDataException("Archiwum loadera jest zbyt duże.");
                    var path=SafePath(stage,entry.FullName); Directory.CreateDirectory(Path.GetDirectoryName(path)!); entry.ExtractToFile(path);
                    changes.Add(new(entry.FullName,path,Hash(path)));
                }
            }
            foreach(var mod in mods) foreach(var f in mod.Files) {
                ct.ThrowIfCancellationRequested(); var dest=SafePath(stage,f.Target); Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                if (f.DownloadUrl!=null) File.Copy(await Download(f.DownloadUrl,f.Sha256,progress,55,0,ct),dest);
                else {
                    using var zip=ZipFile.OpenRead(archives[f.ArchiveId]);
                    var entries=zip.Entries.Where(e=>e.FullName.Replace('\\','/')==f.Entry.Replace('\\','/')).ToList();
                    if(entries.Count!=1 || entries[0].Length>500_000_000) throw new InvalidDataException("Brak lub błędny plik w archiwum: "+f.Entry);
                    entries[0].ExtractToFile(dest);
                }
                CheckHash(dest,f.Sha256); changes.Add(new(f.Target,dest,f.Sha256,f.PreserveExisting));
            }
            foreach(var file in legacy) changes.Add(new(file,null,null));
            // Preflight all collisions before changing anything.
            changes=changes.Where(change=>{
                var target=SafePath(gameDir,change.Target);
                if(Directory.Exists(target)) throw new IOException("Katalog blokuje plik: "+change.Target);
                if(!File.Exists(target)) return change.Source!=null;
                if(change.Preserve) return false;
                var hash=Hash(target);
                if(change.Hash!=null && hash.Equals(change.Hash,StringComparison.OrdinalIgnoreCase)) return false;
                var owner=state.Files.FirstOrDefault(f=>f.Target.Equals(change.Target,StringComparison.OrdinalIgnoreCase));
                if(!LegacyTarget(change.Target) && (owner==null || owner.InstalledHash==null || !owner.InstalledHash.Equals(hash,StringComparison.OrdinalIgnoreCase)))
                    throw new IOException("Nie nadpisano obcego lub zmienionego pliku: "+change.Target+". Zachowaj go poza folderem gry lub użyj funkcji przywracania.");
                return true;
            }).ToList();
            assertGameClosed(gameDir); ct.ThrowIfCancellationRequested();
            var txn=CreateJournal(gameDir); var journal=txn.Journal;
            try {
                // Save backups and durable journal BEFORE mutating files.
                foreach(var change in changes) BackupEntry(txn.Path,journal,gameDir,change.Target,change.Hash);
                BackupEntry(txn.Path,journal,gameDir,StateFile,null);
                progress?.Report(new(60,"Kopia plików gotowa. Instalowanie…"));
                var count=0;
                foreach(var change in changes) {
                    ct.ThrowIfCancellationRequested(); var dest=SafePath(gameDir,change.Target);
                    if(change.Source==null) File.Delete(dest); else AtomicCopy(change.Source,dest);
                    var owner=state.Files.FirstOrDefault(f=>f.Target.Equals(change.Target,StringComparison.OrdinalIgnoreCase));
                    if(owner==null) {
                        var before=journal.Entries.First(x=>x.Target==change.Target);
                        owner=new OwnedFile { Target=change.Target,OriginalBackup=before.Backup,OriginalHash=before.BeforeHash }; state.Files.Add(owner);
                    }
                    owner.InstalledHash=change.Hash;
                    if(change.Hash!=null) CheckHash(dest,change.Hash);
                    TestAfterWrite?.Invoke(++count);
                    progress?.Report(new(60+35d*count/Math.Max(1,changes.Count),"Zapisano: "+change.Target));
                }
                foreach(var mod in mods) { foreach(var f in mod.Files) if(!f.PreserveExisting) CheckHash(SafePath(gameDir,f.Target),f.Sha256); state.Mods[mod.Id]=mod.Version; }
                state.PackVersion=pack.Version;
                var stagedState=Path.Combine(stage,"state.json"); Json.Save(stagedState,state);
                journal.Entries.Last().AfterHash=Hash(stagedState); Json.Save(txn.Path,journal);
                AtomicCopy(stagedState,SafePath(gameDir,StateFile));
                journal.Status="committed"; Json.Save(txn.Path,journal);
                progress?.Report(new(100,$"Gotowe. Zainstalowano / sprawdzono {mods.Count} modów.")); Log("Instalacja zakończona. Kopia: "+Path.GetDirectoryName(txn.Path));
            } catch { Rollback(txn.Path,journal); throw; }
        } catch(Exception ex) { Log(ex.ToString()); throw; } finally { gate.Release(); }
    }
    private FileStream AcquireGameLock(string gameDir) {
        var key=Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(gameDir).ToUpperInvariant())));
        return new FileStream(Path.Combine(DataDir,key+".lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
    }
    private (string Path,Journal Journal) CreateJournal(string gameDir) {
        var folder=Path.Combine(DataDir,"Backups",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var path=Path.Combine(folder,"transaction.json"); var j=new Journal { GameDir=Path.GetFullPath(gameDir) }; Json.Save(path,j); return (path,j);
    }
    private void BackupEntry(string journalPath,Journal j,string gameDir,string target,string? after) {
        var path=SafePath(gameDir,target); string? backup=null,hash=null;
        if(File.Exists(path)) {
            var copy=SafePath(Path.GetDirectoryName(journalPath)!,"files/"+target);
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!); File.Copy(path,copy); hash=Hash(path); CheckHash(copy,hash);
            backup=Path.GetRelativePath(Path.Combine(DataDir,"Backups"),copy);
        }
        j.Entries.Add(new() { Target=target,Backup=backup,BeforeHash=hash,AfterHash=after }); Json.Save(journalPath,j);
    }
    private static void AtomicCopy(string source,string dest) {
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!); var temp=dest+".sdmi-"+Guid.NewGuid().ToString("N");
        try { File.Copy(source,temp); File.Move(temp,dest,true); } finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    private void Rollback(string path,Journal j) {
        foreach(var e in Enumerable.Reverse(j.Entries)) {
            if(!(ModTarget(e.Target)||LoaderTarget(e.Target)||LegacyTarget(e.Target)||e.Target==StateFile)) throw new InvalidDataException("Nieprawidłowa ścieżka dziennika.");
            var dest=SafePath(j.GameDir,e.Target);
            if(e.Backup!=null) { var original=BackupPath(e.Backup); CheckHash(original,e.BeforeHash!); AtomicCopy(original,dest); }
            else if(File.Exists(dest)) {
                if(e.AfterHash==null || !Hash(dest).Equals(e.AfterHash,StringComparison.OrdinalIgnoreCase)) throw new IOException("Plik zmienił się poza instalatorem. Zachowano go: "+e.Target);
                File.Delete(dest);
            }
        }
        j.Status="rolled-back"; Json.Save(path,j); Log("Cofnięto operację: "+path);
    }
    public List<string> PendingTransactions(string gameDir) {
        var root=Path.Combine(DataDir,"Backups"); if(!Directory.Exists(root)) return new();
        return Directory.GetFiles(root,"transaction.json",SearchOption.AllDirectories).Where(p=>{
            var j=Json.Read<Journal>(p); return j.Status=="pending" && Path.GetFullPath(j.GameDir).Equals(Path.GetFullPath(gameDir),StringComparison.OrdinalIgnoreCase);
        }).ToList();
    }
    private void EnsureNoPending(string gameDir) { if(PendingTransactions(gameDir).Count>0) throw new IOException("Znaleziono przerwaną operację. Użyj PRZYWRÓĆ PLIKI, aby najpierw ją cofnąć."); }
    public async Task Restore(string gameDir,IProgress<ProgressInfo>? progress,CancellationToken ct) {
        await gate.WaitAsync(ct);
        try {
            using var appLock=AcquireGameLock(gameDir); assertGameClosed(gameDir);
            var pending=PendingTransactions(gameDir);
            if(pending.Count>0) { foreach(var p in pending) Rollback(p,Json.Read<Journal>(p)); progress?.Report(new(100,"Cofnięto przerwaną operację. Sprawdź stan gry.")); return; }
            var state=ReadState(gameDir) ?? throw new InvalidOperationException("Ta aplikacja nie ma rejestru instalacji dla wybranego folderu. Nie usunięto żadnych plików.");
            // Validate original copies before making any changes.
            foreach(var f in state.Files) if(f.OriginalBackup!=null) CheckHash(BackupPath(f.OriginalBackup),f.OriginalHash!);
            var txn=CreateJournal(gameDir);
            try {
                foreach(var f in state.Files) BackupEntry(txn.Path,txn.Journal,gameDir,f.Target,f.OriginalHash);
                BackupEntry(txn.Path,txn.Journal,gameDir,StateFile,null);
                int count=0;
                foreach(var f in state.Files) {
                    ct.ThrowIfCancellationRequested(); var dest=SafePath(gameDir,f.Target);
                    if(f.OriginalBackup!=null) AtomicCopy(BackupPath(f.OriginalBackup),dest);
                    else if(File.Exists(dest)) {
                        if(f.InstalledHash!=null && Hash(dest).Equals(f.InstalledHash,StringComparison.OrdinalIgnoreCase)) File.Delete(dest);
                        else Log("Zachowano zmodyfikowany plik: "+f.Target);
                    }
                    progress?.Report(new(95d*++count/Math.Max(1,state.Files.Count),"Przywracanie: "+f.Target));
                }
                File.Delete(SafePath(gameDir,StateFile)); txn.Journal.Status="committed"; Json.Save(txn.Path,txn.Journal);
                progress?.Report(new(100,"Przywrócono pliki. Zapisy gry i obce mody pozostały na miejscu.")); Log("Przywrócono instalację. Kopia: "+Path.GetDirectoryName(txn.Path));
            } catch { Rollback(txn.Path,txn.Journal); throw; }
        } catch(Exception ex) { Log(ex.ToString()); throw; } finally { gate.Release(); }
    }
}
