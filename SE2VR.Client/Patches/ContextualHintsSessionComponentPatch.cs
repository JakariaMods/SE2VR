using HarmonyLib;
using Keen.Game2.Client.GameSystems;
using Keen.Game2.Simulation.GameSystems.UI.ContextualHints;

namespace SE2VR.Client.Patches;

/// <summary>
/// Patch to disable tutorial hints
/// </summary>
[HarmonyPatch(typeof(ContextualHintsSessionComponent), nameof(ContextualHintsSessionComponent.ShowHint), [typeof(ContextualHintDefinition), typeof(TimeSpan)])]
public class ContextualHintsSessionComponentPatch
{
    public static bool Prefix()
    {
        return false;
    }
}
