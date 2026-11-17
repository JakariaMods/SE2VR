using System.Reflection;
using HarmonyLib;
using SE2VR.Client.Wrappers;

namespace SE2VR.Client.Patches;

/// <summary>
/// Patch to disable UI rendering in anything except the VR UI texture
/// </summary>
[HarmonyPatch]
public static class MainUISystemPatch
{
    /// <summary>
    /// Render thread
    /// </summary>
    public static Func<MyCommandList, MyTexture, (bool Draw, MyTexture Target)>? PreDraw;

    /// <summary>
    /// Render thread
    /// </summary>
    public static Action<MyCommandList, MyTexture>? PostDraw;

    static MethodBase? TargetMethod() => AccessTools.Method("Keen.VRage.Render12.UIStage.MainUISystem:DoWork");

    [HarmonyPrefix]
    static bool Prefix(object[] __args)
    {
        if (PreDraw == null)
            return true;

        var (draw, target) = PreDraw(new MyCommandList(__args[0]), new MyTexture(__args[1]));
        __args[1] = target.Instance;
        return draw;
    }

    [HarmonyPostfix]
    static void Postfix(object[] __args) => PostDraw?.Invoke(new MyCommandList(__args[0]), new MyTexture(__args[1]));
}
