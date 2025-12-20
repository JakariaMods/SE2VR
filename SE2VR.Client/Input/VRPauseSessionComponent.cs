using System.Reflection;
using System.Runtime.InteropServices;
using Keen.Game2.Client.GameSystems.GPS;
using Keen.Game2.Client.UI.InGame;
using Keen.Game2.Client.UI.TerminalScreen;
using Keen.Game2.Client.WorldObjects.ColonizationMap;
using Keen.Game2.Simulation.StreamedUI.Terminal;
using Keen.VRage.Core;
using Keen.VRage.Core.Game.Components;
using Keen.VRage.Core.Game.Systems;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.DCS.Components;
using Keen.VRage.Input;
using Keen.VRage.Library.Reflection.DependencyInjections;
using Keen.VRage.Library.Utils;
using Keen.VRage.UI.EngineComponents;
using SE2VR.Client.Components;
using SE2VR.Client.Patches;
using Valve.VR;

namespace SE2VR.Client.Input;

/// <summary>
/// Session component for handling pausing in VR when the dashboard is opened
/// </summary>
public partial class VRPauseSessionComponent : SessionComponent
{
    [Service]
    private readonly SessionInGameUISessionComponent _ui;

    [Service]
    private readonly ColonizationMapSessionComponent? _map;

    [Component]
    private readonly GPSMarkerRenderSessionComponent _gps;

    private FieldInfo _menuField;
    private IObservableDisposable? _terminal;
    
    [Init]
    protected new void Init()
    {
        base.Init();
        
        _menuField = typeof(SessionInGameUISessionComponent).GetField("_inGameMenuScreen", BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    [SessionInGameUISessionComponent.OnTerminalOpenedSignal]
    private void OnTerminalOpened(IObservableDisposable screen, TerminalScreenViewModel terminalScreenViewModel, InteractionInfo interactionInfo)
    {
        if (_terminal == screen)
            return;

        _terminal = screen;
        _terminal.OnDisposed += OnTerminalDisposed;
    }

    private void OnTerminalDisposed(IObservableDisposable obj)
    {
        _terminal = null;
    }

    [DoNotPause]
    [OpenVRInputEngineComponent.OpenVRInput]
    private void UpdateDashboardState()
    {
        var vrEvent = new VREvent_t();
        uint eventSize = (uint)Marshal.SizeOf<VREvent_t>();

        while (OpenVR.System.PollNextEvent(ref vrEvent, eventSize))
        {
            EVREventType type = (EVREventType)vrEvent.eventType;

            switch (type)
            {
                case EVREventType.VREvent_DashboardActivated:
                    if (GameWindowPatch.CursorVisible || _terminal != null)
                        return;

                    _ui.CreateInGameMenuScreen();
                    break;

                case EVREventType.VREvent_DashboardDeactivated:
                    if (!GameWindowPatch.CursorVisible)
                        return;

                    OpenVREngineComponent.Instance.Entity.Get<GameInputProcessorComponent>().PressOnce(VRageCore.Instance.Engine.Get<UIEngineComponent>().InputManager.DefaultScreenClosingAction);

                    (_menuField?.GetValue(_ui) as IObservableDisposable)?.Dispose();
                    _terminal?.Dispose();

                    break;

                default:
                    //Do nothing for other events
                    break;
            }
        }
    }
}
