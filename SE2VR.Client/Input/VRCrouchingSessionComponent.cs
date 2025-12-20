using Keen.Game2.Client.WorldObjects.Character;
using Keen.Game2.Simulation;
using Keen.Game2.Simulation.WorldObjects.Characters;
using Keen.VRage.Core.EngineComponents;
using Keen.VRage.Core.Game.Components;
using Keen.VRage.Core.Systems;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.Library.Reflection.DependencyInjections;
using static SE2VR.Client.OpenVREngineComponent;

namespace SE2VR.Client.Input;

/// <summary>
/// Client session component for automatically making the player crouch when the HMD height is beneath a certain value
/// </summary>
public partial class VRCrouchingSessionComponent : SessionComponent
{
    private static readonly Guid _crouchInput = new("d5ad431e-2d93-4570-937e-6388e2b42387");

    [Before(typeof(FrameUpdateSystem.VRageInputJob))]
    [After(typeof(VREngineTick))]
    private partial class VRCrouchDetection : JobGroup;

    [Service]
    private readonly IOptions _options;

    [Service]
    private readonly OpenVRSessionComponent _vrSession;

    private OpenVROptions _vrOptions;

    private OpenVREngineComponent _engine = OpenVREngineComponent.Instance;

    [Init]
    protected new void Init()
    {
        base.Init();

        _vrOptions = _options.GetOrCreatePart<OpenVROptions>();
    }

    [SparseUpdate(UpdateTime.UPDATE_STEPS_PER_SECOND)]
    [VRCrouchDetection]
    private void DetectCrouching()
    {
        if (_engine.Head is not { } head || _vrSession.ControlledEntity.Entity == null || _vrSession.ControlledEntity.Entity.TryGet<CharacterMovementControlComponent>() is not { } character)
            return;

        if (head.Position.Y < _vrOptions.CrouchHeight)
        {
            if (!character.Entity.IsCrouched())
                character.RequestCrouchToggle();
        }
        else
        {
            if (character.Entity.IsCrouched())
                character.RequestCrouchToggle();
        }
    }
}
