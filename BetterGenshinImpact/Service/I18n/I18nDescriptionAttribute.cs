using System;
using System.ComponentModel;

namespace BetterGenshinImpact.Service.I18n;

/// <summary>
/// 与 <see cref="DescriptionAttribute"/> 行为一致（Description 仍是原中文值），
/// 额外让 I18nSync 把它收录为界面翻译 Key，界面通过 i18n:TBind 翻译显示。
/// </summary>
[AttributeUsage(AttributeTargets.All)]
public sealed class I18nDescriptionAttribute : DescriptionAttribute
{
    public I18nDescriptionAttribute(string description) : base(description)
    {
    }
}
