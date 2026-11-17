using Keen.VRage.Library.Mathematics;
using OpenVRAPI;
using SE2VR.Simulation;
using Valve.VR;

namespace SE2VR.Client;

/// <summary>
/// VR overlay in VR-local space, drawn on top of the scene.
/// If creation fails the handle stays invalid and every call is ignored by <see cref="OpenVR"/>.
/// </summary>
public sealed class VROverlay : IDisposable
{
    private const ETrackingUniverseOrigin ORIGIN = ETrackingUniverseOrigin.TrackingUniverseStanding;

    private readonly ulong _overlayHandle;
    private Vector2 _mouseScale = Vector2.One;

    public bool Visible { get; private set; }

    public VROverlay(string key, string name)
    {
        var error = OpenVR.Overlay.CreateOverlay(key, name, ref _overlayHandle);
        if (error != EVROverlayError.None)
            Logging.Error($"Failed to create overlay '{key}': {error}");
    }

    public void Dispose()
    {
        OpenVR.Overlay?.DestroyOverlay(_overlayHandle);
    }

    public void Show()
    {
        if (Visible)
            return;

        OpenVR.Overlay.ShowOverlay(_overlayHandle);
        Visible = true;
    }

    public void Hide()
    {
        if (!Visible)
            return;

        OpenVR.Overlay.HideOverlay(_overlayHandle);
        Visible = false;
    }

    public void SetWidth(float meters)
    {
        OpenVR.Overlay.SetOverlayWidthInMeters(_overlayHandle, meters);
    }

    public void SetFlag(VROverlayFlags flag, bool enabled)
    {
        OpenVR.Overlay.SetOverlayFlag(_overlayHandle, flag, enabled);
    }

    public void SetTransform(Matrix trackingTransform)
    {
        var matrix = trackingTransform.ToHmdMatrix();
        OpenVR.Overlay.SetOverlayTransformAbsolute(_overlayHandle, ORIGIN, ref matrix);
    }

    public void SetTexture(ref Texture_t texture)
    {
        var error = OpenVR.Overlay.SetOverlayTexture(_overlayHandle, ref texture);
        if (error != EVROverlayError.None)
            Logging.Debug("Setting overlay texture failed: {0}", error);
    }

    public void SetMouseScale(Vector2 size)
    {
        if (size == _mouseScale)
            return;

        var scale = new HmdVector2_t { v0 = size.X, v1 = size.Y };
        OpenVR.Overlay.SetOverlayMouseScale(_overlayHandle, ref scale);
        _mouseScale = size;
    }

    public void SetCursorPosition(Vector2 position)
    {
        var cursor = new HmdVector2_t { v0 = position.X, v1 = _mouseScale.Y - position.Y };
        OpenVR.Overlay.SetOverlayCursorPositionOverride(_overlayHandle, ref cursor);
    }

    public void ClearCursorPosition()
    {
        OpenVR.Overlay.ClearOverlayCursorPositionOverride(_overlayHandle);
    }
}
