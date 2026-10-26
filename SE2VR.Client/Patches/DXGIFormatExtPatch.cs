using HarmonyLib;
using Keen.VRage.Render.Utils;
using Vortice.DXGI;

/// <summary>
/// Patch to fix Keen's hacky intel graphics fix that breaks compatibility with OpenVR on Proton
/// </summary>
[HarmonyPatch(typeof(DXGIFormatExt), "ToResourceFormat")]
public class DXGIFormatExtPatch
{
    public static bool ForceFormat = false;

    public static bool Prefix(Format format, ref Format __result)
    {
        if (ForceFormat)
        {
            __result = format;
            return false;
        }

        return true;
    }
}
