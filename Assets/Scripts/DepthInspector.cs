using UnityEngine;

public class DepthInspector : MonoBehaviour
{
    public float[][,] depthFramesNpy;

    public void Inspect(int camIndex = 0, int frameIndex = 0)
    {
        float[,] frame = depthFramesNpy[camIndex];

        float min = float.MaxValue;
        float max = float.MinValue;
        float sum = 0f;

        int h = frame.GetLength(0);
        int w = frame.GetLength(1);

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = frame[y, x];

                if (float.IsNaN(v) || float.IsInfinity(v))
                    continue;

                min = Mathf.Min(min, v);
                max = Mathf.Max(max, v);
                sum += v;
            }

        float mean = sum / (h * w);

        Debug.Log($"DEPTH INSPECT → cam {camIndex}");
        Debug.Log($"Min: {min}, Max: {max}, Mean: {mean}");
    }
}