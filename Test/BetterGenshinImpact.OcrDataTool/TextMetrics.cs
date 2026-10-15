using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BetterGenshinImpact.OcrDataTool;

internal static partial class TextMetrics
{
    /// <summary>
    /// 统一为 NFC 并合并空白。越南语输入法可能输出分解形式（组合声调符号），
    /// 而模型字典只含预组合字符，不统一会把正确结果误判为错误
    /// </summary>
    public static string Canonical(string text)
    {
        return Whitespace().Replace(text.Normalize(NormalizationForm.FormC), " ").Trim();
    }

    public static string RemoveSpaces(string text)
    {
        return text.Replace(" ", "");
    }

    /// <summary>
    /// 去除附加符号（声调、重音等），đ/Đ 无法分解，手动折叠为 d/D。
    /// 用于区分"字母认错"与"只错了附加符号"
    /// </summary>
    public static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(c switch
            {
                'đ' => 'd',
                'Đ' => 'D',
                _ => c
            });
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// 编辑距离（按 UTF-16 码元；越南语预组合字母均在 BMP 内，每个字母计为 1）
    /// </summary>
    public static int Levenshtein(string source, string target)
    {
        if (source.Length == 0) return target.Length;
        if (target.Length == 0) return source.Length;

        var previous = new int[target.Length + 1];
        var current = new int[target.Length + 1];
        for (var j = 0; j <= target.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= source.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= target.Length; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[target.Length];
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
