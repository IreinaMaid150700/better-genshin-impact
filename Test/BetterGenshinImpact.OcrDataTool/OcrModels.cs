using BetterGenshinImpact.Core.Recognition.OCR.Engine;
using BetterGenshinImpact.Core.Recognition.ONNX;
using Microsoft.Extensions.Logging.Abstractions;
using ModelType = BetterGenshinImpact.Core.Recognition.OCR.Paddle.PaddleOcrService.PaddleOcrModelType;

namespace BetterGenshinImpact.OcrDataTool;

/// <summary>
/// 解析 --model 参数：BetterGI 已内置的模型预设，或自行训练的识别模型目录
/// </summary>
internal static class OcrModels
{
    private static readonly Dictionary<string, ModelType> Presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["V4"] = ModelType.V4,
        ["V4En"] = ModelType.V4En,
        ["V5"] = ModelType.V5,
        ["V6"] = ModelType.V6,
        ["V5Latin"] = ModelType.V5Latin,
        ["V5Eslav"] = ModelType.V5Eslav,
        ["V5Korean"] = ModelType.V5Korean,
    };

    public static string PresetNames => string.Join(", ", Presets.Keys);

    public static BgiOnnxFactory CreateFactory()
    {
        return new BgiOnnxFactory(NullLogger<BgiOnnxFactory>.Instance);
    }

    /// <summary>
    /// 自定义识别模型固定搭配 V5 检测模型，与在 BetterGI 中接入新识别模型的方式一致；
    /// 字典从模型同目录 inference.yml 的 PostProcess.character_dict 读取
    /// </summary>
    /// <param name="spec">预设名，或 slim.onnx 文件 / 其所在目录</param>
    public static (string Name, ModelType Type) Resolve(string spec)
    {
        if (Presets.TryGetValue(spec, out var preset))
        {
            return (spec, preset);
        }

        var path = Path.GetFullPath(spec);
        if (Directory.Exists(path))
        {
            path = Path.Combine(path, "slim.onnx");
        }

        if (!File.Exists(path))
        {
            throw new UsageException(
                $"Không tìm thấy model \"{spec}\". Dùng preset ({PresetNames}) hoặc đường dẫn tới slim.onnx / thư mục chứa nó");
        }

        var name = new DirectoryInfo(Path.GetDirectoryName(path)!).Name;
        var recognitionModel = BgiOnnxModel.CreateUnregistered("OcrDataTool_" + name, path);
        return (name, ModelType.Create(
            BgiOnnxModel.PaddleOcrDetV5,
            OcrVersionConfig.PpOcrV5,
            recognitionModel,
            OcrVersionConfig.PpOcrV5));
    }
}
