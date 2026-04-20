using Keen.VRage.Core;
using Keen.VRage.Core.EngineComponents;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Library.UI;
using Keen.VRage.Library.Units;

namespace SE2VR.Client;

/// <summary>
/// Client settings for <see cref="OpenVR"/>
/// </summary>
[Serialize]
public partial class OpenVROptions : ObservableObject, IOptionsPart
{
    private const float CHARACTER_HEIGHT = 1.75f;

    /// <summary>
    /// When true, a 3D representation of the crosshair will be drawn at the interaction point depending on where your primary hand is oriented.
    /// </summary>
    [Notify]
    private bool _showCrosshair = true;

    /// <summary>
    /// When true, the character's boots will show with animation. This can improve spatial (orientation) awareness within the scene.
    /// </summary>
    [Notify]
    private bool _showBoots = true;

    /// <summary>
    /// Rotates the scene on the yaw axis; used to allow the player to have a preferred orientation within their playspace.
    /// </summary>
    [Notify]
    private float _playSpaceRotation;

    /// <summary>
    /// Sensitivity multiplier for pitch and yaw rotation. Multiplies together with game sensitivity setting
    /// </summary>
    [Notify]
    private float _sensitivity = 10;

    /// <summary>
    /// Minimum height of the HMD for the character to crouch.
    /// </summary>
    [Notify, Length]
    private float _crouchHeight = 1;

    /// <summary>
    /// Height of the player in meters. Used to scale the player to a uniform height
    /// </summary>
    [Notify, Length]
    private float _playerHeight = CHARACTER_HEIGHT;

    /// <summary>
    /// When true, the HMD will be used as a flat-screen camera instead.
    /// </summary>
    [Notify]
    private bool _cameraMode;

    [NoSerialize]
    public Vector3 WorldOffset;

    /// <summary>
    /// When true, the vanilla game's camera shakes will be translated into controller vibrations
    /// </summary>
    [Notify]
    private bool _haptics = true;

    /// <summary>
    /// The name of the dashboard to open when the player opens any window. (Should be the one that the game is displayed in)
    /// </summary>
    [Notify]
    private string _dashboardWindow = "system.desktop.1";

    public void ForceSerialization()
    {
        OnPropertyChanged();
    }

    public float GetScale() => CHARACTER_HEIGHT / _playerHeight;

    public RelativeTransform Rescale(RelativeTransform transform)
    {
        transform.Position *= GetScale();
        return transform;
    }
}
