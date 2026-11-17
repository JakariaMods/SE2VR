using Keen.Game2.Game.Helpers;
using Keen.VRage.Core.EngineComponents;
using Keen.VRage.Core.Plugins;
using Keen.VRage.Core.Project;
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
    private const string CONTENT_PROJECT = @"SE2VR.Content\SE2VR.Content.vrgproj";

    private readonly bool _hmd = true;

    public Plugin(PluginHost plugins)
    {
        plugins.OnBeforeEngineInstantiated += Plugins_OnBeforeEngineInstantiated;
        plugins.OnBeforeProjectsLoaded += Plugins_OnBeforeProjectsLoaded;

        if (plugins.Args.Any(x => x.Equals("-noHmd", StringComparison.InvariantCultureIgnoreCase)))
        {
            Logging.Error("Skipping VR initialization");
            _hmd = false;
        }

        // Lets not brick the game boot if no HMD is present
        if (!OpenVR.IsHmdPresent())
        {
            Logging.Error("HMD is not present, aborting plugin init");
            _hmd = false;
        }

        // Also dont brick boot if we dont at least have runtime...
        if (!OpenVR.IsRuntimeInstalled())
        {
            Logging.Error("{0} runtime is not installed", nameof(OpenVR));
            _hmd = false;
        }
    }

    private void Plugins_OnBeforeProjectsLoaded(List<VRageProject> projects)
    {
        if (!_hmd)
            return;

        string path = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!, CONTENT_PROJECT);
        if (!File.Exists(path))
        {
            Logging.Error($"Content project not found at '{path}'");
            return;
        }

        projects.Add(GameContent.GetProjectByPath(path));
    }

    private void Plugins_OnBeforeEngineInstantiated(EngineBuilder engineBuilder)
    {
        if (_hmd)
        {
            engineBuilder.EntityBuilder.WithComponent<OpenVREngineComponent>();
            engineBuilder.EntityBuilder.WithComponent<OpenVRInputEngineComponent>();
            engineBuilder.EntityBuilder.WithComponent<VRRenderEngineComponent>();
            engineBuilder.EntityBuilder.WithComponent<VRUIEngineComponent>();
            engineBuilder.EntityBuilder.WithComponent<VRLoadingEngineComponent>();

            engineBuilder.SceneBuilder.AddJobsFromAssembly(typeof(OpenVREngineComponent).Assembly);
        }

        engineBuilder.EntityBuilder.WithComponent<ConditionalVRServerEngineComponent>();
        engineBuilder.SceneBuilder.AddJobsFromAssembly(typeof(ConditionalVRServerEngineComponent).Assembly);
    }
}