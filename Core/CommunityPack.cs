using System.Reflection;
using System.Text.Json;
using StellarDrive.Installer;
namespace StellarDrive.Server;

public static class CommunityPack {
 public static Pack Load(){
  using var stream=typeof(CommunityPack).Assembly.GetManifestResourceStream("community-modpack.json")??throw new IOException("Brak manifestu paczki.");
  var pack=JsonSerializer.Deserialize<Pack>(stream,StellarDrive.Installer.Json.Options)??throw new IOException("Pusty manifest.");
  InstallerEngine.ValidatePack(pack);
  if(pack.Mods.Count!=39||pack.Mods.SelectMany(m=>m.Files).Any(f=>f.Target.Contains("AutoHost",StringComparison.OrdinalIgnoreCase)||f.Target.Contains("DedicatedPrototype",StringComparison.OrdinalIgnoreCase)))throw new IOException("Nieprawidłowy zestaw 39 modów.");
  return pack;
 }
 public static List<string> Verify(string game){var failures=new List<string>();foreach(var mod in Load().Mods)foreach(var file in mod.Files){var target=ServerFiles.Safe(game,file.Target);if(!File.Exists(target))failures.Add(mod.Name+": brak "+file.Target);else if(!file.PreserveExisting&&!ServerFiles.Hash(target).Equals(file.Sha256,StringComparison.OrdinalIgnoreCase))failures.Add(mod.Name+": inna wersja "+file.Target);}return failures;}
 public static void AssertComplete(string game){var failures=Verify(game);if(failures.Count>0)throw new IOException("Paczka 39 modów jest niekompletna. Uruchom ponownie instalator 0.3.0 lub nowszy.\n"+string.Join("\n",failures.Take(8)));}
 public static async Task Install(string game,IProgress<ProgressInfo>? progress){
  var pack=Load();var engine=new InstallerEngine(ServerFiles.Safe(game,"StellarDriveCommunity"),dir=>{if(ServerFiles.GameRunning(dir))throw new IOException("Zamknij tę kopię gry.");});
  await engine.Install(pack,game,pack.Mods.Select(m=>m.Id),false,progress,CancellationToken.None);
  AssertComplete(game);
 }
}
