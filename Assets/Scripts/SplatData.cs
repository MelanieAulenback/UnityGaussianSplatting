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

    private GraphicsBuffer _positionsA;
    private GraphicsBuffer _positionsB;

    private GraphicsBuffer _colorsA;
    private GraphicsBuffer _colorsB;

    private GraphicsBuffer _axesA;
    private GraphicsBuffer _axesB;

    public int Count => Positions != null ? Positions.Length : 0;

    public GraphicsBuffer PositionsBuffer => _positionsA;
    public GraphicsBuffer ColorsBuffer => _colorsA;
    public GraphicsBuffer AxesBuffer => _axesA;

    public GraphicsBuffer PositionsBufferOut => _positionsB;
    public GraphicsBuffer ColorsBufferOut => _colorsB;
    public GraphicsBuffer AxesBufferOut => _axesB;

    private GraphicsBuffer _weightsA;
    public GraphicsBuffer GaussianWeightBuffer => _weightsA;

    private Vector2[] uvCoords;

    public bool IsReady => _positionsA != null;

    private Matrix4x4 vp;
    private Matrix4x4 invVP;
    private Matrix4x4 localToWorld;


    //swap input and output buffers
    public void SwapBuffers()
    {
        (_positionsA, _positionsB) = (_positionsB, _positionsA);
        (_colorsA, _colorsB) = (_colorsB, _colorsA);
        (_axesA, _axesB) = (_axesB, _axesA);
    }

    //initializes buffers
    public void InitializeBuffers()
    {
        Dispose(); // important: reset old GPU memory safely

        int count = Count;

        if (Positions == null || Colors == null || Axes == null || Count == 0)
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

        float[] initWeights = new float[count];
        _weightsA.SetData(initWeights);

        _positionsA.SetData(Positions);
        _positionsB.SetData(Positions);

        _colorsA.SetData(Colors);
        _colorsB.SetData(Colors);

        _axesA.SetData(Axes);
        _axesB.SetData(Axes);
    }

    //deletes old GPU memory
    public void Dispose()
    {
        _positionsA?.Dispose();
        _positionsA = null;

        _positionsB?.Dispose();
        _positionsB = null;

        _colorsA?.Dispose();
        _colorsA = null;

        _colorsB?.Dispose();
        _colorsB = null;

        _axesA?.Dispose();
        _axesA = null;

        _axesB?.Dispose();
        _axesB = null;
    }

    private void OnDisable()
    {
        Dispose();
    }

    private void OnDestroy()
    {
        Dispose();
    }

    public void LoadFromFile(string filePath)
    {
        byte[] bytes = File.ReadAllBytes(filePath);

        Dispose();

        int count = bytes.Length / 32;

        Positions = new Vector3[count];
        Axes = new Vector3[count * 3];
        Colors = new Color[count];

        ReadOnlySpan<ReadData> records = MemoryMarshal.Cast<byte, ReadData>(bytes);

        for (int i = 0; i < count; i++)
        {
            var record = records[i];

            float rotX = (record.rx - 128f) / 128f;
            float rotY = (record.ry - 128f) / 128f;
            float rotZ = (record.rz - 128f) / 128f;
            float rotW = (record.rw - 128f) / 128f;

            Quaternion rot = new(-rotX, -rotY, rotZ, rotW);

            Positions[i] = new(-record.px, -record.py, -record.pz);

            Axes[i * 3 + 0] = (rot * Vector3.right * record.sx).normalized;
            Axes[i * 3 + 1] = (rot * Vector3.up * record.sy).normalized;
            Axes[i * 3 + 2] = (rot * Vector3.forward * record.sz).normalized;

            Colors[i] = new Color(
                record.r / 255f,
                record.g / 255f,
                record.b / 255f,
                record.a / 255f
            );
        }

        InitializeBuffers();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ReadData
    {
        public float px, py, pz;
        public float sx, sy, sz;
        public byte r, g, b, a;
        public byte rw, rx, ry, rz;
    }

    // generates splats from depth map instead of splat file
    public void GenerateFromDepthMap(
    Texture2D colorImage,
    Texture2D depthMap,
    Camera sourceCamera,
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
        var uvList = new List<Vector2>();

        int width = depthMap.width;
        int height = depthMap.height;

        Color[] depthPixels = depthMap.GetPixels();
        Color[] colorPixels = colorImage.GetPixels();

        float stepUV = 1f / Mathf.Max(width, height);

        for (int y = 0; y < height; y += pixelStep)
        {
            for (int x = 0; x < width; x += pixelStep)
            {
                int i = y * width + x;

                float depthRaw = depthPixels[i].r;
                if (invertDepth) depthRaw = 1f - depthRaw;
                if (depthRaw <= 0.001f) continue;

                float u = (x + 0.5f) / width;
                float v = (y + 0.5f) / height;

                float z = Mathf.Lerp(nearDepth, farDepth, depthRaw);

                Color color = colorPixels[i];

                Vector3 viewport = new Vector3(u, v, z);
                Vector3 worldPos = sourceCamera.ViewportToWorldPoint(viewport);

                Vector3 worldRight = sourceCamera.ViewportToWorldPoint(new Vector3(u + stepUV, v, z));
                Vector3 worldUp = sourceCamera.ViewportToWorldPoint(new Vector3(u, v + stepUV, z));

                Vector3 normal = Vector3.Normalize(Vector3.Cross(worldRight - worldPos, worldUp - worldPos));

                Vector3 tangent = Vector3.Cross(Vector3.up, normal);
                if (tangent.sqrMagnitude < 1e-6f)
                    tangent = Vector3.Cross(Vector3.right, normal);

                tangent.Normalize();

                Vector3 bitangent = Vector3.Cross(normal, tangent);
                Quaternion rot = Quaternion.LookRotation(bitangent, normal);

                positions.Add(worldPos);
                colors.Add(color);

                axes.Add(rot * Vector3.right * gaussianSize);
                axes.Add(rot * Vector3.up * gaussianSize);
                axes.Add(rot * Vector3.forward * gaussianSize);

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
    Texture2D depthMap,
    Camera sourceCamera,
    float nearDepth,
    float farDepth,
    float gaussianSize,
    bool invertDepth)
    {
        Debug.Log("UpdateFromDepthMap called");
        if (uvCoords == null || uvCoords.Length != Count)
            return;

        int width = depthMap.width;
        int height = depthMap.height;

        Color[] depthPixels = depthMap.GetPixels();
        Color[] colorPixels = colorImage.GetPixels();

        bool positionsChanged = false;
        bool colorsChanged = false;

        for (int i = 0; i < Count; i++)
        {
            Vector2 uv = uvCoords[i];

            int x = Mathf.Clamp((int)(uv.x * width), 0, width - 1);
            int y = Mathf.Clamp((int)(uv.y * height), 0, height - 1);

            int idx = y * width + x;

            float depthRaw = depthPixels[idx].r;
            if (invertDepth) depthRaw = 1f - depthRaw;

            float z = Mathf.Lerp(nearDepth, farDepth, depthRaw);

            Vector3 newPos = sourceCamera.ViewportToWorldPoint(new Vector3(uv.x, uv.y, z));
            Color newCol = colorPixels[idx];

            if (!positionsChanged && Vector3.Distance(Positions[i], newPos) > 0.0001f)
                positionsChanged = true;

            if (!colorsChanged && Colors[i] != newCol)
                colorsChanged = true;

            Positions[i] = newPos;
            Colors[i] = newCol;
        }

        if (positionsChanged)
            UpdatePositionsOnly(Positions);

        if (colorsChanged)
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