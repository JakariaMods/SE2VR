using System.Runtime.InteropServices;
using Keen.VRage.Library.Mathematics;
using Valve.VR;

namespace SE2VR.Client.Input;

/// <summary>
/// Simple analog input that converts VR joystick input to SE2 pointer input directly (XY only)
/// </summary>
public readonly struct PointerInput : IInputType
{
    public IInputType.VRInputType VRInput => IInputType.VRInputType.Digital;

    public readonly bool InvertX;
    public readonly bool InvertY;
    public readonly float Sensitivity;

    private readonly OpenVROptions? _settings;

    public PointerInput(OpenVROptions? settings, float sensitivity = 1, bool invertX = false, bool invertY = false)
    {
        _settings = settings;
        Sensitivity = sensitivity;
        InvertX = invertX;
        InvertY = invertY;
    }

    public (object Value, bool Changed, bool Active) GetValue(ulong input)
    {
        InputAnalogActionData_t pointer = default;
        var error = OpenVR.Input.GetAnalogActionData(input, ref pointer, (uint)Marshal.SizeOf<InputAnalogActionData_t>(), OpenVR.k_ulInvalidInputValueHandle);
        if (error != EVRInputError.None)
            throw new Exception(error.ToString());

        return (new Vector2(pointer.x * (InvertX ? -1 : 1), pointer.y * (InvertY ? -1 : 1)) * (_settings?.Sensitivity ?? 1) * Sensitivity, pointer.deltaX != 0 || pointer.deltaY != 0, pointer.x != 0 || pointer.y != 0);
    }
}