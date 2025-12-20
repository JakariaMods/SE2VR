using System.Runtime.InteropServices;
using Keen.Game2.Client.GameSystems.CameraSystems;
using Keen.VRage.Core;
using Keen.VRage.Core.EngineComponents;
using Keen.VRage.Core.Game.Components;
using Keen.VRage.Core.Game.Data;
using Keen.VRage.Core.Game.Systems;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.DCS.Components;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Reflection.DependencyInjections;
using Keen.VRage.Render.Options;
using Keen.VRage.UI.EngineComponents;
using OpenVRAPI;
using SE2VR.Client.Patches;
using SE2VR.Client.Wrappers;
using SE2VR.Simulation;
using Valve.VR;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace SE2VR.Client.Components;

/// <summary>
/// Manage the rendering of the HMD
/// </summary>
[DefaultTag("VRRender")]
public partial class VRRenderEngineComponent : EngineComponent
{
    public CameraComponent? Camera;
    public Entity? Body;
    public RelativeTransform HMD = RelativeTransform.Identity;

    [Service]
    private readonly IOptions _options;

    private EVREye _currentPass;
    private EVREye _preparedPass;
    private VRTextureBounds_t _imageBounds = new() { uMax = 1, vMax = 1 };
    private OpenVROptions _vrOptions;
    private SimpleOverlay? _overlay;

    private IntPtr _texturePtr;

    [Init]
    protected new void Init()
    {
        base.Init();

        if (!OpenVR.IsHmdPresent())
            return;

        Logging.Info($"Initializing {nameof(VRRenderEngineComponent)}");

        OpenVR.Compositor.SetExplicitTimingMode(EVRCompositorTimingMode.Explicit_ApplicationPerformsPostPresentHandoff);

        RenderCommandBufferPatch.PreCommit += OverrideCamera; //60hz
        Render12EngineComponentPatch.PostDraw += PostDraw; //up to 200hz
        
        uint width = 0, height = 0;
        OpenVR.System.GetRecommendedRenderTargetSize(ref width, ref height);

        _vrOptions = _options.GetOrCreatePart<OpenVROptions>();

        if (!_vrOptions.CameraMode)
        {
            var renderOptions = _options.GetOrCreatePart<RenderDisplayOptionsPart>();
            renderOptions.FullScreen = true;
            renderOptions.Resolution = new Vector2I((int)width, (int)height);

            if (_options.GetOrCreatePart<RenderOptionsPart2>().AntiAliasing == RenderOptions.AntiAliasing.FSR)
                _options.GetOrCreatePart<RenderOptionsPart2>().AntiAliasing = RenderOptions.AntiAliasing.FXAA;
        }

        //_overlay = new SimpleOverlay();

        GameWindowPatch.OnCursorVisibleChanged += GameWindowPatch_OnCursorVisibleChanged;
        _texturePtr = Marshal.AllocHGlobal(Marshal.SizeOf<D3D12TextureData_t>());
    }

    [Destructor]
    protected void Destroy()
    {
        _overlay?.Dispose();
        _overlay = null;

        Marshal.FreeHGlobal(_texturePtr);
    }

    private void GameWindowPatch_OnCursorVisibleChanged(bool visible)
    {
        if (visible)
        {
            OpenVR.Overlay?.ShowDashboard("system.desktop.1");
        }
        else
        {

        }
        /*if (visible)
        {
            _overlay.Show();
        }
        else
        {
            _overlay.Hide();
        }*/
    }

    private void OverrideCamera()
    {
        if (OpenVR.Compositor == null)
            return;

        if (_preparedPass == _currentPass)
        {
            Logging.Debug($"Already Prepared {_currentPass}");
            return;
        }

        _preparedPass = _currentPass;

        switch (_currentPass)
        {
            case EVREye.Eye_Left:
                TrackedDevicePose_t[] pRenderPoseArray = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
                TrackedDevicePose_t[] pGamePoseArray = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
                OpenVR.Compositor.WaitGetPoses(pRenderPoseArray, pGamePoseArray);
                Logging.Debug($"WaitGetPoses");

                var matrix = pGamePoseArray[OpenVR.k_unTrackedDeviceIndex_Hmd].mDeviceToAbsoluteTracking;
                HMD = _vrOptions.Rescale(new RelativeTransform(Quaternion.CreateFromYawPitchRoll(OpenVREngineComponent.Instance.VROptions.PlaySpaceRotation, 0, 0)) * VRUtils.ToTransform(matrix));

                break;

            case EVREye.Eye_Right:
                break;

            default:
                throw new Exception($"Unexpected render pass {_currentPass}");
        }

        if (Camera != null && Body != null)
        {
            WorldTransform wt = Body.Data.GetWorldTransform();
            wt.Position += WorldTransform.TransformDirection(_vrOptions.WorldOffset, wt);
            wt *= HMD;

            if (GameWindowPatch.CursorVisible)
            {
                MatrixPatch.CurrentEye = null;
                CameraPatch.Transform = null;
            }
            else
            {
                if (_vrOptions.CameraMode)
                {
                    MatrixPatch.CurrentEye = null;
                }
                else
                {
                    var eyeMatrix = OpenVREngineComponent.Instance.System.GetEyeToHeadTransform(_currentPass);
                    wt.Position += Vector3.Transform(VRUtils.ToTransform(eyeMatrix).Position, wt.Orientation) * _vrOptions.GetScale();
                    MatrixPatch.CurrentEye = _currentPass;
                }

                if (wt.IsValid())
                    CameraPatch.Transform = wt;
            }

            Camera.UpdateRenderSettings();
        }
    }

    private void PostDraw()
    {
        if (OpenVR.Compositor == null)
            return;

        switch (_currentPass)
        {
            case EVREye.Eye_Left:
                PresentEye(_currentPass);
                _currentPass = EVREye.Eye_Right;
                break;

            case EVREye.Eye_Right:
                PresentEye(_currentPass);
                OpenVR.Compositor.PostPresentHandoff();
                Logging.Debug("Handoff");
                _currentPass = EVREye.Eye_Left;
                break;

            default:
                throw new Exception($"Unexpected render pass {_currentPass}");
        }
    }

    private void PresentEye(EVREye eye)
    {
        if (OpenVR.Compositor == null)
            return;

        IDXGISwapChain3 swapchain = MyCoreSystems.SwapChain.GetD3DSwapChain();
        ID3D12Resource resource = swapchain.GetBuffer<ID3D12Resource>(swapchain.CurrentBackBufferIndex);
        ID3D12CommandQueue queue = MyCoreSystems.DeviceContext.GetPresentQueue().GetQueue();
        
        var data = new D3D12TextureData_t
        {
            m_pResource = resource.NativePointer,
            m_pCommandQueue = queue.NativePointer
        };
        Marshal.StructureToPtr(data, _texturePtr, false);

        var input = new Texture_t
        {
            eColorSpace = EColorSpace.Auto,
            eType = ETextureType.DirectX12,
            handle = _texturePtr
        };

        var error = OpenVR.Compositor.Submit(eye, ref input, ref _imageBounds, EVRSubmitFlags.Submit_Default);
        Logging.Debug($"Uploading {eye} Error:{error}");

        if (eye == EVREye.Eye_Left)
        {
            _overlay?.SetTexture(ref input);
        }
    }
}
