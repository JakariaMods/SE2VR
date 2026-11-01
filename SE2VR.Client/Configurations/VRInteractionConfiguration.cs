using Keen.Game2.Client.GameSystems.Interaction;
using Keen.Game2.Simulation.WorldObjects.Shared;
using Keen.VRage.DCS.Components;
using Keen.VRage.Library.Collections.Readers;
using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Library.UI.PropertyInspector;
using Keen.VRage.Library.Units;

namespace SE2VR.Client.Configurations;

/// <summary>
/// Configuration for interacting with use dummies
/// </summary>
public partial class VRInteractionConfiguration : Configuration
{
    /// <summary>
    /// Radius of the hand around its grip point. <see cref="VRToolsConfiguration.RightControllerGrip"/>.
    /// </summary>
    [Length]
    public float HandRadius { get; private set; } = 0.03f;

    /// <summary>
    /// Extra size added to <see cref="VRInteractionMode.Touch"/> dummies where they are highlighted, before the hand touches them
    /// </summary>
    [Length]
    public float TouchHoverMargin { get; private set; } = 0.05f;

    /// <summary>
    /// Time after a touch before the same hand can touch again
    /// </summary>
    [TimeSpanScope(Scope = TimeSpanScope.Milliseconds)]
    public TimeSpan TouchCooldown { get; private set; } = TimeSpan.FromSeconds(0.3);

    /// <summary>
    /// Haptic amplitude when a hand starts hovering an interaction
    /// </summary>
    public float HoverHapticAmplitude { get; private set; } = 0.15f;

    /// <summary>
    /// Haptic amplitude when an interaction is activated
    /// </summary>
    public float UseHapticAmplitude { get; private set; } = 0.5f;

    /// <summary>
    /// How each use action is used with the hands. Actions that aren't listed use <see cref="DefaultMode"/>
    /// </summary>
    public DictionaryReader<UseActionDefinition, VRInteractionModes> Modes { get; private set; } = new Dictionary<UseActionDefinition, VRInteractionModes>();

    /// <summary>
    /// Modes of <see cref="UseActionDefinition"/>s that aren't in <see cref="Modes"/>
    /// </summary>
    public VRInteractionModes DefaultMode { get; private set; } = new() { Root = VRInteractionMode.Trigger };

    /// <summary>
    /// Gets the interaction mode the given dummy uses
    /// </summary>
    public VRInteractionMode GetMode(Entity entity, UseActionDefinition definition)
    {
        var modes = Modes.TryGetValue(definition, out var configured) ? configured : DefaultMode;

        if (entity.SingleOrDefault<IDummyInteractionRoot>() == null)
            return modes.Subpart ?? modes.Root;

        return modes.Root;
    }
}

/// <summary>
/// How a use action is activated with the hands
/// </summary>
[Serialize]
public partial struct VRInteractionModes
{
    /// <summary>
    /// Mode when the dummy is on the main entity (block, character, floating object)
    /// </summary>
    public VRInteractionMode Root;

    /// <summary>
    /// Mode when the dummy is on a subpart of the main entity (doors)
    /// </summary>
    public VRInteractionMode? Subpart;
}

/// <summary>
/// Hand gestures that use an interaction dummy
/// </summary>
[Flags]
public enum VRInteractionMode
{
    /// <summary>
    /// The dummy can't be used by the hands
    /// </summary>
    None = 0,

    /// <summary>
    /// Used once when the hand touches the dummy
    /// </summary>
    Touch = 1 << 0,

    /// <summary>
    /// Used by pressing the trigger with the hand in the dummy
    /// </summary>
    Trigger = 1 << 1,

    /// <summary>
    /// Used by pressing the grip with the hand in the dummy
    /// </summary>
    Grip = 1 << 2,

    /// <summary>
    /// Every gesture
    /// </summary>
    All = Touch | Trigger | Grip,
}
