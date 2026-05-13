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

    private static void OnSnipCaptured(Bitmap snipBitmap)
    {
        try
        {
            var snipPath = Path.Combine(Path.GetTempPath(), $"flicksy-snip-{Guid.NewGuid():N}.png");
            snipBitmap.Save(snipPath, ImageFormat.Png);
            using var clipboardImage = new Bitmap(snipBitmap);
            System.Windows.Forms.Clipboard.SetImage(clipboardImage);
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

    private static void OnRecordingCompleted(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true
            });
        }
        catch (InvalidOperationException ex)
        {
            System.Windows.MessageBox.Show($"Recording completed at:\n{path}\n\n{ex.Message}", "Flicksy Snipper", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            System.Windows.MessageBox.Show($"Recording completed at:\n{path}\n\n{ex.Message}", "Flicksy Snipper", MessageBoxButton.OK, MessageBoxImage.Information);
        }
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
