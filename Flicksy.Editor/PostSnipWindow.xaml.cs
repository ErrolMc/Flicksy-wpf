using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Flicksy.Editor;

public partial class PostSnipWindow : Window
{
    public string? MediaPath { get; private set; }

    public bool IsVideo { get; private set; }

    public PostSnipWindow()
    {
        InitializeComponent();
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
        PreviewVideo.Play();

        EmptyStateText.Visibility = Visibility.Collapsed;

        MediaPath = videoPath;
        IsVideo = true;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        // Intentionally left blank for now.
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
            PreviewVideo.Stop();
        }
        catch
        {
            // no-op
        }
    }
}
