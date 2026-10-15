using System.Text;
using BetterGenshinImpact.Core.Recognition.OCR.Paddle;
using OpenCvSharp;

namespace BetterGenshinImpact.OcrDataTool;

/// <summary>
/// 用 BetterGI 的检测模型与透视裁剪逻辑切出文字行，保证与实际运行时送入识别模型的图像一致，
/// 再用现有识别模型预标注，生成供人工校对的 review.html
/// </summary>
internal static class CropCommand
{
    public static int Run(CommandArgs args)
    {
        var inputDirectory = Path.GetFullPath(args.Required("in"));
        var outputDirectory = Path.GetFullPath(args.Required("out"));
        var maxSameText = args.Int("max-same-text", 3);
        var minChars = args.Int("min-chars", 2);
        var (modelName, modelType) = OcrModels.Resolve(args.Optional("model") ?? "V5Latin");

        var preLabelsPath = Path.Combine(outputDirectory, "prelabels.tsv");
        var labelsPath = Path.Combine(outputDirectory, "labels.tsv");
        if (File.Exists(preLabelsPath) || File.Exists(labelsPath))
        {
            throw new UsageException($"{outputDirectory} đã có dữ liệu (prelabels.tsv/labels.tsv). Chọn --out khác để không ghi đè nhãn đã sửa.");
        }

        if (!Directory.Exists(inputDirectory))
        {
            throw new UsageException($"Không tìm thấy thư mục {inputDirectory}");
        }

        var images = ImageFiles.Enumerate(inputDirectory).ToList();
        if (images.Count == 0)
        {
            throw new UsageException($"Không có ảnh trong {inputDirectory}");
        }

        Directory.CreateDirectory(Path.Combine(outputDirectory, "crops"));
        var entries = new List<ReviewPage.Entry>();
        var textCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var duplicates = 0;
        var tooShort = 0;

        var (det, rec) = modelType.Build(OcrModels.CreateFactory());
        using (det)
        using (rec)
        using (var preLabels = new StreamWriter(preLabelsPath, false, new UTF8Encoding(false)))
        using (var meta = new StreamWriter(Path.Combine(outputDirectory, "meta.tsv"), false, new UTF8Encoding(false)))
        {
            meta.WriteLine("path\tsource\trect\tscore");
            for (var index = 0; index < images.Count; index++)
            {
                var imagePath = images[index];
                using var source = ImageFiles.ReadBgr(imagePath);
                var boxes = PaddleOcrService.SortBoxes(det.RunBoxes(source));
                var crops = new List<Mat>(boxes.Length);
                try
                {
                    foreach (var box in boxes)
                    {
                        crops.Add(PaddleOcrService.GetRotateCropImage(source, box.Points));
                    }

                    var results = rec.Run(crops.ToArray());
                    for (var i = 0; i < crops.Count; i++)
                    {
                        var text = TextMetrics.Canonical(results[i].Text);
                        // 图标、队伍序号等单字符结果对训练价值低；空结果保留，可能是现有模型完全认不出的越南语文字
                        if (text.Length > 0 && text.Length < minChars)
                        {
                            tooShort++;
                            continue;
                        }

                        // 相同预标注的文字行（重复出现的 UI 文案）只保留前几条，减少重复标注
                        if (text.Length > 0)
                        {
                            var count = textCounts.GetValueOrDefault(text);
                            if (count >= maxSameText)
                            {
                                duplicates++;
                                continue;
                            }

                            textCounts[text] = count + 1;
                        }

                        var relativePath = $"crops/{Path.GetFileNameWithoutExtension(imagePath)}_{i:D3}.png";
                        ImageFiles.WritePng(Path.Combine(outputDirectory, relativePath), crops[i]);
                        var rect = boxes[i].Rect.BoundingRect();
                        preLabels.WriteLine($"{relativePath}\t{text}");
                        meta.WriteLine(
                            $"{relativePath}\t{Path.GetFileName(imagePath)}\t{rect.X},{rect.Y},{rect.Width},{rect.Height}\t{results[i].Score:F3}");
                        entries.Add(new ReviewPage.Entry(relativePath, text));
                    }
                }
                finally
                {
                    foreach (var crop in crops)
                    {
                        crop.Dispose();
                    }
                }

                Console.WriteLine($"[{index + 1}/{images.Count}] {Path.GetFileName(imagePath)}: {boxes.Length} dòng chữ");
            }
        }

        var reviewPath = Path.Combine(outputDirectory, "review.html");
        ReviewPage.Write(reviewPath, modelName, entries);
        Console.WriteLine();
        Console.WriteLine($"Đã cắt {entries.Count} dòng bằng model {modelName} (bỏ {duplicates} dòng trùng nhãn sơ bộ, {tooShort} dòng ngắn hơn {minChars} ký tự).");
        Console.WriteLine($"Bước tiếp: mở {reviewPath} để sửa nhãn, bấm \"Tải labels.tsv\" rồi chép file vào {outputDirectory}");
        return 0;
    }
}
