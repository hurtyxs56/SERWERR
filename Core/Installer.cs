using System.IO.Compression;
using System.Net.Http;
using Microsoft.Win32;

namespace StellarDrive.Server;
public sealed class Installer {
 public Action<int>? FaultAfterWrite {get;set;}
 public async Task<string> DownloadLoader(string staging,IProgress<string>? log=null){
  var zip=Path.Combine(staging,"MelonLoader.zip");
  using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromMinutes(10)};
  var url=new Uri("https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip");
  for(int i=0;i<8;i++){
   if(url.Scheme!="https"||!(url.Host=="github.com"||url.Host.EndsWith(".githubusercontent.com",StringComparison.OrdinalIgnoreCase)))throw new IOException("Nieautoryzowane źródło loadera.");
   log?.Report("Pobieranie oficjalnego MelonLoadera 0.7.3…");using var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead);
   if((int)response.StatusCode>=300&&(int)response.StatusCode<400){url=new Uri(url,response.Headers.Location??throw new IOException("Brak adresu przekierowania."));continue;}
   response.EnsureSuccessStatusCode();using(var target=File.Create(zip)){using var input=await response.Content.ReadAsStreamAsync();await input.CopyToAsync(target);}
   if(ServerFiles.Hash(zip)!=ServerFiles.LoaderZipHash)throw new IOException("Niezgodna suma SHA-256 archiwum MelonLoadera.");return zip;
  }throw new IOException("Zbyt wiele przekierowań pobierania.");
 }
 public void ExtractLoader(string zip,string stage){
  if(ServerFiles.Hash(zip)!=ServerFiles.LoaderZipHash)throw new IOException("Niezweryfikowany loader.");using var archive=ZipFile.OpenRead(zip);long total=0;
  foreach(var entry in archive.Entries){if(entry.FullName.EndsWith('/'))continue;var name=entry.FullName.Replace('\\','/');
   if(name!="version.dll"&&!name.StartsWith("MelonLoader/",StringComparison.OrdinalIgnoreCase))continue;
   total+=entry.Length;if(total>600_000_000)throw new IOException("Zbyt duże archiwum.");var output=ServerFiles.Safe(stage,name);Directory.CreateDirectory(Path.GetDirectoryName(output)!);entry.ExtractToFile(output,false);
  }
  if(!ServerFiles.HasLoader(stage))throw new IOException("Niekompletny loader w archiwum.");
 }
 public Installation Install(string game,string payload,ServerConfig config,bool loaderWasMissing,IProgress<string>? log=null){
  game=ServerFiles.ValidateGame(game);ServerFiles.ValidateConfig(config);if(ServerFiles.GameRunning(game))throw new IOException("Zamknij serwer przed instalacją/aktualizacją.");
  var root=ServerFiles.Root(game);Directory.CreateDirectory(root);using var gate=new FileStream(Path.Combine(root,"install.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
  var manifestPath=Path.Combine(root,"installation.json");var previous=File.Exists(manifestPath)?ServerFiles.Read<Installation>(manifestPath):null;
  if(previous!=null&&(previous.Schema!=1||!string.Equals(Path.GetFullPath(previous.GameDir),game,StringComparison.OrdinalIgnoreCase)))throw new IOException("Manifest instalacji dotyczy innej gry.");
  var files=Directory.GetFiles(payload,"*",SearchOption.AllDirectories).OrderBy(x=>x).ToArray();var next=previous==null?new Installation{GameDir=game}:ServerFiles.Read<Installation>(manifestPath);next.Version="0.2.1-alpha.1";
  var tx=Path.Combine(root,"Backups",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(tx);var written=new List<(string target,string? restore)>();int counter=0;
  try{
   foreach(var source in files){var relative=Path.GetRelativePath(payload,source).Replace('\\','/');bool loader=relative=="version.dll"||relative.StartsWith("MelonLoader/",StringComparison.OrdinalIgnoreCase);
    if(!loader&&!relative.StartsWith(ServerFiles.Folder+"/",StringComparison.OrdinalIgnoreCase)&&relative!="Mods/StellarDrive.AutoHost.dll")throw new IOException("Nieoczekiwany plik instalacji: "+relative);
    var target=ServerFiles.Safe(game,relative);var hash=ServerFiles.Hash(source);var entry=next.Files.FirstOrDefault(f=>f.Path.Equals(relative,StringComparison.OrdinalIgnoreCase));
    if(File.Exists(target)&&ServerFiles.Hash(target)==hash){if(entry==null)next.Files.Add(new OwnedFile{Path=relative,Hash=hash,Preserve=true});continue;}
    if(File.Exists(target)&&entry!=null&&ServerFiles.Hash(target)!=entry.Hash)throw new IOException("Plik zmieniony poza instalatorem: "+relative+". Zachowano go.");
    if(File.Exists(target)&&entry==null)throw new IOException("Istniejący niezarządzany plik: "+relative+". Nie nadpisano go.");
    if(loader&&!loaderWasMissing)throw new IOException("Nie wolno nadpisywać istniejącego loadera.");
    string? restore=null;if(File.Exists(target)){restore=Path.Combine(tx,counter+".bak");File.Copy(target,restore);}
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);written.Add((target,restore));File.Copy(source,target,true);
    if(ServerFiles.Hash(target)!=hash)throw new IOException("Błąd weryfikacji pliku: "+relative);
    if(entry==null){entry=new OwnedFile{Path=relative,Hash=hash,Preserve=loader};next.Files.Add(entry);}else entry.Hash=hash;
    log?.Report("Zainstalowano: "+relative);FaultAfterWrite?.Invoke(++counter);
   }
   var cfg=Path.Combine(root,"autohost.json");if(!File.Exists(cfg))ServerFiles.Write(cfg,config); // existing server settings belong to the user
   ServerFiles.Write(manifestPath,next);return next;
  }catch{
   foreach(var item in written.AsEnumerable().Reverse()){if(item.restore!=null)File.Copy(item.restore,item.target,true);else if(File.Exists(item.target))File.Delete(item.target);}throw;
  }
 }
 public List<string> Uninstall(string game){
  game=Path.GetFullPath(game);if(ServerFiles.GameRunning(game))throw new IOException("Najpierw zatrzymaj serwer.");var root=ServerFiles.Root(game);var manifest=Path.Combine(root,"installation.json");var state=ServerFiles.Read<Installation>(manifest);
  if(state.Schema!=1||!string.Equals(Path.GetFullPath(state.GameDir),game,StringComparison.OrdinalIgnoreCase))throw new IOException("Nieprawidłowy manifest.");
  var retained=new List<string>();using var gate=new FileStream(Path.Combine(root,"install.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
  foreach(var entry in state.Files){var path=ServerFiles.Safe(game,entry.Path);if(entry.Preserve||entry.Path.EndsWith("StellarDrive-Server-Setup.exe",StringComparison.OrdinalIgnoreCase)){retained.Add(entry.Path);continue;}
   if(!File.Exists(path))continue;if(ServerFiles.Hash(path)!=entry.Hash){retained.Add(entry.Path+" (zmieniony)");continue;}
   if(!entry.Path.StartsWith(ServerFiles.Folder+"/",StringComparison.OrdinalIgnoreCase)&&entry.Path!="Mods/StellarDrive.AutoHost.dll")throw new IOException("Manifest zawiera plik spoza programu.");
   if(entry.Backup!=null)File.Copy(ServerFiles.Safe(game,entry.Backup),path,true);else File.Delete(path);
  }
  File.Move(manifest,manifest+".uninstalled-"+DateTime.Now.ToString("yyyyMMddHHmmss"));return retained;
 }
}
