using System.Globalization;

namespace BetterGenshinImpact.OcrDataTool;

/// <summary>
/// 解析 --key value 形式的参数，同名参数可重复出现（如多个 --model）
/// </summary>
internal sealed class CommandArgs
{
    private readonly Dictionary<string, List<string>> _values = new(StringComparer.OrdinalIgnoreCase);

    public static CommandArgs Parse(IEnumerable<string> args)
    {
        var result = new CommandArgs();
        string? key = null;
        foreach (var arg in args)
        {
            if (key == null)
            {
                if (!arg.StartsWith("--", StringComparison.Ordinal) || arg.Length <= 2)
                {
                    throw new UsageException($"Tham số không hợp lệ: {arg}");
                }

                key = arg[2..];
                continue;
            }

            if (!result._values.TryGetValue(key, out var list))
            {
                list = [];
                result._values[key] = list;
            }

            list.Add(arg);
            key = null;
        }

        if (key != null)
        {
            throw new UsageException($"Thiếu giá trị cho --{key}");
        }

        return result;
    }

    public string Required(string key)
    {
        return Optional(key) ?? throw new UsageException($"Thiếu tham số --{key}");
    }

    public string? Optional(string key)
    {
        return _values.TryGetValue(key, out var list) ? list[^1] : null;
    }

    public IReadOnlyList<string> All(string key)
    {
        return _values.TryGetValue(key, out var list) ? list : [];
    }

    public int Int(string key, int defaultValue)
    {
        var value = Optional(key);
        if (value == null)
        {
            return defaultValue;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new UsageException($"--{key} phải là số nguyên: {value}");
    }

    public double Double(string key, double defaultValue)
    {
        var value = Optional(key);
        if (value == null)
        {
            return defaultValue;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new UsageException($"--{key} phải là số: {value}");
    }
}
