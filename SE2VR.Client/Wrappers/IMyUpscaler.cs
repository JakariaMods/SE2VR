namespace SE2VR.Client.Wrappers;

/// <summary>
/// Interface for an upscaler implementation
/// </summary>
public interface IMyUpscaler
{
    /// <summary>
    /// Context of the upscaler, null when inactive/disabled/stale
    /// </summary>
    object? Context { get; set; }

    /// <summary>
    /// The settings <see cref="Context"/> was created with
    /// </summary>
    object ContextKey { get; }

    /// <summary>
    /// Dispose a context that is no longer assigned to the upscaler
    /// </summary>
    void DisposeContext(object context);
}
