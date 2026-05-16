using System;
using System.IO;
using System.Linq;
using System.Windows;
using Flicksy.Editor.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Flicksy.Editor;

public partial class App : Application
{
    private const string LaunchFilePathArgName = "--launch-file";

    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddTransient<PostSnipViewModel>();
        builder.Services.AddTransient<PostSnipWindow>();

        _host = builder.Build();
        _host.Start();

        var window = _host.Services.GetRequiredService<PostSnipWindow>();
        var configuration = _host.Services.GetRequiredService<IConfiguration>();
        var mediaPath = ResolveStartupMediaPath(e.Args, configuration);

        if (!string.IsNullOrWhiteSpace(mediaPath))
        {
            var extension = Path.GetExtension(mediaPath).ToLowerInvariant();
            if (IsVideoExtension(extension))
            {
                window.ViewModel.LoadVideo(mediaPath);
            }
            else
            {
                window.ViewModel.LoadImage(mediaPath);
            }
        }

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

    private static bool IsVideoExtension(string extension)
    {
        return extension is ".mp4" or ".mov" or ".avi" or ".wmv" or ".mkv";
    }

    private static string? ResolveStartupMediaPath(string[] args, IConfiguration configuration)
    {
        var argumentPath = GetLaunchFilePathFromArguments(args) ?? args.FirstOrDefault();
        if (TryValidatePath(argumentPath, out var validatedFromArgs))
        {
            return validatedFromArgs;
        }

        return TryValidatePath(configuration["LaunchEditorWithFilePath"], out var validatedFromSettings)
            ? validatedFromSettings
            : null;
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
