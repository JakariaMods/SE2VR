using Keen.Game2.Client.GameSystems.BlockPlacement.BlockPlacer;
using Keen.Game2.Client.GameSystems.Interaction;
using Keen.Game2.Client.GameSystems.PlayerControl;
using Keen.Game2.Client.WorldObjects.Tools;
using Keen.Game2.Simulation.GameSystems.Interaction;
using Keen.Game2.Simulation.GameSystems.Toolbars;
using Keen.Game2.Simulation.WorldObjects.Characters;
using Keen.Game2.Simulation.WorldObjects.CubeBlocks;
using Keen.Game2.Simulation.WorldObjects.CubeBlocks.Toolbar;
using Keen.Game2.Simulation.WorldObjects.CubeGrids;
using Keen.Game2.Simulation.WorldObjects.Shared;
using Keen.VRage.Core;
using Keen.VRage.Core.Game.Components;
using Keen.VRage.Core.Game.Data;
using Keen.VRage.Core.Game.GameSystems.GameTimes;
using Keen.VRage.Core.Game.GameSystems.Queries;
using Keen.VRage.Core.Game.Systems;
using Keen.VRage.Core.Model;
using Keen.VRage.Core.Systems;
using Keen.VRage.DCS.Accessors;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.DCS.Components;
using Keen.VRage.DCS.ObjectBuilders;
using Keen.VRage.Game.Components.Models;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Memory;
using Keen.VRage.Library.Reflection.DependencyInjections;
using Keen.VRage.Library.Threading;
using Keen.VRage.Library.Utils;
using Keen.VRage.Multiplayer.Sessions;
using Keen.VRage.Physics;
using Keen.VRage.Physics.Queries;
using SE2VR.Client.Configurations;
using SE2VR.Client.Input;
using Valve.VR;
using Buffer = Keen.VRage.Library.Memory.Buffer;

namespace SE2VR.Client.Components;

/// <summary>
/// Session component that replaces the vanilla interaction with a system that works for tracked hands. Only exists in <see cref="ControlMode.Motion"/>, where the vanilla interactor is patched off
/// </summary>
public partial class VRInteractionSessionComponent : SessionComponent, IInSceneListener
{
    [Before(typeof(OnDataReplication))]
    private class HandInteractionJob : JobGroup;

    [Service]
    private readonly IReplicationMapping _replication;

    [Service]
    private readonly IPhysics _physics;

    [Service]
    private readonly IGameTime _gameTime;

    [Service]
    private readonly OpenVRSessionComponent _vrSession;

    [Service]
    private readonly ClientPlayersSessionComponent _players;

    [Service]
    private readonly HapticsSessionComponent _haptics;

    [Configuration]
    private readonly VRInteractionConfiguration _config;

    [Configuration]
    private readonly VRToolsConfiguration _tools;

    /// <summary>
    /// Use actions hovered by one or both hands, and their highlight
    /// </summary>
    private readonly Dictionary<UseAction, (int RefCount, IDisposable? Hover)> _targets = new();

    private UseAction? _leftTarget;
    private UseAction? _rightTarget;

    private bool _leftTouching;
    private bool _rightTouching;

    private DateTime _leftLastTouch;
    private DateTime _rightLastTouch;

    private bool _leftTrigger;
    private bool _rightTrigger;
    private bool _leftGrip;
    private bool _rightGrip;

    private UseAction? _leftAction;
    private UseAction? _rightAction;
    private VRInteractionMode _leftHeldGesture;
    private VRInteractionMode _rightHeldGesture;

    void IInSceneListener.OnBeforeRemovedFromScene()
    {
        ReleaseHand(ref _leftTarget, ref _leftTouching, ref _leftAction);
        ReleaseHand(ref _rightTarget, ref _rightTouching, ref _rightAction);
    }

    [HandInteractionJob]
    private void Tick()
    {
        var vr = OpenVREngineComponent.Instance;
        var input = OpenVRInputEngineComponent.Instance;
        var character = _vrSession.ControlledEntity.Entity is { } controlled && controlled.TryGet<CharacterComponent>() != null ? controlled : null;
        var left = _vrSession.LeftHandOverride ?? vr.LeftHand;
        var right = vr.RightHand;
        var bindings = character?.GetComposition().TryGetDefinition<InteractorEntityComponent, InteractorEntityComponentDefinition>()?.UseActionBindings;
        var controller = _players.LocalPlayerController;

        if (!vr.Initalized || character == null || bindings == null)
        {
            ReleaseHand(ref _leftTarget, ref _leftTouching, ref _leftAction);
            ReleaseHand(ref _rightTarget, ref _rightTouching, ref _rightAction);
            return;
        }
        
        var leftModes = _vrSession.TwoHanded ? VRInteractionMode.Touch : VRInteractionMode.All;
        var rightModes = controller.GetTopControllable<ToolControllableComponent>() != null || controller.GetTopControllable<BlockPlacerEntityComponent>() != null ? VRInteractionMode.Touch : VRInteractionMode.All;

        var wt = character.Data.GetWorldTransform();
        UpdateHand(ETrackedControllerRole.LeftHand, character, bindings, left.HasValue ? wt * (left.Value * _tools.LeftControllerGrip) : null, leftModes, input.LeftTrigger, input.LeftGrab, ref _leftTarget, ref _leftTouching, ref _leftLastTouch, ref _leftTrigger, ref _leftGrip, ref _leftAction, ref _leftHeldGesture);
        UpdateHand(ETrackedControllerRole.RightHand, character, bindings, right.HasValue ? wt * (right.Value * _tools.RightControllerGrip) : null, rightModes, input.RightTrigger, input.RightGrab, ref _rightTarget, ref _rightTouching, ref _rightLastTouch, ref _rightTrigger, ref _rightGrip, ref _rightAction, ref _rightHeldGesture);
    }

    private void UpdateHand
    (
        ETrackedControllerRole role, 
        Entity character, 
        UseActionBindingsDefinition bindings, 
        WorldTransform? handPose, 
        VRInteractionMode allowed, 
        bool trigger, 
        bool grip,

        ref UseAction? hovered,
        ref bool touching, 
        ref DateTime lastTouch, 
        ref bool wasTrigger, 
        ref bool wasGrip, 
        ref UseAction? held, 
        ref VRInteractionMode heldGesture
    )
    {
        bool triggerPressed = trigger && !wasTrigger;
        bool gripPressed = grip && !wasGrip;
        wasTrigger = trigger;
        wasGrip = grip;

        if (hovered != null && !Session.IsEntityInScene(hovered.Entity))
        {
            ReleaseHand(ref hovered, ref touching, ref held);
        }

        if (handPose is not { } pose)
        {
            ReleaseHand(ref hovered, ref touching, ref held);
            return;
        }

        var best = GetClosestDummy(character, bindings, pose.Position, allowed);
        (Entity Entity, ModelDummy Dummy)? hoveredKey = hovered != null ? (hovered.Entity, hovered.ModelDummy) : null;
        if (held == null && best?.Key != hoveredKey)
        {
            ReleaseHand(ref hovered, ref touching, ref held);

            if (best is { } found && GetOrCreateUseAction(found.Key.Entity, found.Key.Dummy, found.Definition, character) is { } target)
            {
                hovered = target;
                _haptics.Vibrate(role, _config.HoverHapticAmplitude);
            }
        }

        if (hovered == null)
            return;

        var (mode, inContact) = best is { } current && current.Key == (hovered.Entity, hovered.ModelDummy) ? (current.Mode, current.InContact) : (VRInteractionMode.None, false);

        if (held != null && ((heldGesture == VRInteractionMode.Touch && !inContact) || (heldGesture == VRInteractionMode.Trigger && !trigger) || (heldGesture == VRInteractionMode.Grip && !grip)))
        {
            held.InteractPrimaryStop();
            held = null;
        }

        if (mode.HasAnyFlag(VRInteractionMode.Touch))
        {
            if (inContact && !touching && held == null && _gameTime.CurrentGameTime - lastTouch >= _config.TouchCooldown)
            {
                lastTouch = _gameTime.CurrentGameTime;
                ActivateAction(role, hovered, VRInteractionMode.Touch, ref held, ref heldGesture);
            }

            touching |= inContact;
        }

        bool canStart = held == null || heldGesture == VRInteractionMode.Touch;

        if (mode.HasAnyFlag(VRInteractionMode.Trigger) && triggerPressed && canStart)
        {
            held?.InteractPrimaryStop();
            held = null;

            ActivateAction(role, hovered, VRInteractionMode.Trigger, ref held, ref heldGesture);
        }
        else if (mode.HasAnyFlag(VRInteractionMode.Grip) && gripPressed && canStart)
        {
            held?.InteractPrimaryStop();
            held = null;

            ActivateAction(role, hovered, VRInteractionMode.Grip, ref held, ref heldGesture);
        }
    }

    private ((Entity Entity, ModelDummy Dummy) Key, UseActionDefinition Definition, VRInteractionMode Mode, bool InContact)? GetClosestDummy(Entity character, UseActionBindingsDefinition bindings, Vector3D point, VRInteractionMode allowed)
    {
        using var candidates = Set.Inline8<Entity>();
        using var hits = Buffer.Inline4<NearestQueryHit>();
        using var query = new PointQueryArgs(point, CubeGridCoords.CELL_SIZE);
        _physics.QueryPoint(hits, query, CollisionPreset.Bodies);

        foreach (var hit in hits)
        {
            if (Entity.TryGetFromDataEntity(new DEntityContext(Session.Scene, hit.Body)) is not { } entity)
                continue;

            if (entity.TryGet<CubeGridComponent>() is { } grid)
            {
                var gridTransform = grid.Data.GetWorldTransform();
                var cells = CubeGridCoords.WorldToGrid(BoundingBoxD.CreateFromHalfExtent(point, CubeGridCoords.CELL_SIZE), gridTransform);

                foreach (var cell in cells.EnumeratePointsMaxInclusive())
                {
                    if (grid.GetBlock(cell) is { } block)
                        candidates.Add(block);
                }
            }
            else
            {
                candidates.Add(entity);
            }
        }

        ((Entity Entity, ModelDummy Dummy) Key, UseActionDefinition Definition, VRInteractionMode Mode, bool InContact)? best = null;
        double bestDistance = double.MaxValue;

        foreach (var root in candidates)
        {
            if (character.IsAncestorOf(root, includeSelf: true))
                continue;

            foreach (var entity in root.GetDescendants())
            {
                if (entity.TryGet<ModelComponent>() is not { } model)
                    continue;

                var dummies = model.Definition.Dummies;
                var entityTransform = entity.Data.GetWorldTransform();

                foreach (var dummy in dummies)
                {
                    if (Vector3.IsZero(dummy.Scale) || !bindings.DummyTypesMap.TryGetValue(dummy.Type, out var definition))
                        continue;

                    var mode = _config.GetMode(entity, definition) & allowed;
                    if (mode == VRInteractionMode.None)
                        continue;

                    var dummyTransform = dummy.Transform.Transform * entityTransform;
                    var box = new OrientedBoundingBoxD(dummyTransform.Position, dummy.Scale / 2 + definition.Inflation + new Vector3(_config.HandRadius), dummyTransform.Orientation);

                    var contactBox = box;
                    if (mode.HasAnyFlag(VRInteractionMode.Touch))
                        box.HalfExtent += new Vector3(_config.TouchHoverMargin);

                    if (!box.Contains(point))
                        continue;

                    double distance = Vector3D.Distance(point, box.Center);
                    if (distance >= bestDistance || !CanUse(entity, dummy, definition))
                        continue;

                    best = ((entity, dummy), definition, mode, contactBox.Contains(point));
                    bestDistance = distance;
                }
            }
        }

        return best;

        static bool CanUse(Entity entity, ModelDummy dummy, UseActionDefinition definition)
        {
            if (entity.TryGet<BreakableBlockComponent>()?.IsSupportingFractureDead(dummy) == true)
                return false;

            if (!definition.WorkWhenDisabled && entity.Data.Has<DisabledTag>())
                return false;

            if (entity.TryGet<FunctionalBlockComponent>() is { } functionalBlock && !functionalBlock.Functional)
                return false;

            return true;
        }
    }

    /// <summary>
    /// Gets or creates the use action of a dummy and highlights it. Pair with <see cref="ReleaseHand"/>
    /// </summary>
    private UseAction? GetOrCreateUseAction(Entity entity, ModelDummy dummy, UseActionDefinition definition, Entity interactor)
    {
        foreach (var (existing, (refCount, hover)) in _targets)
        {
            if (existing.Entity != entity || existing.ModelDummy != dummy)
                continue;

            _targets[existing] = (refCount + 1, hover);
            return existing;
        }

        if (Activator.CreateInstance(definition.UseActionType, dummy, entity, interactor, definition) is not UseAction action)
            return null;

        if (!action.CanActivate)
        {
            (action as IDisposable)?.Dispose();
            return null;
        }

        _targets.Add(action, (1, action.OnHover()));
        return action;
    }

    private void ReleaseHand(ref UseAction? hovered, ref bool touching, ref UseAction? held)
    {
        if (held != null)
        {
            held.InteractPrimaryStop();
            held = null;
        }

        touching = false;

        if (hovered is not { } target)
            return;

        hovered = null;

        var (refCount, hover) = _targets[target];
        if (refCount > 1)
        {
            _targets[target] = (refCount - 1, hover);
        }
        else
        {
            _targets.Remove(target);
            hover?.Dispose();
            (target as IDisposable)?.Dispose();
        }
    }

    private void ActivateAction(ETrackedControllerRole role, UseAction action, VRInteractionMode gesture, ref UseAction? held, ref VRInteractionMode heldGesture)
    {
        var root = action.Entity;
        while (root.FirstOrDefault<IDummyInteractionRoot>() == null && root.TryGet<HierarchyComponent>()?.Parent is { } parent)
            root = parent;

        InteractorSessionComponent.DummyWasInteracted(root, action.Interactor, action.ModelDummy.Type, action.ModelDummy.Name).SkipWait(); //Missions
        _haptics.Vibrate(role, _config.UseHapticAmplitude);

        switch (action)
        {
            case UseToolbarSlotAction toolbarSlot when gesture == VRInteractionMode.Touch:
                var serverComposite = _replication.GetServerComposite(action.Entity.GetComposition());
                int toolbarIndex = BlockToolbarServerComponent.TryGetToolbarIndex(serverComposite, toolbarSlot.Definition.ToolbarTag);
                int slotIndex = UseActionDefinition.GetSlotIndex(action.ModelDummy);

                if (toolbarIndex == -1 || slotIndex == -1)
                    return;

                BlockToolbarServerComponent.RequestActivateTile(action.Entity, toolbarIndex, new ToolbarSelection(toolbarSlot.Definition.ToolbarGroupIndex, slotIndex)).SkipWait();
                return;

            case UseToolbarSlotAction toolbarSlot:
                Session.Get<TerminalControllerSessionComponent>().OpenTerminal(toolbarSlot.Definition.PrimaryScreen, action.Entity, action.Interactor, action.ModelDummy).SkipWait();
                return;

            default:
                action.InteractPrimaryStart();
                held = action;
                heldGesture = gesture;
                return;
        }
    }
}
