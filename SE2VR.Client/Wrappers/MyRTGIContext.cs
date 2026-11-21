using System.Reflection;
using HarmonyLib;

namespace SE2VR.Client.Wrappers;

/// <summary>
/// RTGIContext wrapper
/// </summary>
public class MyRTGIContext
{
#pragma warning disable KN023
    private static readonly PropertyInfo _rtgiContextProperty;
    private static readonly ConstructorInfo _constructor;
    private static readonly FieldInfo _memoryHierarchyField;
    private static readonly FieldInfo _defaultRaytracingField;

    public static MyRTGIContext? CurrentContext
    {
        get
        {
            object? context = MyCoreSystems.DrawContexts is { } contexts ? _rtgiContextProperty.GetValue(contexts) : null;
            return context != null ? new MyRTGIContext(context) : null;
        }
        set => _rtgiContextProperty.SetValue(MyCoreSystems.DrawContexts, value!._instance);
    }
#pragma warning restore KN023

    private readonly object _instance;

    static MyRTGIContext()
    {
        var type = AccessTools.TypeByName("Keen.VRage.Render12.LightingStage.RTGIContext");
        _rtgiContextProperty = AccessTools.Property(AccessTools.TypeByName("Keen.VRage.Render12.Core.Systems.DrawContextManager"), "RTGIContext");
        _memoryHierarchyField = AccessTools.Field(AccessTools.TypeByName("Keen.VRage.Render12.Core.CoreSystems"), "MemoryHierarchy");
        _defaultRaytracingField = AccessTools.Field(_memoryHierarchyField.FieldType, "DefaultRaytracing");
        _constructor = AccessTools.GetDeclaredConstructors(type).Single();
    }

    private MyRTGIContext(object instance)
    {
        _instance = instance;
    }

    public static MyRTGIContext Create()
    {
        object? allocationGroup = _defaultRaytracingField.GetValue(_memoryHierarchyField.GetValue(null));
        return new MyRTGIContext(_constructor.Invoke([allocationGroup]));
    }

    public void Dispose() => ((IDisposable)_instance).Dispose();
}
