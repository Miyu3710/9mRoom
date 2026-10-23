using Window = System.Windows.Window;

using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace NineMRoom.Client.App.Controls;

/// <summary>
/// 待办时间字段的日期 + 时间选择器。
/// 日期使用 M3 日期网格（月份切换 + 6×7 日格 + 选中/今天状态），
/// 时间使用桌面端更高效的键盘输入变体（24 小时制）。
/// 返回值：null = 取消，空字符串 = 清除，其它为 MM-dd HH:mm。
/// </summary>
public partial class M3DateTimeDialog : Window
{
    private static readonly Regex TimePattern = new(
        @"^(?:(?<y>\d{4})-)?(?:(?<m>\d{1,2})-(?<d>\d{1,2})\s+)?(?<h>\d{1,2}):(?<min>\d{2})$",
        RegexOptions.Compiled);

    private DateTime _visibleMonth;
    private DateTime _selectedDate;

    public string? Result { get; private set; }

    public M3DateTimeDialog(string? current)
    {
        InitializeComponent();
        LoadInitial(current);
    }

    public static string? Pick(Window? owner, string? current)
    {
        var dialog = new M3DateTimeDialog(current);
        if (owner is not null)
        {
            dialog.Owner = owner;
        }

        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    private void LoadInitial(string? current)
    {
        var now = DateTime.Now;
        _selectedDate = now.Date;
        var hour = now.Hour;
        var minute = now.Minute;

        if (!string.IsNullOrWhiteSpace(current) && TimePattern.Match(current.Trim()) is { Success: true } match)
        {
            var month = match.Groups["m"].Success ? int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture) : now.Month;
            var day = match.Groups["d"].Success ? int.Parse(match.Groups["d"].Value, CultureInfo.InvariantCulture) : now.Day;
            var year = match.Groups["y"].Success
                ? int.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture)
                : now.Year;

            if (month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month))
            {
                _selectedDate = new DateTime(year, month, day);
            }

            hour = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);
            minute = int.Parse(match.Groups["min"].Value, CultureInfo.InvariantCulture);
        }

        _visibleMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
        HourBox.Text = hour.ToString("00", CultureInfo.InvariantCulture);
        MinuteBox.Text = minute.ToString("00", CultureInfo.InvariantCulture);
        RebuildCalendar();
        UpdatePreview();
    }

    private void RebuildCalendar()
    {
        var first = _visibleMonth;
        var offset = ((int)first.DayOfWeek + 6) % 7; // 周一为一周第一天
        var start = first.AddDays(-offset);
        var today = DateTime.Today;
        var options = new List<DateOption>(42);

        for (var i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            options.Add(new DateOption(
                date.Day,
                date.Month == first.Month,
                date.Date == _selectedDate.Date,
                date.Date == today,
                date));
        }

        MonthTitle.Text = $"{first.Year} 年 {first.Month} 月";
        DayGrid.ItemsSource = options;
    }

    private void PreviousMonth_Click(object sender, RoutedEventArgs e)
    {
        _visibleMonth = _visibleMonth.AddMonths(-1);
        RebuildCalendar();
    }

    private void NextMonth_Click(object sender, RoutedEventArgs e)
    {
        _visibleMonth = _visibleMonth.AddMonths(1);
        RebuildCalendar();
    }

    private void Day_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: DateOption option })
        {
            return;
        }

        _selectedDate = option.Date;
        if (_selectedDate.Month != _visibleMonth.Month)
        {
            _visibleMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
        }

        RebuildCalendar();
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        PreviewText.Text = TryBuild(out var text, out _) ? text : "日期或时间无效";
    }

    private bool TryBuild(out string text, out string error)
    {
        text = string.Empty;
        error = string.Empty;

        if (!int.TryParse(HourBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour) ||
            hour is < 0 or > 23)
        {
            error = "小时必须是 0–23 的整数。";
            return false;
        }

        if (!int.TryParse(MinuteBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minute) ||
            minute is < 0 or > 59)
        {
            error = "分钟必须是 0–59 的整数。";
            return false;
        }

        text = $"{_selectedDate:MM-dd} {hour:00}:{minute:00}";
        return true;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuild(out var text, out var error))
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        Result = text;
        DialogResult = true;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        Result = string.Empty;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}

/// <summary>日期网格中的一格。</summary>
public sealed class DateOption
{
    public DateOption(int day, bool isCurrentMonth, bool isSelected, bool isToday, DateTime date)
    {
        Day = day;
        IsCurrentMonth = isCurrentMonth;
        IsSelected = isSelected;
        IsToday = isToday;
        Date = date;
    }

    public int Day { get; }

    public bool IsCurrentMonth { get; }

    public bool IsSelected { get; }

    public bool IsToday { get; }

    public DateTime Date { get; }
}
