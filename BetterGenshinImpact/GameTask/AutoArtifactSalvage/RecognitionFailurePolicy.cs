using System.ComponentModel;
using BetterGenshinImpact.Service.I18n;

namespace BetterGenshinImpact.GameTask.AutoArtifactSalvage
{
    public enum RecognitionFailurePolicy
    {
        [I18nDescription("跳过")]
        Skip,
        [I18nDescription("终止")]
        Abort
    }
}
