using Avalonia.Controls;
using Avalonia.Interactivity;
using ViewMd.Models;

namespace ViewMd;

public partial class PreferencesWindow : Window
{
    public bool Accepted { get; private set; }
    public string ThemeMode { get; private set; } = "Default";
    public string? SelectedFontFamily { get; private set; }
    public double BaseFontSize { get; private set; }
    public double LineHeightMultiplier { get; private set; }
    public double DocumentMargin { get; private set; }

    public PreferencesWindow() : this(new AppSettings())
    {
    }

    public PreferencesWindow(AppSettings current)
    {
        InitializeComponent();

        ThemeCombo.SelectedIndex = current.Theme switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0,
        };
        FontFamilyCombo.Text = string.IsNullOrWhiteSpace(current.FontFamily) ? "Default" : current.FontFamily;
        FontSizeUpDown.Value = (decimal)current.BaseFontSize;
        LineHeightUpDown.Value = (decimal)current.LineHeightMultiplier;
        MarginUpDown.Value = (decimal)current.DocumentMargin;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        ThemeMode = ThemeCombo.SelectedIndex switch
        {
            1 => "Light",
            2 => "Dark",
            _ => "Default",
        };

        var fontText = FontFamilyCombo.Text?.Trim();
        SelectedFontFamily = string.IsNullOrWhiteSpace(fontText) || string.Equals(fontText, "Default", StringComparison.OrdinalIgnoreCase)
            ? null
            : fontText;

        BaseFontSize = (double)(FontSizeUpDown.Value ?? 14m);
        LineHeightMultiplier = (double)(LineHeightUpDown.Value ?? 1.1m);
        DocumentMargin = (double)(MarginUpDown.Value ?? 20m);

        Accepted = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Accepted = false;
        Close();
    }
}
