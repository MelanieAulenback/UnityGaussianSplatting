using UnityEngine.VFX;
using UnityEngine.VFX.Utility;
using UnityEngine;


[VFXBinder("Splat Data")]
public class SplatDataBinder : VFXBinderBase {
    public SplatAnimator animator;

    [VFXPropertyBinding("System.UInt32")]
    private ExposedProperty _countProperty = "Count";

    [VFXPropertyBinding("UnityEngine.GraphicsBuffer")]
    private ExposedProperty _positionBufferProperty = "PositionBuffer";

    [VFXPropertyBinding("UnityEngine.GraphicsBuffer")]
    private ExposedProperty _axisBufferProperty = "AxisBuffer";

    [VFXPropertyBinding("UnityEngine.GraphicsBuffer")]
    private ExposedProperty _colorBufferProperty = "ColorBuffer";

    [VFXPropertyBinding("UnityEngine.Texture2D")]
    private ExposedProperty _combinedDepthProperty = "CombinedDepth";

    public override bool IsValid(VisualEffect component) {
        return animator != null &&
               component.HasUInt(_countProperty) &&
               component.HasGraphicsBuffer(_positionBufferProperty) &&
               component.HasGraphicsBuffer(_axisBufferProperty) &&
               component.HasGraphicsBuffer(_colorBufferProperty) && 
               component.HasTexture(_combinedDepthProperty);
    }

    public override void UpdateBinding(VisualEffect component) {

        if (animator == null)
            return;

        SplatData Data = animator.CurrentSplat;

        if (Data == null)
            return;

        if (animator.PlayerCombinedDepth == null)
            return;

        component.SetUInt(_countProperty, (uint)Data.Count);
        component.SetGraphicsBuffer(_positionBufferProperty, Data.PositionsBuffer);
        component.SetGraphicsBuffer(_axisBufferProperty, Data.AxesBuffer);
        component.SetGraphicsBuffer(_colorBufferProperty, Data.ColorsBuffer);
        component.SetTexture(_combinedDepthProperty, animator.PlayerCombinedDepth);
    }

    public override string ToString() {
        return $"Splat Data Binder: {_countProperty}, {_positionBufferProperty}, {_axisBufferProperty}, {_colorBufferProperty}";
    }
}
