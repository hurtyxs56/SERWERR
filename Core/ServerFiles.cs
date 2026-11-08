using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace StellarDrive.Server;
public sealed class ServerConfig {
 public bool AutoHost {get;set;}=true;
 public string WorldPath {get;set;}="";
 public int MaxPlayers {get;set;}=8;
 public bool FriendsOnly {get;set;}=true;
 public double AutoSaveMinutes {get;set;}=5;
 public bool PauseWhenEmpty {get;set;}=true;
 public bool HeadlessShaderCompatibility {get;set;}=true;
 public int StartupTimeoutSeconds {get;set;}=600;
}
public sealed class OwnedFile {
 public string Path {get;set;}="";
 public string Hash {get;set;}="";
 public string? Backup {get;set;}
 public bool Preserve {get;set;}
}
public sealed class Installation {
 public int Schema {get;set;}=1;
 public string Version {get;set;}="0.2.0-alpha.1";
 public string GameDir {get;set;}="";
 public List<OwnedFile> Files {get;set;}=new();
 public bool Autostart {get;set;}
}
public static class ServerFiles {
 public const string AssemblyHash="25D3F704C52EF689EA04E1EDDDCDB014914D8CBCE071F88871066A6B1375715B";
 public const string LoaderHash="7C0E8C59A4EC02CBCAEC91D0393E7CAF9886F21D00AD35984B7013BCA695225B";
 public const string LoaderZipHash="5B2B2F3D1CD42B59EC886C5BDC2663EDAE87A0097A4F4A8F58C0965A99DDA416";
 public const string ModHash="3B794EF574676FC4A71E79C21716A32D2A10F86D8932668244EBDCAE6D4BD19E";
 public const string Folder="StellarDriveServer";
 public static readonly JsonSerializerOptions Json=new(){WriteIndented=true,PropertyNameCaseInsensitive=true};
 public static string Hash(string path){using var s=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(s));}
 public static string Root(string game)=>Safe(game,Folder);
 public static string Safe(string root,string relative){
  if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.Contains(':'))throw new IOException("Nieprawidłowa ścieżka względna.");
  var parts=relative.Replace('\\','/').Split('/');if(parts.Any(p=>p is "" or "." or ".."||p.EndsWith('.')||p.EndsWith(' ')||p.IndexOfAny(Path.GetInvalidFileNameChars())>=0))throw new IOException("Nieprawidłowa ścieżka pliku.");
  var full=Path.GetFullPath(Path.Combine(root,relative));var basis=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
  if(!full.StartsWith(basis,StringComparison.OrdinalIgnoreCase))throw new IOException("Ścieżka wychodzi poza folder.");
  for(string? p=full;p!=null;p=Path.GetDirectoryName(p))if((Directory.Exists(p)||File.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Dowiązania nie są obsługiwane: "+p);
  return full;
 }
 public static T Read<T>(string path)=>JsonSerializer.Deserialize<T>(File.ReadAllText(path),Json)??throw new IOException("Pusty plik: "+path);
 public static void Write<T>(string path,T value){Directory.CreateDirectory(Path.GetDirectoryName(path)!);var temp=path+".new-"+Guid.NewGuid().ToString("N");File.WriteAllText(temp,JsonSerializer.Serialize(value,Json));File.Move(temp,path,true);}
 public static string ValidateGame(string dir){
  dir=Path.GetFullPath(dir.Trim().Trim('"'));
  if(!File.Exists(Safe(dir,"StellarDrive.exe")))throw new IOException("Wskaż folder zawierający StellarDrive.exe.");
  var assembly=Safe(dir,"StellarDrive_Data/Managed/Assembly-CSharp.dll");if(!File.Exists(assembly)||Hash(assembly)!=AssemblyHash)throw new IOException("Nieobsługiwana wersja gry. Wymagane StellarDrive Demo 0.8.2.");
  if(Directory.Exists(Safe(dir,"BepInEx"))||File.Exists(Safe(dir,"doorstop_config.ini")))throw new IOException("Wykryto BepInEx. Instalator go nie usuwa — użyj osobnej kopii gry z MelonLoaderem.");
  return dir;
 }
 public static bool HasLoader(string game){
  var loader=Safe(game,"MelonLoader/net35/MelonLoader.dll");if(File.Exists(loader)){if(Hash(loader)!=LoaderHash)throw new IOException("Inna wersja MelonLoadera. Wymagane 0.7.3 net35; nie nadpisano loadera.");if(!File.Exists(Safe(game,"version.dll")))throw new IOException("Niekompletny MelonLoader: brak version.dll.");return true;}
  if(Directory.Exists(Safe(game,"MelonLoader"))||File.Exists(Safe(game,"version.dll")))throw new IOException("Istniejący loader/proxy wymaga sprawdzenia. Nie nadpisano plików.");return false;
 }
 public static void ValidateConfig(ServerConfig c){
  if(!Path.IsPathRooted(c.WorldPath)||!File.Exists(c.WorldPath)||!Path.GetFileName(c.WorldPath).Equals("world.json",StringComparison.OrdinalIgnoreCase))throw new IOException("Wybierz istniejący world.json. Przenoś cały katalog świata razem z plikami modów.");
  using var json=JsonDocument.Parse(File.ReadAllText(c.WorldPath));if(json.RootElement.ValueKind!=JsonValueKind.Object)throw new IOException("Nieprawidłowy zapis JSON.");
  if(c.MaxPlayers<1||c.MaxPlayers>32766||!double.IsFinite(c.AutoSaveMinutes)||c.AutoSaveMinutes<1d/3||c.AutoSaveMinutes>60)throw new IOException("Gracze: 1–32766. Autozapis: 20–3600 sekund.");
 }
 public static List<string> FindGames(){
  var roots=new HashSet<string>(StringComparer.OrdinalIgnoreCase);using var key=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");var steam=key?.GetValue("SteamPath") as string;
  if(!string.IsNullOrWhiteSpace(steam)){roots.Add(steam);var libraries=Path.Combine(steam,"steamapps","libraryfolders.vdf");if(File.Exists(libraries))foreach(Match m in Regex.Matches(File.ReadAllText(libraries),"\"path\"\\s+\"([^\"]+)\""))roots.Add(m.Groups[1].Value.Replace(@"\\",@"\"));}
  var result=new List<string>();foreach(var root in roots){var acf=Path.Combine(root,"steamapps","appmanifest_4689730.acf");if(!File.Exists(acf))continue;var match=Regex.Match(File.ReadAllText(acf),"\"installdir\"\\s+\"([^\"]+)\"");if(match.Success){var path=Path.Combine(root,"steamapps","common",match.Groups[1].Value);if(File.Exists(Path.Combine(path,"StellarDrive.exe")))result.Add(path);}}
  return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
 }
 public static bool GameRunning(string game){foreach(var process in Process.GetProcessesByName("StellarDrive"))using(process){try{if(string.Equals(process.MainModule?.FileName,Path.Combine(game,"StellarDrive.exe"),StringComparison.OrdinalIgnoreCase))return true;}catch{throw new IOException("Nie można sprawdzić uruchomionego procesu gry. Zamknij go przed zmianami.");}}return false;}
 public static string TaskName(string game)=>"StellarDrive AutoHost "+Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(game).ToLowerInvariant())))[..12];
}
