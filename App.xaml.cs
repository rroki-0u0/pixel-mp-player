using System.Windows;

namespace PixelMpPlayer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        if (e.Args.Length > 0)
        {
            var mainWindow = new MainWindow();
            mainWindow.LoadFile(e.Args[0]);
            mainWindow.Show();
        }
    }
}