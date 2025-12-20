

using HarmonyLib;
using Keen.Game2.Simulation.WorldObjects.Tools;
using Keen.VRage.Core;

namespace SE2VR.Client.Patches;

/// <summary>
/// Patch for overwriting the debug gun's transform serverside
/// Singleplayer only
/// </summary>
[HarmonyPatch(typeof(GunToolComponent), nameof(GunToolComponent.GetProjectileTransform))]
public static class GunToolComponent_GetProjectileTransform_Patch
{
    public static RelativeTransform? Transform;

    public static void Postfix(ref RelativeTransform __result)
    {
        if (Transform.HasValue)
            __result = Transform.Value;
    }
}

/// <summary>
/// Patch for overwriting the debug gun's transform serverside
/// Singleplayer only
/// </summary>
[HarmonyPatch(typeof(GunToolComponent), nameof(GunToolComponent.GetProjectileVisualsTransform))]
public static class GunToolComponent_GetProjectileVisualsTransform_Patch
{
    public static RelativeTransform? Transform;

    public static void Postfix(ref RelativeTransform __result)
    {
        if (Transform.HasValue)
            __result = Transform.Value;
    }
}