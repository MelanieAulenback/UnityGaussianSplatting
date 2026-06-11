using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public class SplatData : ScriptableObject
{
    public Vector3[] Positions;
    public Vector3[] Axes;
    public Color[] Colors;

    // Stable reconstruction data
    private Vector3[] basePositions;
    private Vector3[] rayDirs;
    private float[] baseDepths;
    private Vector2[] uvCoords;

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

    // =========================
    // BUFFER SYSTEM
    // =========================
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

        if (Positions == null || Colors == null || Axes == null || count == 0)
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

        _weightsA.SetData(new float[count]);
    }

    public void Dispose()
    {
        _positionsA?.Dispose(); _positionsA = null;
        _positionsB?.Dispose(); _positionsB = null;

        _colorsA?.Dispose(); _colorsA = null;
        _colorsB?.Dispose(); _colorsB = null;

        _axesA?.Dispose(); _axesA = null;
        _axesB?.Dispose(); _axesB = null;

        _weightsA?.Dispose(); _weightsA = null;
    }

    private void OnDisable() => Dispose();
    private void OnDestroy() => Dispose();

    // =========================
    // STABLE GENERATION (RUN ONCE)
    // =========================
    public void GenerateFromDepthMap(
        Texture2D colorImage,
        Texture2D depthMap,
        Camera cam,
        float nearDepth,
        float farDepth,
        int pixelStep,
        float gaussianSize,
        bool invertDepth)
    {
        Dispose();

        var positions = new List<Vector3>();
        var colors = new List<Color>();
        var axes = new List<Vector3>();

        var rays = new List<Vector3>();
        var basePos = new List<Vector3>();
        var baseDepth = new List<float>();
        var uvs = new List<Vector2>();

        int width = depthMap.width;
        int height = depthMap.height;

        float stepUV = 1f / Mathf.Max(width, height);

        for (int y = 0; y < height; y += pixelStep)
        {
            for (int x = 0; x < width; x += pixelStep)
            {
                float u = (x + 0.5f) / width;
                float v = (y + 0.5f) / height;

                float d = depthMap.GetPixelBilinear(u, v).r;
                if (invertDepth) d = 1f - d;
                if (d <= 1e-5f) continue;

                // IMPORTANT: treat as linear depth (NO 1/d inversion)
                float depth = Mathf.Lerp(nearDepth, farDepth, d);

                Ray ray = cam.ViewportPointToRay(new Vector3(u, v, 0));
                Vector3 rayDir = ray.direction.normalized;

                Vector3 worldPos = cam.transform.position + rayDir * depth;

                Color col = colorImage.GetPixelBilinear(u, v);

                positions.Add(worldPos);
                colors.Add(col);

                axes.Add(Vector3.right * gaussianSize);
                axes.Add(Vector3.up * gaussianSize);
                axes.Add(Vector3.forward * gaussianSize);

                rays.Add(rayDir);
                basePos.Add(worldPos);
                baseDepth.Add(depth);
                uvs.Add(new Vector2(u, v));
            }
        }

        Positions = positions.ToArray();
        Colors = colors.ToArray();
        Axes = axes.ToArray();

        rayDirs = rays.ToArray();
        basePositions = basePos.ToArray();
        baseDepths = baseDepth.ToArray();
        uvCoords = uvs.ToArray();

        InitializeBuffers();
    }

    // =========================
    // STABLE ANIMATION (NO RECONSTRUCTION)
    // =========================
    public void UpdateFromDepthMap(
        Texture2D colorImage,
        Texture2D depthMap,
        float nearDepth,
        float farDepth,
        bool invertDepth)
    {
        if (uvCoords == null || basePositions == null || rayDirs == null)
            return;

        int count = Mathf.Min(Count, uvCoords.Length);

        for (int i = 0; i < count; i++)
        {
            Vector2 uv = uvCoords[i];

            float d = depthMap.GetPixelBilinear(uv.x, uv.y).r;
            if (invertDepth) d = 1f - d;

            float newDepth = Mathf.Lerp(nearDepth, farDepth, d);

            float delta = newDepth - baseDepths[i];

            Positions[i] = basePositions[i] + rayDirs[i] * delta;
            Colors[i] = colorImage.GetPixelBilinear(uv.x, uv.y);
        }

        UpdatePositionsOnly(Positions);
        UpdateColorsOnly(Colors);
    }

    // =========================
    // GPU UPDATES
    // =========================
    public void UpdatePositionsOnly(Vector3[] newPositions)
    {
        _positionsA.SetData(newPositions);
        _positionsB.SetData(newPositions);
    }

    public void UpdateColorsOnly(Color[] newColors)
    {
        _colorsA.SetData(newColors);
        _colorsB.SetData(newColors);
    }
}