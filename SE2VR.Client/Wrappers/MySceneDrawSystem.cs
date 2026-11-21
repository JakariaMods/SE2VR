using System.Reflection;
using HarmonyLib;

namespace SE2VR.Client.Wrappers;

/// <summary>
/// My SceneDrawSystem
/// Used to acccess internal type
/// </summary>
public class MySceneDrawSystem(object? obj)
{
#pragma warning disable KN023
    private static readonly MethodInfo _drawMethod;
    private static readonly FieldInfo _upsamplingJobField;
    private static readonly FieldInfo _fsrField;
    private static readonly FieldInfo _dlssField;
#pragma warning restore KN023

    public MyFsr Fsr => new(_upsamplingJobField.GetValue(obj) is { } job ? _fsrField.GetValue(job) : null);

    public MyDlss Dlss => new(_upsamplingJobField.GetValue(obj) is { } job ? _dlssField.GetValue(job) : null);

    static MySceneDrawSystem()
    {
        var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.Systems.SceneDrawSystem");
        _drawMethod = AccessTools.Method(type, "Draw");
        _upsamplingJobField = AccessTools.Field(type, "_upsamplingJob");
        var upsamplingJobType = AccessTools.TypeByName("Keen.VRage.Render12.PostProcessStage.Upsampling.UpsamplingJob");
        _fsrField = AccessTools.Field(upsamplingJobType, "_fsr4_1");
        _dlssField = AccessTools.Field(upsamplingJobType, "_dlss");
    }

    public void Draw(object finalLDRBuffer) => _drawMethod.Invoke(obj, [finalLDRBuffer]);
}
