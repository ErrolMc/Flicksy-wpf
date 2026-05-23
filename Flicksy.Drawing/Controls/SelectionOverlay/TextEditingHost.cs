using System.Windows;

namespace Flicksy.Drawing.Controls;

public static class TextEditingHost
{
    public static readonly DependencyProperty KeepTextEditorAliveProperty =
        DependencyProperty.RegisterAttached(
            "KeepTextEditorAlive",
            typeof(bool),
            typeof(TextEditingHost),
            new PropertyMetadata(false));

    public static bool GetKeepTextEditorAlive(DependencyObject obj) =>
        (bool)obj.GetValue(KeepTextEditorAliveProperty);

    public static void SetKeepTextEditorAlive(DependencyObject obj, bool value) =>
        obj.SetValue(KeepTextEditorAliveProperty, value);
}
