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
    // GPU COLOR ACCUMULATION
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

    public int Count => Positions != null ? Positions.Length : 0;

    //creates gaussians given points and gaussian size,
    //does not assign final colour
    public void GaussiansFromCloud(
     Vector3[] verts,
     float gaussianSize)
    {
        //check if there are any positions and if buffers need to be initiated
        bool needsInit =
            Positions == null ||
            Positions.Length != verts.Length ||
            _positionsA == null;

        //fill positions
        Positions = verts;

        //if buffers dont need to be initialized, just update the positions and exit
        if (!needsInit)
        {
            UpdatePositionsOnly(verts);
            return;
        }

        //create arrays for colour and axes using the number of vertices as the length
        Colors = new Color[verts.Length];
        Axes = new Vector3[verts.Length * 3];

        //default the colours to black and set the gaussian size
        for (int i = 0; i < verts.Length; i++)
        {
            Colors[i] = Color.black;

            int a = i * 3;

            Axes[a] =
                Vector3.right * gaussianSize;

            Axes[a + 1] =
                Vector3.up * gaussianSize;

            Axes[a + 2] =
                Vector3.forward * gaussianSize;
        }

        //initialize buffers
        InitializeBuffers();
    }

    //set accumulation back to black (zero)
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
    //initializes buffers
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
        
        //set accumulation colours to black (zero) to start
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

    //set the position buffers
    public void UpdatePositionsOnly(Vector3[] p)
    {
        Positions = p;

        _positionsA.SetData(p);
        _positionsB.SetData(p);
    }

    public void UpdateColorsOnly(Color[] c)
    {
        //if the colour buffers are null, empty them and create new ones
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

        //fill the colour buffers
        _colorsA.SetData(c);
        _colorsB.SetData(c);
    }

    //empties buffers
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