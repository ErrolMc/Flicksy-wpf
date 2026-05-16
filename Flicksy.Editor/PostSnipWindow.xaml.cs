using System;
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

        viewModel.SaveDialogRequested += OnSaveDialogRequested;
        viewModel.CloseRequested += OnCloseRequested;
        viewModel.ErrorOccurred += OnErrorOccurred;
    }

    public PostSnipViewModel ViewModel { get; }

    protected override void OnClosed(EventArgs e)
    {
        ViewModel.SaveDialogRequested -= OnSaveDialogRequested;
        ViewModel.CloseRequested -= OnCloseRequested;
        ViewModel.ErrorOccurred -= OnErrorOccurred;

        ViewModel.Player.Close();
        ViewModel.Player.Dispose();
        ViewModel.DeleteMediaFile();

        base.OnClosed(e);
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
}
