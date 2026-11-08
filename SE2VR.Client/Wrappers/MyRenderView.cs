using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Keen.VRage.Library.Mathematics;

namespace SE2VR.Client.Wrappers;

/// <summary>
/// My RenderView.
/// Used to acccess internal type
/// </summary>
public class MyRenderView(object box)
{
#pragma warning disable KN023
    private static readonly PropertyInfo _invViewDProperty;
    private static readonly PropertyInfo _nearClippingProperty;
    private static readonly PropertyInfo _farClippingProperty;
    private static readonly PropertyInfo _veryFarClippingProperty;
    private static readonly PropertyInfo _lastUpdateWasSmoothProperty;
    private static readonly MethodInfo _setCameraParametersMethod;
    private static readonly MethodInfo _onFrameEndMethod;
    private static readonly PropertyInfo _viewDProperty;
    private static readonly PropertyInfo _projectionProperty;
    private static readonly MethodInfo _createProjectionMatricesMethod;
    private static readonly FieldInfo _cameraSpeedBufferField;
    private static readonly FieldInfo _lastFrameCameraPositionWithCutsField;
    private static readonly FieldInfo[] _temporalFields;
#pragma warning restore KN023

    public object Box => box;

    public MatrixD InvViewD => (MatrixD)_invViewDProperty.GetValue(box)!;
    public float NearClipping => (float)_nearClippingProperty.GetValue(box)!;
    public float FarClipping => (float)_farClippingProperty.GetValue(box)!;
    public float VeryFarClipping => (float)_veryFarClippingProperty.GetValue(box)!;
    public bool LastUpdateWasSmooth => (bool)_lastUpdateWasSmoothProperty.GetValue(box)!;

    public Vector3D LastCameraPosition
    {
        get => (Vector3D)_lastFrameCameraPositionWithCutsField.GetValue(box)!;
        set => _lastFrameCameraPositionWithCutsField.SetValue(box, value);
    }

    static MyRenderView()
    {
        var type = AccessTools.TypeByName("Keen.VRage.Render12.Primitives.RenderView");
        _invViewDProperty = AccessTools.Property(type, "InvViewD");
        _nearClippingProperty = AccessTools.Property(type, "NearClipping");
        _farClippingProperty = AccessTools.Property(type, "FarClipping");
        _veryFarClippingProperty = AccessTools.Property(type, "VeryFarClipping");
        _lastUpdateWasSmoothProperty = AccessTools.Property(type, "LastUpdateWasSmooth");
        _setCameraParametersMethod = AccessTools.Method(type, "SetCameraParameters");
        _onFrameEndMethod = AccessTools.Method(type, "OnFrameEnd");
        _viewDProperty = AccessTools.Property(type, "ViewD");
        _projectionProperty = AccessTools.Property(type, "Projection");
        _createProjectionMatricesMethod = AccessTools.Method(type, "CreateProjectionMatrices");
        _cameraSpeedBufferField = AccessTools.Field(type, "_cameraSpeedBuffer");

        _temporalFields =
        [
            _cameraSpeedBufferField,
            AccessTools.Field(type, "<LastFrameCameraPosition>k__BackingField"),
            _lastFrameCameraPositionWithCutsField = AccessTools.Field(type, "<LastFrameCameraPositionWithCuts>k__BackingField"),
            AccessTools.Field(type, "<JitterCounter>k__BackingField"),
            AccessTools.Field(type, "<MaxBufferedCameraSpeed>k__BackingField"),
            AccessTools.Field(type, "<LastUpdateWasSmooth>k__BackingField"),
        ];
    }

    public MyRenderView CreateCopy() => new(RuntimeHelpers.GetObjectValue(box)!);

    public void CopyTemporalState(MyRenderView? from)
    {
        if (from == null)
        {
            _cameraSpeedBufferField.SetValue(box, null);
            return;
        }

        foreach (var field in _temporalFields)
            field.SetValue(box, field.GetValue(from.Box));
    }

    public void SetCameraParameters(in MatrixD cameraTransform, in Matrix projection, float nearPlane, float farPlane, float veryFarPlane, bool smooth)
    {
        float fov = 2f * MathF.Atan(1f / projection.M22);
        var projectionOffset = new Vector2(projection.M31, projection.M32);
        _setCameraParametersMethod.Invoke(box, [cameraTransform, fov, nearPlane, farPlane, veryFarPlane, projectionOffset, smooth, false]);

        var viewD = (MatrixD)_viewDProperty.GetValue(box)!;
        var viewAt0 = viewD;
        viewAt0.M14 = viewAt0.M24 = viewAt0.M34 = 0;
        viewAt0.M41 = viewAt0.M42 = viewAt0.M43 = 0;
        viewAt0.M44 = 1;

        object? matrices = _createProjectionMatricesMethod.Invoke(null, [projection, viewAt0, viewD]);
        _projectionProperty.SetValue(box, matrices);
    }

    public void OnFrameEnd() => _onFrameEndMethod.Invoke(box, null);
}
