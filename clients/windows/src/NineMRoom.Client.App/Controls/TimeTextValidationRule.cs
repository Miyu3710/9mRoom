using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace NineMRoom.Client.App.Controls;

/// <summary>
/// 待办起止时间校验：允许留空；否则必须是 14:00 / 10-22 14:00 / 2026-10-22 14:00。
/// 错误必须给出文字说明，而不是只把边框变红。
/// </summary>
public sealed class TimeTextValidationRule : ValidationRule
{
    private static readonly Regex Pattern = new(
        @"^(?:(?:\d{4}-)?\d{1,2}-\d{1,2}\s+)?\d{1,2}:\d{2}$",
        RegexOptions.Compiled);

    public override ValidationResult Validate(object? value, CultureInfo cultureInfo)
    {
        var text = value?.ToString()?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return ValidationResult.ValidResult;
        }

        if (Pattern.IsMatch(text))
        {
            return ValidationResult.ValidResult;
        }

        return new ValidationResult(false, "时间格式应为 14:00、10-22 14:00 或 2026-10-22 14:00。");
    }
}
