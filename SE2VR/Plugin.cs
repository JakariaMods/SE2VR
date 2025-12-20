using Keen.Game2.Game.Plugins;
using Keen.Game2.Simulation;
using Keen.VRage.Core.EngineComponents;
using Keen.VRage.Render.EngineComponents;
using SE2VR.Client;
using SE2VR.Client.Components;
using SE2VR.Client.Input;
using SE2VR.Simulation;
using Valve.VR;

namespace SE2VR;

/// <summary>
/// Entry-point of the plugin
/// </summary>
public class Plugin : IPlugin
{
    private readonly bool _hmd = true;

    public Plugin(PluginHost plugins)
    {
        plugins.OnBeforeEngineInstantiated += Plugins_OnBeforeEngineInstantiated;

        if (plugins.Args.Any(x => x.Equals("-noHmd", StringComparison.InvariantCultureIgnoreCase)))
        {
            Logging.Error("Skipping VR initialization");
            _hmd = false;
        }

        // Lets not brick the game boot if no HMD is present
        if (!OpenVR.IsHmdPresent())
        {
            Logging.Error("HMD is not present");
            _hmd = false;
        }

        // Also dont brick boot if we dont at least have runtime...
        if (!OpenVR.IsRuntimeInstalled())
        {
            Logging.Error("{0} runtime is not installed", nameof(OpenVR));
            _hmd = false;
        }
    }

    private void Plugins_OnBeforeEngineInstantiated(EngineBuilder engineBuilder)
    {
        GameFeatureConfig.EquipDelays = false;

        if (_hmd)
        {
            engineBuilder.EntityBuilder.WithComponent<OpenVREngineComponent>();
            engineBuilder.EntityBuilder.WithComponent<OpenVRInputEngineComponent>();
            engineBuilder.EntityBuilder.WithComponent<VRRenderEngineComponent>();

            engineBuilder.SceneBuilder.AddJobsFromAssembly(typeof(OpenVREngineComponent).Assembly);

            //Disable render thread so that render command buffer is guaranteed to be in sync with render
            foreach (var ob in engineBuilder.EntityBuilder.OBsOfType<RenderObjectBuilder>())
            {
                ob.SyncRendering = true;
            }
        }

        engineBuilder.EntityBuilder.WithComponent<ConditionalVRServerEngineComponent>();
        engineBuilder.SceneBuilder.AddJobsFromAssembly(typeof(ConditionalVRServerEngineComponent).Assembly);
    }
}