using HarmonyLib;
using Keen.Game2.Client.WorldObjects.CubeGrids;

namespace SE2VR.Client.Patches;

/// <summary>
/// Patch to disable the grid targeting reticle
/// TODO #9
/// </summary>
[HarmonyPatch(typeof(TargetingGridClientComponent), "DrawHUDMarkers")]
public class TargetingGridClientComponentPatch
{
    public static bool Prefix()
    {
        return false;
    }
}
