namespace SE2VR.Client.Input;

/// <summary>
/// Base interface for converting a VR input into an SE2 input
/// </summary>
public interface IInputType
{
    /// <summary>
    /// What format is the VR value?
    /// </summary>
    VRInputType VRInput { get; }

    /// <summary>
    /// Gets the value for the given and if it is different from the last sample
    /// </summary>
    (object Value, bool Changed, bool Active) GetValue(ulong input);

    /// <summary>
    /// Identifies how an action from VR should be read
    /// </summary>
    enum VRInputType
    {
        Digital,
        Analog,
        Skeletal
    }
}