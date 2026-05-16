using System;
using System.ComponentModel;
using System.Windows;
using Flicksy.Editor.ViewModels;
using Microsoft.Win32;

namespace Flicksy.Editor;

public partial class PostSnipWindow : Window
{
    public PostSnipWindow(PostSnipViewModel viewModel)
    {
        InitializeComponent();

        ViewModel = viewModel;
        DataContext = viewModel;

        PlaybackOverlay.AttachMediaElement(PreviewVideo);

        viewModel.SaveDialogRequested += OnSaveDialogRequested;
        viewModel.CloseRequested += OnCloseRequested;
        viewModel.ErrorOccurred += OnErrorOccurred;
        viewModel.VideoLoaded += OnVideoLoaded;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public PostSnipViewModel ViewModel { get; }

    protected override void OnClosed(EventArgs e)
    {
        StopVideo();
        PreviewVideo.Source = null;

        ViewModel.SaveDialogRequested -= OnSaveDialogRequested;
        ViewModel.CloseRequested -= OnCloseRequested;
        ViewModel.ErrorOccurred -= OnErrorOccurred;
        ViewModel.VideoLoaded -= OnVideoLoaded;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        ViewModel.DeleteMediaFile();
        base.OnClosed(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PostSnipViewModel.IsVideo) && !ViewModel.IsVideo)
        {
            StopVideo();
            PlaybackOverlay.ResetState();
        }
    }

    private void OnVideoLoaded(object? sender, EventArgs e)
    {
        PreviewVideo.Position = TimeSpan.Zero;
        PlaybackOverlay.Pause();
    }

    private void OnSaveDialogRequested(object? sender, SaveDialogRequest request)
    {
        var dialog = new SaveFileDialog
        {
            Title = request.Title,
            FileName = request.SuggestedFileName,
            DefaultExt = request.DefaultExtension,
            AddExtension = true,
            OverwritePrompt = true,
            Filter = request.Filter,
        };

        if (dialog.ShowDialog(this) == true)
        {
            request.SelectedPath = dialog.FileName;
        }
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnErrorOccurred(object? sender, string message)
    {
        MessageBox.Show(this, message, "Editor", MessageBoxButton.OK, MessageBoxImage.Information);
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
