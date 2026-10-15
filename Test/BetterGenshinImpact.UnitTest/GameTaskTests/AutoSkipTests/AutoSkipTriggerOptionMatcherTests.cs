using BetterGenshinImpact.GameTask.AutoSkip;
using Microsoft.Extensions.Localization;
using System.Globalization;

namespace BetterGenshinImpact.UnitTest.GameTaskTests.AutoSkipTests;

public class AutoSkipTriggerOptionMatcherTests : IClassFixture<LocalizationFixture>
{
    /// <summary>
    /// 设置页中可选的全部游戏语言
    /// </summary>
    public static TheoryData<string> GameCultures => new() { "zh-Hans", "zh-Hant", "en", "ja", "ru", "it", "vi" };

    private readonly IStringLocalizer<AutoSkipTrigger> _stringLocalizer;

    public AutoSkipTriggerOptionMatcherTests(LocalizationFixture localization)
    {
        _stringLocalizer = localization.CreateStringLocalizer<AutoSkipTrigger>();
    }

    [Theory]
    [MemberData(nameof(GameCultures))]
    /// <summary>
    /// 每种游戏语言的本地化模式都应是合法正则（缺少资源时回退为中文键）
    /// </summary>
    public void CreateOptionMatcher_EveryGameCulture_ShouldBuildValidPattern(string culture)
    {
        var gameCulture = new CultureInfo(culture);

        var exception = Record.Exception(() =>
        {
            AutoSkipTrigger.CreateOptionMatcher(_stringLocalizer, gameCulture, AutoSkipTrigger.DailyCommissionOptionKey);
            AutoSkipTrigger.CreateOptionMatcher(_stringLocalizer, gameCulture, AutoSkipTrigger.ExpeditionOptionKey);
            AutoSkipTrigger.CreateOptionMatcher(_stringLocalizer, gameCulture, AutoSkipTrigger.DefaultPauseOptionKey);
        });

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("zh-Hans", "领取「每日委托」奖励", true, false)]
    [InlineData("zh-Hans", "探索派遣", false, true)]
    [InlineData("zh-Hant", "領取「每日委託」獎勵", true, false)]
    [InlineData("en", "Daily Commission", true, false)]
    [InlineData("en", "Expedition", false, true)]
    [InlineData("vi", "Nhận thưởng \"Ủy Thác Mỗi Ngày\"", true, false)]
    [InlineData("vi", "Nhan thuong \"Uy Thac Moi Ngay\"", true, false)]
    [InlineData("vi", "Nhận thưởng \"Uỷ Thác Mỗi Ngày\"", true, false)]
    [InlineData("vi", "Nhn thưng \"y Thác Mồi Ngày", true, false)]
    [InlineData("vi", "Nhn thưng \"Úy Thác Mi Ngày\"", true, false)]
    [InlineData("vi", "Phái Đi Thám Hiểm", false, true)]
    [InlineData("vi", "Phai Di Tham Hiem", false, true)]
    [InlineData("vi", "Nhận thưởng \"Hạng Mạo Hiểm\"", false, false)]
    [InlineData("vi", "Xem thông tin Hành Trình Hào Đấu", false, false)]
    [InlineData("vi", "Tạm biệt.", false, false)]
    /// <summary>
    /// 凯瑟琳对话中的橙色选项应按游戏语言正确识别为每日委托 / 探索派遣 / 普通选项
    /// </summary>
    public void OptionMatchers_KatheryneOptions_ShouldClassifyByGameCulture(string culture, string ocrText,
        bool expectedDailyCommission, bool expectedExpedition)
    {
        var gameCulture = new CultureInfo(culture);
        var dailyCommissionMatcher = AutoSkipTrigger.CreateOptionMatcher(_stringLocalizer, gameCulture, AutoSkipTrigger.DailyCommissionOptionKey);
        var expeditionMatcher = AutoSkipTrigger.CreateOptionMatcher(_stringLocalizer, gameCulture, AutoSkipTrigger.ExpeditionOptionKey);

        Assert.Equal(expectedDailyCommission, dailyCommissionMatcher.IsMatch(ocrText));
        Assert.Equal(expectedExpedition, expeditionMatcher.IsMatch(ocrText));
    }

    [Theory]
    [InlineData("zh-Hans", "领取「每日委托」奖励", true)]
    [InlineData("zh-Hans", "探索派遣", true)]
    [InlineData("zh-Hans", "委托锻造", false)]
    [InlineData("zh-Hans", "你好，凯瑟琳。", false)]
    [InlineData("en", "Daily Commission", true)]
    [InlineData("en", "Expedition", true)]
    [InlineData("vi", "Nhận thưởng \"Ủy Thác Mỗi Ngày\"", true)]
    [InlineData("vi", "Nhn thưng \"y Thác Mồi Ngày", true)]
    [InlineData("vi", "Nhn thưng \"Úy Thác Mi Ngày\"", true)]
    [InlineData("vi", "Phái Đi Thám Hiểm", true)]
    [InlineData("vi", "Nhận thưởng \"Hạng Mạo Hiểm\"", false)]
    [InlineData("vi", "Xem thông tin Hành Trình Hào Đấu", false)]
    [InlineData("vi", "Xin chào, Katheryne.", false)]
    [InlineData("vi", "Về Snezhnaya...", false)]
    [InlineData("vi", "Tạm biệt.", false)]
    /// <summary>
    /// 没有橙色选项时，凯瑟琳的每日委托 / 探索派遣选项应按游戏语言暂停，不能被默认选项点击
    /// </summary>
    public void DefaultPauseOptionMatcher_KatheryneOptions_ShouldPauseOnlySpecialOptions(string culture, string ocrText,
        bool expectedPause)
    {
        var gameCulture = new CultureInfo(culture);
        var defaultPauseOptionMatcher = AutoSkipTrigger.CreateOptionMatcher(_stringLocalizer, gameCulture, AutoSkipTrigger.DefaultPauseOptionKey);

        Assert.Equal(expectedPause, defaultPauseOptionMatcher.IsMatch(ocrText));
    }
}
