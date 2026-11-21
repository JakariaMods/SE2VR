using HarmonyLib;
using Keen.Game2.Client.GameSystems.GPS;

namespace SE2VR.Client.Patches;

/// <summary>
/// Patch to disable GPS markers
/// TODO #9
/// </summary>
[HarmonyPatch(typeof(GPSMarkerRenderSessionComponent), "DrawHUDMarkers")]
public class GPSMarkerRenderSessionComponentPatch
{
    public static bool Prefix()
    {
        return false;
    }
}
