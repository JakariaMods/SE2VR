using Keen.VRage.Core;
using Keen.VRage.Core.Game.Components;
using Keen.VRage.Core.Systems;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.DCS.Annotations.Signals;
using Keen.VRage.DCS.Components;
using SE2VR.Client.Input;
using static SE2VR.Simulation.Components.PhysicsHandServerSessionComponent;

namespace SE2VR.Client.Components;

/// <summary>
/// Client session component that syncs hand transforms to the server
/// </summary>
public partial class PhysicsHandClientSessionComponent : SessionComponent
{
    public bool LeftGrab;
    public bool RightGrab;

    [Init]
    protected new void Init()
    {
        base.Init();
        OpenVRInputEngineComponent.OnActionPressedSignal.Subscribe(OpenVRInputEngineComponent.Instance.Entity, OnActionPressedImpl);
    }

    [Destructor]
    protected void Destroy()
    {
        OpenVRInputEngineComponent.OnActionPressedSignal.Unsubscribe(OpenVRInputEngineComponent.Instance.Entity, OnActionPressedImpl);
    }

    [CustomSignal(Signal = typeof(SyncHandDataSignal))]
    private partial void SyncHandData(VRHandData wt);

    private void OnActionPressedImpl(Entity _, in VRInputHandle inputHandle, object? value, bool changed, bool active)
    {
        if (inputHandle.DebugName.Contains("InteractPrimary"))
        {
            RightGrab = active;
        }
        else if (inputHandle.DebugName.Contains("InteractSecondary"))
        {
            LeftGrab = active;
        }
    }

    [OnDataReplication.ClientToServer]
    private void SyncHandDataClient()
    {
        SyncHandData(new VRHandData
        {
            LeftHand = OpenVREngineComponent.Instance.LeftHand ?? RelativeTransform.Identity,
            RightHand = OpenVREngineComponent.Instance.RightHand ?? RelativeTransform.Identity,
            LeftGrab = OpenVREngineComponent.Instance.LeftHand != null ? LeftGrab : false,
            RightGrab = OpenVREngineComponent.Instance.RightHand != null ? RightGrab : false
        });
    }
}
