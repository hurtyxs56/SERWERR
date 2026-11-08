using StellarDrive.Server;
// Isolated file-operation tests: these are NOT a game or multiplayer simulation.
var gameSource=args[0];var root=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);
var game=Path.Combine(root,"game");Directory.CreateDirectory(Path.Combine(game,"StellarDrive_Data","Managed"));
File.Copy(Path.Combine(gameSource,"StellarDrive.exe"),Path.Combine(game,"StellarDrive.exe"));
File.Copy(Path.Combine(gameSource,"StellarDrive_Data","Managed","Assembly-CSharp.dll"),Path.Combine(game,"StellarDrive_Data","Managed","Assembly-CSharp.dll"));
var save=Path.Combine(root,"world.json");File.WriteAllText(save,"{\"sentinel\":42}");var config=new ServerConfig{WorldPath=save};
Directory.CreateDirectory(Path.Combine(game,"Mods"));var foreign=Path.Combine(game,"Mods","foreign.dll");File.WriteAllText(foreign,"do not overwrite");
var payload=Path.Combine(root,"payload");Directory.CreateDirectory(Path.Combine(payload,ServerFiles.Folder));Directory.CreateDirectory(Path.Combine(payload,"Mods"));
File.Copy(Path.Combine(AppContext.BaseDirectory,"../../../../Payload/StellarDrive.AutoHost.dll"),Path.Combine(payload,"Mods","StellarDrive.AutoHost.dll"));
var own=Path.Combine(payload,ServerFiles.Folder,"test.txt");File.WriteAllText(own,"v1");
int tests=0;void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);tests++;}
void Reject(Action action,string name){try{action();}catch(IOException){Check(true,name);return;}throw new Exception("Not rejected: "+name);}
foreach(var bad in new[]{"../outside","C:/outside","a:stream","a/../b","a. /b"})Reject(()=>ServerFiles.Safe(game,bad),"unsafe path "+bad);
var installer=new Installer{FaultAfterWrite=_=>throw new IOException("injected copy failure")};
Reject(()=>installer.Install(game,payload,config,false),"copy failure propagated");
Check(!File.Exists(Path.Combine(game,"Mods","StellarDrive.AutoHost.dll"))&&!File.Exists(Path.Combine(ServerFiles.Root(game),"installation.json")),"rollback removes copied files and does not commit manifest");
installer.FaultAfterWrite=null;installer.Install(game,payload,config,false);
Check(File.ReadAllText(foreign)=="do not overwrite"&&File.ReadAllText(save)=="{\"sentinel\":42}","foreign mod and world preserved");
var cfgPath=Path.Combine(ServerFiles.Root(game),"autohost.json");config.MaxPlayers=12;ServerFiles.Write(cfgPath,config);File.WriteAllText(own,"v2");installer.Install(game,payload,new ServerConfig{WorldPath=save},false);
Check(File.ReadAllText(Path.Combine(ServerFiles.Root(game),"test.txt"))=="v2"&&ServerFiles.Read<ServerConfig>(cfgPath).MaxPlayers==12,"update and config retention");
File.WriteAllText(Path.Combine(ServerFiles.Root(game),"test.txt"),"user changed");
Reject(()=>installer.Install(game,payload,config,false),"modified installed file protected");
installer.Uninstall(game);Check(!File.Exists(Path.Combine(game,"Mods","StellarDrive.AutoHost.dll")),"owned mod removed");
Check(File.ReadAllText(Path.Combine(ServerFiles.Root(game),"test.txt"))=="user changed"&&File.Exists(cfgPath)&&File.Exists(save)&&File.Exists(foreign),"uninstall preserves user changes, config, save and unrelated mods");
File.WriteAllText(Path.Combine(game,"Mods","StellarDrive.AutoHost.dll"),"unmanaged conflict");Reject(()=>installer.Install(game,payload,config,false),"unmanaged mod collision protected");
Check(!ServerFiles.HasLoader(game),"missing loader detected");Directory.CreateDirectory(Path.Combine(game,"MelonLoader"));Reject(()=>ServerFiles.HasLoader(game),"partial loader rejected");
config.AutoSaveMinutes=0;Reject(()=>ServerFiles.ValidateConfig(config),"invalid save interval rejected");
Console.WriteLine($"{tests} assertions passed. Fixture: {root}");

