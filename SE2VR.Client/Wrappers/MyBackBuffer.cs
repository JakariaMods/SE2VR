using System.Reflection;
using HarmonyLib;
using Vortice.Direct3D12;

namespace SE2VR.Client.Wrappers;

/// <summary>
/// MY BackBuffer :)
/// Used to acccess internal type
/// </summary>
public class MyBackBuffer(object? obj)
{

#pragma warning disable KN023
    private static readonly FieldInfo _d3dResourceField;
#pragma warning restore KN023

    public bool IsValid => obj != null;

    static MyBackBuffer()
    {
        var type = AccessTools.TypeByName("Keen.VRage.Render12.Resources.BindableTextures.BackBuffer");
        type.PrintFields();
        _d3dResourceField = AccessTools.Field(type, "_d3dResource");
    }

    public ID3D12Resource GetResource() => (ID3D12Resource)_d3dResourceField.GetValue(obj)!;
}
