using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Flicksy.Editor;

public partial class PostSnipWindow : Window
{
    public string? MediaPath { get; private set; }

    public bool IsVideo { get; private set; }

    public PostSnipWindow()
    {
        InitializeComponent();
        PlaybackOverlay.AttachMediaElement(PreviewVideo);
    }

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

        StopVideo();

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        PreviewImage.Source = bitmap;
        PreviewImage.Visibility = Visibility.Visible;

        PreviewVideo.Source = null;
        PreviewVideo.Visibility = Visibility.Collapsed;

        PlaybackOverlay.Visibility = Visibility.Collapsed;
        PlaybackOverlay.ResetState();

        EmptyStateText.Visibility = Visibility.Collapsed;

        MediaPath = imagePath;
        IsVideo = false;
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

        PreviewImage.Source = null;
        PreviewImage.Visibility = Visibility.Collapsed;

        PreviewVideo.Source = new Uri(videoPath, UriKind.Absolute);
        PreviewVideo.Visibility = Visibility.Visible;
        PreviewVideo.Position = TimeSpan.Zero;

        PlaybackOverlay.Visibility = Visibility.Visible;
        PlaybackOverlay.Pause();

        EmptyStateText.Visibility = Visibility.Collapsed;

        MediaPath = videoPath;
        IsVideo = true;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(MediaPath) || !File.Exists(MediaPath))
        {
            MessageBox.Show(this, "There is no media to save.", "Save", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sourceExtension = Path.GetExtension(MediaPath);
        if (string.IsNullOrWhiteSpace(sourceExtension))
        {
            sourceExtension = IsVideo ? ".mp4" : ".png";
        }

        var dialog = new SaveFileDialog
        {
            Title = IsVideo ? "Save Recording" : "Save Snip",
            FileName = $"Flicksy_{DateTime.Now:yyyyMMdd_HHmmss}{sourceExtension}",
            DefaultExt = sourceExtension,
            AddExtension = true,
            OverwritePrompt = true,
            Filter = IsVideo
                ? "MP4 Video (*.mp4)|*.mp4|All Files (*.*)|*.*"
                : "PNG Image (*.png)|*.png|All Files (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            if (!string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(MediaPath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(MediaPath, dialog.FileName, overwrite: true);
            }

            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to save file:\n{ex.Message}", "Save", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        StopVideo();
        base.OnClosed(e);
    }

    private void StopVideo()
    {
        try
        {
            PlaybackOverlay.Stop();
        }
        catch
        {
            // no-op
        }
    }
}
