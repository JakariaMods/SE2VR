using System.Reflection;
using HarmonyLib;

namespace SE2VR.Client.Wrappers;

/// <summary>
/// This is all mine now
/// Used to acccess internal type
/// </summary>
public class MyDeviceContext(object? obj)
{
#pragma warning disable KN023
    private static readonly FieldInfo _presentQueueField;
#pragma warning restore KN023

    public bool IsValid => obj != null;

    static MyDeviceContext()
    {
        var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.Device.DeviceContext");
        type.PrintFields();

        _presentQueueField = AccessTools.Field(type, "<PresentQueue>k__BackingField");
    }

    public MyCommandQueue GetPresentQueue() => new MyCommandQueue(_presentQueueField.GetValue(obj));
}
