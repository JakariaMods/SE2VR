using Keen.Game2.Client.GameSystems.UI3D;
using Keen.Game2.Client.WorldObjects.Tools;
using Keen.Game2.Simulation;
using Keen.Game2.Simulation.GameSystems.EntityDetection;
using Keen.Game2.Simulation.WorldObjects.Characters;
using Keen.VRage.Core;
using Keen.VRage.Core.Game.Components;
using Keen.VRage.Core.Game.Data;
using Keen.VRage.Core.Game.GameSystems.OWT;
using Keen.VRage.Core.Game.GameSystems.Queries;
using Keen.VRage.Core.Game.Systems;
using Keen.VRage.Core.Systems;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.DCS.Components;
using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Memory;
using Keen.VRage.Library.Reflection.DependencyInjections;
using Keen.VRage.Physics;
using Keen.VRage.Physics.Queries;
using Keen.VRage.Physics.Utils;
using Keen.VRage.Render.Contracts;
using Keen.VRage.Render.Data;

namespace SE2VR.Client.Components;

/// <summary>
/// Component for rendering a copy of the paint tool crosshair at the interaction point of every other tool
/// </summary>
public partial class CrosshairSessionComponent : SessionComponent, IInSceneListener
{
    [Service]
    private readonly RenderContracts _contracts;

    [Service]
    private readonly IPhysics _physics;

    [Component]
    private readonly OpenVRSessionComponent _openvr;

    private RootEntity _entity;
    private ModelEntity _model;

    void IInSceneListener.OnAddedToScene()
    {
        var sphere = DefinitionManager.Instance.GetDefinitionsOfType<PaintToolRenderDefinition>().First().SphereModel;
        _entity = _contracts.CreateRootEntity("PaintToolBrushRoot", WorldTransform.Identity);
        _model = _contracts.CreateModelEntity("PaintToolBrushModel", sphere, RelativeTransform.Identity, _entity);
        _model.SetRenderFlags(RenderFlags.Visible | RenderFlags.SkipCulling | RenderFlags.ForceHighestLOD);

        UpdateCrosshair();
    }

    void IInSceneListener.OnBeforeRemovedFromScene()
    {
        _model.Dispose();
        _entity.Dispose();
    }

    [Before(typeof(UI3DSessionComponent.RenderJob))]
    [After(typeof(TransformsFinalized))]
    [After(typeof(RenderSubmissionBegin))]
    private class UpdateCrosshairJob : JobGroup;
    
    [UpdateCrosshairJob]
    private void UpdateCrosshair()
    {
        if (_openvr.InteractorEntity is null)
            return;

        var wt = _openvr.InteractorEntity.Data.GetWorldTransform();
        using var hits = new Buffer<SweepQueryHit>();
        _physics.CastRay(hits, new RayCastArgs
        {
            Position = wt.Position,
            Direction = wt.Orientation.GetForward() * 10
        }, CollisionPreset.Bodies);

        DetectionResult result = new DetectionResult();
        RaycastEntityDetectorHelper.ProcessHits(hits, new RayD(wt.Position, wt.Orientation.GetForward() * 10), ref result, new DetectionArgs
        {
            AllowDuplication = false,
            DetectChildEntities = true,
            UsePreciseColliderForBlocks = true,
            DetectProjections = true,
        }, Entity.Scene);

        Vector3D? finalHit = null;
        for (int i = 0; i < result.Entities.Count; i++)
        {
            if (result.Entities[i].TryGet<CharacterComponent>() is not null)
                continue;

            finalHit = result.Positions[i];
        }

        result.Dispose();
        
        if (finalHit != null)
        {
            _entity.UpdateTransform(new WorldTransform(finalHit.Value));
            _model.SetEntityCustomData(new PaintPreviewMeshData(0.05f, ColorSRGB.Yellow));
        }
        else
        {
            _model.SetEntityCustomData(new PaintPreviewMeshData(default, ColorSRGB.Yellow));
        }
    }
}
