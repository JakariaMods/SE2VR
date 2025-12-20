using System.Runtime.InteropServices;
using Valve.VR;

namespace SE2VR.Client.Input;

/// <summary>
/// Input that converts a given VR axis into an SE2 analog input
/// </summary>
public readonly struct AnalogInput : IInputType
{
    public IInputType.VRInputType VRInput => IInputType.VRInputType.Analog;

    public readonly bool Invert;

    /// <summary>
    /// Index within the input vector that will be used
    /// </summary>
    public readonly int Component;

    public AnalogInput(int component, bool invert = false)
    {
        Component = component;
        Invert = invert;
    }

    public (object Value, bool Changed, bool Active) GetValue(ulong input)
    {
        InputAnalogActionData_t analog = default;
        var error = OpenVR.Input.GetAnalogActionData(input, ref analog, (uint)Marshal.SizeOf<InputDigitalActionData_t>(), OpenVR.k_ulInvalidInputValueHandle);
        if (error != EVRInputError.None)
            throw new Exception(error.ToString());

        float value;
        float delta;

        switch (Component)
        {
            case (0):
                value = analog.x;
                delta = analog.deltaX;
                break;

            case (1):
                value = analog.y;
                delta = analog.deltaY;
                break;

            case (2):
                value = analog.z;
                delta = analog.deltaZ;
                break;

            default:
                throw new Exception($"Unexpected component {Component}");
        }

        return (value * (Invert ? -1 : 1), delta != 0, value != 0);
    }
}
