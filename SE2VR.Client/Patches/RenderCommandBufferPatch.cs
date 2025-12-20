using HarmonyLib;
using Keen.VRage.Library.Extensions;
using Keen.VRage.Render.FrameData;

namespace SE2VR.Client.Patches;

/// <summary>
/// Last point in render submission where commands can be submitted
/// </summary>
[HarmonyPatch(typeof(RenderCommandBuffer), nameof(RenderCommandBuffer.Commit))]
public static class RenderCommandBufferPatch
{
    public static Action? PreCommit;

    [HarmonyPrefix]
    static void Prefix() => PreCommit.InvokeIfNotNull();
}