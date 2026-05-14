using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using Flicksy.Snipper.Overlays;

namespace Flicksy.Snipper;

internal sealed class SnipperSessionController : IDisposable
{
    private readonly System.Windows.Application _application;
    private readonly ScreenRecorder _screenRecordingService;
    private PreSnipOverlayWindow? _overlayWindow;
    private VideoRecordingOverlayWindow? _videoOverlayWindow;
    private bool _isShuttingDown;

    public SnipperSessionController(System.Windows.Application application)
    {
        _application = application;
        _screenRecordingService = new ScreenRecorder(OnRecordingCompleted);
    }

    public void Start()
    {
        ShowPreSnipOverlay();
    }

    public void Dispose()
    {
        _screenRecordingService.Dispose();
        _overlayWindow?.Close();
        _videoOverlayWindow?.Close();
    }

    private void ShowPreSnipOverlay()
    {
        var cursor = Cursor.Position;
        var bounds = Screen.FromPoint(cursor).Bounds;

        var overlay = new PreSnipOverlayWindow(bounds, OnSnipCaptured, ShowVideoRecordingOverlay);
        _overlayWindow = overlay;
        overlay.Closed += (_, _) =>
        {
            if (ReferenceEquals(_overlayWindow, overlay))
            {
                _overlayWindow = null;
            }

            TryShutdownWhenIdle();
        };
        overlay.Show();
        overlay.Activate();
    }

    private void ShowVideoRecordingOverlay(Rectangle bounds, Rectangle selectionRect)
    {
        var overlay = new VideoRecordingOverlayWindow(
            bounds,
            selectionRect,
            _screenRecordingService.StartRecording,
            _screenRecordingService.StopRecording,
            ShowPreSnipOverlay);
        _videoOverlayWindow = overlay;
        overlay.Closed += (_, _) =>
        {
            if (ReferenceEquals(_videoOverlayWindow, overlay))
            {
                _videoOverlayWindow = null;
            }

            TryShutdownWhenIdle();
        };
        overlay.Show();
        overlay.Activate();
    }

    private void OnSnipCaptured(Bitmap snipBitmap)
    {
        string? snipPath = null;
        try
        {
            snipPath = Path.Combine(Path.GetTempPath(), $"flicksy-snip-{Guid.NewGuid():N}.png");
            snipBitmap.Save(snipPath, ImageFormat.Png);
            using var clipboardImage = new Bitmap(snipBitmap);
            System.Windows.Forms.Clipboard.SetImage(clipboardImage);

            LaunchEditorWithMedia(snipPath);
        }
        catch (ExternalException ex)
        {
            System.Windows.MessageBox.Show($"Unable to copy snip to clipboard.\n{ex.Message}", "Flicksy Snipper", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Windows.MessageBox.Show($"Unable to save snip file.\n{ex.Message}", "Flicksy Snipper", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (IOException ex)
        {
            System.Windows.MessageBox.Show($"Unable to save snip file.\n{ex.Message}", "Flicksy Snipper", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            snipBitmap.Dispose();
        }
    }

    private void OnRecordingCompleted(string path)
    {
        LaunchEditorWithMedia(path);
    }

    private void LaunchEditorWithMedia(string mediaPath)
    {
        if (TryLaunchEditorWithMedia(mediaPath, out var errorMessage))
        {
            return;
        }

        System.Windows.MessageBox.Show(
            $"{errorMessage}\n\nSaved media:\n{mediaPath}",
            "Flicksy Snipper",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    internal bool TryLaunchEditorWithMedia(string mediaPath, out string? errorMessage)
    {
        var editorPath = ResolveEditorExecutablePath();
        if (string.IsNullOrWhiteSpace(editorPath))
        {
            errorMessage = "Flicksy.Editor.exe was not found. Build Flicksy.Editor first.";
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = editorPath,
                Arguments = $"\"{mediaPath}\"",
                WorkingDirectory = Path.GetDirectoryName(editorPath),
                UseShellExecute = true
            });

            errorMessage = null;
            return true;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            errorMessage = $"Unable to start Flicksy.Editor:\n{ex.Message}";
            return false;
        }
        catch (InvalidOperationException ex)
        {
            errorMessage = $"Unable to start Flicksy.Editor:\n{ex.Message}";
            return false;
        }
    }

    private static string? ResolveEditorExecutablePath()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "Flicksy.Editor.exe"),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "Flicksy.Editor", "bin", "Debug", "net10.0-windows", "Flicksy.Editor.exe")),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "Flicksy.Editor", "bin", "Release", "net10.0-windows", "Flicksy.Editor.exe"))
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private void TryShutdownWhenIdle()
    {
        if (_isShuttingDown || _overlayWindow is not null || _videoOverlayWindow is not null)
        {
            return;
        }

        _isShuttingDown = true;
        _application.Shutdown();
    }
}
