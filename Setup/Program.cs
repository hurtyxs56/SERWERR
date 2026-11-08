using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using StellarDrive.Server;
using StellarDrive.Server.UI;
public static class Program {[STAThread]public static void Main(string[] args){Ui.App().Run(new SetupWindow(args));}}
public sealed class SetupWindow:Window {
 readonly StackPanel page=new();readonly TextBox game=new(),world=new(),output=new(){IsReadOnly=true,AcceptsReturn=true,Height=250,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};readonly Button next=new(){Content="Dalej →"},back=new(){Content="← Wstecz"};readonly TextBlock info=Ui.Text("");readonly CheckBox loader=new(){Content="Pobierz i zainstaluj MelonLoader 0.7.3, jeśli go brakuje",IsChecked=true};readonly bool uninstall;int step;bool success;
 public SetupWindow(string[] args){
  uninstall=args.Contains("--uninstall");Title="StellarDrive Server Setup";Width=850;Height=670;MinWidth=740;MinHeight=600;Background=Ui.Bg;Foreground=Ui.Ink;WindowStartupLocation=WindowStartupLocation.CenterScreen;
  var layout=new DockPanel{Margin=new Thickness(25)};Content=layout;var header=new StackPanel();header.Children.Add(Ui.Text(uninstall?"STELLARDRIVE / ODINSTALOWANIE":"STELLARDRIVE / SERVER SETUP",26));header.Children.Add(Ui.Text("0.2.0 alpha • instalator i panel Windows • AutoHost 0.1.2"));DockPanel.SetDock(header,Dock.Top);layout.Children.Add(header);
  var bottom=new StackPanel();bottom.Children.Add(info);var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};buttons.Children.Add(back);buttons.Children.Add(next);bottom.Children.Add(buttons);DockPanel.SetDock(bottom,Dock.Bottom);layout.Children.Add(bottom);layout.Children.Add(new ScrollViewer{Content=page,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
  game.Text=Ui.Arg(args,"--game")??ServerFiles.FindGames().FirstOrDefault()??"";
  next.Click+=async(_,_)=>{try{if(step==0){Validate();step=1;Draw();}else if(step==1){await Execute();}else {if(success&&!uninstall)Ui.Launch(Path.Combine(ServerFiles.Root(game.Text),"StellarDrive-Server-Manager.exe"),game.Text);Close();}}catch(Exception e){info.Text=e.Message;}};
  back.Click+=(_,_)=>{step=0;Draw();};Draw();
 }
 void Validate(){game.Text=ServerFiles.ValidateGame(game.Text);if(ServerFiles.GameRunning(game.Text))throw new IOException("Zamknij tę kopię StellarDrive.");if(!uninstall){ServerFiles.ValidateConfig(new ServerConfig{WorldPath=world.Text});if(!ServerFiles.HasLoader(game.Text)&&loader.IsChecked!=true)throw new IOException("Serwer wymaga MelonLoadera.");}else if(!File.Exists(Path.Combine(ServerFiles.Root(game.Text),"installation.json")))throw new IOException("Brak instalacji AutoHost w tym folderze.");}
 void Draw(){page.Children.Clear();info.Text="";back.IsEnabled=step==1;next.IsEnabled=true;
  if(step==0){next.Content="Dalej →";page.Children.Add(Ui.Text("Folder gry"));page.Children.Add(game);page.Children.Add(Ui.Button("Wybierz folder…",()=>{var p=Ui.Folder("Folder StellarDrive Demo");if(p!=null)game.Text=p;}));
   var found=ServerFiles.FindGames();if(found.Count>0){var choices=new ComboBox{ItemsSource=found,Margin=new Thickness(0,6,0,10)};choices.SelectionChanged+=(_,_)=>game.Text=choices.SelectedItem as string??game.Text;page.Children.Add(Ui.Text("Instalacje wykryte w bibliotekach Steam:"));page.Children.Add(choices);}
   if(!uninstall){page.Children.Add(Ui.Text("Zapis świata — wybierz world.json z pełnego katalogu"));page.Children.Add(world);page.Children.Add(Ui.Button("Wybierz zapis…",()=>{var p=Ui.World();if(p!=null)world.Text=p;}));page.Children.Add(loader);page.Children.Add(Ui.Text("Zachowamy istniejące mody i zapisy. Instalator doda AutoHost oraz panel do folderu StellarDriveServer. Przy aktualizacji zachowa istniejącą konfigurację."));}
   page.Children.Add(Ui.Text("Wydanie eksperymentalne: lokalny host headless przetestowany; VPS, dołączenie drugiego konta Steam i praca 24/7 nie zostały jeszcze potwierdzone."));
  }else if(step==1){next.Content=uninstall?"Odinstaluj":"Instaluj";page.Children.Add(Ui.Text("Sprawdź plan zmian",21));page.Children.Add(Ui.Text("Gra: "+game.Text));if(uninstall){page.Children.Add(Ui.Text("Usunięte zostaną wyłącznie niezmienione pliki należące do instalacji AutoHost i panelu. Zapisy, konfiguracja, logi, loader i pozostałe mody zostaną zachowane."));}else{page.Children.Add(Ui.Text("Świat: "+world.Text));page.Children.Add(Ui.Text("Mody w folderze: "+(Directory.Exists(Path.Combine(game.Text,"Mods"))?Directory.GetFiles(Path.Combine(game.Text,"Mods"),"*.dll").Length:0)+" — pozostaną zachowane."));page.Children.Add(Ui.Text(ServerFiles.HasLoader(game.Text)?"MelonLoader 0.7.3: wykryty, pozostanie bez zmian.":"Pobierzemy MelonLoader 0.7.3 z oficjalnego wydania GitHub i sprawdzimy SHA-256."));page.Children.Add(Ui.Text("Dodamy mod AutoHost, panel zarządzania, skrót na pulpicie i wpis odinstalowania. Serwer uruchomisz przyciskiem START w panelu."));}}
  else{next.Content="Zakończ";back.IsEnabled=false;page.Children.Add(Ui.Text(success?(uninstall?"Odinstalowano pliki programu":"Instalacja zakończona") :"Instalacja nie została ukończona",23));page.Children.Add(output);if(success&&!uninstall)page.Children.Add(Ui.Text("Zakończ otworzy panel. Steam musi być zalogowany. Zacznij od kopii świata i testu z drugim graczem."));}
 }
 async Task Execute(){
  next.IsEnabled=back.IsEnabled=false;info.Text=uninstall?"Odinstalowywanie…":"Instalowanie…";output.Text="";var progress=new Progress<string>(s=>{output.AppendText(s+Environment.NewLine);info.Text=s;});string folder=game.Text,save=world.Text;
  try{
   foreach(var p in Process.GetProcessesByName("StellarDrive-Server-Manager"))using(p){if(p.MainModule?.FileName?.StartsWith(ServerFiles.Root(folder),StringComparison.OrdinalIgnoreCase)==true)throw new IOException("Zamknij Server Manager przed zmianami.");}
   if(uninstall){var install=ServerFiles.Read<Installation>(Path.Combine(ServerFiles.Root(folder),"installation.json"));if(install.Autostart)await ServerControl.SetAutostart(folder,false);var retained=await Task.Run(()=>new Installer().Uninstall(folder));ServerControl.Unregister(folder);output.Text="Zachowano dane oraz pliki wymagające zachowania:\n"+string.Join("\n",retained);}
   else {
    await Task.Run(async()=>{
     bool missing=!ServerFiles.HasLoader(folder);var stage=ServerFiles.Safe(folder,"StellarDriveServer/InstallWork/"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);var payload=Path.Combine(stage,"payload");Directory.CreateDirectory(payload);var assembly=Assembly.GetExecutingAssembly();
     foreach(var name in assembly.GetManifestResourceNames().Where(n=>n.StartsWith("payload/"))){var file=name[8..];var relative=file=="StellarDrive.AutoHost.dll"?"Mods/"+file:ServerFiles.Folder+"/"+file;var target=ServerFiles.Safe(payload,relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);using var input=assembly.GetManifestResourceStream(name)!;using var targetStream=File.Create(target);input.CopyTo(targetStream);}
     var manager=ServerFiles.Safe(payload,ServerFiles.Folder+"/StellarDrive-Server-Manager.exe");using(var input=assembly.GetManifestResourceStream("manager.exe")??throw new IOException("Brak panelu w instalatorze."))using(var target=File.Create(manager))input.CopyTo(target);
     File.Copy(Environment.ProcessPath!,ServerFiles.Safe(payload,ServerFiles.Folder+"/StellarDrive-Server-Setup.exe"));
     if(ServerFiles.Hash(ServerFiles.Safe(payload,"Mods/StellarDrive.AutoHost.dll"))!=ServerFiles.ModHash)throw new IOException("Niezgodna suma moda w instalatorze.");
     var installer=new Installer();if(missing){var zip=await installer.DownloadLoader(stage,progress);installer.ExtractLoader(zip,payload);}
     installer.Install(folder,payload,new ServerConfig{WorldPath=save},missing,progress);
    });
    try{ServerControl.Register(folder);}catch(Exception e){output.AppendText("Pliki zainstalowane. Skrót/wpis systemowy wymaga sprawdzenia: "+e.Message+"\n");}
   }
   success=true;
  }catch(Exception e){success=false;output.AppendText(e.ToString());}
  finally{step=2;Draw();}
 }
}

