using System.Globalization;
using System.Windows.Data;

namespace NineMRoom.Client.App;

/// <summary>布尔取反，用于「运行中禁用」这类反向启用状态。</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool flag ? !flag : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool flag ? !flag : false;
}
