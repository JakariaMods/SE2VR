using Keen.Game2.Simulation;
using Keen.Game2.Simulation.Replication;
using Keen.Game2.Simulation.WorldObjects.Characters;
using Keen.Game2.Simulation.WorldObjects.FloatingObjects;
using Keen.Game2.Simulation.WorldObjects.Shared.Movement;
using Keen.VRage.Core;
using Keen.VRage.Core.Game.Components;
using Keen.VRage.Core.Game.Data;
using Keen.VRage.Core.Game.GameSystems.Queries;
using Keen.VRage.Core.Game.RuntimeSystems.Components;
using Keen.VRage.Core.Game.Systems;
using Keen.VRage.DCS.Accessors;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.DCS.Components;
using Keen.VRage.DCS.ObjectBuilders;
using Keen.VRage.Game.Components.Models;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Memory;
using Keen.VRage.Library.Reflection.DependencyInjections;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Multiplayer.Annotations;
using Keen.VRage.Physics;
using Keen.VRage.Physics.Data;
using Keen.VRage.Physics.Queries;
using SE2VR.Simulation.Configurations;

namespace SE2VR.Simulation.Components;

/// <summary>
/// Server component that allows a player to grab and move any physical object.
/// </summary>
[ServerOnly]
[JobContext(AutoInstantiate = true)]
public partial class PhysicsHandServerSessionComponent : SessionComponent
{
    [After(typeof(ThrustComponent.OnComputeThrust))]
    [Before(typeof(Physics))]
    private class PhysicsHandsJob : JobGroup;

    [Service]
    private readonly IPhysics _physics;

    [Configuration]
    private readonly PhysicsHandsConfiguration _config;

    [Configuration]
    private readonly ToolsConfiguration _tools;

    //TODO move to a character component. it's already per chararacter, has no advantage doing it in session comp
    private readonly Dictionary<Entity, ((Entity Entity, RelativeTransform Offset)? Left, (Entity Entity, RelativeTransform Offset)? Right, VRHandData Previous)> _characters = new();

    [PhysicsHandsJob]
    [MustHave(typeof(CharacterComponent))]
    private static void UpdateCharacterHands(Entity character, in VRHandData hands, [JobContext] PhysicsHandServerSessionComponent? session)
    {
        session?.UpdateCharacter(character, hands);
    }

    [PhysicsHandsJob]
    [OnRemoved(typeof(VRHandData))]
    private static void OnCharacterHandsRemoved(Entity character, [JobContext] PhysicsHandServerSessionComponent? session)
    {
        session?._characters.Remove(character);
    }

    private void UpdateCharacter(Entity character, in VRHandData hands)
    {
        if (!_characters.TryGetValue(character, out var state))
            state.Previous = hands;

        var tools = character.TryGet<ToolsComponent>();
        float toolMass = tools?.CurrentToolItem is { } tool ? (float)tool.MassPerUnit : 0;

        if (hands.LeftTracked)
        {
            float leftMass = hands.TwoHanded ? toolMass / 2 : 0;
            leftMass += _config.HandMass;
            UpdateHand(character, hands, ref state.Left, state.Right, hands.LeftHand, hands.LeftGrab, state.Previous.LeftGrab, leftMass);
        }

        if (hands.RightTracked)
        {
            float rightMass = hands.TwoHanded ? toolMass / 2 : toolMass;
            rightMass += _config.HandMass;
            bool rightGrab = hands.RightGrab && tools?.LocalTool == null;
            UpdateHand(character, hands, ref state.Right, state.Left, hands.RightHand, rightGrab, state.Previous.RightGrab, rightMass);
        }

        state.Previous = hands;
        _characters[character] = state;

        void UpdateHand(Entity character, in VRHandData hands, ref (Entity Entity, RelativeTransform Offset)? held, (Entity Entity, RelativeTransform Offset)? otherHeld, RelativeTransform handTransform, bool grab, bool wasGrabbing, float mass)
        {
            WorldTransform wt = character.Data.GetWorldTransform() * handTransform;
            if (held is { } current && (!grab || !Session.IsEntityInScene(current.Entity) || Vector3D.Distance(current.Entity.Data.GetWorldTransform().Position, (wt * current.Offset).Position) > _config.MaxGrabDistance))
            {
                held = null;

                if (otherHeld?.Entity != current.Entity && current.Entity.TryGet<FloatingObjectComponent>() != null && Session.IsEntityInScene(current.Entity) && IsTransformBehindHead(hands, handTransform))
                    Session.Get<IGameServer>()?.PickupFloatingObject(current.Entity, character);
            }

            if (grab && !wasGrabbing && held == null)
                held = TryGrab(character, wt);

            if (held is { } grabbed)
                UpdateGrabbedObject(character, grabbed, wt, mass);
        }

        void UpdateGrabbedObject(Entity character, (Entity Entity, RelativeTransform Offset) held, WorldTransform handTransform, float handMass)
        {
            var body = held.Entity.Data;
            if (!body.TryGet(out RigidBodyMassProperties mass) || !body.TryGet(out RigidBodyData velocity))
                return;

            var centerOfMass = new RelativeTransform(mass.CenterOfMass, Quaternion.Identity);
            var current = body.GetWorldTransform() * centerOfMass;
            var target = handTransform * held.Offset * centerOfMass;

            var characterVelocity = character.Data.TryGet(out RigidBodyData characterBody) ? characterBody.LinearVelocity : Vector3.Zero;

            var (linear, angular) = GetVelocityTo(current, target, characterVelocity);
            float strength = Math.Clamp(handMass * _config.GrabStrength / mass.Mass, 0, 1);

            body.Set(new RigidBodyData(Vector3.Lerp(velocity.LinearVelocity, linear, strength), Vector3.Lerp(velocity.AngularVelocity, angular, strength)));
        }
    }

    private (Vector3 Linear, Vector3 Angular) GetVelocityTo(WorldTransform current, WorldTransform target, Vector3 characterVelocity)
    {
        var linear = Vector3.ClampToSphere((Vector3)(target.Position - current.Position) / _physics.StepDelta, _config.MaxLinearSpeed) + characterVelocity;
        var delta = Quaternion.Normalize(target.Orientation * Quaternion.Conjugate(current.Orientation));

        delta.GetAxisAngle(out var axis, out float angle);

        if (angle > MathF.PI)
            angle -= MathF.PI * 2;

        var angular = Vector3.ClampToSphere(axis * (angle / _physics.StepDelta), _config.MaxAngularSpeed);
        return (linear, angular);
    }

    /// <summary>
    /// Gets the closest grabbable object, null if none
    /// TODO do it on clientside so that we can highlight grabbale objects too
    /// </summary>
    private (Entity Entity, RelativeTransform Offset)? TryGrab(Entity character, WorldTransform handTransform)
    {
        using var hits = new Buffer<NearestQueryHit>();
        using (var query = new PointQueryArgs(handTransform.Position, _config.GrabRadius))
            _physics.QueryPoint(hits, query, CollisionPreset.Bodies);

        Entity? closest = null;
        double closestDistance = double.MaxValue;
        foreach (var hit in hits)
        {
            if (hit.SeparatingDistance >= closestDistance)
                continue;

            if (Entity.TryGetFromDataEntity(new DEntityContext(Session.Scene, hit.Body)) is not { } entity)
                continue;

            if (entity.TryGet<CharacterComponent>() != null || !entity.Data.Has<DynamicTag>() || !entity.Data.Has<RigidBodyMassProperties>())
                continue;

            closest = entity;
            closestDistance = hit.SeparatingDistance;
        }

        if (closest == null)
            return null;

        return (closest, (RelativeTransform)WorldTransform.GetRelativeTransform(closest.Data.GetWorldTransform(), handTransform));
    }

    [JobContext]
    private static PhysicsHandServerSessionComponent? CreateJobContext(Session session) => session.TryGet<PhysicsHandServerSessionComponent>();

    private static bool IsTransformBehindHead(in VRHandData hands, RelativeTransform handTransform)
    {
        if (!hands.HeadTracked)
            return false;

        var facing = Vector3.Cross(Vector3.Up, hands.Head.Orientation.GetRight());
        return Vector3.Dot(facing, handTransform.Position - hands.Head.Position) < 0;
    }

    /// <summary>
    /// Data that is used to sync VR transforms to server
    /// </summary>
    [SyncFromClients]
    [Serialize]
    public partial struct VRHandData
    {
        public RelativeTransform Head;
        public bool HeadTracked;

        public RelativeTransform LeftHand;
        public bool LeftTracked;
        public bool LeftGrab;

        public RelativeTransform RightHand;
        public bool RightTracked;
        public bool RightGrab;

        /// <summary>
        /// The left hand is supporting the tool held by the right hand
        /// </summary>
        public bool TwoHanded;
    }
}
