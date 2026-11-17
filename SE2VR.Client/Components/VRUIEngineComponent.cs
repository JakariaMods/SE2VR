using System.Runtime.InteropServices;
using Keen.VRage.Core.EngineComponents;
using Keen.VRage.Core.Game.Systems;
using Keen.VRage.Core.Platform;
using Keen.VRage.Core.Systems;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.Input;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Reflection.DependencyInjections;
using Keen.VRage.UI.EngineComponents;
using OpenVRAPI;
using SE2VR.Client.Input;
using SE2VR.Client.Patches;
using SE2VR.Client.Wrappers;
using Valve.VR;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace SE2VR.Client.Components;

/// <summary>
/// Engine component for VR UI interaction and render
/// </summary>
public partial class VRUIEngineComponent : EngineComponent
{
    private const string MENU_KEY = "se2vr.menu";
    private const string MENU_NAME = "Space Engineers 2 Menu";

    private const int MENU_TEXTURE_COUNT = 2;

    [After(typeof(OpenVRInputEngineComponent.OpenVRInput))]
    [Before(typeof(FrameUpdateSystem.VRageInputJob))]
    private class VRUIInput : JobGroup;

    [Service]
    private readonly IOptions _options;

    [Service]
    private readonly IPlatformWindows _platform;

    [Component]
    private readonly GameInputProcessorComponent _input;

    [Component]
    private readonly OpenVRInputEngineComponent _vrInput;

    [Component]
    private readonly UIEngineComponent _ui;

    [Component]
    private readonly OpenVREngineComponent _vr;

    private OpenVROptions _vrOptions = null!;
    private VROverlay? _menu;

    //Main thread
    private Matrix _menuTransform = Matrix.Identity;
    private bool _dirtyTransform;
    private float _menuWidth;
    private bool _menuShown;
    private EDeviceActivityLevel _headActivity = EDeviceActivityLevel.k_EDeviceActivityLevel_Unknown;
    private ETrackedControllerRole _pointingHand = ETrackedControllerRole.RightHand;
    private bool _leftTriggerWasHeld;
    private bool _rightTriggerWasHeld;
    private bool _clickHeld;
    private Vector2? _heldCursorPosition;

    //Shared
    private bool _menuVisible;
    private readonly object _menuResolutionLock = new();
    private Vector2I? _menuResolution;

    //Render
    private readonly MyTexture?[] _textures = new MyTexture?[MENU_TEXTURE_COUNT];
    private int _textureIndex;
    private MyTexture? _texture;
    private bool _textureDrawn;
    private bool _drawingUI;

    [Init]
    protected new void Init()
    {
        base.Init();

        if (!_vr.Initalized)
            return;

        _vrOptions = _options.GetOrCreatePart<OpenVROptions>();

        _menu = new VROverlay(MENU_KEY, MENU_NAME);
        _menu.SetFlag(VROverlayFlags.IgnoreTextureAlpha, true);

        MainUISystemPatch.PreDraw += PreDrawUI; //Render thread
        MainUISystemPatch.PostDraw += PostDrawUI; //Render thread
        Render12EngineComponentPatch.PostDraw += SubmitMenu; //Render thread
    }

    [Destructor]
    protected void Destroy()
    {
        MainUISystemPatch.PreDraw -= PreDrawUI;
        MainUISystemPatch.PostDraw -= PostDrawUI;
        Render12EngineComponentPatch.PostDraw -= SubmitMenu;

        GameFormPatch.CursorPosition = null;
        _menu?.Dispose();

        foreach (var texture in _textures)
            texture?.Dispose();
    }

    [DoNotPause]
    [VRUIInput]
    private void UpdateMenu()
    {
        if (_menu == null)
            return;

        var activity = OpenVR.System.GetTrackedDeviceActivityLevel(OpenVR.k_unTrackedDeviceIndex_Hmd);
        bool wasInactive = _headActivity == EDeviceActivityLevel.k_EDeviceActivityLevel_Unknown || _headActivity == EDeviceActivityLevel.k_EDeviceActivityLevel_Idle || _headActivity == EDeviceActivityLevel.k_EDeviceActivityLevel_Standby;

        if (activity == EDeviceActivityLevel.k_EDeviceActivityLevel_UserInteraction && wasInactive)
            _dirtyTransform = true; //Move in front of head when the headset is put on

        _headActivity = activity;

        bool shouldBeVisible = _platform.Window.ShowCursor;
        _menuVisible = shouldBeVisible;

        if (shouldBeVisible)
        {
            if (!_menuShown)
                _dirtyTransform = true;

            var headPose = _vr.DevicePoses[OpenVR.k_unTrackedDeviceIndex_Hmd];
            bool headTracked = headPose.bPoseIsValid && headPose.eTrackingResult == ETrackingResult.Running_OK;

            if (headTracked && _dirtyTransform)
            {
                ResetMenuTransform(headPose.mDeviceToAbsoluteTracking.ToMatrix());
                _menu.Show();
                _menuShown = true;
                _dirtyTransform = false;
            }

            if (_menuShown)
            {
                UpdateCursor();
                UpdateClick();
            }
        }
        else if (_menuShown)
        {
            HideMenu();
        }
    }

    private void ResetMenuTransform(Matrix head)
    {
        Assert.NotNull(_menu);

        var headForward = head.Forward;
        var forward = new Vector3(headForward.X, 0, headForward.Z);
        forward = Vector3.IsZero(forward) ? Vector3.Forward : Vector3.Normalize(forward);

        _menuTransform = Matrix.CreateWorld(head.Translation + forward * _vrOptions.MenuDistance, forward, Vector3.Up);
        _menuWidth = _vrOptions.MenuWidth;
        _menu.SetWidth(_menuWidth);
        _menu.SetTransform(_menuTransform);
    }

    private void UpdateCursor()
    {
        bool left = _vrInput.LeftTrigger;
        bool right = _vrInput.RightTrigger;

        if (left && !_leftTriggerWasHeld)
            _pointingHand = ETrackedControllerRole.LeftHand;
        else if (right && !_rightTriggerWasHeld)
            _pointingHand = ETrackedControllerRole.RightHand;

        _leftTriggerWasHeld = left;
        _rightTriggerWasHeld = right;

        Vector2I? menuResolution;
        lock (_menuResolutionLock)
            menuResolution = _menuResolution;

        if (menuResolution is not { } resolution || TryGetPointerRay(_pointingHand) is not { } ray || TryGetRayIntersection(ray, resolution) is not { } uv)
        {
            _menu!.ClearCursorPosition();
            return;
        }

        var position = new Vector2(uv.X * resolution.X, uv.Y * resolution.Y);
        _menu!.SetMouseScale(new Vector2(resolution.X, resolution.Y));
        _menu.SetCursorPosition(position);
        GameFormPatch.CursorPosition = position;

        if (_heldCursorPosition != position)
        {
            _input.Hold(_ui.InputManager.CursorMove, position);
            _heldCursorPosition = position;
        }
    }

    private void UpdateClick()
    {
        bool click = _vrInput.LeftTrigger;
        if (click == _clickHeld)
            return;

        if (click)
            _input.Hold(_ui.InputManager.CursorButton1);
        else
            _input.Release(_ui.InputManager.CursorButton1);

        _clickHeld = click;
    }

    private void HideMenu()
    {
        _menu!.ClearCursorPosition();
        _menu.Hide();
        _menuShown = false;
        GameFormPatch.CursorPosition = null;

        if (_clickHeld)
            _input.Release(_ui.InputManager.CursorButton1);

        if (_heldCursorPosition != null)
            _input.Release(_ui.InputManager.CursorMove);

        _clickHeld = false;
        _heldCursorPosition = null;
    }

    private Vector2? TryGetRayIntersection(Ray ray, Vector2I resolution)
    {
        var normal = _menuTransform.Backward;
        if (Vector3.Dot(ray.Direction, normal) > 0) //Behind the menu
            return null;

        if (ray.Intersects(new Plane(_menuTransform.Translation, normal)) is not { } distance)
            return null;

        var point = ray.Position + ray.Direction * distance;
        var local = point - _menuTransform.Translation;
        float height = _menuWidth * resolution.Y / resolution.X;

        var uv = new Vector2(Vector3.Dot(local, _menuTransform.Right) / _menuWidth + 0.5f, 0.5f - Vector3.Dot(local, _menuTransform.Up) / height);
        if (uv.X < 0 || uv.X > 1 || uv.Y < 0 || uv.Y > 1)
            return null;

        return uv;
    }

    private Ray? TryGetPointerRay(ETrackedControllerRole hand)
    {
        var data = new InputPoseActionData_t();
        var error = OpenVR.Input.GetPoseActionDataRelativeToNow(_vrInput.GetPointerAction(hand), ETrackingUniverseOrigin.TrackingUniverseStanding, 0, ref data, (uint)Marshal.SizeOf<InputPoseActionData_t>(), OpenVR.k_ulInvalidInputValueHandle);

        if (error == EVRInputError.None && data.bActive && data.pose.bPoseIsValid)
            return data.pose.mDeviceToAbsoluteTracking.ToRay();

        //Fallback if ray pose doesn't exist
        uint device = OpenVR.System.GetTrackedDeviceIndexForControllerRole(hand);
        if (device < OpenVR.k_unMaxTrackedDeviceCount && _vr.DevicePoses[device].bPoseIsValid)
            return _vr.DevicePoses[device].mDeviceToAbsoluteTracking.ToRay();

        return null;
    }

    private (bool Draw, MyTexture Target) PreDrawUI(MyCommandList commandList, MyTexture target)
    {
        if (!_menuVisible || _drawingUI)
            return (true, target);

        if (VRRenderEngineComponent.Instance?.IsRightEye == true)
            return (false, target); //Only draw once per frame

        _textureIndex = (_textureIndex + 1) % _textures.Length;
        _texture = _textures[_textureIndex];

        var resolution = target.Resolution;
        if (_texture == null || _texture.Resolution != resolution)
        {
            _texture?.Dispose();

            DXGIFormatExtPatch.ForceFormat = true;
            _texture = MyTexture.CreateRWResizableRenderTargetTexture($"VRMenu{_textureIndex}", Format.R8G8B8A8_UNorm_SRgb, resolution, Format.R8G8B8A8_UNorm, 1, null, null);
            DXGIFormatExtPatch.ForceFormat = false;

            _textures[_textureIndex] = _texture;
            lock (_menuResolutionLock)
                _menuResolution = resolution;
        }

        commandList.ClearRenderTargetView(_texture);
        return (true, _texture);
    }

    private void PostDrawUI(MyCommandList commandList, MyTexture target)
    {
        if (_texture == null || target.Instance != _texture.Instance)
            return;

        commandList.TransitionTo(_texture, ResourceStates.PixelShaderResource);
        _textureDrawn = true;
    }

    public void DrawMenuToScreen(MyTexture screen)
    {
        if (!_textureDrawn)
            return;

        _drawingUI = true;
        using (var commandList = MyCoreSystems.CreateDirectCommandList("VRMenuToScreen"))
            MyCoreSystems.DrawUI(commandList, screen);
        _drawingUI = false;
    }

    private unsafe void SubmitMenu()
    {
        if (!_textureDrawn || _texture == null || _menu == null)
            return;

        _textureDrawn = false;

        var data = new D3D12TextureData_t
        {
            m_pResource = _texture.D3DResource.NativePointer,
            m_pCommandQueue = MyCoreSystems.DeviceContext.GetDirectQueue().GetQueue().NativePointer
        };

        var texture = new Texture_t
        {
            eColorSpace = EColorSpace.Auto,
            eType = ETextureType.DirectX12,
            handle = (IntPtr)(&data)
        };

        _menu.SetTexture(ref texture);
    }
}
