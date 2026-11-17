using System.Reflection;
using HarmonyLib;

namespace SE2VR.Client.Wrappers;

/// <summary>
/// CoreSystems is an internal class, this is MY Core Systems, not keens.
/// Used to acccess internal type
/// </summary>
public static class MyCoreSystems
{
#pragma warning disable KN023
    private static readonly FieldInfo _deviceContextField;
    private static readonly FieldInfo _sceneDrawSystemField;
    private static readonly FieldInfo _settingsField;
    private static readonly FieldInfo _commonResourcesField;
    private static readonly FieldInfo _drawContextsField;
    private static readonly FieldInfo _frameDispatcherField;
    private static readonly FieldInfo _bindableTexturesField;
    private static readonly FieldInfo _gpuResourcePoolField;
    private static readonly FieldInfo _screenBuffersField;
    private static readonly FieldInfo _timeField;
    private static readonly PropertyInfo _animationIsPausedProperty;
    private static readonly PropertyInfo _finalLDRTextureProperty;
    private static readonly MethodInfo _createDirectCommandListMethod;
    private static readonly MethodInfo _flushAllQueuesMethod;
    private static readonly MethodInfo _flushQueuesMethod;
    private static readonly MethodInfo _commonResourcesBeginDrawMethod;
    private static readonly MethodInfo _commonResourcesEndDrawMethod;
    private static readonly MethodInfo _drawContextsBeginDrawMethod;
    private static readonly MethodInfo _drawContextsEndDrawMethod;
    private static readonly FieldInfo _mainUISystemField;
    private static readonly MethodInfo _mainUISystemDoWorkMethod;
#pragma warning restore KN023

    public static MyDeviceContext DeviceContext => new(_deviceContextField.GetValue(null));
    public static MySceneDrawSystem SceneDrawSystem => new(_sceneDrawSystemField.GetValue(null));
    public static MySettings Settings => new(_settingsField.GetValue(null));
    public static object? CommonResources => _commonResourcesField.GetValue(null);
    public static object? DrawContexts => _drawContextsField.GetValue(null);
    public static object? BindableTextures => _bindableTexturesField.GetValue(null);
    public static object? GPUResourcePool => _gpuResourcePoolField.GetValue(null);

    public static MyTexture FinalLDRTexture => new(_finalLDRTextureProperty.GetValue(_screenBuffersField.GetValue(null))!);

    public static bool AnimationIsPaused
    {
        get => (bool)_animationIsPausedProperty.GetValue(_timeField.GetValue(null))!;
        set => _animationIsPausedProperty.SetValue(_timeField.GetValue(null), value);
    }

    static MyCoreSystems()
    {
        var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.CoreSystems");
        _deviceContextField = AccessTools.Field(type, "DeviceContext");
        _sceneDrawSystemField = AccessTools.Field(type, "SceneDrawSystem");
        _settingsField = AccessTools.Field(type, "Settings");
        _commonResourcesField = AccessTools.Field(type, "CommonResources");
        _drawContextsField = AccessTools.Field(type, "DrawContexts");
        _commonResourcesBeginDrawMethod = AccessTools.Method(_commonResourcesField.FieldType, "OnBeginDraw");
        _commonResourcesEndDrawMethod = AccessTools.Method(_commonResourcesField.FieldType, "OnEndDraw");
        _drawContextsBeginDrawMethod = AccessTools.Method(_drawContextsField.FieldType, "OnBeginDraw");
        _drawContextsEndDrawMethod = AccessTools.Method(_drawContextsField.FieldType, "OnEndDraw");
        _frameDispatcherField = AccessTools.Field(type, "FrameDispatcher");
        _bindableTexturesField = AccessTools.Field(type, "BindableTextures");
        _gpuResourcePoolField = AccessTools.Field(type, "GPUResourcePool");
        _screenBuffersField = AccessTools.Field(type, "ScreenBuffers");
        _timeField = AccessTools.Field(type, "Time");
        _animationIsPausedProperty = AccessTools.Property(_timeField.FieldType, "AnimationIsPaused");
        _finalLDRTextureProperty = AccessTools.Property(_screenBuffersField.FieldType, "FinalLDRTexture");
        _mainUISystemField = AccessTools.Field(type, "MainUISystem");
        _mainUISystemDoWorkMethod = AccessTools.Method(_mainUISystemField.FieldType, "DoWork");

        var frameDispatcher = AccessTools.TypeByName("Keen.VRage.Render12.Core.Systems.FrameDispatcher");
        _createDirectCommandListMethod = AccessTools.Method(frameDispatcher, "CreateDirectCommandList");
        _flushAllQueuesMethod = AccessTools.Method(frameDispatcher, "FlushAllQueuesAndWaitCpu", []);
        _flushQueuesMethod = AccessTools.Method(frameDispatcher, "FlushQueues");
    }

    public static MyCommandList CreateDirectCommandList(string name) => new(_createDirectCommandListMethod.Invoke(_frameDispatcherField.GetValue(null), [name, true]));

    public static void FlushAllQueuesAndWaitCpu() => _flushAllQueuesMethod.Invoke(_frameDispatcherField.GetValue(null), null);

    public static void FlushQueues() => _flushQueuesMethod.Invoke(_frameDispatcherField.GetValue(null), null);

    public static void DrawUI(MyCommandList commandList, MyTexture target) => _mainUISystemDoWorkMethod.Invoke(_mainUISystemField.GetValue(null), [commandList.Instance, target.Instance, target.Resolution]);

    public static void BeginDraw()
    {
        Settings.OnBeginDraw();
        _commonResourcesBeginDrawMethod.Invoke(CommonResources, null);
        _drawContextsBeginDrawMethod.Invoke(DrawContexts, null);
    }

    public static void EndDraw()
    {
        _drawContextsEndDrawMethod.Invoke(DrawContexts, null);
        _commonResourcesEndDrawMethod.Invoke(CommonResources, null);
        Settings.OnEndDraw();
    }
}

