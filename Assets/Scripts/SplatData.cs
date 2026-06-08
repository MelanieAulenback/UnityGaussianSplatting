using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SplatData : ScriptableObject
{
    public Vector3[] Positions;
    public Vector3[] Axes;
    public Color[] Colors;

    private Vector2[] uvCoords;

    public int Count => Positions != null ? Positions.Length : 0;

    private Vector3[] canonicalPositions;

    private GraphicsBuffer _positionsA;
    private GraphicsBuffer _positionsB;
    private GraphicsBuffer _colorsA;
    private GraphicsBuffer _colorsB;
    private GraphicsBuffer _axesA;
    private GraphicsBuffer _axesB;

    public GraphicsBuffer PositionsBuffer => _positionsA;
    public GraphicsBuffer ColorsBuffer => _colorsA;
    public GraphicsBuffer AxesBuffer => _axesA;

    public void InitializeBuffers()
    {
        Dispose();

        int count = Count;

        if (count == 0 || Positions == null)
        {
            Debug.LogError("No splat data.");
            return;
        }

        _positionsA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(float) * 3);
        _positionsB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(float) * 3);

        _colorsA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(float) * 4);
        _colorsB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(float) * 4);

        _axesA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count * 3, sizeof(float) * 3);
        _axesB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count * 3, sizeof(float) * 3);

        SyncToGPU();
    }

    public void Dispose()
    {
        _positionsA?.Dispose();
        _positionsB?.Dispose();
        _colorsA?.Dispose();
        _colorsB?.Dispose();
        _axesA?.Dispose();
        _axesB?.Dispose();
    }

    private void SyncToGPU()
    {
        if (Positions == null) return;

        _positionsA.SetData(Positions);
        _positionsB.SetData(Positions);

        _colorsA.SetData(Colors);
        _colorsB.SetData(Colors);

        _axesA.SetData(Axes);
        _axesB.SetData(Axes);
    }

    // =========================================================
    // GENERATE (FIXED MULTI-CAMERA FUSION)
    // =========================================================
    public void GenerateFromDepthMap(
        Texture2D colorImage,
        Texture2D depthMap,
        Matrix4x4 cameraToWorld,
        Camera cam,
        float nearDepth,
        float farDepth,
        int pixelStep,
        float gaussianSize,
        bool invertDepth)
    {
        var positions = new List<Vector3>();
        var colors = new List<Color>();
        var axes = new List<Vector3>();
        var uvs = new List<Vector2>();
        var depths = new List<float>();

        int width = depthMap.width;
        int height = depthMap.height;

        Color[] depthPixels = depthMap.GetPixels();
        Color[] colorPixels = colorImage.GetPixels();

        // -------------------------
        // 1. BUILD POINT CLOUD
        // -------------------------
        for (int y = 0; y < height; y += pixelStep)
        {
            for (int x = 0; x < width; x += pixelStep)
            {
                int i = y * width + x;

                float d = depthPixels[i].r;
                if (invertDepth) d = 1f - d;
                if (d <= 0.001f) continue;

                float depth = Mathf.Lerp(nearDepth, farDepth, d);

                float u = (x + 0.5f) / width;
                float v = (y + 0.5f) / height;

                Ray ray = cam.ViewportPointToRay(new Vector3(u, v, 1f));

                Vector3 worldPos =
                    cam.transform.position +
                    ray.direction * depth;

                positions.Add(worldPos);
                colors.Add(colorPixels[i]);
                depths.Add(depth);

                Vector3 right = cameraToWorld.MultiplyVector(Vector3.right);
                Vector3 up = cameraToWorld.MultiplyVector(Vector3.up);
                Vector3 forward = cameraToWorld.MultiplyVector(Vector3.forward);

                axes.Add(right * gaussianSize);
                axes.Add(up * gaussianSize);
                axes.Add(forward * gaussianSize);

                uvs.Add(new Vector2(u, v));
            }
        }

        if (positions.Count == 0)
        {
            Debug.LogError("No valid depth points.");
            return;
        }

        // -------------------------
        // 2. DEPTH NORMALIZATION (CRITICAL FIX)
        // -------------------------
        float meanDepth = depths.Average();
        float scale = 1f / meanDepth;

        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 dir = (positions[i] - cam.transform.position);
            positions[i] = cam.transform.position + dir * scale;
        }

        // -------------------------
        // 3. CENTER ALIGNMENT
        // -------------------------
        Vector3 center = Vector3.zero;
        foreach (var p in positions)
            center += p;

        center /= positions.Count;

        for (int i = 0; i < positions.Count; i++)
            positions[i] -= center;

        // -------------------------
        // 4. FINAL ASSIGN
        // -------------------------
        Positions = positions.ToArray();
        Colors = colors.ToArray();
        Axes = axes.ToArray();
        uvCoords = uvs.ToArray();

        canonicalPositions = (Vector3[])Positions.Clone();

        Debug.Log("Count after generation = " + Count);

        InitializeBuffers();
    }

    // =========================================================
    // UPDATE (MULTI-CAMERA AVERAGE FUSION)
    // =========================================================
    public void UpdateFromDepthMap(
        Texture2D colorImage,
        Texture2D depthMap,
        Camera cam,
        float nearDepth,
        float farDepth,
        float gaussianSize,
        bool invertDepth)
    {
        if (uvCoords == null || uvCoords.Length != Count)
            return;

        int width = depthMap.width;
        int height = depthMap.height;

        Color[] depthPixels = depthMap.GetPixels();
        Color[] colorPixels = colorImage.GetPixels();

        Vector3[] accum = new Vector3[Count];

        for (int i = 0; i < Count; i++)
        {
            Vector2 uv = uvCoords[i];

            int x = Mathf.Clamp((int)(uv.x * width), 0, width - 1);
            int y = Mathf.Clamp((int)(uv.y * height), 0, height - 1);

            int idx = y * width + x;

            float d = depthPixels[idx].r;
            if (invertDepth) d = 1f - d;

            float depth = Mathf.Lerp(nearDepth, farDepth, d);

            Ray ray = cam.ViewportPointToRay(new Vector3(uv.x, uv.y, 1f));

            Vector3 world =
                cam.transform.position +
                ray.direction * depth;

            Vector3 delta =
                world - (cam.transform.position + ray.direction * nearDepth);

            accum[i] = canonicalPositions[i] + delta;
            Colors[i] = colorPixels[idx];
        }

        Positions = accum;

        UpdatePositionsOnly(Positions);
        UpdateColorsOnly(Colors);
    }

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