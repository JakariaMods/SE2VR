using Keen.Game2.Simulation.WorldObjects.Weapons;
using Keen.VRage.Core.Game.Components;
using Keen.VRage.Core.Game.GameSystems.GameTimes;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.DCS.Components;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Reflection.DependencyInjections;
using SE2VR.Client.Configurations;
using SE2VR.Client.Patches;
using Valve.VR;

namespace SE2VR.Client.Components;

/// <summary>
/// Session component that rotates the hands when a gun is fired (instead of the body/camera in vanilla)
/// </summary>
public partial class VRRecoilSessionComponent : SessionComponent
{
    [Configuration]
    private readonly VRRecoilConfiguration _config;

    [Service]
    private readonly HapticsSessionComponent _haptics;

    [Service]
    private readonly IGameTime _gameTime;

    [Component(WeakDependency = true)]
    private readonly OpenVRSessionComponent _vrSession;

    private Vector2 _pendingRecoil;
    private float _recoilSecondsRemaining;
    private Vector2 _currentRecoil;

    [Init]
    protected new void Init()
    {
        base.Init();
        CharacterMovementControlComponentPatch.OnRecoil += OnRecoil;
    }

    [Destructor]
    protected void Destroy()
    {
        CharacterMovementControlComponentPatch.OnRecoil -= OnRecoil;
    }

    private void OnRecoil(Entity source, RecoilKick kick, RecoilPatternDefinition pattern)
    {
        var angles = new Vector2(kick.PitchRadians, kick.YawRadians);
        _pendingRecoil += angles * _config.RecoilMultiplier;
        _recoilSecondsRemaining = pattern.KickApplySeconds;

        float amplitude = Math.Clamp(angles.Length() * pattern.ShakePowerPerRadian, 0f, 1f);

        if (_vrSession.TwoHanded)
        {
            amplitude /= 2;
            _haptics.Vibrate(ETrackedControllerRole.LeftHand, amplitude);
        }

        _haptics.Vibrate(ETrackedControllerRole.RightHand, amplitude);
    }

    [OpenVRSessionComponent.ToolUpdateSystem.RecoilUpdate]
    private void Update()
    {
        float deltaSeconds = (float)_gameTime.DeltaTime.TotalSeconds;

        if (_recoilSecondsRemaining > 0)
        {
            float progress = Math.Min(deltaSeconds / _recoilSecondsRemaining, 1f);
            _currentRecoil += _pendingRecoil * progress;
            _pendingRecoil -= _pendingRecoil * progress;
            _recoilSecondsRemaining -= deltaSeconds;
        }

        _currentRecoil *= MathF.Exp(-_config.RecoveryRate * deltaSeconds);
    }

    /// <summary>
    /// Rotation of the recoil, pitching up and yawing right relative to <paramref name="aim"/>
    /// </summary>
    public Quaternion GetRotation(Quaternion aim)
    {
        var yaw = Quaternion.CreateFromAxisAngle(aim.GetUp(), -_currentRecoil.Y);
        var pitch = Quaternion.CreateFromAxisAngle(aim.GetRight(), _currentRecoil.X);
        return yaw * pitch;
    }
}
