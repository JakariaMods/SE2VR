using System.Reflection;
using HarmonyLib;

namespace SE2VR.Client.Wrappers;

/// <summary>
/// CoreSystems is an internal class, this is MY Core Systems, not keens.
/// </summary>
public static class MyCoreSystems
{

#pragma warning disable KN023
    private static readonly FieldInfo _swapChainField;
    private static readonly FieldInfo _deviceContextField;
    private static readonly FieldInfo _sceneDrawSystemField;
#pragma warning restore KN023

    public static MySwapChain SwapChain => new MySwapChain(_swapChainField.GetValue(null));
    public static MyDeviceContext DeviceContext => new MyDeviceContext(_deviceContextField.GetValue(null));
    public static MySceneDrawSystem SceneDrawSystem => new MySceneDrawSystem(_sceneDrawSystemField.GetValue(null));

    static MyCoreSystems()
    {
        var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.CoreSystems");
        _swapChainField = AccessTools.Field(type, "SwapChain");
        _deviceContextField = AccessTools.Field(type, "DeviceContext");
        _sceneDrawSystemField = AccessTools.Field(type, "SceneDrawSystem");
    }
}
