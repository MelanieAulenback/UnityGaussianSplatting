using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public class SplatData : ScriptableObject
{
    public Texture2D referenceImage;
    public Camera referenceCam;

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
    public void GenerateFlatImage(
    Texture2D colorImage,
    Camera cam,
    float distance,
    float pixelStep,
    float pointSize)
    {
        Dispose();

        var positions = new List<Vector3>();
        var colors = new List<Color>();
        var axes = new List<Vector3>();

        var uvList = new List<Vector2>();

        int width = colorImage.width;
        int height = colorImage.height;

        for (int y = 0; y < height; y += (int)pixelStep)
        {
            for (int x = 0; x < width; x += (int)pixelStep)
            {
                float u = (x + 0.5f) / width;
                float v = (y + 0.5f) / height;

                Color col = colorImage.GetPixelBilinear(u, v);

                Ray ray = cam.ViewportPointToRay(new Vector3(u, v, 0));

                Vector3 pos =
                    cam.transform.position +
                    ray.direction.normalized * distance;

                positions.Add(pos);
                colors.Add(col);

                axes.Add(Vector3.right * pointSize);
                axes.Add(Vector3.up * pointSize);
                axes.Add(Vector3.forward * pointSize);

                uvList.Add(new Vector2(u, v));
            }
        }

        Positions = positions.ToArray();
        Colors = colors.ToArray();
        Axes = axes.ToArray();

        uvCoords = uvList.ToArray();

        InitializeBuffers();
    }

    public void UpdateFlatImage(Texture2D colorImage)
    {
        if (uvCoords == null)
            return;

        for (int i = 0; i < uvCoords.Length; i++)
        {
            Vector2 uv = uvCoords[i];

            Colors[i] = colorImage.GetPixelBilinear(
                uv.x,
                uv.y
            );
        }

        UpdateColorsOnly(Colors);
    }
    public void GenerateFromDepthNpy(
    Texture2D colorImage,
    float[,,] depthNpy,
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

        int height = depthNpy.GetLength(0);
        int width = depthNpy.GetLength(1);

        float minDepth = float.MaxValue;
        float maxDepth = float.MinValue;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float d = depthNpy[y, x, 0];

                if (d < minDepth)
                    minDepth = d;

                if (d > maxDepth)
                    maxDepth = d;
            }
        }

        Debug.Log(
            $"Depth range: min={minDepth} max={maxDepth}"
        );

        for (int y = 0; y < height; y += pixelStep)
        {
            for (int x = 0; x < width; x += pixelStep)
            {
                float u = (x + 0.5f) / width;
                float v = (y + 0.5f) / height;

                float d = depthNpy[y, x, 0];

                if (invertDepth)
                    d = 1f - d;

                float depth =
                    Mathf.Lerp(
                        nearDepth,
                        farDepth,
                        Mathf.Clamp01(d)
                    );

                Ray ray =
                    cam.ViewportPointToRay(
                        new Vector3(u, v, 0)
                    );

                Vector3 rayDir = ray.direction.normalized;

                float viewSpaceZ = Mathf.Lerp(nearDepth, farDepth, Mathf.Clamp01(d));

                // camera space point
                Vector3 viewPos = new Vector3(u * 2f - 1f, v * 2f - 1f, 1f) * viewSpaceZ;

                // better: use ray direction but normalize to camera forward basis
                Vector3 worldPos = cam.transform.TransformPoint(rayDir * viewSpaceZ);

                Color col =
                    colorImage.GetPixelBilinear(u, v);

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
    public void UpdateFromDepthNpy(
    Texture2D colorImage,
    float[,,] depthNpy,
    Camera cam,
    float nearDepth,
    float farDepth,
    bool invertDepth)
    {
        if (uvCoords == null)
            return;

        int width = depthNpy.GetLength(1);
        int height = depthNpy.GetLength(0);

        int count = Mathf.Min(
            Positions.Length,
            uvCoords.Length
        );

        for (int i = 0; i < count; i++)
        {
            Vector2 uv = uvCoords[i];

            int x =
                Mathf.Clamp(
                    (int)(uv.x * width),
                    0,
                    width - 1
                );

            int y =
                Mathf.Clamp(
                    (int)(uv.y * height),
                    0,
                    height - 1
                );

            float d = depthNpy[y, x, 0];

            if (invertDepth)
                d = 1f - d;

            float newDepth =
                Mathf.Lerp(
                    nearDepth,
                    farDepth,
                    Mathf.Clamp01(d)
                );

            float delta =
                newDepth - baseDepths[i];

            float viewSpaceZ = Mathf.Lerp(nearDepth, farDepth, Mathf.Clamp01(d));

            Vector3 worldPos =
                cam.transform.TransformPoint(rayDirs[i] * viewSpaceZ);

            Positions[i] = worldPos;

            Colors[i] =
                colorImage.GetPixelBilinear(
                    uv.x,
                    uv.y
                );
        }

        UpdatePositionsOnly(Positions);
        UpdateColorsOnly(Colors);
    }

    public void LoadPointCloud(string path)
    {
        float[] data = NpyLoader.LoadFloatArray(path);

        int count = data.Length / 3;

        Positions = new Vector3[count];
        Colors = new Color[count];
        Axes = new Vector3[count * 3];

        if (referenceImage == null || referenceCam == null)
        {
            Debug.LogError("Reference image or camera not assigned!");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            float x = data[i * 3 + 0];
            float y = data[i * 3 + 1];
            float z = data[i * 3 + 2];

            Vector3 worldPos = new Vector3(x, y, z);
            Positions[i] = worldPos;

            // -----------------------------
            // WORLD → CAMERA SPACE
            // -----------------------------
            Vector3 viewPos = referenceCam.worldToCameraMatrix.MultiplyPoint(worldPos);

            if (viewPos.z <= 0)
            {
                Colors[i] = Color.black;
                continue;
            }

            // -----------------------------
            // PROJECT TO NDC
            // -----------------------------
            Vector3 clip = referenceCam.projectionMatrix.MultiplyPoint(viewPos);

            float u = clip.x * 0.5f + 0.5f;
            float v = clip.y * 0.5f + 0.5f;

            // flip Y for textures
            v = 1f - v;

            // -----------------------------
            // SAMPLE COLOR
            // -----------------------------
            Colors[i] = referenceImage.GetPixelBilinear(u, v);

            // fallback safety
            if (u < 0 || u > 1 || v < 0 || v > 1)
                Colors[i] = Color.black;

            // axes
            Axes[i * 3 + 0] = Vector3.right * 0.01f;
            Axes[i * 3 + 1] = Vector3.up * 0.01f;
            Axes[i * 3 + 2] = Vector3.forward * 0.01f;
        }

        InitializeBuffers();
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