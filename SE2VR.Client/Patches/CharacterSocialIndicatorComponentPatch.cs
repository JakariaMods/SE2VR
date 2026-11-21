using HarmonyLib;
using Keen.Game2.Client.WorldObjects.Character;

namespace SE2VR.Client.Patches;

/// <summary>
/// Patch to disable character status indicators
/// TODO #9
/// </summary>
[HarmonyPatch(typeof(CharacterSocialIndicatorComponent), "DrawIndicator")]
public class CharacterSocialIndicatorComponentPatch
{
    public static bool Prefix()
    {
        return false;
    }
}
