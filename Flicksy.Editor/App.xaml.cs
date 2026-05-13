using System.IO;
using System.Windows;

namespace Flicksy.Editor;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var startupWindow = new PostSnipWindow();
        var mediaPath = e.Args.FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(mediaPath) && File.Exists(mediaPath))
        {
            var extension = Path.GetExtension(mediaPath).ToLowerInvariant();

            if (IsVideoExtension(extension))
            {
                startupWindow.LoadVideo(mediaPath);
            }
            else
            {
                startupWindow.LoadImage(mediaPath);
            }
        }

        MainWindow = startupWindow;
        startupWindow.Show();
    }

    private static bool IsVideoExtension(string extension)
    {
        return extension is ".mp4" or ".mov" or ".avi" or ".wmv" or ".mkv";
    }
}
