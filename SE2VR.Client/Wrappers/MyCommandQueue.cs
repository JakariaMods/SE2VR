using System.Reflection;
using HarmonyLib;
using Vortice.Direct3D12;

namespace SE2VR.Client.Wrappers;

/// <summary>
/// My Command Queue
/// </summary>
/// <param name="obj"></param>
public class MyCommandQueue(object? obj)
{

#pragma warning disable KN023
    private static readonly FieldInfo _d3dQueueField;
#pragma warning restore KN023

    public bool IsValid => obj != null;

    static MyCommandQueue()
    {
        var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.Device.CommandQueue");
        type.PrintFields();
        _d3dQueueField = AccessTools.Field(type, "<D3DQueue>k__BackingField");
    }

    public ID3D12CommandQueue GetQueue() => (ID3D12CommandQueue)_d3dQueueField.GetValue(obj)!;
}
