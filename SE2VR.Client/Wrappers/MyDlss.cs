using System.Reflection;
using HarmonyLib;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Render.Data;

namespace SE2VR.Client.Wrappers;

/// <summary>
/// My DLSS upscaler
/// </summary>
public class MyDlss(object? obj) : IMyUpscaler
{
#pragma warning disable KN023
    private static readonly FieldInfo _contextField;
    private static readonly FieldInfo _contextRenderSizeField;
    private static readonly FieldInfo _contextDisplaySizeField;
    private static readonly FieldInfo _contextQualityField;
    private static readonly MethodInfo _disposeContextMethod;
#pragma warning restore KN023

    public object? Context
    {
        get => obj == null ? null : _contextField.GetValue(obj);
        set => _contextField.SetValue(obj, value);
    }

    public object ContextKey
    {
        get
        {
            var renderSize = (Vector2I)_contextRenderSizeField.GetValue(obj)!;
            var displaySize = (Vector2I)_contextDisplaySizeField.GetValue(obj)!;
            var quality = (DLSSQuality)_contextQualityField.GetValue(obj)!;
            return (renderSize, displaySize, quality);
        }
    } 

    static MyDlss()
    {
        var type = AccessTools.TypeByName("Keen.VRage.Render12.PostProcessStage.Upsampling.DLSS.DLSSUpscaler");
        var contextType = AccessTools.TypeByName("Keen.VRage.Render12.PostProcessStage.Upsampling.DLSS.DLSSContext");
        _contextField = AccessTools.Field(type, "_context");
        _contextRenderSizeField = AccessTools.Field(type, "_contextRenderSize");
        _contextDisplaySizeField = AccessTools.Field(type, "_contextDisplaySize");
        _contextQualityField = AccessTools.Field(type, "_contextQuality");
        _disposeContextMethod = AccessTools.Method(contextType, "Dispose");
    }

    public void DisposeContext(object context) => _disposeContextMethod.Invoke(context, null);
}
