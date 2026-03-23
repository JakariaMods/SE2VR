using HarmonyLib;
using Keen.Game2.Client.UI.HUD.Reticle;
using Keen.Game2.Client.UI.InGame;
using Keen.VRage.Library.Utils;

namespace SE2VR.Client.Patches;

/// <summary>
/// Harmony patch for disabling the flatscreen crosshair/reticle
/// </summary>
[HarmonyPatch(typeof(SessionInGameUISessionComponent), "CreateReticleScreen")]
public class SessionInGameUISessionComponentPatch
{
    public static bool Prefix(ref IObservableDisposable __result)
    {
        __result = null!;
        return false;
    }
}