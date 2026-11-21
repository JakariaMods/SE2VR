using System.Reflection;
using HarmonyLib;
using Keen.VRage.Library.Mathematics;

namespace SE2VR.Client.Wrappers;

/// <summary>
/// My FSR upscaler
/// Used to acccess internal type
/// </summary>
public class MyFsr(object? obj) : IMyUpscaler
{
#pragma warning disable KN023
    private static readonly FieldInfo _contextField;
    private static readonly FieldInfo _resourcesField;
    private static readonly FieldInfo _forceFsr31Field;
    private static readonly PropertyInfo _finalResolutionProperty;
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
            object? resources = _resourcesField.GetValue(obj);
            Vector2I finalResolution = resources == null ? default : (Vector2I)_finalResolutionProperty.GetValue(resources)!;
            return (finalResolution, (bool)_forceFsr31Field.GetValue(obj)!);
        }
    }

    static MyFsr()
    {
        var type = AccessTools.TypeByName("Keen.VRage.Render12.PostProcessStage.Upsampling.FSR4.FSR4_1");
        var contextType = AccessTools.TypeByName("Keen.VRage.Render12.PostProcessStage.Upsampling.FSR4.FSR4_1Context");
        _contextField = AccessTools.Field(type, "_context");
        _resourcesField = AccessTools.Field(type, "_resources");
        _forceFsr31Field = AccessTools.Field(type, "_contextForceFsr31");
        _finalResolutionProperty = AccessTools.Property(_resourcesField.FieldType, "FinalResolution");
        _disposeContextMethod = AccessTools.Method(contextType, "Dispose");
    }

    public void DisposeContext(object context) => _disposeContextMethod.Invoke(context, null);
}
