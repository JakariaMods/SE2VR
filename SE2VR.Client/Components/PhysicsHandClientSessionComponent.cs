using Keen.Game2.Simulation.Replication;
using Keen.Game2.Simulation.WorldObjects.Characters;
using Keen.VRage.Core;
using Keen.VRage.Core.Game.Components;
using Keen.VRage.Core.Systems;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.DCS.Components;
using Keen.VRage.Library.Reflection.DependencyInjections;
using Keen.VRage.Multiplayer.Sessions;
using SE2VR.Client.Configurations;
using SE2VR.Client.Input;
using static SE2VR.Simulation.Components.PhysicsHandServerSessionComponent;

namespace SE2VR.Client.Components;

/// <summary>
/// Client session component that syncs hand grips and grab inputs to the server
/// </summary>
public partial class PhysicsHandClientSessionComponent : SessionComponent
{
    [Before(typeof(OnDataReplication))]
    private class WriteHandDataJob : JobGroup;

    [Configuration]
    private readonly VRToolsConfiguration _tools;

    [Service]
    private readonly OpenVRSessionComponent _vrSession;

    private Entity? _lastCharacter;

    [WriteHandDataJob]
    private void WriteHandData()
    {
        var character = _vrSession.ControlledEntity.Entity is { } controlled && controlled.TryGet<CharacterComponent>() != null ? controlled : null;
        if (character != _lastCharacter) //todo use lifetime event
        {
            if (_lastCharacter != null && Session.IsEntityInScene(_lastCharacter))
                _lastCharacter.Data.TryRemove<VRHandData>();

            _lastCharacter = character;
        }

        if (character == null)
            return;

        var vr = OpenVREngineComponent.Instance;
        var input = OpenVRInputEngineComponent.Instance;

        var left = _vrSession.LeftHandOverride ?? vr.LeftHand;
        var right = vr.RightHand;

        character.Data.Set(new VRHandData
        {
            Head = vr.Head ?? RelativeTransform.Identity,
            HeadTracked = vr.Head.HasValue,

            LeftHand = left.HasValue ? left.Value * _tools.LeftControllerGrip : RelativeTransform.Identity,
            LeftTracked = left.HasValue,
            LeftGrab = left.HasValue && input.LeftGrab && !_vrSession.TwoHanded,

            RightHand = right.HasValue ? right.Value * _tools.RightControllerGrip : RelativeTransform.Identity,
            RightTracked = right.HasValue,
            RightGrab = right.HasValue && input.RightGrab,

            TwoHanded = _vrSession.TwoHanded
        });
    }

    [OnDataReplication.ClientToServer]
    [DoesNotHave(typeof(NoAutoSync))]
    [OnAdded(typeof(VRHandData))]
    [OnChanged(typeof(VRHandData))]
    private static void SyncHandData(Entity entity, in VRHandData hands, IDataReplication replication)
    {
        replication.Override(entity, hands);
    }

    [OnDataReplication.ClientToServer]
    [DoesNotHave(typeof(NoAutoSync))]
    [OnRemoved(typeof(VRHandData))]
    private static void RemoveHandData(Entity entity, IDataReplication replication)
    {
        replication.Remove<VRHandData>(entity);
    }
}
