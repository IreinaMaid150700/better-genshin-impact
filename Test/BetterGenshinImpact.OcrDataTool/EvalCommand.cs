using System.Text;
using OpenCvSharp;

namespace BetterGenshinImpact.OcrDataTool;

/// <summary>
/// 在人工校对过的文字行上评估识别模型（只跑识别模型，与 BetterGI 运行时的识别输入一致）
/// </summary>
internal static class EvalCommand
{
    private sealed record Sample(string RelativePath, string Label);

    private sealed record Report(
        string Name,
        int Total,
        int Exact,
        int ExactIgnoringSpaces,
        double CharacterErrorRate,
        double CharacterErrorRateIgnoringDiacritics);

    public static int Run(CommandArgs args)
    {
        var labelsPath = Path.GetFullPath(args.Required("labels"));
        var root = Path.GetFullPath(args.Optional("root") ?? Path.GetDirectoryName(labelsPath)!);
        var modelSpecs = args.All("model");
        if (modelSpecs.Count == 0)
        {
            throw new UsageException("Cần ít nhất một --model");
        }

        if (!File.Exists(labelsPath))
        {
            throw new UsageException($"Không tìm thấy {labelsPath}");
        }

        var samples = ReadSamples(labelsPath, out var skipped);
        Console.WriteLine($"Đọc {samples.Count} dòng có nhãn từ {labelsPath} (bỏ qua {skipped} dòng trống/nhãn rỗng)");
        if (samples.Count == 0)
        {
            return 1;
        }

        var images = new List<Mat>(samples.Count);
        try
        {
            foreach (var sample in samples)
            {
                images.Add(ImageFiles.ReadBgr(Path.Combine(root, sample.RelativePath)));
            }

            var factory = OcrModels.CreateFactory();
            var reports = new List<Report>();
            foreach (var spec in modelSpecs)
            {
                var (name, modelType) = OcrModels.Resolve(spec);
                var (det, rec) = modelType.Build(factory);
                using (det)
                using (rec)
                {
                    var predictions = rec.Run(images.ToArray()).Select(result => result.Text).ToArray();
                    reports.Add(Evaluate(name, samples, predictions, modelType.RecLabel(), root));
                }
            }

            PrintSummary(reports);
            return 0;
        }
        finally
        {
            foreach (var image in images)
            {
                image.Dispose();
            }
        }
    }

    private static List<Sample> ReadSamples(string labelsPath, out int skipped)
    {
        var samples = new List<Sample>();
        skipped = 0;
        foreach (var line in File.ReadLines(labelsPath, Encoding.UTF8))
        {
            var separator = line.IndexOf('\t');
            var label = separator < 0 ? "" : TextMetrics.Canonical(line[(separator + 1)..]);
            if (line.StartsWith('#') || label.Length == 0)
            {
                skipped++;
                continue;
            }

            samples.Add(new Sample(line[..separator], label));
        }

        return samples;
    }

    private static Report Evaluate(string name, IReadOnlyList<Sample> samples, IReadOnlyList<string> predictions,
        IReadOnlyList<string> dictionary, string outputDirectory)
    {
        int exact = 0, exactIgnoringSpaces = 0;
        long distance = 0, distanceIgnoringDiacritics = 0, length = 0, lengthIgnoringDiacritics = 0;
        // 标注中出现、但识别结果中缺少的字符计数（字符多重集之差，粗略定位丢失的字母）
        var missing = new Dictionary<char, int>();
        var labelCharacters = new HashSet<char>();

        var detailPath = Path.Combine(outputDirectory, $"eval_{SafeFileName(name)}.tsv");
        using (var detail = new StreamWriter(detailPath, false, new UTF8Encoding(false)))
        {
            detail.WriteLine("path\tlabel\tpredict\tdistance");
            for (var i = 0; i < samples.Count; i++)
            {
                var label = samples[i].Label;
                var prediction = TextMetrics.Canonical(predictions[i]);
                var labelCompact = TextMetrics.RemoveSpaces(label);
                var predictionCompact = TextMetrics.RemoveSpaces(prediction);

                if (label == prediction) exact++;
                if (labelCompact == predictionCompact) exactIgnoringSpaces++;

                var lineDistance = TextMetrics.Levenshtein(labelCompact, predictionCompact);
                distance += lineDistance;
                length += labelCompact.Length;

                var labelBase = TextMetrics.RemoveDiacritics(labelCompact);
                distanceIgnoringDiacritics +=
                    TextMetrics.Levenshtein(labelBase, TextMetrics.RemoveDiacritics(predictionCompact));
                lengthIgnoringDiacritics += labelBase.Length;

                foreach (var group in labelCompact.GroupBy(c => c))
                {
                    var lost = group.Count() - predictionCompact.Count(c => c == group.Key);
                    if (lost > 0)
                    {
                        missing[group.Key] = missing.GetValueOrDefault(group.Key) + lost;
                    }
                }

                labelCharacters.UnionWith(labelCompact);
                detail.WriteLine($"{samples[i].RelativePath}\t{label}\t{prediction}\t{lineDistance}");
            }
        }

        var dictionarySet = dictionary.ToHashSet(StringComparer.Ordinal);
        var notInDictionary = labelCharacters
            .Where(c => !dictionarySet.Contains(c.ToString()))
            .Order()
            .ToArray();
        var report = new Report(
            name,
            samples.Count,
            exact,
            exactIgnoringSpaces,
            length == 0 ? 0 : (double)distance / length,
            lengthIgnoringDiacritics == 0 ? 0 : (double)distanceIgnoringDiacritics / lengthIgnoringDiacritics);

        Console.WriteLine();
        Console.WriteLine($"== {name} (dict {dictionary.Count} ký tự) ==");
        Console.WriteLine($"  Khớp tuyệt đối     : {report.Exact}/{report.Total} ({(double)report.Exact / report.Total:P1})");
        Console.WriteLine($"  Khớp bỏ dấu cách   : {report.ExactIgnoringSpaces}/{report.Total} ({(double)report.ExactIgnoringSpaces / report.Total:P1})");
        Console.WriteLine($"  CER                : {report.CharacterErrorRate:P2}");
        Console.WriteLine($"  CER khi bỏ mọi dấu : {report.CharacterErrorRateIgnoringDiacritics:P2} (chênh lệch với CER = lỗi do dấu)");
        Console.WriteLine($"  Ký tự trong nhãn nhưng KHÔNG có trong dict ({notInDictionary.Length}): {new string(notInDictionary)}");
        Console.WriteLine("  Ký tự bị mất nhiều nhất: " + string.Join(", ",
            missing.OrderByDescending(pair => pair.Value).Take(15).Select(pair => $"{pair.Key}×{pair.Value}")));
        Console.WriteLine($"  Chi tiết từng dòng: {detailPath}");
        return report;
    }

    private static void PrintSummary(IReadOnlyList<Report> reports)
    {
        if (reports.Count < 2)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"{"Model",-36} {"Khớp",8} {"Khớp*",8} {"CER",8} {"CER bỏ dấu",11}");
        foreach (var report in reports)
        {
            Console.WriteLine(
                $"{report.Name,-36} {(double)report.Exact / report.Total,8:P1} {(double)report.ExactIgnoringSpaces / report.Total,8:P1} {report.CharacterErrorRate,8:P2} {report.CharacterErrorRateIgnoringDiacritics,11:P2}");
        }

        Console.WriteLine("(Khớp* = khớp khi bỏ dấu cách)");
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
