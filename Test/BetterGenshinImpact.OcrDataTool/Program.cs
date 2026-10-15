using System.Text;

namespace BetterGenshinImpact.OcrDataTool;

/// <summary>
/// OCR 数据集工具：采集游戏截图、裁剪文字行并预标注、评估识别模型
/// </summary>
internal static class Program
{
    private const string Usage = """
        BetterGenshinImpact.OcrDataTool - công cụ dữ liệu OCR

        capture  Tự chụp cửa sổ game mỗi khi màn hình thay đổi (UID được che)
                 --out <thư mục> [--interval 3] [--min-change 0.005] [--max 500]
                 [--mode BitBlt] [--process YuanShen,GenshinImpact]
        crop     Phát hiện dòng chữ (Det V5) trong ảnh chụp, cắt ra, gán nhãn sơ bộ và tạo review.html để sửa nhãn
                 --in <thư mục ảnh> --out <thư mục dataset> [--model V5Latin] [--max-same-text 3] [--min-chars 2]
        eval     Đo độ chính xác model nhận dạng trên labels.tsv đã duyệt
                 --labels <labels.tsv> --model <preset | thư mục model | slim.onnx> [--model ...] [--root <thư mục ảnh>]

        Preset model: {0}
        Model tự train: thư mục chứa slim.onnx + inference.yml (PostProcess.character_dict)
        """;

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage(Console.Out);
            return 0;
        }

        try
        {
            var options = CommandArgs.Parse(args.Skip(1));
            return args[0].ToLowerInvariant() switch
            {
                "capture" => await CaptureCommand.RunAsync(options),
                "crop" => CropCommand.Run(options),
                "eval" => EvalCommand.Run(options),
                _ => throw new UsageException($"Lệnh không hợp lệ: {args[0]}")
            };
        }
        catch (UsageException e)
        {
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine();
            PrintUsage(Console.Error);
            return 2;
        }
    }

    private static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine(string.Format(Usage, OcrModels.PresetNames));
    }
}

/// <summary>
/// 参数错误，打印用法后退出
/// </summary>
internal sealed class UsageException(string message) : Exception(message);
