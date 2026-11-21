using System.Reflection;
using HarmonyLib;

namespace SE2VR.Client.Wrappers;

/// <summary>
/// IRCacheResourcesManager wrapper
/// </summary>
public static class MyIRCacheResources
{
#pragma warning disable KN023
    private static readonly FieldInfo _irCacheResourcesField;
    private static readonly FieldInfo _gridSettingsFrameIdField;
#pragma warning restore KN023

    static MyIRCacheResources()
    {
        var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.CoreSystems");
        _irCacheResourcesField = AccessTools.Field(type, "IRCacheResources");
        _gridSettingsFrameIdField = AccessTools.Field(_irCacheResourcesField.FieldType, "_gridSettingsFrameId");
    }

    /// <summary>
    /// Invalidate ir cache so that it is recomputed for the correct eye
    /// </summary>
    public static void InvalidateView()
    {
        if (_irCacheResourcesField.GetValue(null) is { } resources)
            _gridSettingsFrameIdField.SetValue(resources, -1);
    }
}
