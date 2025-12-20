using HarmonyLib;
using Keen.VRage.Library.Extensions;

namespace SE2VR.Client.Patches;

/// <summary>
/// Patch that allows the ability to hook events for showing and hiding of the mouse cursor
/// </summary>
[HarmonyPatch("Keen.VRage.Platform.Windows.Forms.GameWindow", "IsCursorVisible", MethodType.Setter)]
public static class GameWindowPatch
{
    public static bool CursorVisible;
    public static event Action<bool>? OnCursorVisibleChanged;

    [HarmonyPostfix]
    public static void Postfix(bool value)
    {
        CursorVisible = value;
        OnCursorVisibleChanged.InvokeIfNotNull(value);
    }
}
