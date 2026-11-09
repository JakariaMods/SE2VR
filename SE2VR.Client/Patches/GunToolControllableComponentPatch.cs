using HarmonyLib;
using Keen.Game2.Client.WorldObjects.Tools;

namespace SE2VR.Client.Patches;

/// <summary>
/// Patch to access aim down sights state of <see cref="GunToolControllableComponent"/>
/// </summary>
public static class GunToolControllableComponentPatch
{
    private static readonly AccessTools.FieldRef<GunToolControllableComponent, bool> _wasAds = AccessTools.FieldRefAccess<GunToolControllableComponent, bool>("_wasAds");

    public static bool IsAimingDownSights(this GunToolControllableComponent gun) => _wasAds(gun);
}
