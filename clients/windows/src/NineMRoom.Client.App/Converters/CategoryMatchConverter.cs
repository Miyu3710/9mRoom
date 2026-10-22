using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NineMRoom.Client.App;

public sealed class CategoryMatchConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var selected = values.Length > 0 ? values[0] as string ?? string.Empty : string.Empty;
        var option = values.Length > 1 ? values[1] as string ?? string.Empty : string.Empty;
        return string.Equals(selected.Trim(), option.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
