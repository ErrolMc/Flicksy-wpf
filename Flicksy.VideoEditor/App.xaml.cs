using System;
using System.IO;
using System.Linq;
using System.Windows;
using Flicksy.Drawing.Media;
using Flicksy.VideoEditor.Windows;
using Microsoft.Extensions.Hosting;

namespace Flicksy.VideoEditor;

public partial class App : Application
{
    private const string NewVideoProjectArgName = "--new-video-project";

    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            FfmpegLocator.Initialize();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"FFmpeg initialization failed:\n{ex.Message}\n\nThe application will exit.",
                "Flicksy.VideoEditor",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var builder = Host.CreateApplicationBuilder();
        _host = builder.Build();
        _host.Start();

        var mode = ResolveStartupMode(e.Args);
        Window window = mode switch
        {
            StartupMode.EmptyEditor => new VideoEditorWindow(),
            StartupMode.EditorWithSource src => new VideoEditorWindow(src.Path),
            _ => new WelcomeWindow(),
        };

        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
            _host = null;
        }

        base.OnExit(e);
    }

    public static StartupMode ResolveStartupMode(string[] args)
    {
        if (args.Length == 0)
        {
            return new StartupMode.Welcome();
        }

        if (args.Any(a => a.Equals(NewVideoProjectArgName, StringComparison.OrdinalIgnoreCase)))
        {
            return new StartupMode.EmptyEditor();
        }

        if (TryValidatePath(args[0], out var validatedPath))
        {
            return new StartupMode.EditorWithSource(validatedPath);
        }

        return new StartupMode.Welcome();
    }

    private static bool TryValidatePath(string? rawPath, out string validatedPath)
    {
        validatedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(rawPath.Trim());
            if (!File.Exists(fullPath))
            {
                return false;
            }

            validatedPath = fullPath;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
