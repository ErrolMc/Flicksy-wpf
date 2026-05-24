using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Flicksy.VideoEditor.Converters;

/// <summary>
/// Returns <see cref="Visibility.Visible"/> when the bound value equals the
/// <c>ConverterParameter</c>, otherwise <see cref="Visibility.Collapsed"/>. Used to swap
/// stub panel content based on <c>CurrentLeftTab</c> / <c>CurrentRightTab</c>.
/// </summary>
public sealed class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Equals(value, parameter) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
