using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FpdSimViewer.Converters;

public sealed class FsmStateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var state = value is uint uintValue ? uintValue : 0U;
        return state switch
        {
            0U => new SolidColorBrush(Color.FromRgb(0xD9, 0xE2, 0xEC)),
            7U or 8U => new SolidColorBrush(Color.FromRgb(0x0B, 0x6E, 0x4F)),
            10U => new SolidColorBrush(Color.FromRgb(0x8F, 0xBC, 0x8B)),
            15U => new SolidColorBrush(Color.FromRgb(0xC1, 0x12, 0x1F)),
            _ => new SolidColorBrush(Color.FromRgb(0x3C, 0x91, 0xE6)),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
