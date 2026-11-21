using System.Reflection;
using HarmonyLib;
using Keen.VRage.Library.Extensions;

namespace SE2VR.Client.Patches;

/// <summary>
/// Patch that announces when the engine resets its draw contexts
/// </summary>
[HarmonyPatch]
public static class DrawContextsResetPatch
{
    public static Action? Reset;

    static MethodBase? TargetMethod() => AccessTools.Method("Keen.VRage.Render12.Core.Systems.DrawContextManager:OnResetContext");

    [HarmonyPostfix]
    static void Postfix() => Reset.InvokeIfNotNull();
}
