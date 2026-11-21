using System.Reflection;
using HarmonyLib;
using SE2VR.Client.Wrappers;

namespace SE2VR.Client.Patches;

/// <summary>
/// Patch to hook to scene draw
/// </summary>
[HarmonyPatch]
public static class SceneDrawSystemPatch
{
    public static Action<MySceneDrawSystem, object>? PreDraw;
    public static Action<MySceneDrawSystem, object>? PostDraw;

    static MethodBase? TargetMethod() => AccessTools.Method(AccessTools.TypeByName("Keen.VRage.Render12.Core.Systems.SceneDrawSystem"), "Draw");

    [HarmonyPrefix]
    static void Prefix(object __instance, object[] __args) => PreDraw?.Invoke(new MySceneDrawSystem(__instance), __args[0]);

    [HarmonyPostfix]
    static void Postfix(object __instance, object[] __args) => PostDraw?.Invoke(new MySceneDrawSystem(__instance), __args[0]);
}
