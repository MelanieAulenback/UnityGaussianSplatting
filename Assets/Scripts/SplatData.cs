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

        Vector3[] verts = mesh.vertices;

        for (int i = 0; i < verts.Length; i++)
            verts[i] = t.TransformPoint(verts[i]);

        // cache visibility components
        DepthVisibility[] depthCams = new DepthVisibility[cameras.Length];

        for (int i = 0; i < cameras.Length; i++)
            depthCams[i] = cameras[i].GetComponent<DepthVisibility>();

        foreach (Vector3 vertex in verts)
        {
            Color accum = Color.black;
            float weightSum = 0f;

            for (int camIndex = 0; camIndex < cameras.Length; camIndex++)
            {
                Camera cam = cameras[camIndex];
                Texture2D image = images[camIndex];
                DepthVisibility dv = depthCams[camIndex];

                //if (dv == null)
                // continue;

                Vector3 viewport = cam.WorldToViewportPoint(vertex);

                if (viewport.z <= 0f)
                    continue;

                if (viewport.x < 0f || viewport.x > 1f ||
                    viewport.y < 0f || viewport.y > 1f)
                    continue;

                if (!dv.IsVisible(vertex))
                    continue;

                Vector3 toPoint =
                    (vertex - cam.transform.position).normalized;

                float angleWeight =
                    Mathf.Max(0f, Vector3.Dot(cam.transform.forward, toPoint));

                float centerWeight =
                    1f - Vector2.Distance(new Vector2(viewport.x, viewport.y), new Vector2(0.5f, 0.5f));

                float w = angleWeight * centerWeight;

                Color c = image.GetPixelBilinear(viewport.x, viewport.y);

                accum += c * w;
                weightSum += w;
            }

            if (weightSum < 1e-5f)
            {
                accum = Color.magenta; // debug fallback so you SEE failures
            }
            else
            {
                accum /= weightSum;
            }
            positions.Add(vertex);
            colors.Add(accum);

            axes.Add(Vector3.right * gaussianSize);
            axes.Add(Vector3.up * gaussianSize);
            axes.Add(Vector3.forward * gaussianSize);
        }

        Positions = positions.ToArray();
        Colors = colors.ToArray();
        Axes = axes.ToArray();

        InitializeBuffers();

        Debug.Log($"Generated {Positions.Length} gaussians with visibility + blending.");
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