using Valve.VR;

namespace SE2VR.Client;

/// <summary>
/// Overlay that clones the game's screen so that UI is completely visible
/// </summary>
public class SimpleOverlay : IDisposable
{
    private const string APP_KEY = "Space_Engineers_2";
    private const string APP_NAME = "Space Engineers 2";

    private ulong _overlayHandle;

    public SimpleOverlay()
    {
        OpenVR.Overlay.CreateOverlay(APP_KEY, APP_NAME, ref _overlayHandle);
        OpenVR.Overlay.SetOverlayWidthInMeters(_overlayHandle, 1);
        
        Show();
    }

    public void Dispose()
    {
        OpenVR.Overlay?.DestroyOverlay(_overlayHandle);
    }

    public void Show()
    {
        var poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        OpenVR.System.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0, poses);
        HmdMatrix34_t hmdPose = poses[OpenVR.k_unTrackedDeviceIndex_Hmd].mDeviceToAbsoluteTracking;
        var offsetMatrix = new HmdMatrix34_t
        {
            m0 = 1,
            m1 = 0,
            m2 = 0,
            m3 = 0,
            m4 = 0,
            m5 = 1,
            m6 = 0,
            m7 = 0,
            m8 = 0,
            m9 = 0,
            m10 = 1,
            m11 = -1.0f
        };

        HmdMatrix34_t overlayMatrix = MultiplyMatrices(hmdPose, offsetMatrix);
        OpenVR.Overlay.SetOverlayTransformAbsolute(_overlayHandle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref overlayMatrix);
        OpenVR.Overlay.ShowOverlay(_overlayHandle);
    }

    public void Hide()
    {
        OpenVR.Overlay.HideOverlay(_overlayHandle);
    }

    public void SetTexture(ref Texture_t texture)
    {
        OpenVR.Overlay.SetOverlayTexture(_overlayHandle, ref texture);
    }

    private static HmdMatrix34_t MultiplyMatrices(HmdMatrix34_t matA, HmdMatrix34_t matB)
    {
        return new HmdMatrix34_t()
        {
            m0 = matA.m0 * matB.m0 + matA.m1 * matB.m4 + matA.m2 * matB.m8,
            m1 = matA.m0 * matB.m1 + matA.m1 * matB.m5 + matA.m2 * matB.m9,
            m2 = matA.m0 * matB.m2 + matA.m1 * matB.m6 + matA.m2 * matB.m10,
            m3 = matA.m0 * matB.m3 + matA.m1 * matB.m7 + matA.m2 * matB.m11 + matA.m3,

            m4 = matA.m4 * matB.m0 + matA.m5 * matB.m4 + matA.m6 * matB.m8,
            m5 = matA.m4 * matB.m1 + matA.m5 * matB.m5 + matA.m6 * matB.m9,
            m6 = matA.m4 * matB.m2 + matA.m5 * matB.m6 + matA.m6 * matB.m10,
            m7 = matA.m4 * matB.m3 + matA.m5 * matB.m7 + matA.m6 * matB.m11 + matA.m7,

            m8 = matA.m8 * matB.m0 + matA.m9 * matB.m4 + matA.m10 * matB.m8,
            m9 = matA.m8 * matB.m1 + matA.m9 * matB.m5 + matA.m10 * matB.m9,
            m10 = matA.m8 * matB.m2 + matA.m9 * matB.m6 + matA.m10 * matB.m10,
            m11 = matA.m8 * matB.m3 + matA.m9 * matB.m7 + matA.m10 * matB.m11 + matA.m11,
        };
    }
}
