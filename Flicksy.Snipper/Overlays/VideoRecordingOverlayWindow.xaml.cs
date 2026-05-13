using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DrawingRectangle = System.Drawing.Rectangle;

namespace Flicksy.Snipper.Overlays;

public partial class VideoRecordingOverlayWindow : Window
{
    private readonly DrawingRectangle _screenBounds;
    private readonly DrawingRectangle _selectionRect;
    private readonly Action<DrawingRectangle, DrawingRectangle> _onStart;
    private readonly Action _onStop;
    private readonly Action _onCancel;
    private readonly DispatcherTimer _recordingTimer;
    private DateTime _recordingStartedUtc;
    private bool _isRecording;

    public VideoRecordingOverlayWindow(
        DrawingRectangle bounds,
        DrawingRectangle selectionRect,
        Action<DrawingRectangle, DrawingRectangle> onStart,
        Action onStop,
        Action onCancel)
    {
        _screenBounds = bounds;
        _selectionRect = selectionRect;
        _onStart = onStart;
        _onStop = onStop;
        _onCancel = onCancel;
        _recordingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _recordingTimer.Tick += (_, _) => UpdateTimerText();

        InitializeComponent();

        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;

        SetButtonStyle(StartStopButton);
        SetButtonStyle(CancelButton);

        Loaded += (_, _) =>
        {
            Focus();
            LayoutSelectionBorder();
        };
        SizeChanged += (_, _) => LayoutSelectionBorder();
    }

    protected override void OnClosed(EventArgs e)
    {
        _recordingTimer.Stop();
        base.OnClosed(e);
    }

    private void OnStartStopClick(object sender, RoutedEventArgs e)
    {
        if (_isRecording)
        {
            _recordingTimer.Stop();
            _isRecording = false;
            _onStop();
            Close();
            return;
        }

        _onStart(_screenBounds, _selectionRect);
        _recordingStartedUtc = DateTime.UtcNow;
        _isRecording = true;
        StartStopButton.Content = "Stop";
        UpdateTimerText();
        _recordingTimer.Start();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        if (_isRecording)
        {
            _recordingTimer.Stop();
            _isRecording = false;
            _onStop();
        }

        Close();
        _onCancel();
    }

    private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (_isRecording)
        {
            _recordingTimer.Stop();
            _isRecording = false;
            _onStop();
        }

        Close();
        _onCancel();
    }

    private void LayoutSelectionBorder()
    {
        Canvas.SetLeft(SelectionBorder, _selectionRect.X);
        Canvas.SetTop(SelectionBorder, _selectionRect.Y);
        SelectionBorder.Width = _selectionRect.Width;
        SelectionBorder.Height = _selectionRect.Height;
    }

    private static void SetButtonStyle(System.Windows.Controls.Button button)
    {
        button.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(70, 70, 70));
        button.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(120, 120, 120));
        button.BorderThickness = new Thickness(1);
    }

    private void UpdateTimerText()
    {
        var elapsed = DateTime.UtcNow - _recordingStartedUtc;
        TimerText.Text = $"{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }
}
