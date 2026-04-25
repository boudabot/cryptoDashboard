using System.Windows;
using System.Windows.Controls;

namespace LocalCrypto.App.Controls;

public partial class StatusBadge : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(StatusBadge), new PropertyMetadata("-"));

    public static readonly DependencyProperty ToneBrushProperty =
        DependencyProperty.Register(nameof(ToneBrush), typeof(string), typeof(StatusBadge), new PropertyMetadata("#FBBF24"));

    public StatusBadge()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string ToneBrush
    {
        get => (string)GetValue(ToneBrushProperty);
        set => SetValue(ToneBrushProperty, value);
    }
}
