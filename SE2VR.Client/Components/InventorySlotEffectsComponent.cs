using Keen.Game2.Client.UI.TerminalScreen.Inventory;
using Keen.Game2.Client.WorldObjects.Items;
using Keen.VRage.Audio;
using Keen.VRage.Core.Game.Data;
using Keen.VRage.Core.Model;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.DCS.Components;
using Keen.VRage.Game.Components.Models;
using Keen.VRage.Library.Collections.Readers;
using Keen.VRage.Library.Reflection.DependencyInjections;
using Keen.VRage.Multiplayer.Annotations;
using SE2VR.Simulation.Components;

namespace SE2VR.Client.Components;

/// <summary>
/// Component that plays a pickup sound when <see cref="InventorySlotsInsertServerComponent"/> grabs an item
/// </summary>
public partial class InventorySlotEffectsComponent : Component
{
    [Configuration]
    private readonly InventoryEffectsSessionComponentConfiguration _config;

    [Component]
    private readonly InventorySlotSyncComponent _slots;

    [Component]
    private readonly ModelComponent _model;

    [Service]
    private readonly IAudioManager _audio;

    private ListReader<ModelDummy> _dummies;

    [Init]
    protected void Init()
    {
        _dummies = _model.Definition.DummiesByType[_slots.Definition.SlotDummy];
    }

    [Clients, InventorySlotsInsertServerComponent.OnItemInsertedSignal]
    private void OnItemInserted(int slotIndex)
    {
        if (_config.OnObjectClickAudioEvent is not { } audioEvent)
            return;
        
        using var instance = _audio.CreateAudioEventInstance3D(audioEvent);
        instance.UpdateTransform(Data.GetWorldTransform() * _dummies[slotIndex].Transform.Transform);
        instance.Play();
    }
}
