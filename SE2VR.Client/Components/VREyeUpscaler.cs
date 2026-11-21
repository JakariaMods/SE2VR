using SE2VR.Client.Wrappers;

namespace SE2VR.Client.Components;

/// <summary>
/// Gives the right eye its own upscaler context/history
/// </summary>
public class VREyeUpscaler
{
    private object? _rightContext;
    private object? _rightKey;
    private object? _leftContext;
    private object? _leftKey;

    public void BeginRightEye(IMyUpscaler upscaler)
    {
        if (upscaler.Context == null)
        {
            if (_rightContext != null)
                upscaler.DisposeContext(_rightContext);

            _rightContext = null;
            return;
        }

        _leftContext = upscaler.Context;
        _leftKey = upscaler.ContextKey;

        if (_rightContext != null && !Equals(_rightKey, _leftKey))
        {
            upscaler.DisposeContext(_rightContext);
            _rightContext = null;
        }

        upscaler.Context = _rightContext;
    }

    public void EndRightEye(IMyUpscaler upscaler)
    {
        if (_leftContext == null)
            return;

        _rightContext = upscaler.Context;
        _rightKey = upscaler.ContextKey;

        if (!Equals(_leftKey, _rightKey))
        {
            upscaler.DisposeContext(_leftContext);
            _leftContext = null;
        }

        upscaler.Context = _leftContext;
        _leftContext = null;
    }
}
