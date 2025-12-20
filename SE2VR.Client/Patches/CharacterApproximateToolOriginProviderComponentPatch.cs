using HarmonyLib;
using Keen.Game2.Simulation.WorldObjects.Characters;
using Keen.VRage.Core;

namespace SE2VR.Client.Patches;

/// <summary>
/// Patch for overwriting the interaction origin server-side
/// Singleplayer only
/// </summary>
[HarmonyPatch(typeof(CharacterApproximateToolOriginProviderComponent), nameof(CharacterApproximateToolOriginProviderComponent.WorldTransform), MethodType.Getter)]
public static class CharacterApproximateToolOriginProviderComponent_WorldTransform_Patch
{
    public static WorldTransform? Origin;

    public static void Postfix(ref WorldTransform __result)
    {
        if (Origin.HasValue)
            __result = Origin.Value;
    }
}