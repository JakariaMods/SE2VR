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
using Keen.VRage.Library.Utils;
using Keen.VRage.Render.Options;
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
public partial class VRRenderEngineComponent : EngineComponent
{
    public static VRRenderEngineComponent? Instance { get; private set; }

    public CameraComponent? Camera;
    public Entity? Body;

    public bool IsRightEye { get; private set; }

    [Service]
    private readonly IOptions _options;

    private OpenVROptions _vrOptions;
    private VRTextureBounds_t _imageBounds = new() { uMax = 1, vMax = 1 };
    private bool _originalFullScreen;
    private Vector2I _originalWindowedResolution;
    private Vector2I _originalFullscreenResolution;

    //All render thread
    private readonly TrackedDevicePose_t[] _renderPoses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
    private MatrixD _head = MatrixD.Identity;
    private readonly VREyeContext _eyeContext = new();
    private readonly VREyeFsr _fsr = new();
    private MyRenderView _centerView = null!;
    private MyRenderView _centerPreviousView = null!;
    private readonly Dictionary<EVREye, MyTexture> _eyeTextures = new();
    private readonly Dictionary<EVREye, MyRenderView> _eyeViews = new();
    private bool _eyeTexturesReady;

    [Init]
    protected new void Init()
    {
        base.Init();

        if (!OpenVR.IsHmdPresent())
            return;

        Logging.Info($"Initializing {nameof(VRRenderEngineComponent)}");
        Instance = this;

        OpenVR.Compositor.SetExplicitTimingMode(EVRCompositorTimingMode.Explicit_ApplicationPerformsPostPresentHandoff);

        CommitFramePatch.PreCommit += OverrideCamera; //Main thread

        SceneDrawSystemPatch.PreDraw += PreSceneDraw; //Render thread
        SceneDrawSystemPatch.PostDraw += PostSceneDraw; //Render thread
        Render12EngineComponentPatch.PreDraw += PreDraw; //Render thread
        Render12EngineComponentPatch.PostDraw += PostDraw; //Render thread
        TemporalResourcePatch.Created += _eyeContext.Register; //Render thread
        TemporalResourcePatch.Disposed += _eyeContext.Unregister; //Render thread

        uint width = 0, height = 0;
        OpenVR.System.GetRecommendedRenderTargetSize(ref width, ref height);

        _vrOptions = _options.GetOrCreatePart<OpenVROptions>();

        if (!_vrOptions.CameraMode)
        {
            var renderOptions = _options.GetOrCreatePart<RenderDisplayOptionsPart>();

            _originalFullScreen = renderOptions.FullScreen;
            _originalWindowedResolution = renderOptions.WindowedResolution;
            _originalFullscreenResolution = renderOptions.FullscreenResolution;

            renderOptions.FullScreen = true;

            Logging.Info($"Resizing from {renderOptions.Resolution} to {new Vector2I((int)width, (int)height)}");
            renderOptions.Resolution = new Vector2I((int)width, (int)height);

            _options.GetOrCreatePart<RenderOptionsPart2>().SetResolution(new Vector2I((int)width, (int)height));
        }

        GameWindowPatch.OnCursorVisibleChanged += GameWindowPatch_OnCursorVisibleChanged;
    }

    [Destructor]
    protected void Destroy()
    {
        CommitFramePatch.PreCommit -= OverrideCamera;
        SceneDrawSystemPatch.PreDraw -= PreSceneDraw;
        SceneDrawSystemPatch.PostDraw -= PostSceneDraw;
        Render12EngineComponentPatch.PreDraw -= PreDraw;
        Render12EngineComponentPatch.PostDraw -= PostDraw;
        TemporalResourcePatch.Created -= _eyeContext.Register;
        TemporalResourcePatch.Disposed -= _eyeContext.Unregister;

        GameWindowPatch.OnCursorVisibleChanged -= GameWindowPatch_OnCursorVisibleChanged;

        if (Instance == this)
            Instance = null;

        if (_vrOptions != null && !_vrOptions.CameraMode)
        {
            var renderOptions = _options.GetOrCreatePart<RenderDisplayOptionsPart>();
            renderOptions.FullScreen = _originalFullScreen;
            renderOptions.WindowedResolution = _originalWindowedResolution;
            renderOptions.FullscreenResolution = _originalFullscreenResolution;

            EngineOptionsStorageEngineComponent.NeedsUpdate?.Invoke();
        }
    }

    private void GameWindowPatch_OnCursorVisibleChanged(bool visible)
    {
        if (visible)
            OpenVR.Overlay?.ShowDashboard(_vrOptions.DashboardWindow);
    }

    private void OverrideCamera()
    {
        if (Camera == null || Body == null)
            return;

        var body = Body.Data.GetWorldTransform();
        body.Position += Vector3D.Transform(_vrOptions.WorldOffset, body.Orientation);

        var wt = _vrOptions.CameraMode ? body * (OpenVREngineComponent.Instance.Head ?? RelativeTransform.Identity) : body;
        CameraPatch.Transform = wt.IsValid() ? wt : null;

        Camera.UpdateRenderSettings();
    }

    private void PreDraw()
    {
        if (OpenVR.Compositor == null)
            return;

        OpenVR.Compositor.WaitGetPoses(_renderPoses, []);

        var pose = _renderPoses[OpenVR.k_unTrackedDeviceIndex_Hmd];
        if (!pose.bPoseIsValid)
            return;

        _head = (MatrixD)(VRUtils.ToMatrix(pose.mDeviceToAbsoluteTracking) * Matrix.CreateRotationY(_vrOptions.PlaySpaceRotation));
        _head.Translation *= _vrOptions.GetScale();
    }

    private void PreSceneDraw(MySceneDrawSystem sceneDraw, object target)
    {
        //Draw left eye
        if (_vrOptions.CameraMode || IsRightEye)
            return;

        var settings = MyCoreSystems.Settings;
        _centerView = settings.RenderView;
        _centerPreviousView = settings.PreviousRenderView;

        if (!_centerView.LastUpdateWasSmooth)
            _eyeViews.Clear();

        MyCoreSystems.EndDraw();
        ApplyEyeViewToRender(EVREye.Eye_Left);
        MyCoreSystems.BeginDraw();
    }

    private void PostSceneDraw(MySceneDrawSystem sceneDraw, object target)
    {
        //Draw right eye
        if (_vrOptions.CameraMode || IsRightEye)
            return;

        var settings = MyCoreSystems.Settings;
        bool animationWasPaused = MyCoreSystems.AnimationIsPaused;
        try
        {
            _eyeViews[EVREye.Eye_Left] = settings.RenderView;
            CopyEyeToTexture(EVREye.Eye_Left, new MyTexture(target));

            MyCoreSystems.EndDraw();
            ApplyEyeViewToRender(EVREye.Eye_Right);
            IsRightEye = true;
            _eyeContext.BeginRightEye();
            _fsr.BeginRightEye();

            MyCoreSystems.AnimationIsPaused = true;
            MyCoreSystems.BeginDraw();

            sceneDraw.Draw(target);

            _eyeViews[EVREye.Eye_Right] = settings.RenderView;
            CopyEyeToTexture(EVREye.Eye_Right, new MyTexture(target));
            _eyeTexturesReady = true;
        }
        finally
        {
            if (IsRightEye)
            {
                _fsr.EndRightEye();
                _eyeContext.EndRightEye();
            }

            IsRightEye = false;
            MyCoreSystems.AnimationIsPaused = animationWasPaused;
            settings.RenderView = _centerView;
            settings.PreviousRenderView = _centerPreviousView;
        }

        foreach (var view in _eyeViews.Values)
            view.OnFrameEnd();

        _eyeViews[EVREye.Eye_Left].LastCameraPosition = _eyeViews[EVREye.Eye_Right].InvViewD.Translation;
    }

    private void ApplyEyeViewToRender(EVREye eye)
    {
        var history = _eyeViews.GetValueOrDefault(eye);
        
        MatrixD eyeLocal = VRUtils.ToMatrix(OpenVR.System.GetEyeToHeadTransform(eye));
        eyeLocal.Translation *= _vrOptions.GetScale();
        MatrixD eyeWorld = eyeLocal * _head * _centerView.InvViewD;

        var view = _centerView.CreateCopy();
        view.CopyTemporalState(history);

        if (eye == EVREye.Eye_Right)
            view.LastCameraPosition = _eyeViews[EVREye.Eye_Left].InvViewD.Translation;

        var projection = VRUtils.GetPerspectiveFovRhInfiniteComplementary(eye, _centerView.NearClipping);
        float fov = 2f * MathF.Atan(1f / (float)projection.M22);
        view.SetCameraParameters(eyeWorld, fov, _centerView.NearClipping, _centerView.FarClipping, _centerView.VeryFarClipping, new((float)projection.M31, (float)projection.M32), smooth: history != null);

        MyCoreSystems.Settings.RenderView = view;
        MyCoreSystems.Settings.PreviousRenderView = history ?? view;
    }

    private void CopyEyeToTexture(EVREye eye, MyTexture source)
    {
        var desc = source.D3DResource.Description;
        var res = new Vector2I((int)desc.Width, (int)desc.Height);

        if (!_eyeTextures.TryGetValue(eye, out var texture) || texture.Resolution != res)
        {
            texture?.Dispose();

            //Final texture
            DXGIFormatExtPatch.ForceFormat = true;
            texture = MyTexture.CreateRWResizableRenderTargetTexture(EnumInfo<EVREye>.GetName(eye), Format.R8G8B8A8_UNorm_SRgb, res, Format.R8G8B8A8_UNorm, 1, null, null);
            DXGIFormatExtPatch.ForceFormat = false;
            _eyeTextures[eye] = texture;
        }

        using var commandList = MyCoreSystems.CreateDirectCommandList(EnumInfo<EVREye>.GetName(eye));
        commandList.CopyResource(texture.Instance, source.Instance);
        commandList.TransitionTo(texture, ResourceStates.PixelShaderResource);
    }

    private void PostDraw()
    {
        if (OpenVR.Compositor == null)
            return;

        if (!_eyeTexturesReady)
        {
            CopyEyeToTexture(EVREye.Eye_Left, MyCoreSystems.FinalLDRTexture);
            MyCoreSystems.FlushQueues();
        }

        var queue = MyCoreSystems.DeviceContext.GetDirectQueue().GetQueue();
        SubmitToCompositor(EVREye.Eye_Left, _eyeTextures[EVREye.Eye_Left].D3DResource, queue);
        SubmitToCompositor(EVREye.Eye_Right, _eyeTextures[_eyeTexturesReady ? EVREye.Eye_Right : EVREye.Eye_Left].D3DResource, queue);

        _eyeTexturesReady = false;
        OpenVR.Compositor.PostPresentHandoff();
    }

    private unsafe void SubmitToCompositor(EVREye eye, ID3D12Resource resource, ID3D12CommandQueue queue)
    {
        var data = new D3D12TextureData_t
        {
            m_pResource = resource.NativePointer,
            m_pCommandQueue = queue.NativePointer
        };

        var input = new Texture_t
        {
            eColorSpace = EColorSpace.Auto,
            eType = ETextureType.DirectX12,
            handle = (IntPtr)(&data)
        };

        var error = OpenVR.Compositor.Submit(eye, ref input, ref _imageBounds, EVRSubmitFlags.Submit_Default);
        if (error != EVRCompositorError.None)
            Logging.Debug($"Submitting {eye} failed: {error}");
    }
}
