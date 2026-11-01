using System.Reflection;
using HarmonyLib;
using Keen.Game2.Client.GameSystems.Interaction;
using Keen.Game2.Client.GameSystems.PlayerControl;
using Keen.Game2.Client.UI.HUD.Interaction;
using Keen.Game2.Client.UI.InGame;
using Keen.VRage.Library.Utils;
using Keen.VRage.UI.Shared.ViewModels;

namespace SE2VR.Client.Patches;

/// <summary>
/// Patch for disabling the vanilla interaction entirely
/// </summary>
[HarmonyPatch]
public static class InteractorEntityComponent_UpdateInputContexts_Patch
{
    /// <summary>
    /// Should vanilla interaction be enabled?
    /// </summary>
    public static bool Enabled = true;

    public static MethodBase TargetMethod()
    {
        var map = typeof(InteractorEntityComponent).GetInterfaceMap(typeof(IInputConsumer));
        return map.TargetMethods[Array.IndexOf(map.InterfaceMethods, typeof(IInputConsumer).GetMethod(nameof(IInputConsumer.UpdateInputContexts)))];
    }

    public static bool Prefix()
    {
        return Enabled;
    }
}

/// <summary>
/// Patch for hiding interaction hints
/// </summary>
[HarmonyPatch(typeof(SessionInGameUISessionComponent), nameof(SessionInGameUISessionComponent.CreateInteractionHint), [typeof(string), typeof(InteractionHintScreenViewModel)], [ArgumentType.Normal, ArgumentType.Out])]
public static class InGameUI_CreateInteractionHint_Patch
{
    private static readonly FieldInfo _viewModelFactory = AccessTools.Field(typeof(SessionInGameUISessionComponent), "_viewModelFactory");

    public static bool Prefix(SessionInGameUISessionComponent __instance, string text, out InteractionHintScreenViewModel hint, ref IObservableDisposable __result)
    {
        hint = new InteractionHintScreenViewModel(text, (IViewModelFactory)_viewModelFactory.GetValue(__instance)!);
        __result = new ObservableDisposable();
        return false;
    }
}

/// <summary>
/// <inheritdoc cref="InGameUI_CreateInteractionHint_Patch"/>
/// </summary>
[HarmonyPatch(typeof(SessionInGameUISessionComponent), nameof(SessionInGameUISessionComponent.CreateInteractionHint), [typeof(InteractionHintScreenViewModel)])]
public static class InGameUI_CreateInteractionHintViewModel_Patch
{
    public static bool Prefix(ref IObservableDisposable __result)
    {
        __result = new ObservableDisposable();
        return false;
    }
}
