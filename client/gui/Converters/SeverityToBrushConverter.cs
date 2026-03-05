using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PCWachter.Contracts;

namespace PCWachter.Desktop.Converters;

public sealed class SeverityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        FindingSeverity severity = value switch
        {
            FindingSeverity s => s,
            int i => (FindingSeverity)i,
            string text when Enum.TryParse(text, true, out FindingSeverity parsed) => parsed,
            _ => FindingSeverity.Info
        };

        return severity switch
        {
            FindingSeverity.Critical => ResolveThemeBrush("StatusCritBrush", Color.FromRgb(239, 68, 68)),
            FindingSeverity.Warning => ResolveThemeBrush("StatusWarnBrush", Color.FromRgb(245, 158, 11)),
            FindingSeverity.Info => ResolveThemeBrush("StatusInfoBrush", Color.FromRgb(88, 168, 255)),
            _ => ResolveThemeBrush("StatusInfoBrush", Color.FromRgb(88, 168, 255))
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }

    private static Brush ResolveThemeBrush(string key, Color fallback)
    {
        if (Application.Current?.TryFindResource(key) is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallback);
    }
}
