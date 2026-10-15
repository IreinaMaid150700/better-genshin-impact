using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BetterGenshinImpact.Core.Recognition.OCR;

/// <summary>
/// 多语言 OCR 文本匹配
///
/// 匹配模式通常来自按游戏语言本地化的正则（resx），匹配前对模式与 OCR 文本做相同的归一化，
/// 以容忍 OCR 丢失或识别错声调、重音等附加符号，翻译时可直接书写带附加符号的原文
/// </summary>
public sealed class OcrTextMatcher
{
    /// <summary>
    /// 无法通过 Unicode 分解去除附加符号的字母，需要手动折叠为基础字母
    /// </summary>
    private static readonly FrozenDictionary<char, char> FoldedLetters = new Dictionary<char, char>
    {
        ['đ'] = 'd',
        ['Đ'] = 'D',
    }.ToFrozenDictionary();

    private readonly Regex _regex;

    /// <param name="pattern">正则模式，可包含附加符号</param>
    public OcrTextMatcher(string pattern)
    {
        _regex = new Regex(Normalize(pattern), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public bool IsMatch(string? ocrText)
    {
        return !string.IsNullOrEmpty(ocrText) && _regex.IsMatch(Normalize(ocrText));
    }

    /// <summary>
    /// 去除附加符号：分解后丢弃组合符号，折叠特殊字母，再重新组合（避免韩文等音节文字保持分解形态）
    ///
    /// 日文假名的浊点、半浊点同属组合符号，也会被去除；模式与文本经过相同处理，不影响匹配
    /// </summary>
    public static string Normalize(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(FoldedLetters.GetValueOrDefault(c, c));
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
