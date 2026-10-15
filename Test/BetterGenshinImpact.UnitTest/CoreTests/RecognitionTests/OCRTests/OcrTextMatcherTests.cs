using BetterGenshinImpact.Core.Recognition.OCR;

namespace BetterGenshinImpact.UnitTest.CoreTests.RecognitionTests.OCRTests;

public class OcrTextMatcherTests
{
    [Theory]
    [InlineData("Phái Đi Thám Hiểm", "Phai Di Tham Hiem")]
    [InlineData("Nhận thưởng \"Ủy Thác Mỗi Ngày\"", "Nhan thuong \"Uy Thac Moi Ngay\"")]
    [InlineData("Expédition", "Expedition")]
    [InlineData("领取「每日委托」奖励", "领取「每日委托」奖励")]
    [InlineData("한국어", "한국어")]
    /// <summary>
    /// 归一化应去除附加符号并折叠特殊字母，不改变无附加符号的文字
    /// </summary>
    public void Normalize_VariousScripts_ShouldStripDiacriticsOnly(string text, string expected)
    {
        Assert.Equal(expected, OcrTextMatcher.Normalize(text));
    }

    [Theory]
    [InlineData("Nhận thưởng \"Ủy Thác Mỗi Ngày\"")]
    [InlineData("Nhan thuong \"Uy Thac Moi Ngay\"")]
    [InlineData("Nhận thưởng \"Uỷ Thác Mỗi Ngày\"")]
    [InlineData("NHẬN THƯỞNG \"ỦY THÁC MỖI NGÀY\"")]
    /// <summary>
    /// 带声调的模式应匹配 OCR 丢失声调、不同声调位置写法以及大小写不同的文本
    /// </summary>
    public void IsMatch_OcrTextVariants_ShouldMatchAccentedPattern(string ocrText)
    {
        var matcher = new OcrTextMatcher(@"Ủy\s*Thác|Mỗi\s*Ngày");

        Assert.True(matcher.IsMatch(ocrText));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Nhận thưởng \"Hạng Mạo Hiểm\"")]
    /// <summary>
    /// 空文本或不含关键词的文本不应匹配
    /// </summary>
    public void IsMatch_UnrelatedOrEmptyText_ShouldNotMatch(string? ocrText)
    {
        var matcher = new OcrTextMatcher(@"Thám\s*Hiểm|Phái\s*Đi");

        Assert.False(matcher.IsMatch(ocrText));
    }
}
