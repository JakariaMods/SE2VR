using System.Runtime.InteropServices;
using Valve.VR;

namespace SE2VR.Client.Input;

/// <summary>
/// Simple button input that converts VR digital input to SE2 digital input directly
/// </summary>
public readonly struct DigitalInput : IInputType
{
    public IInputType.VRInputType VRInput => IInputType.VRInputType.Digital;

    public (object Value, bool Changed, bool Active) GetValue(ulong input)
    {
        InputDigitalActionData_t digital = default;
        var error = OpenVR.Input.GetDigitalActionData(input, ref digital, (uint)Marshal.SizeOf<InputDigitalActionData_t>(), OpenVR.k_ulInvalidInputValueHandle);
        if (error != EVRInputError.None)
            throw new Exception(error.ToString());

        return (digital.bState, digital.bChanged, digital.bState);
    }
}
