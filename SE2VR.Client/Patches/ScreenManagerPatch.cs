using Avalonia.Controls;
using Avalonia.Media;
using HarmonyLib;
using Keen.VRage.UI.Screens;

namespace SE2VR.Client.Patches;

/// <summary>
/// Patch to rescale the UI when in gameplay (so that it is somewhat visible ingame)
/// </summary>
[HarmonyPatch(typeof(ScreenManager), "OnMainWindowCreated")]
public static class ScreenManagerPatch
{
    public static Window Window = null!;

    [HarmonyPrefix]
    public static void Prefix(Window mainWindow)
    {
        Window = mainWindow;

        GameWindowPatch.OnCursorVisibleChanged += OnCursorVisibleChanged;
        UpdateResolution();
    }

    public static void UpdateResolution()
    {
        var rootPanel = Window.FindControl<Grid>("RootPanel")!;
        if (GameWindowPatch.CursorVisible)
        {
            rootPanel.RenderTransform = null;
        }
        else
        {
            rootPanel.RenderTransform = new ScaleTransform(0.45, 0.45);
        }
    }

    private static void OnCursorVisibleChanged(bool obj)
    {
        UpdateResolution();
    }

}