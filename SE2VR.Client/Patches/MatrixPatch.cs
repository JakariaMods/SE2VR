using HarmonyLib;
using Keen.Game2.Client.GameSystems.CameraSystems;
using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Mathematics;
using OpenVRAPI;
using Valve.VR;

namespace SE2VR.Client.Patches;

/// <summary>
/// Patch for using the correct projection matrix in the camera
/// </summary>
[HarmonyPatch(typeof(Matrix), nameof(Matrix.CreatePerspectiveFovRhInfiniteComplementary))]
public static class MatrixPatch
{
    public static EVREye? CurrentEye;

    public static void Postfix(ref Matrix __result)
    {
        if (CurrentEye.HasValue && OpenVR.System != null)
            __result = (Matrix)VRUtils.GetPerspectiveFovRhInfiniteComplementary(CurrentEye.Value, DefinitionManager.Instance.GetDefinitionsOfType<CameraDefinition>().FirstOrDefault()?.NearPlane ?? 0.1f);
    }
}
