using Keen.VRage.Core;
using Keen.VRage.Core.Model;
using Keen.VRage.Library.Definitions;

namespace SE2VR.Client.Configurations;

/// <summary>
/// Configuration for aligning hand tools with the VR controllers.
/// </summary>
public partial class VRToolsConfiguration : Configuration
{
    /// <summary>
    /// Dummy that defines where the tool should shoot/interact from
    /// </summary>
    public DummyTypeDefinition BarrelDummy { get; private set; }

    /// <summary>
    /// Dummy that defines where the hand grips the tool
    /// </summary>
    public DummyTypeDefinition HandDummy { get; private set; }

    /// <summary>
    /// Dummy that defines a secondary grip for two-handed tools
    /// </summary>
    public DummyTypeDefinition? SecondaryGripDummy { get; private set; }

    /// <summary>
    /// Grip of the left hand relative to the tracked (raw) controller pose, that <see cref="SecondaryGripDummy"/> is aligned with
    /// </summary>
    public RelativeTransformWithEulerHint LeftControllerGrip { get; private set; } = RelativeTransformWithEulerHint.Identity;

    /// <summary>
    /// Grip of the right hand relative to the tracked (raw) controller pose, that <see cref="HandDummy"/> is aligned with
    /// </summary>
    public RelativeTransformWithEulerHint RightControllerGrip { get; private set; } = RelativeTransformWithEulerHint.Identity;

    /// <summary>
    /// Interaction origin relative to the controller origin, used when the held tool has no <see cref="BarrelDummy"/>
    /// </summary>
    public RelativeTransformWithEulerHint ControllerAim { get; private set; } = RelativeTransformWithEulerHint.Identity;

    /// <summary>
    /// Interaction origin relative to the controller origin while building with <see cref="OpenVROptions.PalmBuilding"/>, facing out of the palm
    /// </summary>
    public RelativeTransformWithEulerHint ControllerPalm { get; private set; } = RelativeTransformWithEulerHint.Identity;
}
