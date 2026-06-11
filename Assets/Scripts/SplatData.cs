using System.Collections.Generic;
using UnityEngine;

public class SplatData : ScriptableObject
{
    public Vector3[] Positions;
    public Vector3[] Axes;
    public Color[] Colors;

    private GraphicsBuffer _positionsA;
    private GraphicsBuffer _positionsB;

    private GraphicsBuffer _colorsA;
    private GraphicsBuffer _colorsB;

    private GraphicsBuffer _axesA;
    private GraphicsBuffer _axesB;

    private GraphicsBuffer _weightsA;

    public GraphicsBuffer PositionsBuffer => _positionsA;
    public GraphicsBuffer PositionsBufferOut => _positionsB;

    public GraphicsBuffer ColorsBuffer => _colorsA;
    public GraphicsBuffer ColorsBufferOut => _colorsB;

    public GraphicsBuffer AxesBuffer => _axesA;
    public GraphicsBuffer AxesBufferOut => _axesB;

    public GraphicsBuffer GaussianWeightBuffer => _weightsA;

    public int Count => Positions != null ? Positions.Length : 0;

    private Vector2[] uvCoords;
    private float[,] depthFrame;
    public Camera sourceCamera;

    public void SwapBuffers()
    {
        (_positionsA, _positionsB) = (_positionsB, _positionsA);
        (_colorsA, _colorsB) = (_colorsB, _colorsA);
        (_axesA, _axesB) = (_axesB, _axesA);
    }

    public void InitializeBuffers()
    {
        Dispose();

        int count = Count;
        if (count == 0 || Positions == null || Colors == null || Axes == null)
        {
            Debug.LogError("No splat data loaded.");
            return;
        }

        _positionsA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(float) * 3);
        _positionsB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(float) * 3);

        _colorsA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(float) * 4);
        _colorsB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(float) * 4);

        _axesA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count * 3, sizeof(float) * 3);
        _axesB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count * 3, sizeof(float) * 3);

        _weightsA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(float));

        _positionsA.SetData(Positions);
        _positionsB.SetData(Positions);

        _colorsA.SetData(Colors);
        _colorsB.SetData(Colors);

        _axesA.SetData(Axes);
        _axesB.SetData(Axes);
    }

    public void Dispose()
    {
        _positionsA?.Dispose(); _positionsA = null;
        _positionsB?.Dispose(); _positionsB = null;

        _colorsA?.Dispose(); _colorsA = null;
        _colorsB?.Dispose(); _colorsB = null;

        _axesA?.Dispose(); _axesA = null;
        _axesB?.Dispose(); _axesB = null;
    }

    private void OnDisable() => Dispose();
    private void OnDestroy() => Dispose();

    public void SetDepthFrame(float[,] depth)
    {
        depthFrame = depth;
    }

    public void GenerateFromDepthMap(
        Texture2D colorImage,
        Camera cam,
        float nearDepth,
        float farDepth,
        int pixelStep,
        float gaussianSize,
        bool invertDepth)
    {
        Dispose();

        sourceCamera = cam;

        var positions = new List<Vector3>();
        var colors = new List<Color>();
        var axes = new List<Vector3>();
        var uvList = new List<Vector2>();

        int height = depthFrame.GetLength(0);
        int width = depthFrame.GetLength(1);

        float stepUV = 1f / Mathf.Max(width, height);

        for (int y = 0; y < height; y += pixelStep)
        {
            for (int x = 0; x < width; x += pixelStep)
            {
                float u = (float)x / width;
                float v = (float)y / height;

                float d = depthFrame[y, x];
                if (invertDepth) d = 1f - d;
                if (d <= 0.0001f) continue;

                Vector3 ray = cam.ViewportPointToRay(new Vector3(u, v, 0)).direction;
                Vector3 worldPos = cam.transform.position + ray * d;

                Color col = colorImage.GetPixelBilinear(u, v);

                positions.Add(worldPos);
                colors.Add(col);
                axes.Add(Vector3.one * gaussianSize); // simplified stability
                uvList.Add(new Vector2(u, v));
            }
        }

        Positions = positions.ToArray();
        Colors = colors.ToArray();
        Axes = axes.ToArray();
        uvCoords = uvList.ToArray();

        InitializeBuffers();
    }

    public void UpdateFromDepthMap(
        Texture2D colorImage,
        Camera cam,
        float nearDepth,
        float farDepth,
        float gaussianSize,
        bool invertDepth)
    {
        if (depthFrame == null || uvCoords == null) return;

        int h = depthFrame.GetLength(0);
        int w = depthFrame.GetLength(1);

        for (int i = 0; i < Count; i++)
        {
            Vector2 uv = uvCoords[i];

            int x = Mathf.Clamp((int)(uv.x * w), 0, w - 1);
            int y = Mathf.Clamp((int)(uv.y * h), 0, h - 1);

            float d = depthFrame[y, x];
            if (invertDepth) d = 1f - d;

            Vector3 ray = cam.ViewportPointToRay(new Vector3(uv.x, uv.y, 0)).direction;
            Positions[i] = cam.transform.position + ray * d;

            Colors[i] = colorImage.GetPixelBilinear(uv.x, uv.y);
        }

        UpdatePositionsOnly(Positions);
        UpdateColorsOnly(Colors);
    }

    public void UpdatePositionsOnly(Vector3[] p)
    {
        _positionsA.SetData(p);
        _positionsB.SetData(p);
    }

    public void UpdateColorsOnly(Color[] c)
    {
        _colorsA.SetData(c);
        _colorsB.SetData(c);
    }
}