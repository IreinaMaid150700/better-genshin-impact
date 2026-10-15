using System;
using System.Collections.Generic;
using System.ComponentModel;
using BetterGenshinImpact.Service.I18n;
using System.Text;

namespace BetterGenshinImpact.GameTask.Model.GameUI
{
    public enum GridScreenName
    {
        [I18nDescription("武器")]
        Weapons,
        [I18nDescription("圣遗物")]
        Artifacts,
        [I18nDescription("养成道具")]
        CharacterDevelopmentItems,
        [I18nDescription("食物")]
        Food,
        [I18nDescription("材料")]
        Materials,
        [I18nDescription("小道具")]
        Gadget,
        [I18nDescription("任务")]
        Quest,
        [I18nDescription("贵重道具")]
        PreciousItems,
        [I18nDescription("摆设")]
        Furnishings,
        [I18nDescription("圣遗物分解")]
        ArtifactSalvage,
        [I18nDescription("圣遗物套装筛选")]
        ArtifactSetFilter,
        [I18nDescription("合成")]
        Crafting,
        /// <summary>
        /// 队伍配置快速编队界面的左侧角色头像网格。
        /// </summary>
        [I18nDescription("队伍配置角色")]
        PartySetupCharacters
    }
}
