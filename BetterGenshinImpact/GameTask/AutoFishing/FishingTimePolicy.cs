using System.ComponentModel;
using BetterGenshinImpact.Service.I18n;

namespace BetterGenshinImpact.GameTask.AutoFishing
{
    public enum FishingTimePolicy
    {
        [I18nDescription("全天")]
        All,
        [I18nDescription("白天")]
        Daytime,
        [I18nDescription("夜晚")]
        Nighttime,
        [I18nDescription("不调")]
        DontChange
    }
}
