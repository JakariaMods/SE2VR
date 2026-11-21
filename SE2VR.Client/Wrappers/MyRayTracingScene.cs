using System.Reflection;
using HarmonyLib;

namespace SE2VR.Client.Wrappers;

/// <summary>
/// RayTracingSceneManager wrapper
/// </summary>
public static class MyRayTracingScene
{

#pragma warning disable KN023
    private static readonly FieldInfo _rayTracingSceneField;
    private static readonly FieldInfo _tlasBuffersField;

    public static object? TLASBuffers
    {
        get => _rayTracingSceneField.GetValue(null) is { } scene ? _tlasBuffersField.GetValue(scene) : null;
        set => _tlasBuffersField.SetValue(_rayTracingSceneField.GetValue(null), value);
    }
#pragma warning restore KN023

    static MyRayTracingScene()
    {
        var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.CoreSystems");
        _rayTracingSceneField = AccessTools.Field(type, "RayTracingScene");
        _tlasBuffersField = AccessTools.Field(_rayTracingSceneField.FieldType, "_tlasBuffers");
    }

    /// <summary>
    /// Create an empty version of <see cref="TLASBuffers"/>
    /// </summary>
    public static object CreateEmptyTLASBuffers(object template)
    {
        var buffers = (Array)template;
        return Array.CreateInstance(buffers.GetType().GetElementType()!, buffers.Length);
    }
}
