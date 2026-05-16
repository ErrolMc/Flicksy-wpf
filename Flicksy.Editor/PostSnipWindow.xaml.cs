using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Flicksy.Editor.ViewModels;
using Microsoft.Win32;

namespace Flicksy.Editor;

public partial class PostSnipWindow : Window
{
    // Windows 10 1809 used attribute 19; 1903+ uses 20. Try the newer one first
    // and fall back to the older one — DwmSetWindowAttribute returns non-zero on
    // unsupported attributes, which is harmless to ignore.
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        int useDark = 1;
        if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref useDark, sizeof(int));
        }
    }

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
