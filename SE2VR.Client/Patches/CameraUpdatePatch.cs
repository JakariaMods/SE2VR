using HarmonyLib;
using Keen.Game2.Client.GameSystems.CameraSystems;
using Keen.VRage.Core;

namespace SE2VR.Client.Patches;

/// <summary>
/// Patch of <see cref="CameraComponent"/> that allows overriding the transform passed to render
/// </summary>
[HarmonyPatch(typeof(CameraComponent), "UpdateRenderSettingsInternal")]
public static class CameraPatch
{
    public static WorldTransform? Transform;

    public static void Prefix(ref WorldTransform wt)
    {
        if (Transform.HasValue)
            wt = Transform.Value;
    }
}