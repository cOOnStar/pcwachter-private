using System.Globalization;
using System.Windows.Data;

namespace PCWachter.Desktop.Converters;

public sealed class LessThanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double numericValue)
        {
            return false;
        }

        if (parameter is null)
        {
            return false;
        }

        if (!double.TryParse(parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double threshold))
        {
            if (!double.TryParse(parameter.ToString(), NumberStyles.Float, culture, out threshold))
            {
                return false;
            }
        }

        return numericValue < threshold;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
