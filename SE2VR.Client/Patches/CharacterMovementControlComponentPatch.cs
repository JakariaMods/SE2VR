using HarmonyLib;
using Keen.Game2.Client.WorldObjects.Character;
using Keen.Game2.Simulation.WorldObjects.Weapons;
using Keen.VRage.DCS.Components;

namespace SE2VR.Client.Patches;

/// <summary>
/// Patch for overriding vanilla recoil when needed
/// </summary>
[HarmonyPatch(typeof(CharacterMovementControlComponent), nameof(CharacterMovementControlComponent.AddRecoilKick))]
public static class CharacterMovementControlComponentPatch
{
    public static event Action<Entity, RecoilKick, RecoilPatternDefinition>? OnRecoil;

    public static bool Prefix(RecoilKick kick, Entity source, RecoilPatternDefinition pattern)
    {
        if (OnRecoil == null)
            return true;

        OnRecoil.Invoke(source, kick, pattern);
        return false;
    }
}
