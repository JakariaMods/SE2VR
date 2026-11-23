using Keen.Game2.Simulation;
using Keen.Game2.Simulation.WorldObjects.CubeBlocks;
using Keen.Game2.Simulation.WorldObjects.FloatingObjects;
using Keen.Game2.Simulation.WorldObjects.Items;
using Keen.VRage.Core.Game.Data;
using Keen.VRage.Core.Game.GameSystems.Queries;
using Keen.VRage.Core.Game.RuntimeSystems.Components;
using Keen.VRage.Core.Model;
using Keen.VRage.DCS.Accessors;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.DCS.Components;
using Keen.VRage.Game.Components.Models;
using Keen.VRage.Library.Collections.Readers;
using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Reflection.DependencyInjections;
using Keen.VRage.Library.Units;
using Keen.VRage.Physics;
using Keen.VRage.Physics.Queries;
using Buffer = Keen.VRage.Library.Memory.Buffer;

namespace SE2VR.Simulation.Components;

/// <summary>
/// <see cref="InventorySlotsInsertServerComponent"/>
/// </summary>
public partial class InventorySlotsInsertServerDefinition : Definition
{
    /// <summary>
    /// Detection radius around the slot dummy's origin
    /// </summary>
    [Length]
    public float InsertRadius { get; private set; } = 0.25f;
}

/// <summary>
/// Server component for blocks with <see cref="InventorySlotsServerComponent"/> that inserts any nearby floating object
/// </summary>
[ServerOnly]
public partial class InventorySlotsInsertServerComponent : Component
{
    [After(typeof(Physics))]
    [Before(typeof(OnInventoryChanged))]
    private class SlotInsertJob : JobGroup;

    [Definition]
    private readonly InventorySlotsInsertServerDefinition _definition;

    [Component]
    private readonly InventorySlotsServerComponent _slots;

    [Component]
    private readonly ModelComponent _model;

    [Service]
    private readonly IPhysics _physics;

    private int _slot;
    private ListReader<ModelDummy> _dummies;

    [Init]
    protected void Init()
    {
        _dummies = _model.Definition.DummiesByType[_slots.Definition.SlotDummy];
    }

    private partial void OnItemInserted(int slotIndex);

    [DoesNotHave(typeof(DisabledTag))]
    [SlotInsertJob]
    private void InsertTouchingFloatingObject()
    {
        int slot = _slot;
        _slot = (slot + 1) % _dummies.Count;

        var inventory = _slots.Inventories[slot];
        if (inventory.Items.Length != 0)
            return;

        var wt = Data.GetWorldTransform();
        var slotPosition = (wt * _dummies[slot].Transform.Transform).Position;

        using var hits = Buffer.Inline1<NearestQueryHit>();
        _physics.QueryPoint(hits, new PointQueryArgs(slotPosition, _definition.InsertRadius), CollisionPreset.Bodies);

        foreach (var hit in hits)
        {
            if (Entity.TryGetFromDataEntity(new DEntityContext(Entity.Scene, hit.Body))?.TryGet<FloatingObjectComponent>() is not { } floatingObject)
                continue;

            if (floatingObject.Amount <= 0 || floatingObject.ItemType is not { } itemType)
                continue;

            var added = inventory.AddItem(new ItemStack(itemType, floatingObject.Amount, floatingObject.MetaData));
            if (added > 0)
            {
                floatingObject.Amount -= added;
                OnItemInserted(slot);
                return;
            }
        }
    }
}
