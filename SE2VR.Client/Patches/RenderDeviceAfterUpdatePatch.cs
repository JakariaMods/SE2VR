using System.Reflection;
using HarmonyLib;
using SE2VR.Client.Components;

namespace SE2VR.Client.Patches;

/// <summary>
/// Allows rendering left and right eye in the same frame by just doing it twice. Render12EngineComponent.RenderDeviceAfterUpdate</c> a second time inside the same
/// main-thread job. The first invocation renders + submits the left eye, the second call renders + submits the right eye.
/// </summary>
[HarmonyPatch]
public static class RenderDeviceAfterUpdatePatch
{
    [ThreadStatic]
    private static bool _reentry;

    private static MethodInfo? _method;

    static MethodBase? TargetMethod()
    {
        var type = AccessTools.TypeByName("Keen.VRage.Render12.EngineComponents.Render12EngineComponent");
        _method = AccessTools.Method(type, "RenderDeviceAfterUpdate");
        return _method;
    }

    [HarmonyPostfix]
    static void Postfix(object __instance)
    {
        if (!VRRenderEngineComponent.DOUBLE_RENDER)
            return;

        if (_reentry)
            return;

        _reentry = true;
        try
        {
            _method!.Invoke(__instance, null);
        }
        finally
        {
            _reentry = false;
        }
    }
}