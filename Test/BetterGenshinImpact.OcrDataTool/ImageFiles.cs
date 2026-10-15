using OpenCvSharp;

namespace BetterGenshinImpact.OcrDataTool;

/// <summary>
/// 图像读写。经由字节编解码，以支持包含非 ASCII 字符的路径
/// </summary>
internal static class ImageFiles
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".bmp" };

    public static IEnumerable<string> Enumerate(string directory)
    {
        return Directory.EnumerateFiles(directory)
            .Where(file => Extensions.Contains(Path.GetExtension(file)))
            .Order(StringComparer.Ordinal);
    }

    /// <summary>
    /// 读取为三通道 BGR，与 BetterGI 送入 OCR 的图像格式一致
    /// </summary>
    public static Mat ReadBgr(string path)
    {
        var mat = Cv2.ImDecode(File.ReadAllBytes(path), ImreadModes.Color);
        if (mat.Empty())
        {
            mat.Dispose();
            throw new InvalidDataException($"Không đọc được ảnh: {path}");
        }

        return mat;
    }

    public static void WritePng(string path, Mat mat)
    {
        Cv2.ImEncode(".png", mat, out var bytes);
        File.WriteAllBytes(path, bytes);
    }
}
