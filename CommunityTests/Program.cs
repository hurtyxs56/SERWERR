using System.IO.Compression;
using StellarDrive.Server;
using StellarDrive.Installer;
// File-only regression tests. No game files, game process, Steam or multiplayer simulation.
var actual=CommunityPack.Load();if(InstallerEngine.Resolve(actual,actual.Mods.Select(m=>m.Id)).Count!=39)throw new Exception("Incomplete dependency graph");
var root=Path.Combine(Path.GetTempPath(),"autohost-community-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
var game=Path.Combine(root,"game");Directory.CreateDirectory(game);Directory.CreateDirectory(Path.Combine(game,"Mods"));
File.WriteAllText(Path.Combine(game,"StellarDrive.exe"),"TEST FIXTURE, NOT EXECUTABLE");Directory.CreateDirectory(Path.Combine(game,"StellarDrive_Data","Managed"));File.WriteAllText(Path.Combine(game,"StellarDrive_Data","Managed","Assembly-CSharp.dll"),"TEST FIXTURE");
File.WriteAllText(Path.Combine(game,"Mods","StellarDrive.AutoHost.dll"),"existing own mod sentinel");File.WriteAllText(Path.Combine(game,"world.json"),"world sentinel");
if(CommunityPack.Verify(game).Count==0)throw new Exception("Missing real pack not detected");
var payload=Path.Combine(root,"payload");Directory.CreateDirectory(payload);
string Zip(string name,Dictionary<string,string> contents){var path=Path.Combine(root,name);using(var z=ZipFile.Open(path,ZipArchiveMode.Create)){foreach(var pair in contents){using var w=new StreamWriter(z.CreateEntry(pair.Key).Open());w.Write(pair.Value);}}var hash=InstallerEngine.Hash(path);File.Copy(path,Path.Combine(payload,hash+".bin"));return hash;}
string HashText(string text){var p=Path.Combine(root,"hash-input");File.WriteAllText(p,text);return InstallerEngine.Hash(p);}
var loaderHash=Zip("loader.zip",new(){{"version.dll","loader test"},{"MelonLoader/net35/MelonLoader.dll","loader assembly test"}});
var modsHash=Zip("mods.zip",new(){{"core.dll","core test"},{"parts.dll","parts test"}});
var pack=new Pack{Id="stellardrive-odyssey",Version="1.0.0",GameVersion="test",LoaderArchiveId="loader",GameHashes=new(){["StellarDrive.exe"]=InstallerEngine.Hash(Path.Combine(game,"StellarDrive.exe")),["StellarDrive_Data/Managed/Assembly-CSharp.dll"]=InstallerEngine.Hash(Path.Combine(game,"StellarDrive_Data","Managed","Assembly-CSharp.dll"))},Archives=new(){new(){Id="loader",Url="https://github.com/test/fixture/loader.zip",Sha256=loaderHash},new(){Id="mods",Url="https://github.com/test/fixture/mods.zip",Sha256=modsHash}},Mods=new(){new(){Id="core",Name="Fixture Core",Version="1.0.0",Compatibility="test",Files=new(){new(){Target="UserLibs/Core.dll",ArchiveId="mods",Entry="core.dll",Sha256=HashText("core test")}}},new(){Id="parts",Name="Fixture Parts",Version="1.0.0",Compatibility="test",Dependencies=new(){"core"},Files=new(){new(){Target="Mods/Parts.dll",ArchiveId="mods",Entry="parts.dll",Sha256=HashText("parts test")}}}}};
var engine=new InstallerEngine(Path.Combine(root,"data"),_=>{}){Offline=true,LocalPayloadDir=payload};
engine.TestAfterWrite=_=>throw new IOException("injected failure");
try{await engine.Install(pack,game,new[]{"parts"},false,null,CancellationToken.None);throw new Exception("Expected failure");}catch(IOException e)when(e.Message=="injected failure"){}
if(File.Exists(Path.Combine(game,"Mods","Parts.dll"))||File.Exists(Path.Combine(game,"version.dll")))throw new Exception("Rollback failed");
engine.TestAfterWrite=null;await engine.Install(pack,game,new[]{"parts"},false,null,CancellationToken.None);
if(!File.Exists(Path.Combine(game,"UserLibs","Core.dll"))||!File.Exists(Path.Combine(game,"Mods","Parts.dll")))throw new Exception("Missing dependency or mod");
await engine.Install(pack,game,new[]{"parts"},false,null,CancellationToken.None);
File.WriteAllText(Path.Combine(game,"Mods","Parts.dll"),"user modified");
try{await engine.Install(pack,game,new[]{"parts"},false,null,CancellationToken.None);throw new Exception("Collision was not blocked");}catch(IOException e)when(e.Message.Contains("Nie nadpisano")){}
if(File.ReadAllText(Path.Combine(game,"Mods","StellarDrive.AutoHost.dll"))!="existing own mod sentinel"||File.ReadAllText(Path.Combine(game,"world.json"))!="world sentinel")throw new Exception("Unrelated files modified");
Console.WriteLine("PASS: real 39-mod manifest and dependencies; missing-pack detection; transactional rollback; offline verified archive install; dependency installation; idempotent repeat; collision protection; existing AutoHost and world preservation. No game executed.");
