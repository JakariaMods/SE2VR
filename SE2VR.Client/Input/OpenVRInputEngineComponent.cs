using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using Keen.VRage.Core.EngineComponents;
using Keen.VRage.Core.Game.Systems;
using Keen.VRage.Core.Platform;
using Keen.VRage.Core.Systems;
using Keen.VRage.DCS.Annotations;
using Keen.VRage.DCS.Components;
using Keen.VRage.Input;
using Keen.VRage.Library.Collections;
using Keen.VRage.Library.Definitions;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Library.Extensions;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Library.Reflection.DependencyInjections;
using SE2VR.Client.Components;
using Valve.VR;
using static SE2VR.Client.Components.OpenVREngineComponent;

namespace SE2VR.Client.Input;

/// <summary>
/// Component responsible for handling/invoking VR input.
/// https://github-wiki-see.page/m/corycorvus/VR-Input-Wiki/wiki/SteamVR-Input-Binding
/// https://github.com/ValveSoftware/openvr/wiki/SteamVR-Input
/// https://github.com/OpenVR-Advanced-Settings/OpenVR-AdvancedSettings/blob/master/docs/SteamVRInputGuide.md
/// </summary>
public partial class OpenVRInputEngineComponent : Component
{
    public static OpenVRInputEngineComponent Instance { get; private set; } = null!;

    private const string ACTIONS_FILE = "actions.json";

    /// <summary>
    /// Job group where VR input is processed
    /// </summary>
    [Before(typeof(FrameUpdateSystem.VRageInputJob))]
    [After(typeof(VREngineTick))]
    public class OpenVRInput : JobGroup;

    [Service]
    private readonly IOptions _options;

    [Service]
    private readonly IPlatformWindows _platform;

    [Component]
    private readonly EngineDataLoaderComponent _engineDataLoader;

    [Component]
    private readonly OpenVREngineComponent _system;

    [Component]
    private readonly GameInputProcessorComponent _input;

    public readonly Dictionary<ulong, VRInputSet> ActionSets = new();
    private VRActiveActionSet_t[] _pushedActionSets = null!;
    private OpenVROptions _vrOptions = null!;

    [Init]
    protected void Init()
    {
        Assert.Null(Instance);
        Instance = this;

        if (!OpenVREngineComponent.Instance.Initalized)
            return;

        _vrOptions = _options.GetOrCreatePart<OpenVROptions>();

        string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, ACTIONS_FILE);
        if (OpenVR.Input.SetActionManifestPath(path) != EVRInputError.None)
            throw new Exception($"Failed to set VR action manifest at path {path}");

        AddMapping("/actions/default", "/actions/default/in/InteractPrimary", new DigitalInput(),
        [
            new Guid("b065cbdb-e7c0-4fc5-a2b8-72278e042995"),
            new Guid("9a9cffa0-8648-41a2-8c86-458f4c704a76"),
            new Guid("7866c6f7-9b4a-4e19-ab7b-4ebf445ead8a"),
            new Guid("72e61d01-ce14-41e2-968c-6518a35e5235"),
            new Guid("ceeca5da-e11a-4ea5-8a16-4823ebea418e"),
            new Guid("8bc6ef33-831c-450b-bfba-7a6f9f41e324"),
            new Guid("7dff0428-9066-423e-abb6-0561ffe02ce1"),
            new Guid("13be7633-2aea-4301-b875-7e11a7d12ddd"),
            new Guid("9b669c09-2fbe-4960-9914-ddf408913381"),
            new Guid("f4228ad4-ae27-42a8-8226-5e7143c404d2")
        ], null);

        AddMapping("/actions/default", "/actions/default/in/InteractSecondary", new DigitalInput(),
        [
            new Guid("6207a6e9-aec1-47cd-a99f-7f7e19ca01f4"),
            new Guid("4a3d5a12-1c80-4693-b18c-c4530212cde2"),
            new Guid("2e5c11ee-6b15-4225-983c-8b91e9ec5d38"),
            new Guid("f1789abd-986b-464a-9c2a-7ebd13c53c25"),
            new Guid("571669cd-d93f-4e81-b471-4a2ffc84c4be"),
            new Guid("b652e273-94dd-40a8-b8c9-4fe59610fdb4")
        ], null);

        //Primary Joystick Look
        AddMapping("/actions/default", "/actions/default/in/Look", new AnalogInput(1, positiveOnly: true),
        [
            new Guid("f8517b34-838c-45bb-b3ee-4631b7ddcba4"), //LookUp
        ], null);
        AddMapping("/actions/default", "/actions/default/in/Look", new AnalogInput(1, invert: true, positiveOnly: true),
        [
            new Guid("6b9d6675-0c44-47a6-9c44-2fbe8e52397d"), //LookDown
        ], null);
        AddMapping("/actions/default", "/actions/default/in/Look", new AnalogInput(0, invert: true, positiveOnly: true),
        [
            new Guid("9c78f323-ff0e-48ba-8e8c-b541d9094f42"), //LookLeft
        ], null);
        AddMapping("/actions/default", "/actions/default/in/Look", new AnalogInput(0, positiveOnly: true),
        [
            new Guid("2326e7e3-eafc-4072-b99b-c1749c278dea"), //LookRight
        ], null);

        //Secondary Joystick Forward/Backward movement
        AddMapping("/actions/default", "/actions/default/in/Move", new AnalogInput(1),
        [
            new Guid("70e8a38b-ee2b-4fc1-8edf-6da0e27607e0"),
        ], null);
        //Secondary Joystick Left/Right movement
        AddMapping("/actions/default", "/actions/default/in/Move", new AnalogInput(0),
        [
            new Guid("d4529b0d-bb1e-4d0b-a61c-72d21a1e216c"),
        ], null);

        AddMapping("/actions/default", "/actions/default/in/MoveUp", new DigitalInput(),
        [
            new Guid("fc7128fe-2609-4e1d-b501-e8c5051777d1"),
            new Guid("383e31b2-7c33-45bb-8e6d-60505e3b1602"),
        ], null);

        AddMapping("/actions/default", "/actions/default/in/MoveDown", new DigitalInput(),
        [
            new Guid("5e4ea1d9-1ccb-4227-8a76-f54f384c2295")
        ], null);

        AddMapping("/actions/default", "/actions/default/in/SpeedBoost", new DigitalInput(),
        [
            new Guid("f4893db3-3a7c-415a-8b69-6aee4283515a"),
            new Guid("314f02e6-f6d4-4cc5-9497-4e175b3d2f50"),
        ], null);

        AddMapping("/actions/default", "/actions/default/in/RollRight", new DigitalInput(),
        [
            new Guid("d849517e-1b28-4e40-bc04-f6c72661e67c"),
        ], null);
        AddMapping("/actions/default", "/actions/default/in/RollLeft", new DigitalInput(),
        [
            new Guid("06e925c6-e7e3-4bc5-a03c-ca19fcf898c3"),
        ], null);
        AddMapping("/actions/default", "/actions/default/in/ToggleJetpack", new DigitalInput(),
        [
            new Guid("b2af6de8-b7da-4367-a1ef-c1226acf04ee"),
        ], null);
        AddMapping("/actions/default", "/actions/default/in/ToggleLights", new DigitalInput(),
        [
            new Guid("400f4dee-cb80-46cf-80b3-c7343dc84b9a"),
            new Guid("35eee78d-5fa0-4f15-b3f6-8b25a4db01f9"),
        ], null);

        AddMapping("/actions/default", "/actions/default/in/Pause", new DigitalInput(),
        [
            new Guid("3f82acb2-793c-4ad0-8e6b-b179c9fa8953"),
        ], null);

        AddMapping("/actions/default", "/actions/default/in/Inventory", new DigitalInput(),
        [
            new Guid("bc29b930-0174-48ca-a3c8-1fdbc73c8b7c"),
        ], null);

        AddMapping("/actions/default", "/actions/default/in/Map", new DigitalInput(),
        [
            new Guid("ceb695dd-3bb7-4c50-8549-d99f1eb92719"),
        ], null);

        var menuContext = new MenuActionSetContext(_platform);

        AddMapping("/actions/menu", "/actions/menu/in/UIUp", new DigitalInput(),
        [
            new Guid("eb1197bb-92fa-46af-98ee-d822504e241e"),
        ], menuContext);

        AddMapping("/actions/menu", "/actions/menu/in/UIRight", new DigitalInput(),
        [
            new Guid("30ce4e7c-a4b6-4c1e-b4fa-d4fdfa8109b9"),
        ], menuContext);

        AddMapping("/actions/menu", "/actions/menu/in/UIDown", new DigitalInput(),
        [
            new Guid("8c13b974-2979-4936-9811-21810a677cdb"),
        ], menuContext);

        AddMapping("/actions/menu", "/actions/menu/in/UILeft", new DigitalInput(),
        [
            new Guid("234928bc-b370-4103-8410-37da935f3581"),
        ], menuContext);
        
        AddMapping("/actions/build", "/actions/build/in/DeleteBlock", new DigitalInput(),
        [
            new Guid("0e386bb0-f6b2-474f-968d-bd346683eb77"),
            new Guid("f4228ad4-ae27-42a8-8226-5e7143c404d2")
        ], null);

        AddMapping("/actions/build", "/actions/build/in/SlideRotate", new DigitalInput(),
        [
            new Guid("a7bf55e2-53ed-420e-815b-0997ef423919"),
            new Guid("0d35492d-bac7-43a7-a174-85e62a5f163b")
        ], null);

        AddMapping("/actions/paint", "/actions/paint/in/CycleColor", new AnalogInput(1),
        [
            new Guid("39873574-65ed-4cca-881c-03a90278ca94"),
        ], null);

        AddMapping("/actions/default", "/actions/default/in/CycleToolbarTile", new AnalogInput(1),
        [
            new Guid("d7e89256-139e-4394-ba9f-094829416245"),
        ], null);

        PushMappings();
    }

    private partial void OnActionPressed(in VRInputHandle inputHandle, object? value, bool changed, bool active);

    public void PushMappings()
    {
        _pushedActionSets = ActionSets.Select(x => new VRActiveActionSet_t
        {
            ulActionSet = x.Key,
            ulRestrictedToDevice = OpenVR.k_ulInvalidInputValueHandle
        }).ToArray();
    }

    /// <summary>
    /// Adds a new VR mapping that relates a VR input to any SE2 input action. <see cref="PushMappings"/> should be called after all inputs are added.
    /// </summary>
    public void AddMapping(string actionSet, string action, IInputType type, Guid[] inputActions, IActionSetContext? context)
    {
        ulong actionHandle = 0;
        if (OpenVR.Input.GetActionHandle(action, ref actionHandle) != EVRInputError.None)
            throw new Exception("Input failure");

        ulong setHandle = 0;
        if (OpenVR.Input.GetActionSetHandle(actionSet, ref setHandle) != EVRInputError.None)
            throw new Exception("Input failure");

        var set = ActionSets.GetOrAdd(setHandle, (x) => new VRInputSet()
        {
            DebugName = actionSet,
            Context = context
        });
        
        set.Actions.Add(actionHandle, new VRInputHandle
        {
            DebugName = action,
            Actions = inputActions.ToImmutableArray(),
            InputType = type
        });
    }

    [DoNotPause]
    [OpenVRInput]
    public void UpdateInput()
    {
        if (!OpenVREngineComponent.Instance.Initalized)
            return;

        OpenVR.Input.UpdateActionState(_pushedActionSets, (uint)Marshal.SizeOf<VRActiveActionSet_t>());
        foreach (var set in ActionSets.Values)
        {
            if (set.Context == null)
                continue;

            bool wasActive = set.Context.IsActive;
            set.Context.UpdateActive();
            if (wasActive != set.Context.IsActive)
            {
                if (wasActive)
                {
                    //Cancel all inputs for this action set
                    foreach (var actions in set.Actions.Values)
                    {
                        foreach (var input in actions)
                        {
                            foreach (var inputAction in input.Actions)
                            {
                                if (!DefinitionManager.Instance.TryGetDefinition<InputActionDefinition>(inputAction, out var definition))
                                    continue;

                                _input.Release(definition);
                            }
                        }
                    }
                }
            }
        }

        foreach (var actionSet in _pushedActionSets)
        {
            var set = ActionSets[actionSet.ulActionSet];
            if (set.Context != null && !set.Context.IsActive)
                continue;

            foreach (var actionHandleSet in set.Actions)
            {
                foreach (var action in actionHandleSet.Value)
                {
                    var (value, changed, active) = action.InputType.GetValue(actionHandleSet.Key);
                    if (!changed)
                        continue;

                    OnActionPressed(in action, value, changed, active);
                }
            }
        }
    }


    [OnActionPressedSignal]
    private void OnActionPressedImpl(in VRInputHandle inputHandle, object? value, bool changed, bool active)
    {
        if (active)
        {
            foreach (var input in inputHandle.Actions)
            {
                if (!DefinitionManager.Instance.TryGetDefinition<InputActionDefinition>(input, out var definition))
                    continue;

                if (value is bool boolValue)
                    _input.Hold(definition, boolValue);
                else if (value is float analogValue)
                    _input.Hold(definition, analogValue);
                else if (value is Vector2 pointerValue)
                    _input.Hold(definition, pointerValue);
                else
                    throw new Exception($"Unexpected input type {value?.GetType().Name}");
            }
        }
        else
        {
            foreach (var input in inputHandle.Actions)
            {
                if (!DefinitionManager.Instance.TryGetDefinition<InputActionDefinition>(input, out var definition))
                    continue;

                _input.Release(definition);
            }
        }
    }
}

/// <summary>
/// Mapping of a VR input to a collection of VRAGE inputs
/// </summary>
public struct VRInputHandle
{
    /// <summary>
    /// The data type of the input
    /// </summary>
    public IInputType InputType;

    /// <summary>
    /// The input actions that will be invoked. GUIDs are used since it is not guaranteed that definitions are loaded yet. This may change.
    /// </summary>
    public ImmutableArray<Guid> Actions;

    public string DebugName;
}

/// <summary>
/// Collection of action handles for an input set
/// </summary>
public struct VRInputSet
{
    public IActionSetContext? Context = null;

    public readonly ListDictionary<ulong, VRInputHandle> Actions = new();

    public string DebugName = string.Empty;

    public VRInputSet() { }
}