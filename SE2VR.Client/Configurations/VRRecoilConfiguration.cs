using Keen.VRage.Library.Definitions;

namespace SE2VR.Client.Configurations;

/// <summary>
/// Configuration for gun recoil in VR
/// </summary>
public partial class VRRecoilConfiguration : Configuration
{
    /// <summary>
    /// Multiplier of vanilla recoil
    /// </summary>
    public float RecoilMultiplier { get; private set; } = 4f;

    /// <summary>
    /// Exponential decay rate the tool returns to the controller's aim
    /// </summary>
    public float RecoveryRate { get; private set; } = 8f;
}
