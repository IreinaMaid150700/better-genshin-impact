using System;
using System.Globalization;
using System.Windows.Data;

namespace BetterGenshinImpact.Service.I18n;

/// <summary>
/// 将作为配置值使用的中文 Key 翻译为当前界面语言，只影响显示，不改变绑定的值。
/// values[0]：中文 Key；values[1]：I18nService.Revision，语言切换时触发重新取值。
/// </summary>
public sealed class I18nKeyConverter : IMultiValueConverter
{
    public static I18nKeyConverter Instance { get; } = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        return values.Length > 0 && values[0] is string key
            ? I18nService.Instance.Translate(key)
            : string.Empty;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
