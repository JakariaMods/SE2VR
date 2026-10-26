using System.Reflection;
using Keen.Game2.Client.GameSystems.BlockPlacement;
using Keen.Game2.Client.GameSystems.BlockPlacement.BlockPlacer;
using Keen.Game2.Client.GameSystems.CameraSystems;
using Keen.Game2.Client.GameSystems.CameraSystems.Helpers;
using Keen.Game2.Client.GameSystems.CameraSystems.Modes;
using Keen.Game2.Client.GameSystems.PlayerControl;
using Keen.Game2.Client.GameSystems.UI3D;
using Keen.Game2.Client.WorldObjects.Spectators;
using Keen.Game2.Client.WorldObjects.Tools;
using Keen.Game2.Simulation.WorldObjects.Characters;
using Keen.Game2.Simulation.WorldObjects.Movement;
using Keen.Game2.Simulation.WorldObjects.Shared;
using Keen.VRage.Animation.Client;
using Keen.VRage.Core;
using Keen.VRage.Core.EngineComponents;
using Keen.VRage.Core.Game.Components;
using Keen.VRage.Core.Game.Data;
using Keen.VRage.Core.Game.GameSystems.OWT;
using Keen.VRage.Core.Game.Systems;
using Keen.VRage.Core.Game.Utils;
using Keen.VRage.Core.Render;
using Keen.VRage.Core.Systems;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.DCS.Builders;
using Keen.VRage.DCS.Components;
using Keen.VRage.Game.Components.Models;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Reflection.DependencyInjections;
using Keen.VRage.Render.Options;
using SE2VR.Client.Configurations;
using SE2VR.Client.Input;
using SE2VR.Client.Patches;
using SE2VR.Simulation;
using SE2VR.Simulation.Configurations;

namespace SE2VR.Client.Components;

/// <summary>
/// Component that is responsible for updating the <see cref="OpenVREngineComponent"/>
/// </summary>
public partial class OpenVRSessionComponent : SessionComponent, IInSceneListener
{
    [After(typeof(BoneParentedPositionBaseComponent.TransformationUpdateJob))]
    [Before(typeof(TransformsFinalized))]
    [Before(typeof(RenderSubmissionBegin))]
    private class ToolTransformJob : JobGroup;

    [Service]
    private readonly IDebugDraw _debugDraw;

    [Service]
    private readonly UI3DSessionComponent _ui3D;

    [Service]
    private readonly IOptions _options;

    [Service]
    private readonly ClientPlayersSessionComponent _players;

    [Configuration]
    private readonly VRToolsConfiguration _tools;

    [Configuration]
    private readonly PhysicsHandsConfiguration _physicsHands;

    public WeakEntityReference ControlledEntity = new();
    public Entity? InteractorEntity;

    private Entity? _heldTool;
    private RelativeTransform _heldToolTransform;

    /// <summary>
    /// The left hand is supporting the tool held by the right hand
    /// </summary>
    public bool TwoHanded { get; private set; }

    /// <summary>
    /// Controller pose the left hand is snapped to while <see cref="TwoHanded"/>
    /// </summary>
    public RelativeTransform? LeftHandOverride { get; private set; }

    private bool _leftGrabHeld;

    private OpenVREngineComponent _vr;
    private OpenVROptions _settings;

    private bool _originalAutoRotation;

    [Init]
    protected new void Init()
    {
        base.Init();
        _vr = OpenVREngineComponent.Instance;

        //VRageCore.Instance.Engine.Get<UIEngineComponent>().ScreenManager.UIVisible = false;

        _settings = _options.GetOrCreatePart<OpenVROptions>();

        //The player should rotate their hand to rotate the block preview
        _originalAutoRotation = _options.GetOrCreatePart<BlockPlacerOptions>().AutoRotation;
        Logging.Info($"Loaded into world with resolution of {_options.GetOrCreatePart<RenderDisplayOptionsPart>().Resolution}");
    }

    [Destructor]
    protected void Destroy()
    {
        _options.GetOrCreatePart<BlockPlacerOptions>().AutoRotation = _originalAutoRotation;
    }

    void IInSceneListener.OnBeforeRemovedFromScene()
    {
        VRageCore.Instance.Engine.Get<VRRenderEngineComponent>().Camera = null;
        VRageCore.Instance.Engine.Get<VRRenderEngineComponent>().Body = null;
    }

    [ClientPlayersSessionComponent.LocalPlayerChangedSignal]
    private void LocalPlayerChanged()
    {
        var controller = _players.LocalPlayerController;
        PlayerControllerComponent.PossessionChangedSignal.Subscribe(controller.Entity, PossessionsChanged);
    }

    private void PossessionsChanged(Entity entity, IControllable controllable, bool isPossessed)
    {
        if (controllable.Entity.TryGet<CharacterComponent>() == null && controllable.Entity.TryGet<SpectatorComponent>() == null)
            return;

        if (isPossessed)
        {
            ControlledEntity.Entity = controllable.Entity;

            var eob = EntityBuilder.ForScene(Entity.Scene)
                .WithName("ToolOrigin")
                .WithComponent((typeof(ChildTransformComponent), new ChildTransformComponentObjectBuilder()))
                .WithComponent((typeof(HierarchyComponent), new HierarchyComponentObjectBuilder(), [nameof(HierarchyComponent), "ToolOrigin"]))
                .CompileObjectBuilder();

            //ToolOrigin
            Assert.Null(InteractorEntity);
            InteractorEntity = ControlledEntity.Entity.Get<HierarchyComponent>().AddChild(eob);
        }
        else if(ControlledEntity.Entity == controllable.Entity)
        {
            Assert.NotNull(InteractorEntity);
            ControlledEntity.Entity.Get<HierarchyComponent>().RemoveChild(InteractorEntity);
            InteractorEntity = null;

            ControlledEntity.Entity = null;
        }
    }

    [DoNotPause]
    [RigUpdateSystem.InverseKinematicsPostRagdollWindow.Begin]
    private void Tick()
    {
        if (ControlledEntity.Entity == null)
            return;

        var camera = Session.GetEntitiesOfType<CameraComponent>().Single().Get<CameraComponent>();
        var wt = ControlledEntity.Entity.Data.GetWorldTransform();

        UpdateInteraction(wt, _vr.RightHand, _vr.Head);

        VRageCore.Instance.Engine.Get<VRRenderEngineComponent>().Camera = camera;
        VRageCore.Instance.Engine.Get<VRRenderEngineComponent>().Body = ControlledEntity.Entity;

        if (ControlledEntity.Entity?.TryGet<SeatableComponent>()?.CurrentSeat is { } seat) //Maybe offset by different seat types
        {
            //_standingHeight
            _settings.WorldOffset.Y = -1.25f;
        }
        else
        {
            _settings.WorldOffset.Y = 0;
        }

        var system = (CameraSystemComponent)_players.LocalPlayerController.CameraSystem;
        if (system.ActiveCameraController?.TryGet<FirstPersonCameraComponent>() is not null)
        {
            var activeCameraField = typeof(CameraSystemComponent).GetField("_activeCamera", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var activeCamera = activeCameraField.GetValue(system) as CameraControllerHelper;
            activeCamera?.ToggleNextCameraMode();
        }
    }

    [DoNotPause]
    [ToolTransformJob]
    private void UpdateToolTransform()
    {
        _heldTool?.Data.Set(_heldToolTransform);
    }

    private void UpdateInteraction(WorldTransform wt, RelativeTransform? rightHand, RelativeTransform? head)
    {
        ControlledEntity.Entity!.Data.TryRemove<LookOffsetData>();
        _heldTool = null;

        bool wasTwoHanded = TwoHanded;
        TwoHanded = false;
        LeftHandOverride = null;

        bool leftGrab = OpenVRInputEngineComponent.Instance.LeftGrab;
        bool leftGrabPressed = leftGrab && !_leftGrabHeld;
        _leftGrabHeld = leftGrab;

        if (InteractorEntity != null && (rightHand.HasValue || head.HasValue))
        {
            RelativeTransform interactionRt;

            if (rightHand is { } right)
            {
                interactionRt = right * _tools.ControllerAim;

                if (_players.LocalPlayerController.GetTopControllable<ToolControllableComponent>() is { } tool && tool.Entity.TryGet<ModelComponent>() is { } model)
                {
                    var toolRt = (RelativeTransform)WorldTransform.GetRelativeTransform(tool.Data.GetWorldTransform(), wt);

                    if (model.TryGetDummy(_tools.HandDummy) is { } hand)
                    {
                        toolRt = right * _tools.RightControllerGrip * RelativeTransform.Inverse(hand.Transform);

                        if (leftGrab && _vr.LeftHand is { } left && _tools.SecondaryGripDummy is { } supportType && model.TryGetDummy(supportType) is { } support)
                        {
                            RelativeTransform leftGrip = _tools.LeftControllerGrip;
                            var supportPosition = (toolRt * support.Transform).Position;
                            var leftGripPosition = (left * leftGrip).Position;

                            if (wasTwoHanded || (leftGrabPressed && Vector3.Distance(leftGripPosition, supportPosition) <= _physicsHands.GrabRadius))
                            {
                                var pivot = (toolRt * hand.Transform).Position;
                                var swing = Quaternion.CreateFromTwoVectors(supportPosition - pivot, leftGripPosition - pivot);

                                if (!MathHelper.IsValid(swing))
                                    swing = Quaternion.Identity;

                                toolRt = new RelativeTransform(pivot + swing * (toolRt.Position - pivot), swing * toolRt.Orientation);

                                TwoHanded = true;
                                LeftHandOverride = toolRt * support.Transform * RelativeTransform.Inverse(leftGrip);
                            }
                        }

                        _heldTool = tool.Entity;
                        _heldToolTransform = toolRt;
                    }

                    if (model.TryGetDummy(_tools.BarrelDummy) is { } barrel)
                        interactionRt = toolRt * barrel.Transform;
                }
                else if (_settings.PalmBuilding && _players.LocalPlayerController.GetTopControllable<BlockPlacerEntityComponent>() != null)
                {
                    interactionRt = right * _tools.ControllerPalm;
                }
            }
            else
            {
                interactionRt = head!.Value;
            }

            WorldTransform interactionTransform = wt * interactionRt;
            InteractorEntity.Data.Set(interactionTransform);
            _debugDraw.DebugDraw.AddLine(interactionTransform.Position, interactionTransform.Position + (interactionTransform.Orientation.GetForward() * 100), ColorSRGB.White);

            CharacterApproximateToolOriginProviderComponent_WorldTransform_Patch.Origin = interactionTransform;
            CharacterDummyToolOriginProviderComponent_WorldTransform_Patch.Origin = interactionTransform;
            PlayerOriginProviderComponent_GetWorldTransform_Patch.Origin = interactionTransform;
            PlayerOriginProviderComponent_GetWorldTransform_Patch.Owner = ControlledEntity.Entity?.DEntity;
            GunToolComponent_GetProjectileTransform_Patch.Transform = interactionRt;
            GunToolComponent_GetProjectileVisualsTransform_Patch.Transform = interactionRt;
        }
        else
        {
            CharacterApproximateToolOriginProviderComponent_WorldTransform_Patch.Origin = null;
            CharacterDummyToolOriginProviderComponent_WorldTransform_Patch.Origin = null;
            PlayerOriginProviderComponent_GetWorldTransform_Patch.Origin = null;
            GunToolComponent_GetProjectileTransform_Patch.Transform = null;
            GunToolComponent_GetProjectileVisualsTransform_Patch.Transform = null;
        }
    }
}
