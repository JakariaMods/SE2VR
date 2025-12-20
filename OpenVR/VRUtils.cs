using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Keen.VRage.Core;
using Keen.VRage.Library.Mathematics;
using Valve.VR;

namespace OpenVRAPI;

/// <summary>
/// Helper class for converting from <see cref="OpenVR"/> data to VRAGE 3 data
/// </summary>
public static class VRUtils
{
    /// <summary>
    /// Convert VR matrix to VRage transform
    /// </summary>
    public static RelativeTransform ToTransform(HmdMatrix34_t matrix)
    {
        Vector3 position = new(matrix.m3, matrix.m7, matrix.m11);

        Quaternion rotation = new();
        rotation.W = MathF.Sqrt(1.0f + matrix.m0 + matrix.m5 + matrix.m10) / 2.0f;
        rotation.X = ((matrix.m9 - matrix.m6) / (4 * rotation.W));
        rotation.Y = ((matrix.m2 - matrix.m8) / (4 * rotation.W));
        rotation.Z = (matrix.m4 - matrix.m1) / (4 * rotation.W);
        rotation.Normalize();

        return new RelativeTransform(position, rotation);
    }

    /// <summary>
    ///From https://github.com/Math0424/SpaceEngineersVR
    /// </summary>
    public static MatrixD GetPerspectiveFovRhInfiniteComplementary(EVREye eye, double nearPlane)
    {
        float left = 0f, right = 0f, top = 0f, bottom = 0f;
        OpenVR.System.GetProjectionRaw(eye, ref left, ref right, ref top, ref bottom);
        
        //Adapted from decompilation of Matrix.CreatePerspectiveFovRhInfiniteComplementary, Matrix.CreatePerspectiveFieldOfView
        //and https://github.com/ValveSoftware/openvr/wiki/IVRSystem::GetProjectionRaw

        double idx = 1d / (right - left);
        double idy = 1d / (bottom - top);
        double sx = right + left;
        double sy = bottom + top;

        return new MatrixD(
            2d * idx, 0d, 0d, 0d,
            0d, 2d * idy, 0d, 0d,
            sx * idx, sy * idy, 0d, -1d,
            0d, 0d, nearPlane, 0d);
    }

    /// <summary>
    /// Convert VR controller axis to VRage Vector2
    /// </summary>
    public static Vector2 ToAxis(VRControllerAxis_t axis)
    {
        return new(axis.x, axis.y);
    }

    /// <summary>
    /// Get a property from a tracked device (such as device name)
    /// </summary>
    public static string GetDeviceProperty(uint deviceIndex, ETrackedDeviceProperty prop)
    {
        var error = ETrackedPropertyError.TrackedProp_Success;

        StringBuilder sb = new StringBuilder((int)OpenVR.k_unMaxPropertyStringSize);
        OpenVR.System.GetStringTrackedDeviceProperty(deviceIndex, prop, sb, OpenVR.k_unMaxPropertyStringSize, ref error);

        return sb.ToString();
    }

    /// <summary>
    /// Get enum of device type for a controller
    /// </summary>
    public static DeviceType GetDeviceType(uint deviceIndex)
    {
        string name = GetDeviceProperty(deviceIndex, ETrackedDeviceProperty.Prop_TrackingSystemName_String);
        if (name.Contains("Knuckles"))
            return DeviceType.ValveIndex;

        return DeviceType.Other;
    }

    public enum DeviceType
    {
        ValveIndex = 0,
        HTCVive = 1,
        Other = 1
    }
}