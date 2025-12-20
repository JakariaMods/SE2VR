using HarmonyLib;
using Keen.Game2.Client.UI.HUD.Reticle;

namespace SE2VR.Client.Patches;

/// <summary>
/// Harmony patch for disabling the flatscreen crosshair/reticle
/// </summary>
[HarmonyPatch(typeof(ReticleScreenViewModel), nameof(ReticleScreenViewModel.ReticleWidth), MethodType.Getter)]
public class ReticleScreenViewModelPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref float __result)
    {
        __result = 0;
        return false;
    }
}