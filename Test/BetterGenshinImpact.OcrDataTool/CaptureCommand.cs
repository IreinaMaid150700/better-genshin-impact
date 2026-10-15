using System.Diagnostics;
using BetterGenshinImpact.Core.Config;
using Fischless.GameCapture;
using OpenCvSharp;
using Vanara.PInvoke;

namespace BetterGenshinImpact.OcrDataTool;

/// <summary>
/// 定时截取游戏窗口，画面变化足够大时才保存，用于采集 OCR 数据集原图
/// </summary>
internal static class CaptureCommand
{
    /// <summary>
    /// 与 SystemControl.GetGenshinGameProcessNameList 的默认列表一致。
    /// SystemControl 的静态初始化依赖 WPF App，控制台程序中不能直接调用
    /// </summary>
    private static readonly string[] DefaultProcessNames =
        ["YuanShen", "GenshinImpact", "Genshin Impact Cloud Game", "Genshin Impact Cloud"];

    private static readonly Size ThumbnailSize = new(192, 108);

    /// <summary>
    /// 缩略图上像素灰度差超过该值才视为发生变化
    /// </summary>
    private const double PixelChangeThreshold = 25;

    /// <summary>
    /// 缩略图平均亮度低于该值视为黑屏/加载画面，不保存
    /// </summary>
    private const double BlackFrameBrightness = 8;

    public static async Task<int> RunAsync(CommandArgs args)
    {
        var outputDirectory = Path.GetFullPath(args.Required("out"));
        var interval = TimeSpan.FromSeconds(args.Double("interval", 3));
        var minChange = args.Double("min-change", 0.005);
        var maxCount = args.Int("max", 500);
        var modeName = args.Optional("mode") ?? nameof(CaptureModes.BitBlt);
        if (!Enum.TryParse<CaptureModes>(modeName, true, out var mode))
        {
            throw new UsageException($"--mode không hợp lệ: {modeName}. Hỗ trợ: {string.Join(", ", Enum.GetNames<CaptureModes>())}");
        }

        var processNames = args.Optional("process")
                               ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                           ?? DefaultProcessNames;

        // 与主程序一致使用每显示器 DPI 感知，避免高 DPI 缩放下截图尺寸被系统虚拟化
        SHCore.SetProcessDpiAwareness(SHCore.PROCESS_DPI_AWARENESS.PROCESS_PER_MONITOR_DPI_AWARE);

        var hWnd = FindGameWindow(processNames);
        if (hWnd == 0)
        {
            Console.Error.WriteLine($"Không tìm thấy cửa sổ game ({string.Join(", ", processNames)}). Hãy mở game trước, hoặc chỉ định --process.");
            return 1;
        }

        Directory.CreateDirectory(outputDirectory);
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        using var capture = GameCaptureFactory.Create(mode);
        // BitBlt 必须传入该键才会启动；传 false，不修改注册表（BetterGI 主程序已按其配置处理）
        capture.Start(hWnd, new Dictionary<string, object> { ["autoFixWin11BitBlt"] = false });
        Console.WriteLine($"Đang chụp ({mode}, mỗi {interval.TotalSeconds:0.#}s) vào {outputDirectory}. Nhấn Ctrl+C để dừng.");

        Mat? lastThumbnail = null;
        var saved = 0;
        var failures = 0;
        try
        {
            while (!cancellation.IsCancellationRequested && saved < maxCount)
            {
                using (var frame = capture.Capture())
                {
                    if (frame == null)
                    {
                        if (++failures == 5)
                        {
                            Console.Error.WriteLine("Không chụp được ảnh. Thử: không thu nhỏ game, chạy terminal bằng quyền Administrator, hoặc --mode WindowsGraphicsCapture.");
                        }
                    }
                    else
                    {
                        failures = 0;
                        using var image = ToBgr(frame.Frame);
                        var thumbnail = CreateThumbnail(image);
                        var change = lastThumbnail == null ? 1 : ChangedRatio(thumbnail, lastThumbnail);
                        if (change >= minChange && Cv2.Mean(thumbnail).Val0 >= BlackFrameBrightness)
                        {
                            CoverUid(image);
                            var name = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
                            ImageFiles.WritePng(Path.Combine(outputDirectory, name), image);
                            lastThumbnail?.Dispose();
                            lastThumbnail = thumbnail;
                            saved++;
                            Console.WriteLine($"[{saved}] {name} ({image.Width}x{image.Height}, thay đổi {change:P1})");
                        }
                        else
                        {
                            thumbnail.Dispose();
                        }
                    }
                }

                try
                {
                    await Task.Delay(interval, cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    // Ctrl+C
                }
            }
        }
        finally
        {
            capture.Stop();
            lastThumbnail?.Dispose();
        }

        Console.WriteLine($"Đã lưu {saved} ảnh vào {outputDirectory}");
        return 0;
    }

    private static nint FindGameWindow(IEnumerable<string> processNames)
    {
        var sessionId = Process.GetCurrentProcess().SessionId;
        foreach (var name in processNames)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    try
                    {
                        if (process.SessionId == sessionId && process.MainWindowHandle != 0)
                        {
                            return process.MainWindowHandle;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // 进程已退出
                    }
                }
            }
        }

        return 0;
    }

    private static Mat ToBgr(Mat frame)
    {
        return frame.Channels() switch
        {
            4 => frame.CvtColor(ColorConversionCodes.BGRA2BGR),
            3 => frame.Clone(),
            var channels => throw new InvalidDataException($"Số kênh ảnh không hỗ trợ: {channels}")
        };
    }

    private static Mat CreateThumbnail(Mat image)
    {
        using var gray = image.CvtColor(ColorConversionCodes.BGR2GRAY);
        return gray.Resize(ThumbnailSize, 0, 0, InterpolationFlags.Area);
    }

    /// <summary>
    /// 发生明显变化的像素占比。只比较平均差值会漏掉对话框换一行字这类小面积变化
    /// </summary>
    private static double ChangedRatio(Mat current, Mat previous)
    {
        using var difference = new Mat();
        Cv2.Absdiff(current, previous, difference);
        using var changed = difference.Threshold(PixelChangeThreshold, 255, ThresholdTypes.Binary);
        return (double)Cv2.CountNonZero(changed) / (changed.Rows * changed.Cols);
    }

    /// <summary>
    /// 与主程序截图热键（TaskTriggerDispatcher.TakeScreenshot）一致，涂白右下角 UID
    /// </summary>
    private static void CoverUid(Mat image)
    {
        var scale = image.Width / 1920.0;
        var uid = MaskWindowConfig.UidCoverRightBottomRect;
        var rect = new Rect(
            (int)(image.Width - uid.X * scale),
            (int)(image.Height - uid.Y * scale),
            (int)(uid.Width * scale),
            (int)(uid.Height * scale));
        image.Rectangle(rect, Scalar.White, -1);
    }
}
