using System.Reflection;
using Keen.Game2.Client.GameSystems.CameraSystems;
using Keen.Game2.Simulation;
using Keen.VRage.Core.EngineComponents;
using Keen.VRage.Core.Game.Components;
using Keen.VRage.Core.Systems;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.Library.Reflection.DependencyInjections;
using SE2VR.Simulation;
using Valve.VR;

namespace SE2VR.Client.Components;

/// <summary>
/// Session component for vibrating the controllers based on current camera shakes
/// https://github.com/ValveSoftware/openvr/wiki/SteamVR-Input
/// </summary>
public partial class HapticsSessionComponent : SessionComponent
{
    //Someone should balance these values
    private const float MAX_SHAKE = 0.5f;
    private const float MIN_FREQUENCY = 80.0f;
    private const float MAX_FREQUENCY = 200.0f;
    private const float AMPLITUDE_CURVE = 1.5f;
    private const float AMPLITUDE_MULTIPLIER = 4f;

    [Before(typeof(RenderSubmissionBegin))]
    [After(typeof(RenderCameraUpdate))]
    private class HapticsUpdate : JobGroup;

    [Service]
    private readonly IOptions _options;

    private OpenVROptions _vrOptions;

    [Init]
    protected new void Init()
    {
        base.Init();
        _vrOptions = _options.GetOrCreatePart<OpenVROptions>();
    }

    [HapticsUpdate]
    private void Shake()
    {
        if (!_vrOptions.Haptics || Session.GetEntitiesOfType<CameraShakeComponent>().FirstOrDefault() is not { } entity)
            return;

        var shakes = entity.Get<CameraShakeComponent>();

        if (!shakes.Shaking)
            return;

        ulong handle = 0;
        var error = OpenVR.Input.GetActionHandle("/actions/default/out/Haptic", ref handle);
        if (error != EVRInputError.None)
            Logging.Error($"GetActionHandle {error}");

        var shakePowerField = typeof(CameraShakeComponent).GetField("_shakePosPower", BindingFlags.NonPublic | BindingFlags.Instance)!;
        float shakePower = (float)shakePowerField.GetValue(shakes)!;
        float normalizedPower = Math.Clamp(shakePower / MAX_SHAKE, 0.0f, 1.0f);

        if (normalizedPower <= 0.01f)
            return;

        float amplitude = (float)Math.Pow(normalizedPower, AMPLITUDE_CURVE) * AMPLITUDE_MULTIPLIER;
        float frequency = MIN_FREQUENCY + (MAX_FREQUENCY - MIN_FREQUENCY) * normalizedPower;
        OpenVR.Input.TriggerHapticVibrationAction(handle, 0, UpdateTime.SECONDS_PER_STEP * 2, frequency, amplitude, 0);
    }
}
