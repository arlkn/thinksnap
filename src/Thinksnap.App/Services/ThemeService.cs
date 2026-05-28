using System.Windows;
using System.Windows.Media;
using Thinksnap.App.Models;

namespace Thinksnap.App.Services;

public static class ThemeService
{
    public static void Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var palette = settings.ThemePreset switch
        {
            "Graphite" => new ThemePalette("#151515", "#202020", "#2E2E2E", "#F3F3F3", "#B9B9B9"),
            "Aurora" => new ThemePalette("#101820", "#17232D", "#284152", "#F6FBFF", "#B8CEDA"),
            _ => new ThemePalette("#111418", "#181D24", "#303844", "#F4F7FB", "#9DA7B3")
        };

        var resources = System.Windows.Application.Current.Resources;
        resources["AppBackgroundBrush"] = BrushFrom(palette.Background);
        resources["PanelBackgroundBrush"] = BrushFrom(palette.Panel);
        resources["BorderBrushColor"] = BrushFrom(palette.Border);
        resources["PrimaryTextBrush"] = BrushFrom(palette.PrimaryText);
        resources["SecondaryTextBrush"] = BrushFrom(palette.SecondaryText);
        resources["AccentBrush"] = BrushFrom(settings.AccentColor);
        resources["AccentColorValue"] = ColorFrom(settings.AccentColor);
    }

    public static SolidColorBrush BrushFrom(string color)
    {
        var brush = new SolidColorBrush(ColorFrom(color));
        brush.Freeze();
        return brush;
    }

    public static System.Windows.Media.Color ColorFrom(string color)
    {
        return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color)!;
    }

    private sealed record ThemePalette(
        string Background,
        string Panel,
        string Border,
        string PrimaryText,
        string SecondaryText);
}
