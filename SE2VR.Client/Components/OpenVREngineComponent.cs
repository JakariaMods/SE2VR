using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Keen.Game2.Client.RuntimeSystems.CoreScenes;
using Keen.Game2.Simulation;
using Keen.VRage.Core;
using Keen.VRage.Core.EngineComponents;
using Keen.VRage.Core.Game.Systems;
using Keen.VRage.Core.Systems;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Reflection.DependencyInjections;
using OpenVRAPI;
using SE2VR.Client.Input;
using SE2VR.Client.Patches;
using SE2VR.Simulation;
using Valve.VR;

namespace SE2VR.Client.Components;

/// <summary>
/// Engine component responsible for handling the lifetime of <see cref="OpenVR"/>. Why doesn't DI work for this?
/// </summary>
public partial class OpenVREngineComponent : EngineComponent, ISessionConfigurator
{
    private const string MANIFEST_FILE = ".vrmanifest";
    private const string HARMONY_ID = "com.se2vr.patches.client";

    public static OpenVREngineComponent Instance { get; private set; } = null!;

    /// <summary>
    /// Our public client harmony instance
    /// </summary>
    public Harmony Harmony { get; private set; }

    /// <summary>
    /// Job group that processes updated poses
    /// </summary>
    [Before(typeof(FrameUpdateSystem.VRageInputJob))]
    public class VREngineTick : JobGroup;

    [Service]
    private readonly IOptions _options;

    [Configuration]
    private readonly GameRenderComponentCoreConfiguration _renderConfig;

    public CVRSystem System = null!;
    public OpenVROptions VROptions = null!;

    public bool Initalized { get; private set; }

    /// Gameplay
    public RelativeTransform? Head;
    public RelativeTransform? LeftHand;
    public RelativeTransform? RightHand;

    [Init]
    protected new void Init()
    {
        Assert.Null(Instance);
        Instance = this;
        Initalized = false;

        Logging.Info($"Initializing {nameof(OpenVR)}");

        var error = EVRInitError.None;
        System = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Scene);

        // Okay, This isnt good. NOW we can brick booting :)
        if (error != EVRInitError.None)
            throw new Exception($"Failed to initialize {nameof(OpenVR)}");

        Logging.Debug("OpenVR booted with code {0}", error);

        uint hmd = OpenVR.k_unTrackedDeviceIndex_Hmd;
        string manufacturer = VRUtils.GetDeviceProperty(hmd, ETrackedDeviceProperty.Prop_ManufacturerName_String);
        string model = VRUtils.GetDeviceProperty(hmd, ETrackedDeviceProperty.Prop_ModelNumber_String);
        Logging.Info($"Headset: {manufacturer}-{model} ");
        
        string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, MANIFEST_FILE);
        Logging.Debug("OpenVR manifest located at '{0}'", path);
        var appError = OpenVR.Applications.AddApplicationManifest(path, true);

        if (appError != EVRApplicationError.None)
            throw new Exception($"Failed to initialize {nameof(OpenVR)} manifest");

        if (_options.TryGetPart<OpenVROptions>() is { } settings)
        {
            VROptions = settings;
        }
        else
        {
            VROptions = _options.GetOrCreatePart<OpenVROptions>();
            VROptions.ForceSerialization();
        }

        InteractorEntityComponent_UpdateInputContexts_Patch.Enabled = VROptions.ControlMode != ControlMode.Motion;

        GameFeatureConfig.EquipDelays = false;

        Logging.Info($"Successfully initialized {nameof(OpenVR)}");
        Initalized = true;

        Harmony = new Harmony(HARMONY_ID);
        Harmony.PatchAll();

        _renderConfig.HideAlphaBanner();
    }

    [Destructor]
    protected void Destroy()
    {
        if (!Initalized)
            return;

        Initalized = false;

        string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, MANIFEST_FILE);
        OpenVR.Applications.RemoveApplicationManifest(path);

        OpenVR.Shutdown();
    }

    void ISessionConfigurator.ConfigureSession(SessionBuilder sessionBuilder)
    {
        if (!Initalized || sessionBuilder.IsServer)
            return;

        sessionBuilder.SessionComponents.WithComponent<OpenVRSessionComponent>();
        sessionBuilder.SessionComponents.WithComponent<VRArmsSessionComponent>();
        sessionBuilder.SessionComponents.WithComponent<VRBodyHidingSessionComponent>();
        sessionBuilder.SessionComponents.WithComponent<HapticsSessionComponent>();
        sessionBuilder.SessionComponents.WithComponent<PhysicsHandClientSessionComponent>();
        sessionBuilder.SessionComponents.WithComponent<VRPauseSessionComponent>();

        if (VROptions.ControlMode == ControlMode.Motion)
            sessionBuilder.SessionComponents.WithComponent<VRInteractionSessionComponent>();

        if (VROptions.ShowControllers)
            sessionBuilder.SessionComponents.WithComponent<VRControllersSessionComponent>();

        if (VROptions.CrouchHeight > 0)
            sessionBuilder.SessionComponents.WithComponent<VRCrouchingSessionComponent>();

        /*if (VROptions.ShowCrosshair)
            sessionBuilder.SessionComponents.WithComponent<CrosshairSessionComponent>();*/

        sessionBuilder.SceneBuilder.AddJobsFromAssembly(typeof(OpenVREngineComponent).Assembly);

        Logging.Info($"Registered jobs for {nameof(SE2VR)} Client");
    }

    [VREngineTick]
    private void Update()
    {
        if (!Initalized)
            return;

        var devicePoses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        System.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0, devicePoses);

        for (uint device = 0; device < OpenVR.k_unMaxTrackedDeviceCount; device++)
        {
            var activity = System.GetTrackedDeviceActivityLevel(device);
            bool lostTracking = activity == EDeviceActivityLevel.k_EDeviceActivityLevel_Unknown | activity == EDeviceActivityLevel.k_EDeviceActivityLevel_Standby | activity == EDeviceActivityLevel.k_EDeviceActivityLevel_Idle;

            var matrix = devicePoses[device].mDeviceToAbsoluteTracking;

            var trackedDeviceClass = System.GetTrackedDeviceClass(device);
            if (trackedDeviceClass == ETrackedDeviceClass.Controller && VROptions.ControlMode == ControlMode.Motion)
            {
                VRControllerState_t state = default;
                bool hasData = System.GetControllerState(device, ref state, (uint)Marshal.SizeOf<VRControllerState_t>());
                if (!hasData)
                    return;

                switch (System.GetControllerRoleForTrackedDeviceIndex(device))
                {
                    case ETrackedControllerRole.LeftHand:
                        if (lostTracking)
                            LeftHand = null;
                        else
                            LeftHand = VROptions.Rescale(new RelativeTransform(Quaternion.CreateFromYawPitchRoll(VROptions.PlaySpaceRotation, 0, 0)) * VRUtils.ToTransform(matrix));

                        if (LeftHand.HasValue && LeftHand.Value.IsValid())
                            LeftHand = LeftHand.Value with { Position = LeftHand.Value.Position + VROptions.WorldOffset };
                        else
                            LeftHand = null;

                        break;

                    case ETrackedControllerRole.RightHand:
                        if (lostTracking)
                            RightHand = null;
                        else
                            RightHand = VROptions.Rescale(new RelativeTransform(Quaternion.CreateFromYawPitchRoll(VROptions.PlaySpaceRotation, 0, 0)) * VRUtils.ToTransform(matrix));

                        if (RightHand.HasValue && RightHand.Value.IsValid())
                            RightHand = RightHand.Value with { Position = RightHand.Value.Position + VROptions.WorldOffset };
                        else
                            RightHand = null;

                        break;

                    default:
                        break;
                }
            }
            else if (trackedDeviceClass == ETrackedDeviceClass.HMD)
            {
                if (lostTracking)
                    Head = null;
                else
                    Head = VROptions.Rescale(new RelativeTransform(Quaternion.CreateFromYawPitchRoll(VROptions.PlaySpaceRotation, 0, 0)) * VRUtils.ToTransform(matrix));

                if (Head.HasValue)
                    Head = Head.Value with { Position = Head.Value.Position + VROptions.WorldOffset };
            }
        }
    }
}