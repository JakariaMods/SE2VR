using System.Reflection;
using HarmonyLib;
using Keen.VRage.Library.Mathematics;

namespace SE2VR.Client.Wrappers;

/// <summary>
/// Patch for scene draw systems, has strange mix of static and non static... see for a fix later
/// </summary>
/// <param name="obj"></param>
public class MySceneDrawSystem(object? obj)
{
#pragma warning disable KN023
    private static MethodInfo? _drawMethod;
    public static PreDrawDelegate? PreDraw;
    public static PostDrawDelegate? PostDraw;
#pragma warning restore KN023

    public delegate void PreDrawDelegate(ref Vector2I finalResolution, ref bool isComputeQueueBranched);
    public delegate void PostDrawDelegate();

    static MySceneDrawSystem()
    {
        var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.Systems.SceneDrawSystem");
        _drawMethod = AccessTools.Method(type, "Draw", [typeof(Vector2I), typeof(bool)]);
    
        OpenVREngineComponent.Instance.Harmony.Patch(_drawMethod,
            prefix: new HarmonyMethod(typeof(MySceneDrawSystem).GetMethod(nameof(Prefix), BindingFlags.Static | BindingFlags.NonPublic)),
            postfix: new HarmonyMethod(typeof(MySceneDrawSystem).GetMethod(nameof(Postfix), BindingFlags.Static | BindingFlags.NonPublic))
        );
    }

    public void Draw(Vector2I finalResolution, bool isComputeQueueBranched)
    {
        _drawMethod?.Invoke(obj, [ finalResolution, isComputeQueueBranched ]);
    }

    private static void Prefix(ref Vector2I finalResolution, ref bool isComputeQueueBranched) => PreDraw?.Invoke(ref finalResolution, ref isComputeQueueBranched);

    private static void Postfix() => PostDraw?.Invoke();

}
