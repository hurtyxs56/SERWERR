using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Security.Cryptography;
using Core.Services;
using FishNet;
using FishNet.Transporting;
using FishNet.Transporting.Multipass;
using HarmonyLib;
using Managers.Common;
using Managers.Interface;
using Managers.Server;
using Managers.Server.Saving;
using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Players.Services;
using Saving.Services;
using Saving.Interface.Model;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;
[assembly:MelonInfo(typeof(StellarDriveAutoHost.AutoHost),"StellarDrive AutoHost","0.1.2-alpha.1","Codex + użytkownik")]
[assembly:MelonGame("CuriousOwlGames","StellarDrive")]
[assembly:HarmonyDontPatchAll]
namespace StellarDriveAutoHost {
 public sealed class Settings {
  public bool AutoHost=true;public string WorldPath="";public int MaxPlayers=8;public double AutoSaveMinutes=5;public bool PauseWhenEmpty=true;public bool HeadlessShaderCompatibility=false;public int StartupTimeoutSeconds=240;public bool FriendsOnly=true;
 }
 public sealed class AutoHost:MelonMod {
  internal static AutoHost Self;internal static Settings Config;internal static bool Active,WorldLoaded,Failed,Stopping;internal static string Data,Phase="initializing";
  static FileStream worldLock;static HarmonyLib.Harmony harmony;static float nextSave,nextStatus,nextCommand,start;static bool saving;static DateTime beforeSave;static float saveStarted;static int saves;static string lastError;static bool steamAdvertised;
  static string Arg(string name)=>Environment.GetCommandLineArgs().FirstOrDefault(x=>x.StartsWith(name,StringComparison.Ordinal))?.Substring(name.Length).Trim('"');
  internal static void Log(string text){Self.LoggerInstance.Msg(text);}
  public override void OnInitializeMelon(){
   Self=this;var configFile=Arg("--autohost-config=");if(configFile==null){LoggerInstance.Msg("AutoHost nieaktywny: brak --autohost-config=pełna-ścieżka.json");return;}
   try{
    configFile=Path.GetFullPath(configFile);Data=Path.GetDirectoryName(configFile);Config=JsonConvert.DeserializeObject<Settings>(File.ReadAllText(configFile),new JsonSerializerSettings{MissingMemberHandling=MissingMemberHandling.Error,TypeNameHandling=TypeNameHandling.None,MaxDepth=8});
    if(!Config.AutoHost)return;if(Config.MaxPlayers<1||Config.MaxPlayers>32766||Config.AutoSaveMinutes<1d/3||Config.AutoSaveMinutes>60||Config.StartupTimeoutSeconds<30||Config.StartupTimeoutSeconds>900)throw new Exception("Nieprawidłowe limity konfiguracji.");
    if(!Path.IsPathRooted(Config.WorldPath)||!File.Exists(Config.WorldPath)||Path.GetFileName(Config.WorldPath)!="world.json")throw new Exception("WorldPath musi wskazywać istniejący plik world.json w osobnym folderze świata.");
    Config.WorldPath=Path.GetFullPath(Config.WorldPath);
    var game=Path.GetDirectoryName(typeof(MainGameSceneInitializer).Assembly.Location);
    using(var f=File.OpenRead(Path.Combine(game,"Assembly-CSharp.dll")))using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(f)).Replace("-","")!="25D3F704C52EF689EA04E1EDDDCDB014914D8CBCE071F88871066A6B1375715B")throw new Exception("Nieobsługiwana wersja gry: oczekiwane Demo 0.8.2.");
    if(Environment.GetCommandLineArgs().Contains("-dedicated"))throw new Exception("Nie łącz trybu AutoHost z DedicatedPrototype -dedicated.");
    worldLock=new FileStream(Config.WorldPath+".autohost.lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
    Active=true;harmony=new HarmonyLib.Harmony("stellardrive.autohost.steam");
    Patch(typeof(MainGameSceneInitializer),"Start",nameof(StartScene));
    harmony.Patch(AccessTools.Method(typeof(ServerStateLoader),"LoadWorldFromSavedGame"),postfix:new HarmonyMethod(typeof(AutoHost),nameof(Loaded)));
    Patch(typeof(GameSaver),"SaveScreenshot",nameof(NoScreenshot));Patch(typeof(GameAutoSaver),"UpdateAutoSave",nameof(NoNativeAutoSave));
    Patch(typeof(WorldPhysicsServerUpdater),"PrePhysics",nameof(Simulate));Patch(typeof(WorldPhysicsServerUpdater),"PostPhysics",nameof(Simulate));
    var thumbnails=AccessTools.TypeByName("Ships.Services.ShipPartsThumbnailRuntimeCache");if(thumbnails!=null)Patch(thumbnails,"Update",nameof(NoClientWork));
    if(Config.HeadlessShaderCompatibility&&SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)HeadlessCompatibility.Install(harmony);
    Application.runInBackground=true;Application.targetFrameRate=30;
    Application.logMessageReceived+=UnityLog;start=Time.realtimeSinceStartup;Phase="waiting-for-steam";Status();Log("AutoHost enabled. Native Steam transport; no local host player. World: "+Config.WorldPath);
   }catch(Exception e){Fail(e.ToString());}
  }
  static void Patch(Type t,string method,string prefix){var target=AccessTools.Method(t,method);if(target==null)throw new MissingMethodException(t.FullName,method);harmony.Patch(target,prefix:new HarmonyMethod(typeof(AutoHost),prefix));}
  public override void OnLateInitializeMelon(){if(Active&&!Failed)MelonCoroutines.Start(Boot());}
  static IEnumerator Boot(){
   while(!SteamManager.Initialized){if(Time.realtimeSinceStartup-start>Config.StartupTimeoutSeconds){Fail("Steamworks nie zainicjalizował się. Wymagany zalogowany klient Steam i uprawnienie do Demo.");yield break;}yield return new WaitForSecondsRealtime(1);}
   Log("Steamworks initialized; native Steam identity active.");Phase="loading-scene";Status();
   MainGameSceneLoadData.Mode=MainGameSceneLoadData.GameMode.HostGameSteam;
   MainGameSceneLoadData.SaveFileFullName=Config.WorldPath;SceneManager.LoadScene("MainGame");
  }
  static bool StartScene(MainGameSceneInitializer __instance){if(!Active)return true;MelonCoroutines.Start(StartTransport());return false;}
  static IEnumerator StartTransport(){
   yield return new WaitForSecondsRealtime(3);
   try{
    if(!SteamManager.Initialized)throw new Exception("Steamworks disconnected before transport start.");
    var multi=InstanceFinder.TransportManager.Transport as Multipass;if(multi==null)throw new Exception("Native Multipass not found.");
    int index=multi.Transports.ToList().FindIndex(t=>t is FishySteamworks.FishySteamworks);if(index<0)throw new Exception("Native FishySteamworks not found.");
    var transport=(FishySteamworks.FishySteamworks)multi.Transports[index];AccessTools.Field(transport.GetType(),"_maximumClients").SetValue(transport,(ushort)Config.MaxPlayers);
    InstanceFinder.ServerManager.OnRemoteConnectionState+=(connection,args)=>{
     Log("Remote connection "+connection.ClientId+": "+args.ConnectionState);
     if(args.ConnectionState==RemoteConnectionState.Started&&Config.FriendsOnly){
      var address=multi.GetConnectionAddress(connection.ClientId);
      if(!ulong.TryParse(address,out var remote)||!SteamFriends.HasFriend(new CSteamID(remote),EFriendFlags.k_EFriendFlagImmediate)){
       Log("Rejected non-friend or unidentified Steam transport peer.");connection.Disconnect(true);
      }
     }
    };
    Phase="starting-steam-server";Status();if(!multi.StartConnection(true,index))throw new Exception("FishySteamworks rejected StartConnection.");
    Log("FishySteamworks StartConnection accepted. Waiting for world loading callback.");
   }catch(Exception e){Fail(e.ToString());}
  }
  static void Loaded(){if(!Active||Failed)return;WorldLoaded=true;nextSave=Time.realtimeSinceStartup+(float)(Config.AutoSaveMinutes*60);Phase="ready";steamAdvertised=SteamFriends.SetRichPresence("connect","+connect "+SteamUser.GetSteamID());
   var parts=new Ships.Interface.Services.PartSettingsProvider().GetAllPartSettings().Select(p=>new{id=p.id,name=p.fullLabel}).ToArray();
   File.WriteAllText(Path.Combine(Data,"content-inventory.json"),JsonConvert.SerializeObject(new{mods=MelonMod.RegisteredMelons.Select(m=>new{name=m.Info.Name,version=m.Info.Version}).ToArray(),parts},Formatting.Indented));
   Log("World loaded; Steam Rich Presence advertisement result: "+steamAdvertised+"; registered parts: "+parts.Length);Status();}
  static bool NoNativeAutoSave()=>!Active;
  static bool NoClientWork()=>!Active;
  static bool Simulate()=>!Active||!WorldLoaded||!Config.PauseWhenEmpty||ServiceLocator.GetService<PlayersServerTracker>().ActivePlayers.Count>0;
  static IEnumerator Empty(){yield break;}
  static bool NoScreenshot(GameSaver __instance,ref IEnumerator __result){if(!Active)return true;AccessTools.Field(typeof(GameSaver),"_currentSavingParams").SetValue(__instance,null);__result=Empty();return false;}
  static void UnityLog(string message,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error){lastError=message;try{File.AppendAllText(Path.Combine(Data,"unity-errors.log"),DateTime.UtcNow.ToString("O")+" "+message+"\n"+trace+"\n");}catch{}}}
  public override void OnUpdate(){
   if(!Active||Failed)return;
   try{
    if(!WorldLoaded&&Time.realtimeSinceStartup-start>Config.StartupTimeoutSeconds){Fail("Startup timeout: "+Phase);return;}
    if(WorldLoaded&&InstanceFinder.ClientManager?.Started==true){Fail("Unexpected local player client. Dedicated server invariant violated.");return;}
    if(saving){var saver=ServiceLocator.GetService<GameSaver>();if(!saver.IsCurrentlySaving()){
      if(File.GetLastWriteTimeUtc(Config.WorldPath)<=beforeSave)throw new IOException("Save finished without updating world.json.");
      // Parse the actual output before reporting success. Native serializer may use non-standard floating values; JObject supports those.
      JObject.Parse(File.ReadAllText(Config.WorldPath));saving=false;saves++;Log("World save verified on disk (#"+saves+").");
      if(Stopping){Phase="stopped";Status();SteamFriends.ClearRichPresence();InstanceFinder.ServerManager.StopConnection(true);Application.Quit(0);return;}
     }else if(Time.realtimeSinceStartup-saveStarted>120){throw new TimeoutException("Save timeout — process retained, no forced shutdown.");}}
    if(WorldLoaded&&!saving&&!Stopping&&Time.realtimeSinceStartup>=nextSave){BeginSave();}
    if(Time.realtimeSinceStartup>=nextCommand){nextCommand=Time.realtimeSinceStartup+1;var file=Path.Combine(Data,"commands.txt");if(File.Exists(file)&&new FileInfo(file).Length>0){var claimed=file+"."+DateTime.UtcNow.Ticks+".processed";File.Move(file,claimed);var commands=File.ReadAllLines(claimed);foreach(var command in commands){if(command.Trim()=="stop"){Stopping=true;if(!WorldLoaded){Phase="stopped";Status();Application.Quit(0);return;}if(!saving)BeginSave();}else if(command.Trim()=="save"&&!saving)BeginSave();}}}
    if(Stopping&&WorldLoaded&&!saving&&Time.realtimeSinceStartup>=nextSave)BeginSave();
    if(Time.realtimeSinceStartup>=nextStatus){nextStatus=Time.realtimeSinceStartup+2;Status();}
   }catch(Exception e){lastError=e.ToString();Log("ERROR: "+lastError);Phase="save-or-runtime-error";Status();saving=false;nextSave=Time.realtimeSinceStartup+60;}
  }
  static void BeginSave(){
   if(!WorldLoaded)throw new Exception("World not loaded; cannot save.");var saver=ServiceLocator.GetService<GameSaver>();if(saver==null||saver.IsCurrentlySaving())return;
   beforeSave=File.GetLastWriteTimeUtc(Config.WorldPath);saveStarted=Time.realtimeSinceStartup;saving=true;nextSave=Time.realtimeSinceStartup+(float)(Config.AutoSaveMinutes*60);
   File.Copy(Config.WorldPath,Config.WorldPath+".autohost-lastgood",true);Log("Saving world via native GameSaver.");saver.SaveGame(new SaveGameParams{IsAutomatic=true});
  }
  static void Status(){if(Data==null)return;File.WriteAllText(Path.Combine(Data,"status.json"),JsonConvert.SerializeObject(new{version="0.1.2-alpha.1",phase=Phase,utc=DateTime.UtcNow,pid=System.Diagnostics.Process.GetCurrentProcess().Id,world=Config?.WorldPath,worldLoaded=WorldLoaded,steamInitialized=SteamManager.Initialized,steamRichPresence=steamAdvertised,serverStarted=InstanceFinder.ServerManager?.Started??false,localClient=InstanceFinder.ClientManager?.Started??false,graphics=SystemInfo.graphicsDeviceType.ToString(),players=WorldLoaded?ServiceLocator.GetService<PlayersServerTracker>().ActivePlayers.Count:0,saves,saving,lastError},Formatting.Indented));}
  static void Fail(string reason){Failed=true;Phase="failed";lastError=reason;Self.LoggerInstance.Error(reason);try{Status();}catch{}Application.Quit(2);}
  public override void OnDeinitializeMelon(){Application.logMessageReceived-=UnityLog;if(Active&&SteamManager.Initialized)SteamFriends.ClearRichPresence();worldLock?.Dispose();harmony?.UnpatchSelf();}
 }
}
