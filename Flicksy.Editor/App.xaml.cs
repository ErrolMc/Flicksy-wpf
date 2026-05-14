using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;

namespace Flicksy.Editor;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const string AppSettingsFileName = "appsettings.json";
    private const string LaunchFilePathArgName = "--launch-file";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var startupWindow = new PostSnipWindow();
        var mediaPath = ResolveStartupMediaPath(e.Args);

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

    private static string? ResolveStartupMediaPath(string[] args)
    {
        var argumentPath = GetLaunchFilePathFromArguments(args) ?? args.FirstOrDefault();
        if (TryValidatePath(argumentPath, out var validatedFromArgs))
        {
            return validatedFromArgs;
        }

        var configuration = BuildConfiguration();
        return TryValidatePath(configuration["LaunchEditorWithFilePath"], out var validatedFromSettings)
            ? validatedFromSettings
            : null;
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(AppSettingsFileName, optional: true, reloadOnChange: false)
            .Build();
    }

    private static string? GetLaunchFilePathFromArguments(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals(LaunchFilePathArgName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 >= args.Length)
            {
                return null;
            }

            return args[i + 1];
        }

        return null;
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
