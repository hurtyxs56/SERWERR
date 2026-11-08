using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;

namespace StellarDrive.Server;
public sealed class HostStatus {
 public string phase {get;set;}="stopped";public int pid {get;set;}
 public DateTime utc {get;set;}public bool steamInitialized {get;set;}public bool serverStarted {get;set;}
 public bool localClient {get;set;}public bool worldLoaded {get;set;}public int players {get;set;}
 public int saves {get;set;}public string? lastError {get;set;}public string? graphics {get;set;}
}
public static class ServerControl {
 public static HostStatus? Status(string game){var p=Path.Combine(ServerFiles.Root(game),"status.json");try{if(!File.Exists(p))return null;var s=ServerFiles.Read<HostStatus>(p);using var process=Process.GetProcessById(s.pid);return string.Equals(process.MainModule?.FileName,Path.Combine(game,"StellarDrive.exe"),StringComparison.OrdinalIgnoreCase)&&process.StartTime.ToUniversalTime()<=s.utc.ToUniversalTime().AddSeconds(2)?s:null;}catch(IOException){return null;}catch(JsonException){return null;}catch(ArgumentException){return null;}catch(InvalidOperationException){return null;}}
 public static Process Start(string game){
  ServerFiles.ValidateGame(game);if(ServerFiles.GameRunning(game))throw new IOException("Serwer już działa.");
  var root=ServerFiles.Root(game);var config=ServerFiles.Read<ServerConfig>(Path.Combine(root,"autohost.json"));ServerFiles.ValidateConfig(config);
  if(!Process.GetProcessesByName("steam").Any())throw new IOException("Uruchom Steam i zaloguj konto posiadające Demo.");
  if(ServerFiles.Hash(Path.Combine(game,"Mods","StellarDrive.AutoHost.dll"))!=ServerFiles.ModHash)throw new IOException("AutoHost ma inną sumę kontrolną. Użyj aktualizatora zamiast podmiany pliku.");
  var flag=Path.Combine(root,"supervisor.stop");if(File.Exists(flag)){Directory.CreateDirectory(Path.Combine(root,"Archive"));File.Move(flag,Path.Combine(root,"Archive","stop-"+Guid.NewGuid().ToString("N")));}
  var args=new[]{"-NoProfile","-ExecutionPolicy","Bypass","-File",Path.Combine(root,"Supervisor-Entry.ps1"),"-GameDir",game,"-Config",Path.Combine(root,"autohost.json")};
  return StartPowerShell(args);
 }
 public static Process StartPowerShell(IEnumerable<string> args){var psi=new ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};foreach(var arg in args)psi.ArgumentList.Add(arg);return Process.Start(psi)??throw new IOException("Nie udało się uruchomić PowerShell.");}
 public static async Task Stop(string game){
  var root=ServerFiles.Root(game);if(!ServerFiles.GameRunning(game)){File.WriteAllText(Path.Combine(root,"supervisor.stop"),"planned stop");await WaitSupervisor(root);return;}var state=Status(game)??throw new IOException("Brak statusu AutoHost. Nie zatrzymano innej sesji gry.");
  using(var p=Process.GetProcessById(state.pid)){if(!string.Equals(p.MainModule?.FileName,Path.Combine(game,"StellarDrive.exe"),StringComparison.OrdinalIgnoreCase)||p.StartTime.ToUniversalTime()>state.utc.ToUniversalTime().AddSeconds(2))throw new IOException("Status dotyczy innego procesu.");}
  File.WriteAllText(Path.Combine(root,"supervisor.stop"),"planned stop");await File.AppendAllTextAsync(Path.Combine(root,"commands.txt"),"stop"+Environment.NewLine);
  var deadline=DateTime.UtcNow.AddMinutes(3);while(ServerFiles.GameRunning(game)){if(DateTime.UtcNow>deadline)throw new TimeoutException("Serwer nie zakończył jeszcze zapisu. Nie zabito procesu. Sprawdź log.");await Task.Delay(1000);}await WaitSupervisor(root);
 }
 static async Task WaitSupervisor(string root){var path=Path.Combine(root,"supervisor.lock");var deadline=DateTime.UtcNow.AddSeconds(75);while(File.Exists(path)){try{using var gate=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);return;}catch(IOException){if(DateTime.UtcNow>deadline)throw new IOException("Nadzorca nadal działa. Nie uruchomiono drugiego procesu.");await Task.Delay(500);}}}
 public static async Task Restart(string game){await Stop(game);Start(game);}
 public static void SaveConfig(string game,ServerConfig config){if(ServerFiles.GameRunning(game))throw new IOException("Zatrzymaj serwer przed zmianą konfiguracji.");ServerFiles.ValidateConfig(config);ServerFiles.Write(Path.Combine(ServerFiles.Root(game),"autohost.json"),config);}
 public static async Task SetAutostart(string game,bool enabled){
  var root=ServerFiles.Root(game);var name=ServerFiles.TaskName(game);
  var script=Path.Combine(root,"Manage-LogonTask.ps1");
  using var p=StartPowerShell(new[]{"-NoProfile","-ExecutionPolicy","Bypass","-File",script,"-GameDir",game,"-TaskName",name,"-Mode",enabled?"Enable":"Disable"});await p.WaitForExitAsync();if(p.ExitCode!=0)throw new IOException("Nie udało się zmienić zadania autostartu. Sprawdź uprawnienia Harmonogramu zadań.");
  var path=Path.Combine(root,"installation.json");var state=ServerFiles.Read<Installation>(path);state.Autostart=enabled;ServerFiles.Write(path,state);
 }
 public static string RegistryKey(string game)=>@"Software\Microsoft\Windows\CurrentVersion\Uninstall\"+ServerFiles.TaskName(game).Replace(" ","");
 public static void Register(string game){
  var root=ServerFiles.Root(game);using var key=Registry.CurrentUser.CreateSubKey(RegistryKey(game));key.SetValue("DisplayName","StellarDrive Dedicated AutoHost (testowy)");key.SetValue("DisplayVersion","0.2.1-alpha.1");key.SetValue("Publisher","StellarDrive AutoHost contributors");key.SetValue("InstallLocation",root);key.SetValue("UninstallString","\""+Path.Combine(root,"StellarDrive-Server-Setup.exe")+"\" --uninstall --game \""+game+"\"");key.SetValue("NoModify",1);key.SetValue("NoRepair",1);
  var type=Type.GetTypeFromProgID("WScript.Shell")??throw new IOException("Brak obsługi skrótów Windows.");dynamic shell=Activator.CreateInstance(type)!;dynamic shortcut=shell.CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),ServerFiles.TaskName(game)+".lnk"));shortcut.TargetPath=Path.Combine(root,"StellarDrive-Server-Manager.exe");shortcut.Arguments="--game \""+game+"\"";shortcut.WorkingDirectory=root;shortcut.Save();
 }
 public static void Unregister(string game){Registry.CurrentUser.DeleteSubKeyTree(RegistryKey(game),false);var link=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),ServerFiles.TaskName(game)+".lnk");if(File.Exists(link))File.Delete(link);}
 public static string ReadLog(string file,int maxChars=30000){if(!File.Exists(file))return "Brak logu — uruchom serwer.";using var f=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);if(f.Length>maxChars*4)f.Seek(-maxChars*4,SeekOrigin.End);using var r=new StreamReader(f);var text=r.ReadToEnd();return text.Length>maxChars?text[^maxChars..]:text;}
}
