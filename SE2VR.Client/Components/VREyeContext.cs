using SE2VR.Client.Wrappers;

namespace SE2VR.Client.Components;

/// <summary>
/// Gives the right eye its own context/history for temporal effects
/// </summary>
public class VREyeContext
{
    private readonly HashSet<object> _resources = new();
    private readonly Dictionary<object, TexturePair> _pairs = new();
    private readonly List<object> _exchanged = new();

    private MyRTGIContext? _leftRTGIContext;
    private MyRTGIContext? _rightRTGIContext;
    private bool _creatingRightContext;

    private object? _leftTLASBuffers;
    private object? _rightTLASBuffers;

    public void Register(object resource)
    {
        if (_creatingRightContext)
            return; //Don't duplicate the right eye itself

        _resources.Add(resource);
    }

    public void Unregister(object resource)
    {
        _resources.Remove(resource);

        if (!_exchanged.Contains(resource) && _pairs.Remove(resource, out var pair))
            pair.Dispose();
    }

    public void ResetContexts()
    {
        _rightRTGIContext?.Dispose();
        _rightRTGIContext = null;
    }

    public void BeginRightEye()
    {
        //Swap/create necessary textures
        MyCommandList? commandList = null;
        try
        {
            foreach (var resource in _resources)
            {
                var temporal = new MyTemporalResource(resource);
                if (!_pairs.TryGetValue(resource, out var pair))
                {
                    commandList ??= MyCoreSystems.CreateDirectCommandList(nameof(VREyeContext));
                    pair = new TexturePair
                    {
                        Resource0 = Clone(commandList, new MyTexture(temporal.Resource0)),
                        Resource1 = Clone(commandList, new MyTexture(temporal.Resource1)),
                        Swapped = temporal.Swapped,
                    };
                    _pairs[resource] = pair;
                }

                pair.Exchange(temporal);
                _exchanged.Add(resource);
            }

            _leftRTGIContext = MyRTGIContext.CurrentContext;
            if (_leftRTGIContext != null)
            {
                if (_rightRTGIContext == null) //Lazy
                {
                    _creatingRightContext = true;
                    _rightRTGIContext = MyRTGIContext.Create();
                    _creatingRightContext = false;
                }

                MyRTGIContext.CurrentContext = _rightRTGIContext;
            }

            _leftTLASBuffers = MyRayTracingScene.TLASBuffers;
            if (_leftTLASBuffers != null)
            {
                _rightTLASBuffers ??= MyRayTracingScene.CreateEmptyTLASBuffers(_leftTLASBuffers);
                MyRayTracingScene.TLASBuffers = _rightTLASBuffers;
            }
        }
        finally
        {
            commandList?.Dispose();
        }
    }

    public void EndRightEye()
    {
        if (_leftRTGIContext != null)
        {
            MyRTGIContext.CurrentContext = _leftRTGIContext;
            _leftRTGIContext = null;
        }

        if (_leftTLASBuffers != null)
        {
            MyRayTracingScene.TLASBuffers = _leftTLASBuffers;
            _leftTLASBuffers = null;
        }

        //Swap/destroy textures
        if (_exchanged.Count == 0)
            return;

        foreach (var resource in _exchanged)
        {
            var pair = _pairs[resource];
            if (_resources.Contains(resource))
            {
                pair.Exchange(new MyTemporalResource(resource));
                continue;
            }

            pair.Dispose();
            _pairs.Remove(resource);
        }

        _exchanged.Clear();
    }

    private static MyTexture Clone(MyCommandList commandList, MyTexture source)
    {
        var copy = source.CreateEmptyCopy("(RightCopy)");
        copy.Resize(commandList, source.Resolution);
        commandList.CopyResource(copy.Instance, source.Instance);

        return copy;
    }

    /// <summary>
    /// <see cref="MyTemporalResource"/>
    /// </summary>
    private class TexturePair : IDisposable
    {
        public MyTexture? Resource0;
        public MyTexture? Resource1;
        public bool Swapped;

        public void Dispose()
        {
            Resource0?.Dispose();
            Resource1?.Dispose();
            Resource0 = Resource1 = null;
        }

        public void Exchange(MyTemporalResource resource)
        {
            var (resource0, resource1, swapped) = (resource.Resource0, resource.Resource1, resource.Swapped);
            resource.Resource0 = Resource0!.Instance;
            resource.Resource1 = Resource1!.Instance;
            resource.Swapped = Swapped;

            (Resource0, Resource1, Swapped) = (new MyTexture(resource0), new MyTexture(resource1), swapped);
        }
    }
}
