using HarmonyLib;
using Keen.Game2.Client.GameSystems.CameraSystems;
using Keen.VRage.Core;
using Keen.VRage.Core.EngineComponents;
using Keen.VRage.Core.Game.Components;
using Keen.VRage.Core.Game.Data;
using Keen.VRage.Core.Game.Systems;
using Keen.VRage.Core.Render;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.DCS.Components;
using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Reflection.DependencyInjections;
using Keen.VRage.Render.Options;
using OpenVRAPI;
using SE2VR.Client.Patches;
using SE2VR.Client.Wrappers;
using SE2VR.Simulation;
using System.Runtime.InteropServices;
using System.Text.Json;
using Valve.VR;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace SE2VR.Client.Components;

/// <summary>
/// Manage the rendering of the HMD
/// </summary>
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

    private Vector2I _originalResolution;

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

            _originalResolution = renderOptions.Resolution;

            renderOptions.FullScreen = true;

            Logging.Info($"Resizing from {renderOptions.Resolution} to {new Vector2I((int)width, (int)height)}");
            renderOptions.Resolution = new Vector2I((int)width, (int)height);

            _options.GetOrCreatePart<RenderOptionsPart2>().SetResolution(new Vector2I((int)width, (int)height));

            uint x = 0, y = 0, w = 0, h = 0;
            OpenVR.ExtendedDisplay.GetEyeOutputViewport(EVREye.Eye_Left, ref x, ref y, ref w, ref h);
            Logging.Info($"{x} {y} {w} {h}");
        }

        //_overlay = new SimpleOverlay();
        GameWindowPatch.OnCursorVisibleChanged += GameWindowPatch_OnCursorVisibleChanged;
        _texturePtr = Marshal.AllocHGlobal(Marshal.SizeOf<D3D12TextureData_t>());

        Logging.Info($"{JsonSerializer.Serialize(OpenVREngineComponent.Instance.System.GetEyeToHeadTransform(EVREye.Eye_Left), new JsonSerializerOptions { WriteIndented = true, IncludeFields = true })}");
        Logging.Info($"{JsonSerializer.Serialize(OpenVREngineComponent.Instance.System.GetEyeToHeadTransform(EVREye.Eye_Right), new JsonSerializerOptions { WriteIndented = true, IncludeFields = true })}");
        Logging.Info($"{JsonSerializer.Serialize((Matrix)VRUtils.GetPerspectiveFovRhInfiniteComplementary(EVREye.Eye_Left, DefinitionManager.Instance.GetDefinitionsOfType<CameraDefinition>().FirstOrDefault()?.NearPlane ?? 0.1f), new JsonSerializerOptions { WriteIndented = true, IncludeFields = true })}");
        Logging.Info($"{JsonSerializer.Serialize((Matrix)VRUtils.GetPerspectiveFovRhInfiniteComplementary(EVREye.Eye_Right, DefinitionManager.Instance.GetDefinitionsOfType<CameraDefinition>().FirstOrDefault()?.NearPlane ?? 0.1f), new JsonSerializerOptions { WriteIndented = true, IncludeFields = true })}");
    }

    [Destructor]
    protected void Destroy()
    {
        if (_originalResolution != Vector2I.Zero)
        {
            _options.GetOrCreatePart<RenderDisplayOptionsPart>().Resolution = _originalResolution;
        }

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
            var body = Body.Data.GetWorldTransform();
            body.Position += Vector3D.Transform(_vrOptions.WorldOffset, body.Orientation);

            var eyeToHead = VRUtils.ToTransform(OpenVREngineComponent.Instance.System.GetEyeToHeadTransform(_currentPass));
            var eye = HMD * eyeToHead;
            eye.Position *= _vrOptions.GetScale();

            var head = body * HMD;
            head.Position *= _vrOptions.GetScale();

            var wt = body * eye;
            Body.GetSession<IDebugDraw>().DebugDraw.AddArrow(wt.Position, wt.Position + wt.Orientation.GetForward(), _currentPass == EVREye.Eye_Left ? ColorSRGB.Red : ColorSRGB.Green, null, 0.1, false, TimeSpan.FromMilliseconds(50));
            Body.GetSession<IDebugDraw>().DebugDraw.AddArrow(wt.Position, wt.Position + wt.Orientation.GetForward(), _currentPass == EVREye.Eye_Left ? ColorSRGB.Red : ColorSRGB.Blue, null, 0.1, false, TimeSpan.FromMilliseconds(50));
            Body.GetSession<IDebugDraw>().DebugDraw.AddLine(head.Position, head.Position + (head.Orientation.GetForward() / 4), ColorSRGB.Green, false);

            if (wt.IsValid() && !GameWindowPatch.CursorVisible)
            {
                CameraPatch.Transform = wt;

                if (!_vrOptions.CameraMode)
                    MatrixPatch.CurrentEye = _currentPass;
            }
            else
            {
                CameraPatch.Transform = null;
                MatrixPatch.CurrentEye = null;
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
