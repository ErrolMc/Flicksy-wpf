using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flicksy.Editor.Media;

namespace Flicksy.Editor.ViewModels;

public partial class PostSnipViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMedia))]
    [NotifyPropertyChangedFor(nameof(IsImage))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string? mediaPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImage))]
    private bool isVideo;

    [ObservableProperty]
    private ImageSource? imageSource;

    public PostSnipViewModel(IVideoPlayer player, ImageEditToolsViewModel imageEditTools, DrawingViewModel drawing)
    {
        Player = player;
        ImageEditTools = imageEditTools;
        Drawing = drawing;
        SelectionOverlay = new SelectionOverlayViewModel
        {
            SelectedItem = drawing.SelectedItem,
            IsActive = imageEditTools.IsSelectActive,
        };

        drawing.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DrawingViewModel.SelectedItem))
            {
                SelectionOverlay.SelectedItem = drawing.SelectedItem;
            }
        };

        imageEditTools.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ImageEditToolsViewModel.IsSelectActive))
            {
                SelectionOverlay.IsActive = imageEditTools.IsSelectActive;
                if (!imageEditTools.IsSelectActive)
                {
                    drawing.SelectedItem = null;
                }
            }
        };
    }

    public IVideoPlayer Player { get; }

    public ImageEditToolsViewModel ImageEditTools { get; }

    public DrawingViewModel Drawing { get; }

    public SelectionOverlayViewModel SelectionOverlay { get; }

    public bool PreserveMediaFile { get; set; }

    public bool HasMedia => !string.IsNullOrWhiteSpace(MediaPath);

    public bool IsImage => HasMedia && !IsVideo;

    public bool IsEmpty => !HasMedia;

    public event EventHandler<SaveDialogRequest>? SaveDialogRequested;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler? CloseRequested;

    public void LoadImage(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path is required.", nameof(imagePath));
        }

        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("Image file was not found.", imagePath);
        }

        Player.Close();

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        ImageSource = bitmap;
        IsVideo = false;
        MediaPath = imagePath;
        Drawing.Clear();
    }

    public async Task LoadVideoAsync(string videoPath)
    {
        if (string.IsNullOrWhiteSpace(videoPath))
        {
            throw new ArgumentException("Video path is required.", nameof(videoPath));
        }

        if (!File.Exists(videoPath))
        {
            throw new FileNotFoundException("Video file was not found.", videoPath);
        }

        ImageSource = null;
        IsVideo = true;
        MediaPath = videoPath;
        Drawing.Clear();

        await Player.OpenAsync(videoPath).ConfigureAwait(true);
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(MediaPath) || !File.Exists(MediaPath))
        {
            ErrorOccurred?.Invoke(this, "There is no media to save.");
            return;
        }

        var sourceExtension = Path.GetExtension(MediaPath);
        if (string.IsNullOrWhiteSpace(sourceExtension))
        {
            sourceExtension = IsVideo ? ".mp4" : ".png";
        }

        var request = new SaveDialogRequest(
            title: IsVideo ? "Save Recording" : "Save Snip",
            suggestedFileName: $"Flicksy_{DateTime.Now:yyyyMMdd_HHmmss}{sourceExtension}",
            defaultExtension: sourceExtension,
            filter: IsVideo
                ? "MP4 Video (*.mp4)|*.mp4|All Files (*.*)|*.*"
                : "PNG Image (*.png)|*.png|All Files (*.*)|*.*");

        SaveDialogRequested?.Invoke(this, request);

        if (string.IsNullOrWhiteSpace(request.SelectedPath))
        {
            return;
        }

        try
        {
            if (IsImage && Drawing.HasItems && ImageSource is BitmapSource bitmapSource)
            {
                SaveImageWithDrawing(bitmapSource, request.SelectedPath);
            }
            else if (!string.Equals(Path.GetFullPath(request.SelectedPath), Path.GetFullPath(MediaPath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(MediaPath, request.SelectedPath, overwrite: true);
            }

            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Failed to save file:\n{ex.Message}");
        }
    }

    private void SaveImageWithDrawing(BitmapSource source, string destinationPath)
    {
        var width = source.Width;
        var height = source.Height;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(source, new Rect(0, 0, width, height));

            foreach (var item in Drawing.Items)
            {
                item.Render(dc);
            }
        }

        var rtb = new RenderTargetBitmap(source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var stream = File.Create(destinationPath);
        encoder.Save(stream);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void New()
    {
        var snipperPath = ResolveSnipperExecutablePath();
        if (string.IsNullOrWhiteSpace(snipperPath))
        {
            ErrorOccurred?.Invoke(this, "Flicksy.Snipper.exe was not found.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = snipperPath,
                WorkingDirectory = Path.GetDirectoryName(snipperPath),
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Unable to start Flicksy.Snipper:\n{ex.Message}");
            return;
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private static string? ResolveSnipperExecutablePath()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "Flicksy.Snipper.exe"),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "Flicksy.Snipper", "bin", "Debug", "net10.0-windows", "Flicksy.Snipper.exe")),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "Flicksy.Snipper", "bin", "Release", "net10.0-windows", "Flicksy.Snipper.exe")),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    public void DeleteMediaFile()
    {
        if (PreserveMediaFile || string.IsNullOrWhiteSpace(MediaPath))
        {
            return;
        }

        try
        {
            if (File.Exists(MediaPath))
            {
                File.Delete(MediaPath);
            }
        }
        catch
        {
            // Best effort — leave the file if it can't be removed.
        }
    }
}

public sealed class SaveDialogRequest
{
    public SaveDialogRequest(string title, string suggestedFileName, string defaultExtension, string filter)
    {
        Title = title;
        SuggestedFileName = suggestedFileName;
        DefaultExtension = defaultExtension;
        Filter = filter;
    }

    public string Title { get; }
    public string SuggestedFileName { get; }
    public string DefaultExtension { get; }
    public string Filter { get; }
    public string? SelectedPath { get; set; }
}
