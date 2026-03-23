using System.Reflection;
using Keen.Game2.Client.GameSystems.BlockPlacement;
using Keen.Game2.Client.GameSystems.CameraSystems;
using Keen.Game2.Client.GameSystems.CameraSystems.Helpers;
using Keen.Game2.Client.GameSystems.CameraSystems.Modes;
using Keen.Game2.Client.GameSystems.PlayerControl;
using Keen.Game2.Client.GameSystems.Render;
using Keen.Game2.Client.GameSystems.UI3D;
using Keen.Game2.Client.WorldObjects.Character;
using Keen.Game2.Client.WorldObjects.Spectators;
using Keen.Game2.Client.WorldObjects.Tools;
using Keen.Game2.Game.EntityComponents.Toolbar.BlockPlacer;
using Keen.Game2.Simulation.WorldObjects.Characters;
using Keen.Game2.Simulation.WorldObjects.Movement;
using Keen.Game2.Simulation.WorldObjects.Shared;
using Keen.VRage.Animation.Animator;
using Keen.VRage.Animation.Client;
using Keen.VRage.Animation.Data;
using Keen.VRage.Core;
using Keen.VRage.Core.EngineComponents;
using Keen.VRage.Core.Game.Components;
using Keen.VRage.Core.Game.Data;
using Keen.VRage.Core.Game.Systems;
using Keen.VRage.Core.Game.Utils;
using Keen.VRage.Core.Render;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.DCS.Builders;
using Keen.VRage.DCS.Components;
using Keen.VRage.Game.Client.Render;
using Keen.VRage.Game.Components.Models;
using Keen.VRage.Library.Collections;
using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Reflection.DependencyInjections;
using Keen.VRage.Library.Utils;
using Keen.VRage.Render.Options;
using SE2VR.Client.Patches;
using SE2VR.Simulation;

namespace SE2VR.Client.Components;

/// <summary>
/// Component that is responsible for updating the <see cref="OpenVREngineComponent"/>
/// </summary>
public partial class OpenVRSessionComponent : SessionComponent, IInSceneListener
{
    [Service]
    private readonly IDebugDraw _debugDraw;

    [Service]
    private readonly UI3DSessionComponent _ui3D;
    
    [Service]
    private readonly IEntitySpawner _spawner;

    [Service]
    private readonly IOptions _options;

    [Service]
    private readonly ClientPlayersSessionComponent _players;

    public WeakEntityReference ControlledEntity = new();
    public Entity? InteractorEntity;

    private OpenVREngineComponent _vr;
    private OpenVROptions _settings;

    private Entity? _rightController;
    private Entity? _leftController;
    private PresetIdTypeDefinition _hideBodyPreset;

    private bool _originalAutoRotation;

    [Init]
    protected new void Init()
    {
        base.Init();
        _vr = OpenVREngineComponent.Instance;

        //VRageCore.Instance.Engine.Get<UIEngineComponent>().ScreenManager.UIVisible = false;

        _settings = _options.GetOrCreatePart<OpenVROptions>();

        DefinitionManager.Instance.TryGetDefinition(new Guid("44169cc8-0450-4100-9232-f646932c7e62"), out var def);
        _hideBodyPreset = (PresetIdTypeDefinition)def!;

        //The player should rotate their hand to rotate the block preview
        _originalAutoRotation = _options.GetOrCreatePart<BlockPlacerOptions>().AutoRotation;
        Logging.Info($"Loaded into world with resolution of {_options.GetOrCreatePart<RenderDisplayOptionsPart>().Resolution}");
    }

    [Destructor]
    protected void Destroy()
    {
        _options.GetOrCreatePart<BlockPlacerOptions>().AutoRotation = _originalAutoRotation;
    }

    void IInSceneListener.OnAddedToScene()
    {
        _rightController = CreateModelEntity(new Guid("3e14b273-42f2-4813-921a-1d375030d034"));
        _leftController = CreateModelEntity(new Guid("7cf5684a-9a0a-4432-90d5-4c1919f407ce"));
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
                .WithComponent((typeof(ChildTransformComponent), new ChildTransformComponentObjectBuilder()))
                .WithComponent((typeof(HierarchyComponent), new HierarchyComponentObjectBuilder(), ["ToolOrigin"]))
                .CompileObjectBuilder();

            //ToolOrigin
            Assert.Null(InteractorEntity);
            InteractorEntity = ControlledEntity.Entity.Get<HierarchyComponent>().AddChild(eob);
        }
        else if(ControlledEntity.Entity == controllable.Entity)
        {
            foreach (var strategy in ControlledEntity.Entity.All<CharacterRenderStrategyComponent>())
            {
                strategy.UnhidePreset(_hideBodyPreset, this);
            }

            Assert.NotNull(InteractorEntity);
            ControlledEntity.Entity.Get<HierarchyComponent>().RemoveChild(InteractorEntity);
            InteractorEntity = null;

            ControlledEntity.Entity = null;
        }
    }

    private Entity? CreateModelEntity(Guid modelId)
    {
        if (!DefinitionManager.Instance.TryGetDefinition(modelId, out var def))
            return null;

        var model = (ModelComponentDefinition)def!;
        var eob = EntityBuilder.ForScene(Entity.Scene)
            .WithComponent((typeof(WorldTransformComponent), new WorldTransformComponentObjectBuilder()))
            .WithComponent((typeof(ModelComponent), model))
            .WithComponent(typeof(ModelRenderComponent))
            .WithComponent(typeof(RootEntityRenderComponent))
            .CompileObjectBuilder();

        return _spawner.SpawnEntity(eob);
    }

    [DoNotPause]
    [RigUpdateSystem.InverseKinematicsPostRagdollWindow.Begin]
    private void Tick()
    {
        if (ControlledEntity.Entity == null)
            return;

        var camera = Session.GetEntitiesOfType<CameraComponent>().Single().Get<CameraComponent>();
        var wt = ControlledEntity.Entity.Data.GetWorldTransform();

        if (_vr.LeftHand is { } left)
        {
            WorldTransform realLeft = wt * left;
            _leftController?.Data.SetWorldTransform(realLeft);
        }

        if (_vr.RightHand is { } right)
        {
            WorldTransform realRight = wt * right;
            _rightController?.Data.SetWorldTransform(realRight);
        }

        if (ControlledEntity.Entity.TryGet<CharacterComponent>() is { } character)
        {
            UpdateHands(_vr.LeftHand, _vr.RightHand);
            HideBody(character);
        }

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

    private void UpdateHands(RelativeTransform? leftHand, RelativeTransform? rightHand)
    {
        foreach (var rig in ControlledEntity.Entity!.All<IRiggedAnimator>())
        {
            //TODO USE STATIC WRIST/ELBOW POSE
            var skeleton = rig.Skeleton;
            var pose = rig.GetPose();

            if (leftHand is { } left)
            {
                left.Orientation *= Quaternion.CreateFromAxisAngle(Vector3.Right, MathHelper.ToRadians(-48.81f));
                left.Position += left.Orientation.GetUp() * -0.075f;
                left.Position += left.Orientation.GetForward() * -0.15f;

                left.Orientation *= Quaternion.CreateFromYawPitchRoll(-MathF.PI / 2, -MathF.PI / 2, 0);
                left.Orientation *= Quaternion.CreateFromAxisAngle(Vector3.Forward, MathHelper.ToRadians(-23f));
                left.Orientation *= Quaternion.CreateFromAxisAngle(Vector3.Right, MathHelper.ToRadians(5f));

                OverrideHandPose(ref pose, "Wrist_L", "ElbowPart1_L", left);
            }

            if (rightHand is { } right)
            {
                right.Orientation *= Quaternion.CreateFromAxisAngle(Vector3.Right, MathHelper.ToRadians(-48.81f));
                right.Position += right.Orientation.GetUp() * -0.075f;
                right.Position += right.Orientation.GetForward() * -0.15f;

                right.Orientation *= Quaternion.CreateFromYawPitchRoll(MathF.PI / 2, MathF.PI / 2, 0);
                right.Orientation *= Quaternion.CreateFromAxisAngle(Vector3.Forward, MathHelper.ToRadians(-23f));
                right.Orientation *= Quaternion.CreateFromAxisAngle(Vector3.Right, MathHelper.ToRadians(5f));

                OverrideHandPose(ref pose, "Wrist_R", "ElbowPart1_R", right);
            }

            if (_vr.Head.HasValue) //Flashlight
                pose.SetAbsolute(skeleton.GetBone("HeadBoneParent").Index, _vr.Head.Value);

            void OverrideHandPose(ref PoseContainer.PoseReference pose, string wristBone, string elbowPart1Bone, RelativeTransform transform)
            {
                var wrist = skeleton.GetBone(wristBone);
                var elbowPart1 = skeleton.GetBone(elbowPart1Bone);
                var elbow = skeleton.Bones[wrist.Parent];

                RelativeTransform absoluteWrist = pose.GetAbsolute(wrist.Index);
                RelativeTransform absoluteElbowPart1 = pose.GetAbsolute(elbowPart1.Index);
                RelativeTransform absoluteElbow = pose.GetAbsolute(elbow.Index);

                var relativeElbowToWrist = (RelativeTransform)WorldTransform.GetRelativeTransform(absoluteElbow, absoluteWrist);
                var relativeElbowPart1ToWrist = (RelativeTransform)WorldTransform.GetRelativeTransform(absoluteElbowPart1, absoluteWrist);
                pose.SetAbsolute(elbow.Index, transform * relativeElbowToWrist);
                pose.SetAbsolute(elbowPart1.Index, transform * relativeElbowPart1ToWrist);
                pose.SetRelative(wrist.Index, (RelativeTransform)WorldTransform.GetRelativeTransform(transform, transform * relativeElbowToWrist));
            }
        }
    }

    private void UpdateInteraction(WorldTransform wt, RelativeTransform? rightHand, RelativeTransform? head)
    {
        ControlledEntity.Entity!.Data.TryRemove<LookOffsetData>();

        if (InteractorEntity != null && (rightHand.HasValue || head.HasValue))
        {
            RelativeTransform interactionRt;

            if (rightHand is { } right)
            {
                //Empty hand baseline
                interactionRt = right;
                interactionRt.Orientation *= Quaternion.CreateFromAxisAngle(Vector3.Right, MathHelper.ToRadians(-33.81f));
                interactionRt.Position += interactionRt.Orientation.GetUp() * -0.025f;

                if (_players.LocalPlayerController.GetTopControllable<ToolControllableComponent>() is { } tool && tool.Entity.TryGet<ModelComponent>() is { } model)
                {
                    foreach (var dummy in model.Definition.Dummies)
                    {
                        if (dummy.Type.Guid == new Guid("6fc9829d-3ee4-4735-b229-e00157f67a42") || dummy.Name?.Contains("Barrel") == true) //Paint tool/guns
                        {
                            interactionRt = (RelativeTransform)WorldTransform.GetRelativeTransform(tool.Data.GetWorldTransform(), wt) * (dummy.Transform.Transform * new RelativeTransform(Quaternion.CreateFromAxisAngle(Vector3.UnitY, -MathF.PI / 2)));
                            break;
                        }

                        if (dummy.Type.Guid == new Guid("1a2d1774-94f9-4f4b-8e13-d778dc646f79")) //Welder
                        {
                            if (tool.Entity.GetComposition().DebugName.Contains("Welder"))
                                interactionRt = (RelativeTransform)WorldTransform.GetRelativeTransform(tool.Data.GetWorldTransform(), wt) * (dummy.Transform.Transform * new RelativeTransform(Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI)));
                            else if (tool.Entity.GetComposition().DebugName.Contains("Grinder"))
                                interactionRt = (RelativeTransform)WorldTransform.GetRelativeTransform(tool.Data.GetWorldTransform(), wt) * (dummy.Transform.Transform * new RelativeTransform(Quaternion.CreateFromAxisAngle(Vector3.UnitY, -MathF.PI / 2)));

                            break;
                        }
                    }
                }
                else if (_players.LocalPlayerController.GetTopControllable<BlockPlacerEntityComponent>() != null)
                {
                    //Palm
                    interactionRt = right;
                    interactionRt.Position += interactionRt.Orientation.GetUp() * -0.125f;
                    interactionRt.Orientation *= Quaternion.CreateFromAxisAngle(Vector3.Up, MathF.PI / 2);
                    interactionRt.Orientation *= Quaternion.CreateFromAxisAngle(Vector3.Forward, MathF.PI / 2);
                    interactionRt.Orientation *= Quaternion.CreateFromAxisAngle(Vector3.Forward, MathF.PI / 4);
                    interactionRt.Orientation.Normalize();
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
            GunToolComponent_GetProjectileTransform_Patch.Transform = interactionRt;
            GunToolComponent_GetProjectileVisualsTransform_Patch.Transform = interactionRt;
        }
        else
        {
            CharacterApproximateToolOriginProviderComponent_WorldTransform_Patch.Origin = null;
            GunToolComponent_GetProjectileTransform_Patch.Transform = null;
            GunToolComponent_GetProjectileVisualsTransform_Patch.Transform = null;
        }
    }

    private void HideBody(CharacterComponent character)
    {
        MethodInfo hideMethod = typeof(CharacterRenderStrategyComponent).GetMethod("Hide", BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var strategy in character.Entity.All<CharacterRenderStrategyComponent>())
        {
            if (strategy.ModelDefinition.Presets is { } presets && presets.RenderPartsChanges.TryGetValue(_hideBodyPreset, out var changes))
            {
                if (_settings.ShowBoots)
                {
                    var hiddenparts = changes.HiddenParts.ToList();
                    hiddenparts.Remove("MiroslavSokol_BootsProps");
                    changes.HiddenParts = new MergeableList<StringId>(hiddenparts.Count);
                    foreach (var part in hiddenparts)
                    {
                        changes.HiddenParts.Add(part);
                    }
                }

                hideMethod.Invoke(strategy,
                [
                    changes,
                this
                ]);
            }
        }
    }
}