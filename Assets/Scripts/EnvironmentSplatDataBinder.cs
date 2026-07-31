using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;

[VFXBinder("Environment Splat Data")]
public class EnvironmentSplatDataBinder : VFXBinderBase
{
    public SplatAnimator animator;

    [VFXPropertyBinding("System.UInt32")]
    private ExposedProperty _countProperty = "Count";

    [VFXPropertyBinding("UnityEngine.GraphicsBuffer")]
    private ExposedProperty _positionBufferProperty = "PositionBuffer";

    [VFXPropertyBinding("UnityEngine.GraphicsBuffer")]
    private ExposedProperty _axisBufferProperty = "AxisBuffer";

    [VFXPropertyBinding("UnityEngine.GraphicsBuffer")]
    private ExposedProperty _colorBufferProperty = "ColorBuffer";

    public override bool IsValid(VisualEffect component)
    {
        return animator != null &&
               animator.envSplat != null &&
               component.HasUInt(_countProperty) &&
               component.HasGraphicsBuffer(_positionBufferProperty) &&
               component.HasGraphicsBuffer(_axisBufferProperty) &&
               component.HasGraphicsBuffer(_colorBufferProperty);
    }

    public override void UpdateBinding(VisualEffect component)
    {
        SplatData data = animator.envSplat;

        if (data == null)
            return;

        component.SetUInt(
            _countProperty,
            (uint)data.Count
        );

        component.SetGraphicsBuffer(
            _positionBufferProperty,
            data.PositionsBuffer
        );

        component.SetGraphicsBuffer(
            _axisBufferProperty,
            data.AxesBuffer
        );

        component.SetGraphicsBuffer(
            _colorBufferProperty,
            data.ColorsBuffer
        );
    }

    public override string ToString()
    {
        return $"Environment Splat Data Binder: " +
               $"{_countProperty}, " +
               $"{_positionBufferProperty}, " +
               $"{_axisBufferProperty}, " +
               $"{_colorBufferProperty}";
    }
}