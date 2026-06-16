using System;
using System.Collections.Generic;
using UnityEngine;

public class SplatData : ScriptableObject
{
    public Vector3[] Positions;
    public Vector3[] Axes;
    public Color[] Colors;

    private Vector2[] uvCoords;
    private Vector3[] rayDirs;

    // =========================================================
    // BUFFERS (REQUIRED FOR BINDER)
    // =========================================================
    private GraphicsBuffer _positionsA;
    private GraphicsBuffer _positionsB;

    private GraphicsBuffer _colorsA;
    private GraphicsBuffer _colorsB;

    private GraphicsBuffer _axesA;
    private GraphicsBuffer _axesB;

    public GraphicsBuffer PositionsBuffer => _positionsA;
    public GraphicsBuffer PositionsBufferOut => _positionsB;

    public GraphicsBuffer ColorsBuffer => _colorsA;
    public GraphicsBuffer ColorsBufferOut => _colorsB;

    public GraphicsBuffer AxesBuffer => _axesA;
    public GraphicsBuffer AxesBufferOut => _axesB;

    public int Count => Positions != null ? Positions.Length : 0;

    // =========================================================
    // GENERATE (PNG-STYLE DEPTH BEHAVIOR FOR NPY TEST)
    // =========================================================
    /*
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

        float stepUV = 1f / Mathf.Max(width, height);

        for (int y = 0; y < height; y += pixelStep)
        {
            for (int x = 0; x < width; x += pixelStep)
            {
                int i = y * width + x;

                float depthRaw = depthPixels[i].r;

                if (i % 10000 == 0)
                {
                    Debug.Log($"depthRaw = {depthRaw}");
                }

                if (invertDepth)
                    depthRaw = 1f - depthRaw;

                if (depthRaw <= 0.001f)
                    continue;

                float u = (x + 0.5f) / width;
                float v = (y + 0.5f) / height;

                Color color =
                    colorImage.GetPixelBilinear(u, v);

                float z = Mathf.Lerp(
                    nearDepth,
                    farDepth,
                    depthRaw
                );

                Vector3 viewport =
                    new Vector3(u, v, z);

                Vector3 worldPos =
                    sourceCamera.ViewportToWorldPoint(
                        viewport
                    );

                Vector3 worldRight =
                    sourceCamera.ViewportToWorldPoint(
                        new Vector3(
                            u + stepUV,
                            v,
                            z
                        )
                    );

                Vector3 worldUp =
                    sourceCamera.ViewportToWorldPoint(
                        new Vector3(
                            u,
                            v + stepUV,
                            z
                        )
                    );

                Vector3 normal =
                    Vector3.Normalize(
                        Vector3.Cross(
                            worldRight - worldPos,
                            worldUp - worldPos
                        )
                    );

                Vector3 tangent =
                    Vector3.Cross(
                        Vector3.up,
                        normal
                    );

                if (tangent.sqrMagnitude < 1e-6f)
                {
                    tangent =
                        Vector3.Cross(
                            Vector3.right,
                            normal
                        );
                }

                tangent.Normalize();

                Vector3 bitangent =
                    Vector3.Cross(
                        normal,
                        tangent
                    );

                Quaternion rot =
                    Quaternion.LookRotation(
                        bitangent,
                        normal
                    );

                positions.Add(worldPos);
                colors.Add(color);

                axes.Add(
                    rot * Vector3.right * gaussianSize
                );

                axes.Add(
                    rot * Vector3.up * gaussianSize
                );

                axes.Add(
                    rot * Vector3.forward * gaussianSize
                );

                uvList.Add(
                    new Vector2(u, v)
                );
            }
        }

        Positions = positions.ToArray();
        Colors = colors.ToArray();
        Axes = axes.ToArray();
        uvCoords = uvList.ToArray();

        InitializeBuffers();
    }

    // =========================================================
    // UPDATE (SAME PNG LOGIC)
    // =========================================================
    public void UpdateFromDepthMap(
    Texture2D colorImage,
    Texture2D depthMap,
    Camera sourceCamera,
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

        bool positionsChanged = false;
        bool colorsChanged = false;

        for (int i = 0; i < Count; i++)
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

            int idx = y * width + x;

            float depthRaw =
                depthPixels[idx].r;

            if (invertDepth)
                depthRaw = 1f - depthRaw;

            float z =
                Mathf.Lerp(
                    nearDepth,
                    farDepth,
                    depthRaw
                );

            Vector3 newPos =
                sourceCamera.ViewportToWorldPoint(
                    new Vector3(
                        uv.x,
                        uv.y,
                        z
                    )
                );

            Color newCol =
                colorImage.GetPixelBilinear(
                    uv.x,
                    uv.y
                );

            if (!positionsChanged &&
                Vector3.Distance(
                    Positions[i],
                    newPos
                ) > 0.0001f)
            {
                positionsChanged = true;
            }

            if (!colorsChanged &&
                Colors[i] != newCol)
            {
                colorsChanged = true;
            }

            Positions[i] = newPos;
            Colors[i] = newCol;
        }

        if (positionsChanged)
            UpdatePositionsOnly(
                Positions
            );

        if (colorsChanged)
            UpdateColorsOnly(
                Colors
            );
    }
    */

    public void GaussiansFromCloud(
        GameObject pointCloud,
        Camera[] cameras,
        Texture2D[] images,
        float gaussianSize)
    {
        Dispose();

        var positions = new List<Vector3>();
        var colors = new List<Color>();
        var axes = new List<Vector3>();

        Mesh mesh = pointCloud.GetComponent<MeshFilter>().sharedMesh;
        Transform t = pointCloud.transform;

        Vector3[] localVerts = mesh.vertices;

        // =====================================================
        // IMPORTANT: ONLY transform to world space
        // NO scaling here (handled by root transform)
        // =====================================================
        Vector3[] verts = new Vector3[localVerts.Length];

        for (int i = 0; i < localVerts.Length; i++)
        {
            verts[i] = t.TransformPoint(localVerts[i]);
        }

        foreach (Vector3 vertex in verts)
        {
            Color accumulatedColor = Color.black;
            int validViews = 0;

            for (int camIndex = 0; camIndex < cameras.Length; camIndex++)
            {
                Camera cam = cameras[camIndex];
                Texture2D image = images[camIndex];

                Vector3 viewport = cam.WorldToViewportPoint(vertex);

                if (viewport.z <= 0f)
                    continue;

                if (viewport.x < 0f || viewport.x > 1f ||
                    viewport.y < 0f || viewport.y > 1f)
                    continue;

                accumulatedColor += image.GetPixelBilinear(viewport.x, viewport.y);
                validViews++;
            }

            Color finalColor = (validViews == 0)
                ? Color.magenta
                : accumulatedColor / validViews;

            positions.Add(vertex);
            colors.Add(finalColor);

            axes.Add(Vector3.right * gaussianSize);
            axes.Add(Vector3.up * gaussianSize);
            axes.Add(Vector3.forward * gaussianSize);
        }

        Positions = positions.ToArray();
        Colors = colors.ToArray();
        Axes = axes.ToArray();

        InitializeBuffers();

        Debug.Log($"Generated {Positions.Length} gaussians from point cloud.");
    }
    // =========================================================
    // BUFFERS
    // =========================================================
    public void InitializeBuffers()
    {
        Dispose();

        int count = Count;
        if (count == 0) return;

        _positionsA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(float) * 3);
        _positionsB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(float) * 3);

        _colorsA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(float) * 4);
        _colorsB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(float) * 4);

        _axesA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count * 3, sizeof(float) * 3);
        _axesB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count * 3, sizeof(float) * 3);

        _positionsA.SetData(Positions);
        _positionsB.SetData(Positions);

        _colorsA.SetData(Colors);
        _colorsB.SetData(Colors);

        _axesA.SetData(Axes);
        _axesB.SetData(Axes);
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

    public void Dispose()
    {
        _positionsA?.Dispose(); _positionsA = null;
        _positionsB?.Dispose(); _positionsB = null;
        _colorsA?.Dispose(); _colorsA = null;
        _colorsB?.Dispose(); _colorsB = null;
        _axesA?.Dispose(); _axesA = null;
        _axesB?.Dispose(); _axesB = null;
    }
}