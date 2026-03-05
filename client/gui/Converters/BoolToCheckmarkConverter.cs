using System.Globalization;
using System.Windows.Data;

namespace PCWachter.Desktop.Converters;

/// <summary>Converts bool to ✓ or ✗ text.</summary>
public sealed class BoolToCheckmarkConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "✓" : "✗";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
