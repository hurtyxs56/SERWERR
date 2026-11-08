using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace StellarDrive.Server.UI;
public static class Ui {
 public static readonly Brush Bg=new SolidColorBrush(Color.FromRgb(16,20,31)),Panel=new SolidColorBrush(Color.FromRgb(29,36,52)),Ink=new SolidColorBrush(Color.FromRgb(230,237,248)),Accent=new SolidColorBrush(Color.FromRgb(84,104,238));
 public static Application App(){var app=new Application();var button=new Style(typeof(Button));button.Setters.Add(new Setter(Control.BackgroundProperty,Accent));button.Setters.Add(new Setter(Control.ForegroundProperty,Brushes.White));button.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(15,9,15,9)));button.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(4)));button.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(0)));app.Resources.Add(typeof(Button),button);var text=new Style(typeof(TextBox));text.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(8)));text.Setters.Add(new Setter(Control.BackgroundProperty,Panel));text.Setters.Add(new Setter(Control.ForegroundProperty,Ink));text.Setters.Add(new Setter(Control.BorderBrushProperty,new SolidColorBrush(Color.FromRgb(72,82,108))));text.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(0,4,0,8)));app.Resources.Add(typeof(TextBox),text);return app;}
 public static TextBlock Text(string value,double size=14)=>new(){Text=value,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,7,0,7),Foreground=Ink};
 public static Button Button(string name,Action action){var b=new Button{Content=name};b.Click+=(_,_)=>action();return b;}
 public static Button AsyncButton(string name,Func<Task> action){var b=new Button{Content=name};b.Click+=async(_,_)=>{b.IsEnabled=false;try{await action();}catch(Exception e){MessageBox.Show(e.Message,"StellarDrive Server",MessageBoxButton.OK,MessageBoxImage.Error);}finally{b.IsEnabled=true;}};return b;}
 public static string? Folder(string title){var d=new Microsoft.Win32.OpenFolderDialog{Title=title};return d.ShowDialog()==true?d.FolderName:null;}
 public static string? World(){var d=new Microsoft.Win32.OpenFileDialog{Title="Wybierz world.json z pełnego katalogu świata",Filter="Zapis świata|world.json|Pliki JSON|*.json"};return d.ShowDialog()==true?d.FileName:null;}
 public static string? Arg(string[] args,string name){var i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:null;}
 public static void Launch(string path,string? game=null){var p=new System.Diagnostics.ProcessStartInfo(path){UseShellExecute=true};if(game!=null){p.ArgumentList.Add("--game");p.ArgumentList.Add(game);}System.Diagnostics.Process.Start(p);}
}

