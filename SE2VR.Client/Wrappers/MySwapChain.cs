using System.Reflection;
using HarmonyLib;
using Keen.VRage.Core.Render;
using Vortice.DXGI;

namespace SE2VR.Client.Wrappers;

/// <summary>
/// MY SwapChain wrapper
/// Used to acccess internal type
/// </summary>
public class MySwapChain(object? obj)
{

#pragma warning disable KN023
    private static readonly FieldInfo _d3dSwapChainField;
    private static readonly FieldInfo _currentDisplaySettingsField;
    private static readonly FieldInfo _backBuffersField;
#pragma warning restore KN023

    public bool IsValid => obj != null;

    static MySwapChain()
    {
        var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.Device.SwapChain");
        type.PrintFields();

        _d3dSwapChainField = AccessTools.Field(type, "_d3dSwapChain");
        _currentDisplaySettingsField = AccessTools.Field(type, "_currentDisplaySettings");
        _backBuffersField = AccessTools.Field(type, "_backBuffers");
    }

    public IDXGISwapChain3 GetD3DSwapChain() => (IDXGISwapChain3)_d3dSwapChainField.GetValue(obj)!;
}
