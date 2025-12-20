using Keen.VRage.Core.EngineComponents;
using Keen.VRage.Core.Game.Systems;
using SE2VR.Simulation.Components;

namespace SE2VR.Simulation;

/// <summary>
/// Injects server components to the server session if needed.
/// <remarks>This component is attached to client as well! Server filtering is done during session configuration</remarks>
/// </summary>
public partial class ConditionalVRServerEngineComponent : EngineComponent, ISessionConfigurator
{
    public void ConfigureSession(SessionBuilder sessionBuilder)
    {
        //We may want jobs to run on server and client
        sessionBuilder.SceneBuilder.AddJobsFromAssembly(typeof(PhysicsHandServerSessionComponent).Assembly);
        Logging.Info($"Registered jobs for {nameof(SE2VR)} Server");

        if (!sessionBuilder.IsServer)
            return;

        sessionBuilder.SessionComponents.WithComponent<PhysicsHandServerSessionComponent>();
    }
}
