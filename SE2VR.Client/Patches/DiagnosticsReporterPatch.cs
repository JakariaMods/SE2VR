using HarmonyLib;
using Keen.VRage.Core.Platform.CrashReporting;

/// <summary>
/// Patch to disable crash reports
/// </summary>
[HarmonyPatch(typeof(DiagnosticReporter), "Active", MethodType.Getter)]
public static class DiagnosticReporterActivePatch
{
    public static bool Prefix(ref bool __result)
    {
        __result = false;
        return false;
    }
}