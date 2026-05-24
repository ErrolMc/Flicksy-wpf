using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace Flicksy.VideoEditor.Converters;

/// <summary>
/// Maps a full file path to <see cref="Path.GetFileName(string)"/>. Used by clip labels
/// in the timeline so the lane shows <c>clipA.mp4</c> rather than the full source path.
/// Non-string inputs and parse failures pass through as empty string.
/// </summary>
public sealed class FileNameConverter : IValueConverter
{
    public static readonly FileNameConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path)) return string.Empty;
        try { return Path.GetFileName(path); }
        catch { return path; }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
