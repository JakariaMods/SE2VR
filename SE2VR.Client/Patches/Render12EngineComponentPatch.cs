using System.Reflection;
using HarmonyLib;
using Keen.VRage.Library.Extensions;

namespace SE2VR.Client.Patches;

/// <summary>
/// Patch for the draw method.
/// </summary>
[HarmonyPatch]
public static class Render12EngineComponentPatch
{
    public static Action? PostDraw;

    static MethodBase? TargetMethod()
    {
        var type = AccessTools.TypeByName("Keen.VRage.Render12.EngineComponents.Render12EngineComponent");
        return AccessTools.Method(type, "Draw", [typeof(bool)]);
    }

    [HarmonyPostfix]
    static void Postfix() => PostDraw.InvokeIfNotNull();
}
