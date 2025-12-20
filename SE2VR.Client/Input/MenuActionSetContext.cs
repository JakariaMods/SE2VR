using Keen.VRage.Core.Platform;

namespace SE2VR.Client.Input;

/// <summary>
/// Action set context that only enables when the mouse cursor is shown
/// </summary>
public class MenuActionSetContext : IActionSetContext
{
    public bool IsActive { get; private set; }

    private readonly IPlatformWindows _platform;

    public MenuActionSetContext(IPlatformWindows platform)
    {
        _platform = platform;
    }

    public void UpdateActive()
    {
        IsActive = _platform.Window.ShowCursor;
    }
}
