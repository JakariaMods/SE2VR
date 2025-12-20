using Keen.Game2.Simulation.WorldObjects.Characters;
using Keen.VRage.Core;
using Keen.VRage.Core.Game.Components;
using Keen.VRage.Core.Game.RuntimeSystems.Components;
using Keen.VRage.Core.Game.RuntimeSystems.DebugDraw;
using Keen.VRage.Core.Render;
using Keen.VRage.Core.Systems;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.DCS.Annotations.Signals;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Reflection.DependencyInjections;
using Keen.VRage.Library.Serialization;
using Keen.VRage.Physics;

namespace SE2VR.Simulation.Components;

/// <summary>
/// Server component that allows a player to grab and move any physical object. It is a session component so that entity prefabs do not need to be modified.
/// </summary>
[ServerOnly]
public partial class PhysicsHandServerSessionComponent : SessionComponent
{
    [Service]
    private readonly IPhysics _physics;

    private VRHandData _hands; //Yes, this prototype only works for SP

    [Init]
    protected new void Init()
    {
        base.Init();
    }

    [AnyTime]
    [DebugDraw("Physics Hands")]
    private void DebugDraw([JobContext] IDebugDrawProvider debugDraw)
    {
        foreach (var character in Session.GetEntitiesOfType<CharacterComponent>())
        {
            debugDraw.GetBuilder(character.DEntity).AddSphere(_hands.LeftHand, 0.25f, ColorSRGB.Red);
            debugDraw.GetBuilder(character.DEntity).AddSphere(_hands.RightHand, 0.25f, ColorSRGB.Blue);
        }
    }

    [Keen.VRage.Multiplayer.Annotations.Server]
    [SyncHandDataSignal]
    private void SyncHandDataFromClient(VRHandData hands)
    {
        _hands = hands;
    }

    /// <summary>
    /// Data that is used to sync VR transforms to server
    /// </summary>
    [Serialize]
    public partial struct VRHandData
    {
        public RelativeTransform LeftHand;
        public bool LeftGrab;

        public RelativeTransform RightHand;
        public bool RightGrab;
    }

    /// <summary>
    /// Signal that synchronizes client's VR hands to server
    /// </summary>
    public partial class SyncHandDataSignal : SignalAttribute;
}
