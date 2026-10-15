using System;
using System.Windows.Data;
using System.Windows.Markup;

namespace BetterGenshinImpact.Service.I18n;

/// <summary>
/// 绑定到一个值为中文 Key 的属性，并按当前界面语言翻译后显示，例如：Text="{i18n:TBinding Value}"。
/// 绑定的原始值（配置值、枚举键）保持不变，只改变显示文字；语言切换时自动刷新。
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TBindingExtension : MarkupExtension
{
    public TBindingExtension(string path)
    {
        Path = path;
    }

    [ConstructorArgument("path")]
    public string Path { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new MultiBinding
        {
            Converter = I18nKeyConverter.Instance,
            Mode = BindingMode.OneWay,
        };
        binding.Bindings.Add(new Binding(Path));
        binding.Bindings.Add(new Binding(nameof(I18nService.Revision))
        {
            Source = I18nService.Instance,
            Mode = BindingMode.OneWay,
        });

        return binding.ProvideValue(serviceProvider);
    }
}
