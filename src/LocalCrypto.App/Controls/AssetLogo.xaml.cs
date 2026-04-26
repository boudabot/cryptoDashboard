using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace LocalCrypto.App.Controls;

public partial class AssetLogo : UserControl
{
    public static readonly DependencyProperty SymbolProperty =
        DependencyProperty.Register(nameof(Symbol), typeof(string), typeof(AssetLogo), new PropertyMetadata(string.Empty, OnVisualPropertyChanged));

    public static readonly DependencyProperty InitialsProperty =
        DependencyProperty.Register(nameof(Initials), typeof(string), typeof(AssetLogo), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty AccentProperty =
        DependencyProperty.Register(nameof(Accent), typeof(string), typeof(AssetLogo), new PropertyMetadata("#38BDF8"));

    public static readonly DependencyProperty DiameterProperty =
        DependencyProperty.Register(nameof(Diameter), typeof(double), typeof(AssetLogo), new PropertyMetadata(40d));

    public static readonly DependencyProperty InitialFontSizeProperty =
        DependencyProperty.Register(nameof(InitialFontSize), typeof(double), typeof(AssetLogo), new PropertyMetadata(12d));

    public AssetLogo()
    {
        InitializeComponent();
        RefreshLogo();
    }

    public string Symbol
    {
        get => (string)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public string Initials
    {
        get => (string)GetValue(InitialsProperty);
        set => SetValue(InitialsProperty, value);
    }

    public string Accent
    {
        get => (string)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    public double Diameter
    {
        get => (double)GetValue(DiameterProperty);
        set => SetValue(DiameterProperty, value);
    }

    public double InitialFontSize
    {
        get => (double)GetValue(InitialFontSizeProperty);
        set => SetValue(InitialFontSizeProperty, value);
    }

    private static void OnVisualPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ((AssetLogo)dependencyObject).RefreshLogo();
    }

    private void RefreshLogo()
    {
        var key = Symbol?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(key) || key == "-")
        {
            ShowFallback();
            return;
        }

        try
        {
            var uri = new Uri($"pack://application:,,,/Assets/CryptoIcons/128/color/{key}.png", UriKind.Absolute);
            if (Application.GetResourceStream(uri) is null)
            {
                ShowFallback();
                return;
            }

            LogoImage.Source = new BitmapImage(uri);
            ImageHost.Visibility = Visibility.Visible;
            FallbackBadge.Visibility = Visibility.Collapsed;
        }
        catch
        {
            ShowFallback();
        }
    }

    private void ShowFallback()
    {
        LogoImage.Source = null;
        ImageHost.Visibility = Visibility.Collapsed;
        FallbackBadge.Visibility = Visibility.Visible;
    }
}
