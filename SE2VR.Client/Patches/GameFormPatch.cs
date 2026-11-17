using HarmonyLib;
using Keen.VRage.Library.Mathematics;

namespace SE2VR.Client.Patches;

/// <summary>
/// Patch to override the mouse position that is reported to the game
/// </summary>
[HarmonyPatch("Keen.VRage.Platform.Windows.Forms.GameForm", "ClientMousePosition", MethodType.Getter)]
public static class GameFormPatch
{
    public static Vector2? CursorPosition;

    [HarmonyPostfix]
    public static void Postfix(ref Vector2 __result)
    {
        if (CursorPosition is { } position)
            __result = position;
    }
}
