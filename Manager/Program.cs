using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using StellarDrive.Server;
using StellarDrive.Server.UI;
public static class Program {[STAThread]public static void Main(string[] args){Ui.App().Run(new ManagerWindow(args));}}
public sealed class ManagerWindow:Window {
 string game;readonly TextBox world=new(),players=new(){Text="8"},interval=new(){Text="300"};readonly CheckBox friends=new(){Content="Tylko znajomi konta Steam VPS",Foreground=Ui.Ink,IsChecked=true},compat=new(){Content="Zgodność headless z Odyssey (eksperymentalna)",Foreground=Ui.Ink,IsChecked=true};
 readonly TextBlock status=Ui.Text("Odczytywanie statusu…",16),message=Ui.Text("");readonly TextBox log=new(){IsReadOnly=true,AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontFamily=new System.Windows.Media.FontFamily("Consolas"),FontSize=12};readonly ListBox mods=new();readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(2)};
 public ManagerWindow(string[] args){
  game=Ui.Arg(args,"--game")??Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))??"";
  Title="StellarDrive Server Manager • 0.2.0 alpha";Width=1020;Height=760;MinWidth=800;MinHeight=600;Background=Ui.Bg;Foreground=Ui.Ink;WindowStartupLocation=WindowStartupLocation.CenterScreen;
  var layout=new DockPanel{Margin=new Thickness(22)};Content=layout;
  var header=new StackPanel();header.Children.Add(Ui.Text("STELLARDRIVE / SERVER MANAGER",25));header.Children.Add(Ui.Text("Wersja testowa • AutoHost 0.1.2 • Steam i VPS wymagają testu drugiego gracza"));header.Children.Add(status);header.Children.Add(Ui.Text(game));DockPanel.SetDock(header,Dock.Top);layout.Children.Add(header);
  var actions=new WrapPanel();actions.Children.Add(Ui.AsyncButton("▶ START",async()=>{await Task.Run(()=>ServerControl.Start(game));message.Text="Uruchomiono nadzorcę. Serwer może ładować dużą paczkę kilka minut.";}));actions.Children.Add(Ui.AsyncButton("■ STOP I ZAPIS",async()=>{await ServerControl.Stop(game);message.Text="Serwer zakończył proces.";}));actions.Children.Add(Ui.AsyncButton("↻ RESTART",async()=>{await ServerControl.Restart(game);message.Text="Ponowne uruchomienie z zapisanym światem.";}));
  actions.Children.Add(Ui.Button("Folder serwera",()=>Ui.Launch(ServerFiles.Root(game))));actions.Children.Add(Ui.Button("Instrukcja testu VPS",()=>Ui.Launch(Path.Combine(ServerFiles.Root(game),"TEST-NA-VPS.md"))));DockPanel.SetDock(actions,Dock.Top);layout.Children.Add(actions);
  DockPanel.SetDock(message,Dock.Bottom);layout.Children.Add(message);
  var tabs=new TabControl{Margin=new Thickness(0,12,0,0),Background=Ui.Panel,Foreground=System.Windows.Media.Brushes.Black};layout.Children.Add(tabs);mods.Background=Ui.Panel;mods.Foreground=Ui.Ink;
  tabs.Items.Add(new TabItem{Header="Logi",Content=log});tabs.Items.Add(new TabItem{Header="Mody",Content=mods});
  var config=new StackPanel{Margin=new Thickness(18)};tabs.Items.Add(new TabItem{Header="Świat i ustawienia",Content=new ScrollViewer{Content=config}});
  config.Children.Add(Ui.Text("Świat — zachowuj cały katalog razem z plikami dodatków"));config.Children.Add(world);config.Children.Add(Ui.Button("Wybierz world.json…",()=>{var p=Ui.World();if(p!=null)world.Text=p;}));config.Children.Add(Ui.Text("Maksymalna liczba graczy"));config.Children.Add(players);config.Children.Add(Ui.Text("Autozapis co [sekundy, 20–3600]"));config.Children.Add(interval);config.Children.Add(friends);config.Children.Add(compat);
  config.Children.Add(Ui.Text("Gra używa Steam Rich Presence, nie osobnego lobby SteamMatchmaking. Brak fikcyjnych ustawień lobby. Zmiana konfiguracji wymaga zatrzymania serwera."));
  config.Children.Add(Ui.AsyncButton("Zapisz konfigurację",async()=>{var c=new ServerConfig{WorldPath=world.Text.Trim(),MaxPlayers=int.Parse(players.Text),AutoSaveMinutes=double.Parse(interval.Text,CultureInfo.CurrentCulture)/60,FriendsOnly=friends.IsChecked==true,HeadlessShaderCompatibility=compat.IsChecked==true};await Task.Run(()=>ServerControl.SaveConfig(game,c));message.Text="Zapisano konfigurację.";}));
  var maintain=new StackPanel{Margin=new Thickness(18)};tabs.Items.Add(new TabItem{Header="Autostart i instalacja",Content=maintain});
  maintain.Children.Add(Ui.Text("Panel można zamknąć — nadzorca i serwer pozostają uruchomione. Autostart działa po logowaniu Windows; Steam musi być uruchomiony i zalogowany w tej samej sesji."));
  maintain.Children.Add(Ui.AsyncButton("Włącz start po logowaniu",async()=>{await ServerControl.SetAutostart(game,true);message.Text="Włączono zadanie po logowaniu. To nie usługa przed logowaniem Windows.";}));
  maintain.Children.Add(Ui.AsyncButton("Wyłącz autostart",async()=>{await ServerControl.SetAutostart(game,false);message.Text="Wyłączono zadanie autostartu.";}));
  maintain.Children.Add(Ui.Button("Zainstaluj aktualizację z pliku…",()=>{var d=new Microsoft.Win32.OpenFileDialog{Title="Wybierz nowe StellarDrive-Server-Setup.exe",Filter="Instalator|StellarDrive-Server-Setup.exe"};if(d.ShowDialog()==true){Ui.Launch(d.FileName,game);Close();}}));
  maintain.Children.Add(Ui.Button("Odinstaluj AutoHost i panel…",()=>{var p=new ProcessStartInfo(Path.Combine(ServerFiles.Root(game),"StellarDrive-Server-Setup.exe")){UseShellExecute=true};p.ArgumentList.Add("--uninstall");p.ArgumentList.Add("--game");p.ArgumentList.Add(game);Process.Start(p);Close();}));
  Loaded+=(_,_)=>{try{var c=ServerFiles.Read<ServerConfig>(Path.Combine(ServerFiles.Root(game),"autohost.json"));world.Text=c.WorldPath;players.Text=c.MaxPlayers.ToString();interval.Text=(c.AutoSaveMinutes*60).ToString(CultureInfo.CurrentCulture);friends.IsChecked=c.FriendsOnly;compat.IsChecked=c.HeadlessShaderCompatibility;RefreshMods();Refresh();timer.Start();}catch(Exception e){message.Text=e.Message;}};
  timer.Tick+=(_,_)=>Refresh();Closed+=(_,_)=>timer.Stop();
 }
 void Refresh(){try{bool running=ServerFiles.GameRunning(game);var s=ServerControl.Status(game);status.Text=running? $"Proces działa • {s?.phase??"oczekiwanie"} • Steam: {(s?.steamInitialized==true?"gotowy":"oczekiwanie")} • multiplayer: {(s?.serverStarted==true?"aktywny":"oczekiwanie")} • gracze: {s?.players??0} • zapisy: {s?.saves??0}":"Serwer zatrzymany";var path=Path.Combine(game,"MelonLoader","Latest.log");var text=ServerControl.ReadLog(Path.Combine(ServerFiles.Root(game),"supervisor.log"))+Environment.NewLine+"--- GRA ---"+Environment.NewLine+ServerControl.ReadLog(path);if(log.Text!=text){log.Text=text;log.ScrollToEnd();}if(s?.lastError!=null&&running)message.Text="Ostatni błąd silnika (sprawdź logi): "+s.lastError;}catch(Exception e){message.Text=e.Message;}}
 void RefreshMods(){mods.Items.Clear();foreach(var file in Directory.EnumerateFiles(Path.Combine(game,"Mods"),"*.dll").Order()){try{mods.Items.Add(Path.GetFileName(file)+"  "+AssemblyName.GetAssemblyName(file).Version);}catch{mods.Items.Add(Path.GetFileName(file)+" — nie odczytano wersji");}}}
}


