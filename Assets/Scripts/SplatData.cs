using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;

public class SplatData : ScriptableObject
{
    public Vector3[] Positions;
    public Vector3[] Axes;
    public Color[] Colors;

    // =========================================================
    // BUFFERS (REQUIRED FOR BINDER)
    // =========================================================
    private GraphicsBuffer _positionsA;
    private GraphicsBuffer _positionsB;

    private GraphicsBuffer _colorsA;
    private GraphicsBuffer _colorsB;

    private GraphicsBuffer _axesA;
    private GraphicsBuffer _axesB;

    public GraphicsBuffer BestCameraScoreBuffer;

    // =========================================================
    // GPU COLOR ACCUMULATION (NEW)
    // =========================================================
    private GraphicsBuffer _accumColorBuffer;     // float4 per gaussian
    private GraphicsBuffer _contributionBuffer;   // uint per gaussian

    private GraphicsBuffer _finalColorBuffer;
    public GraphicsBuffer FinalColorBuffer => _finalColorBuffer;

    public GraphicsBuffer AccumColorBuffer => _accumColorBuffer;
    public GraphicsBuffer ContributionBuffer => _contributionBuffer;

    public GraphicsBuffer PositionsBuffer => _positionsA;
    public GraphicsBuffer PositionsBufferOut => _positionsB;

    public GraphicsBuffer ColorsBuffer => _colorsA;
    public GraphicsBuffer ColorsBufferOut => _colorsB;

    public GraphicsBuffer AxesBuffer => _axesA;
    public GraphicsBuffer AxesBufferOut => _axesB;

    private GraphicsBuffer _debugBuffer;

    public GraphicsBuffer DebugBuffer => _debugBuffer;

    ComputeBuffer vertexBuffer;
    ComputeBuffer colorAccumBuffer;
    ComputeBuffer countBuffer;

    public int Count => Positions != null ? Positions.Length : 0;

    public void GaussiansFromCloud(
    GameObject pointCloud,
    float gaussianSize)
    {
        Dispose();

        // create lists for position, colour, and axes
        //var positions = new List<Vector3>();
        //var colors = new List<Color>();
        //var axes = new List<Vector3>();

        // get mesh
        Mesh mesh = pointCloud.GetComponent<MeshFilter>().sharedMesh;
        Transform t = pointCloud.transform;

        Vector3[] verts = mesh.vertices;

        Positions = new Vector3[verts.Length];
        Colors = new Color[verts.Length];
        Axes = new Vector3[verts.Length * 3];

        // loop vertices
        for (int i = 0; i < verts.Length; i++)
        {
            Positions[i] = t.TransformPoint(verts[i]);

            
            // Placeholder. Compute shader overwrites these.
            Colors[i] = Color.black;

            int a = i * 3;
            Axes[a] = Vector3.right * gaussianSize;
            Axes[a + 1] = Vector3.up * gaussianSize;
            Axes[a + 2] = Vector3.forward * gaussianSize;
            /*
            positions.Add(verts[v]);
            colors.Add(vertexCol);

            axes.Add(Vector3.right * gaussianSize);
            axes.Add(Vector3.up * gaussianSize);
            axes.Add(Vector3.forward * gaussianSize);
            */
        }
        /*
        Positions = positions.ToArray();
        Colors = colors.ToArray();
        Axes = axes.ToArray();
        */

        InitializeBuffers();

        Debug.Log($"Generated {Positions.Length} gaussians (weighted splat).");
    }

    
    public void ResetAccumulation()
    {
        if (_accumColorBuffer == null || _contributionBuffer == null) return;

        int count = Count;

        Vector4[] zeroColors = new Vector4[count];
        uint[] zeroCounts = new uint[count];

        _accumColorBuffer.SetData(zeroColors);
        _contributionBuffer.SetData(zeroCounts);
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

        BestCameraScoreBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Structured,
            Count,
            sizeof(float)
        );
        // =====================================================
        // NEW GPU ACCUMULATION BUFFERS
        // =====================================================
        _accumColorBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(float) * 4);
        _contributionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(uint));
        _finalColorBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, sizeof(float) * 4);
        
        // init arrays
        Vector4[] zeroColors = new Vector4[count];
        uint[] zeroCounts = new uint[count];

        for (int i = 0; i < count; i++)
        {
            zeroColors[i] = Vector4.zero;
            zeroCounts[i] = 0;
        }

        _accumColorBuffer.SetData(zeroColors);
        _contributionBuffer.SetData(zeroCounts);

        //upload data
        _positionsA.SetData(Positions);
        _positionsB.SetData(Positions);

        _colorsA.SetData(Colors);
        _colorsB.SetData(Colors);

        _axesA.SetData(Axes);
        _axesB.SetData(Axes);

        _finalColorBuffer.SetData(zeroColors);

        _debugBuffer = new GraphicsBuffer(
            GraphicsBuffer.Target.Structured,
            3,
            sizeof(float) * 4
        );
    }

    public void UpdatePositionsOnly(Vector3[] p)
    {
        _positionsA.SetData(p);
        _positionsB.SetData(p);
    }

    public void UpdateColorsOnly(Color[] c)
    {
        if (_colorsA == null || _colorsA.count != c.Length)
        {
            Debug.Log($"Recreating color buffers. Old={_colorsA?.count} New={c.Length}");

            _colorsA?.Dispose();
            _colorsB?.Dispose();

            _colorsA = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                c.Length,
                sizeof(float) * 4
            );

            _colorsB = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                c.Length,
                sizeof(float) * 4
            );
        }

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
        _accumColorBuffer?.Dispose(); _accumColorBuffer = null;
        _contributionBuffer?.Dispose(); _contributionBuffer = null;
        _debugBuffer?.Dispose();
        _debugBuffer = null;
        _finalColorBuffer?.Dispose();
        _finalColorBuffer = null;

        BestCameraScoreBuffer?.Dispose();
        BestCameraScoreBuffer = null;
    }
}