using Keen.VRage.Core.Model;
using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Units;

namespace SE2VR.Simulation.Configurations;

/// <summary>
/// Configuration for server-side physics hands that grabs small grids/floating objects/etc
/// TODO #2 physics hands
/// </summary>
public partial class PhysicsHandsConfiguration : Configuration
{
    /// <summary>
    /// Mass of an empty hand
    /// </summary>
    [Mass]
    public float HandMass { get; private set; } = 2f;

    /// <summary>
    /// Fastest a held object will move to catch up with the hand (relative to the body velocity)
    /// </summary>
    [Speed]
    public float MaxLinearSpeed { get; private set; } = 20f;

    /// <summary>
    /// Fastest a held object will rotate to catch up with the hand
    /// </summary>
    [AngularSpeed]
    public float MaxAngularSpeed { get; private set; } = 30f;

    /// <summary>
    /// Distance from the hand's center where objects can be grabbed
    /// </summary>
    [Length]
    public float GrabRadius { get; private set; } = 0.1f;

    /// <summary>
    /// Maximum distance after grabbing where the object will be released
    /// </summary>
    [Length]
    public float MaxGrabDistance { get; private set; } = 0.5f;

    /// <summary>
    /// How firmly held objects follow the hand. Higher value = stronger grip
    /// </summary>
    public float GrabStrength { get; private set; } = 5f;

    /// <summary>
    /// Dummy on equipped tools where the primary hand holds them
    /// </summary>
    public DummyTypeDefinition ToolGripDummy { get; private set; }

    /// <summary>
    /// How close the primary hand has to be to a dropped tool's <see cref="ToolGripDummy"/> to equip it instead of grabbing it
    /// TODO
    /// </summary>
    [Length]
    public float ToolEquipDistance { get; private set; } = 0.15f;
}
