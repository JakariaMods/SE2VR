using HarmonyLib;
using Keen.VRage.Render.FrameData;
using SE2VR.Client.Components;

namespace SE2VR.Client.Patches;

/// <summary>
/// Prevents ContractsProcessor.ProcessMessageQueue from flattening multiple queued frames into a single one. 
/// </summary>
[HarmonyPatch(typeof(SharedData), nameof(SharedData.GetRenderFrame))]
public static class ContractsProcessorPatch
{
    [HarmonyPrefix]
    static bool Prefix(bool onlyFullFrame, out bool isPreFrame, ref UpdateFrame? __result)
    {
        isPreFrame = false;
        if (VRRenderEngineComponent.DOUBLE_RENDER && onlyFullFrame)
        {
            __result = null;
            return false;
        }

        return true;
    }
}