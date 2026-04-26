using HarmonyLib;
using Keen.Game2.Client.UI.Shared.Mission;

namespace SE2VR.Client.Patches;

/// <summary>
/// Harmony patch for disabling onboarding tooltips
/// </summary>
[HarmonyPatch(typeof(OnboardingTooltipManager), nameof(OnboardingTooltipManager.EnqueueTooltip))]
public class OnboardingTooltipManagerPatch
{
    public static bool Prefix()
    {
        return false;
    }
}
