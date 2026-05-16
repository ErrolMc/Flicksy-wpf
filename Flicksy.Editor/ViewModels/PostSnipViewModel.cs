using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    [ObservableProperty]
    private Uri? videoSource;

    public bool HasMedia => !string.IsNullOrWhiteSpace(MediaPath);

    public bool IsImage => HasMedia && !IsVideo;

    public bool IsEmpty => !HasMedia;

    public event EventHandler<SaveDialogRequest>? SaveDialogRequested;
    public event EventHandler<string>? ErrorOccurred;
    public event EventHandler? CloseRequested;
    public event EventHandler? VideoLoaded;

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

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        ImageSource = bitmap;
        VideoSource = null;
        IsVideo = false;
        MediaPath = imagePath;
    }

    public void LoadVideo(string videoPath)
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
        VideoSource = new Uri(videoPath, UriKind.Absolute);
        IsVideo = true;
        MediaPath = videoPath;

        VideoLoaded?.Invoke(this, EventArgs.Empty);
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
            if (!string.Equals(Path.GetFullPath(request.SelectedPath), Path.GetFullPath(MediaPath), StringComparison.OrdinalIgnoreCase))
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

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteMediaFile()
    {
        if (string.IsNullOrWhiteSpace(MediaPath))
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
