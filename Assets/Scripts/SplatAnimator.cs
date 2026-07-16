using Siccity.GLTFUtility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.UIElements;
using static UnityEngine.Rendering.DebugUI.Table;

public class SplatAnimator : MonoBehaviour
{
    public DA3CameraImporter importer;

    public Transform splatRoot;
    public Transform reconstructionRoot;

    [Header("GLB Root")]
    public GameObject glbRoot;

    [Header("Runtime")]
    public Vector3[] pointCloud;
    public Camera[] renderCameras;
    public Vector3[][] glbCameras;

    public RenderTexture[] depthMaps;
    public RenderTexture[] depthMinMaps;
    public RenderTexture[] linearDepthMaps;
    public Texture2D[] cpuDepthMaps;

    public SplatData[] splatBuffers = new SplatData[2];

    private int activeBuffer = 0;

    public SplatData CurrentSplat =>
        splatBuffers[activeBuffer];

    public SplatData NextSplat =>
        splatBuffers[1 - activeBuffer];

    public Texture2D[] colorFrames;
    //public Texture2D[] depthFrames;

    public RenderTexture[] colorRenderTargets;

    public int numCameras;
    public float fps = 30f;
    public int frameCount;

    public ComputeShader splatCompute;

    //public Slider loadingBar;

    public int currentFrame = 0;
    private float timer;
    bool callNextFrame  = false;

    public float targetSceneSize = 10f;
    float currentScale = 1.0f;
    float gaussianSize;

    public DepthDisplaySetup depthDisplay;

    bool colourReady = false;

    private Vector3[] referencePositions;
    Vector3[] referenceDirections;
    Quaternion[] referenceRotations;
    Vector3 refCentroid;
    Vector3 currentCentroid;

    private Matrix4x4 currentFrameAlignment = Matrix4x4.identity;
    private Matrix4x4 reconstructionMatrix = Matrix4x4.identity;

    private void Start()
    {
        gaussianSize = 0.01f / targetSceneSize;
    }
    public void RunGPUColouring(SplatData targetSplat)
    {
        int count = targetSplat.Count;

        int kernel = splatCompute.FindKernel("ColourGaussians");

        int requestedFrame = currentFrame;

        colourReady = false;

        targetSplat.ResetAccumulation();

        // Reset best camera scores
        float[] initialScores = new float[count];

        for (int i = 0; i < count; i++)
        {
            initialScores[i] = float.MaxValue;
        }

        targetSplat.BestCameraScoreBuffer.SetData(initialScores);

        for (int cam = 0; cam < numCameras; cam++)
        {
            Texture2D image = colorFrames[cam];

            splatCompute.SetInt("_GaussianCount", count);

            splatCompute.SetInt(
                "ColorTextureWidth",
                image.width
            );

            splatCompute.SetInt(
                "ColorTextureHeight",
                image.height
            );

            splatCompute.SetInt("TextureWidth", depthMaps[cam].width);
            splatCompute.SetInt("TextureHeight", depthMaps[cam].height);

            splatCompute.SetBuffer(
                kernel,
                "_BestCameraScore",
                targetSplat.BestCameraScoreBuffer
            );

            Matrix4x4 vp =
                GL.GetGPUProjectionMatrix(
                    renderCameras[cam].projectionMatrix,
                    true)
                *
                renderCameras[cam].worldToCameraMatrix;


            splatCompute.SetMatrix("_ViewProj", vp);


            splatCompute.SetBuffer(
                kernel,
                "_Positions",
                targetSplat.PositionsBuffer
            );

            splatCompute.SetBuffer(
                kernel,
                "_AccumColor",
                targetSplat.AccumColorBuffer
            );

            splatCompute.SetBuffer(
                kernel,
                "_Contribution",
                targetSplat.ContributionBuffer
            );

            splatCompute.SetBuffer(
                kernel,
                "_DebugBuffer",
                targetSplat.DebugBuffer
            );

            splatCompute.SetTexture(
                kernel,
                "_ColorTex",
                image
            );

            splatCompute.SetTexture(
                kernel,
                "_DepthTex",
                 depthMaps[cam]
            );

            int groups = Mathf.CeilToInt(count / 256f);

            splatCompute.Dispatch(
                kernel,
                groups,
                1,
                1
            );

            Vector4[] debug = new Vector4[3];

            targetSplat.DebugBuffer.GetData(debug);

        }

            
        // finalize average colors
        int finalize = splatCompute.FindKernel("FinalizeColours");

        splatCompute.SetInt("_GaussianCount", count);

        splatCompute.SetBuffer(
            finalize,
            "_AccumColor",
            targetSplat.AccumColorBuffer
        );

        splatCompute.SetBuffer(
            finalize,
            "_Contribution",
            targetSplat.ContributionBuffer
        );

        splatCompute.SetBuffer(
            finalize,
            "_FinalColor",
            targetSplat.FinalColorBuffer
        );


        int finalGroups = Mathf.CeilToInt(count / 256f);
        
        splatCompute.Dispatch(
            finalize,
            finalGroups,
            1,
            1
        );


        AsyncGPUReadback.Request(
        targetSplat.FinalColorBuffer,
        request =>
        {
            if (request.hasError)
            {
                UnityEngine.Debug.LogError("Colour readback failed");
                return;
            }


            var data = request.GetData<Vector4>();

            Color[] colors = new Color[data.Length];

            for (int i = 0; i < data.Length; i++)
                colors[i] = data[i];


            targetSplat.Colors = colors;
            targetSplat.UpdateColorsOnly(colors);

            colourReady = true;

            SwapSplats();
        });
    }

    public void GenerateGaussianDepth(
    Camera cam,
    SplatData splatData,
    RenderTexture depthMinTexture,
    int cameraIndex)
    {
        int kernel = splatCompute.FindKernel("WriteDepth");

        splatCompute.SetTexture(
            kernel,
            "DepthTexture",
            depthMinTexture
        );
        /*
        splatCompute.SetTexture(
            kernel,
            "_DA3DepthTex",
            depthFrames[cameraIndex]
        );
        */
        splatCompute.SetBuffer(
            kernel,
            "_Positions",
            splatData.PositionsBuffer
        );

        splatCompute.SetMatrix(
            "_WorldToCamera",
            cam.worldToCameraMatrix
        );

        splatCompute.SetInt(
            "TextureWidth",
            depthMinTexture.width
        );


        splatCompute.SetInt(
            "TextureHeight",
            depthMinTexture.height
        );

        
        Matrix4x4 vp =
    GL.GetGPUProjectionMatrix(
        cam.projectionMatrix,
        true)
    * cam.worldToCameraMatrix;

        splatCompute.SetMatrix(
            "_ViewProj",
            vp
        );

        splatCompute.SetBuffer(
                kernel,
                "_DebugBuffer",
                NextSplat.DebugBuffer
            );

        int groups = Mathf.CeilToInt(
            splatData.Count / 64.0f
        );

        splatCompute.Dispatch(
            kernel,
            groups,
            1,
            1
        );
    }
    // =====================================================
    // INIT FROM GLB + DATASET
    // =====================================================
    public void InitializeScene()
    {
        CreateCamerasFromDataset();

        importer.InitializeCameras();
        importer.cameras = renderCameras;

    }

    // -----------------------------------------------------
    // 1. GLB parsing: geometry_0 = splats, geometry_1+ = cameras
    // -----------------------------------------------------
    public static Vector3[] LoadVertices(string path)
    {
        using FileStream stream = File.OpenRead(path);

        // Read vertex count
        byte[] countBytes = new byte[4];
        stream.Read(countBytes, 0, 4);

        int count = System.BitConverter.ToInt32(countBytes, 0);

        // Allocate output array
        Vector3[] verts = new Vector3[count];

        // Read directly into the Vector3 array's memory
        Span<byte> vertexBytes = MemoryMarshal.AsBytes(verts.AsSpan());

        int totalRead = 0;

        while (totalRead < vertexBytes.Length)
        {
            int bytesRead = stream.Read(vertexBytes.Slice(totalRead));

            if (bytesRead == 0)
                throw new EndOfStreamException();

            totalRead += bytesRead;
        }

        return verts;
    }

    public static Vector3[][] LoadCameraVertices(string path)
    {
        using (BinaryReader reader =
            new BinaryReader(File.OpenRead(path)))
        {
            int cameraCount = reader.ReadInt32();

            Vector3[][] cameras =
                new Vector3[cameraCount][];

            for (int c = 0; c < cameraCount; c++)
            {
                int vertexCount = reader.ReadInt32();

                cameras[c] = new Vector3[vertexCount];

                for (int i = 0; i < vertexCount; i++)
                {
                    cameras[c][i] = new Vector3(
                        reader.ReadSingle(),
                        reader.ReadSingle(),
                        reader.ReadSingle());
                }
            }

            return cameras;
        }
    }

    // -----------------------------------------------------
    // 2. Create Unity cameras from dataset count
    // -----------------------------------------------------
    void CreateCamerasFromDataset()
    {
        string[] camFolders = Directory.GetDirectories(FileSelector.frameFolders[currentFrame])
            .OrderBy(f => f)
            .ToArray();

        numCameras = camFolders.Length;

        renderCameras = new Camera[numCameras];
        colorRenderTargets = new RenderTexture[numCameras];
        depthMaps = new RenderTexture[numCameras];
        depthMinMaps = new RenderTexture[numCameras];

        for (int i = 0; i < numCameras; i++)
        {
            GameObject camObj = new GameObject($"RenderCam_{i:000}");
            Camera cam = camObj.AddComponent<Camera>();

            renderCameras[i] = cam;

            depthMaps[i] = new RenderTexture(
            DA3CameraImporter.imageWidth,
            DA3CameraImporter.imageHeight,
            0,
            RenderTextureFormat.RFloat);

            depthMaps[i].enableRandomWrite = true;
            depthMaps[i].Create();

            depthMinMaps[i] = new RenderTexture(
                DA3CameraImporter.imageWidth,
                DA3CameraImporter.imageHeight,
                0,
                RenderTextureFormat.RInt);

            depthMinMaps[i].enableRandomWrite = true;
            depthMinMaps[i].Create();
        }

        //Debug.Log($"Created {numCameras} Unity cameras");

    }

    public void SetCamPositions(Vector3[][] cameraVertices)
    {
        for (int i = 0; i < renderCameras.Length; i++)
        {
            if (CameraMeshPose.TryGetPose(
                    cameraVertices[i],
                    out Vector3 pos,
                    out Quaternion rot))
            {
                renderCameras[i].transform.position = pos;
                renderCameras[i].transform.rotation = rot;
            }
        }
    }

    void ApplyCameraTransform()
    {
        for (int i = 0; i < numCameras; i++)
        {
            Vector3 oldPos =
                renderCameras[i].transform.position;


            Quaternion oldRot =
                renderCameras[i].transform.rotation;


            Vector3 newPos =
                reconstructionMatrix.MultiplyPoint3x4(
                    renderCameras[i].transform.position);

            Vector3 forward =
                reconstructionMatrix
                .MultiplyVector(
                    oldRot * Vector3.forward);


            Vector3 up =
                reconstructionMatrix
                .MultiplyVector(
                    oldRot * Vector3.up);


            renderCameras[i].transform.position =
                newPos;


            renderCameras[i].transform.rotation =
                Quaternion.LookRotation(
                    forward,
                    up);
        }
    }

    // -----------------------------------------------------
    // 3. Attach everything to root
    // -----------------------------------------------------
    void AttachToRoot()
    {
        
        if (reconstructionRoot == null)
        {
            GameObject reconstruction = new GameObject("ReconstructionRoot");
            reconstructionRoot = reconstruction.transform;
        }

        splatRoot.SetParent(reconstructionRoot, false);

        foreach (var cam in renderCameras)
            cam.transform.SetParent(reconstructionRoot, false);

        Vector3 scaleX = new Vector3(-1, 1, 1);
        reconstructionRoot.localScale = scaleX * targetSceneSize;
    }

    // =====================================================
    // PLAYBACK
    // =====================================================
    public void StartPlayback()
    {
        InitializeScene();
        LoadCurrentFrame();

        if (renderCameras == null || renderCameras.Length == 0)
        {
            UnityEngine.Debug.LogError("renderCameras not initialized. Did you call InitializeScene()?");
            return;
        }

        if (colorFrames == null || colorFrames.Length == 0)
        {
            UnityEngine.Debug.LogError("Frames not loaded.");
            return;
        }


        NextSplat.GaussiansFromCloud(pointCloud, gaussianSize);

        // Generate Gaussian depth maps
        for (int i = 0; i < numCameras; i++)
        {
            // reset integer buffer
            ClearDepthMin(depthMinMaps[i]);


            // write closest gaussian depths
            GenerateGaussianDepth(
                renderCameras[i],
                NextSplat,
                depthMinMaps[i],
                i
            );


            // convert integer mm -> float meters
            ConvertDepth(
                depthMinMaps[i],
                depthMaps[i]
            );
        }


        RunGPUColouring(NextSplat);

        callNextFrame = true;
    }


    void ClearDepthMin(RenderTexture depthTexture)
    {
        int kernel = splatCompute.FindKernel("ClearDepth");

        splatCompute.SetTexture(
            kernel,
            "DepthTexture",
            depthTexture
        );

        splatCompute.SetInt(
            "TextureWidth",
            depthTexture.width
        );

        splatCompute.SetInt(
            "TextureHeight",
            depthTexture.height
        );


        int groupsX = Mathf.CeilToInt(depthTexture.width / 8f);
        int groupsY = Mathf.CeilToInt(depthTexture.height / 8f);

        splatCompute.Dispatch(
            kernel,
            groupsX,
            groupsY,
            1
        );
    }

    void ConvertDepth(
    RenderTexture depthMinTexture,
    RenderTexture depthFloatTexture)
    {
        int kernel = splatCompute.FindKernel("ConvertDepth");


        splatCompute.SetTexture(
            kernel,
            "DepthIntTexture",
            depthMinTexture
        );


        splatCompute.SetTexture(
            kernel,
            "DepthFloatTexture",
            depthFloatTexture
        );


        splatCompute.SetInt(
            "TextureWidth",
            depthFloatTexture.width
        );

        splatCompute.SetInt(
            "TextureHeight",
            depthFloatTexture.height
        );


        int groupsX = Mathf.CeilToInt(depthFloatTexture.width / 8f);
        int groupsY = Mathf.CeilToInt(depthFloatTexture.height / 8f);


        splatCompute.Dispatch(
            kernel,
            groupsX,
            groupsY,
            1
        );
    }

    // =====================================================
    // UPDATE (optional playback)
    // =====================================================
    private void Update()
    {
        if (colorFrames == null || colorFrames.Length == 0)
            return;

        timer += Time.deltaTime;

        if (timer >= 1f / fps)
        {
            timer = 0f;
            if (callNextFrame && colourReady)
            {
                NextFrame();
                if (depthDisplay != null)
                {
                    depthDisplay.SetupDepthTexture();
                }
            }
        }
    }

    void LoadCurrentFrame()
    {
        //Debug.Log("Frame: " + currentFrame);

        string frameFolder = FileSelector.frameFolders[currentFrame];

        pointCloud = LoadVertices(Path.Combine(frameFolder, "points.bin"));

        glbCameras =
            LoadCameraVertices(Path.Combine(frameFolder, "cameras.bin"));

        //----------------------------------------------------
        // Parse GLB
        //----------------------------------------------------

        splatRoot.position = Vector3.zero;
        splatRoot.rotation = Quaternion.identity;
        splatRoot.localScale = Vector3.one;


        //----------------------------------------------------
        // Load colour/depth images
        //----------------------------------------------------

        string camerasPath = Path.Combine(frameFolder, "Cameras");

        string[] camFolders = Directory.GetDirectories(camerasPath)
            .OrderBy(f => f)
            .ToArray();

        for (int cam = 0; cam < numCameras; cam++)
        {
            string colourFolder = Path.Combine(camFolders[cam], "Colour");
            string depthFolder = Path.Combine(camFolders[cam], "Depth");

            FileSelector.LoadSingleImage(colourFolder, cam, colorFrames[cam]);

            //depthFrames[cam] = FileSelector.LoadDepthImage(depthFolder);

        }

        //----------------------------------------------------
        // Update camera poses
        //----------------------------------------------------

        importer.ApplyCameras();

        SetCamPositions(glbCameras);

        //get the camera transforms for frame 0
        if (currentFrame == 0)
        {
            referencePositions = new Vector3[numCameras];
            referenceRotations = new Quaternion[numCameras];

            for (int i = 0; i < numCameras; i++)
            {
                referencePositions[i] = renderCameras[i].transform.position;
                referenceRotations[i] = renderCameras[i].transform.rotation;
            }

            refCentroid = Vector3.zero;

            for (int i = 0; i < numCameras; i++)
                refCentroid += referencePositions[i];

            refCentroid /= numCameras;
        }
        
        //scale
        //get the distance between first two cameras

        float currSize = Vector3.Distance(renderCameras[0].transform.position, renderCameras[1].transform.position);
        float refSize = Vector3.Distance(referencePositions[0], referencePositions[1]);

        currentScale = refSize / currSize;

        //-------------------------
        // Rotation alignment
        //-------------------------

        Vector3 currentUp = Vector3.zero;
        Vector3 referenceUp = Vector3.zero;
        Vector3 currentCenter = Vector3.zero;

        // accumulate camera directions
        for (int i = 0; i < numCameras; i++)
        {
            currentUp += renderCameras[i].transform.up;
            referenceUp += referenceRotations[i] * Vector3.up;
            currentCenter += renderCameras[i].transform.position;
        }

        currentUp.Normalize();
        referenceUp.Normalize();
        currentCenter /= numCameras;

        Vector3 currentDirection =
            (renderCameras[0].transform.position - currentCenter).normalized;

        Vector3 referenceDirection =
            (referencePositions[0] - refCentroid).normalized;


        // yaw alignment
        Quaternion yaw =
            Quaternion.FromToRotation(
                currentDirection,
                referenceDirection);


        // fix roll using camera up vectors

        Vector3 rotatedUp =
            yaw * currentUp;


        Quaternion roll =
            Quaternion.FromToRotation(
                rotatedUp,
                referenceUp);


        Quaternion rotationOffset = roll * yaw;

        Matrix4x4 scaleMatrix = Matrix4x4.Scale(Vector3.one * currentScale);
        Matrix4x4 rotationMatrix = Matrix4x4.Rotate(rotationOffset);

        // temp is only needed for computing delta
        Matrix4x4 rotationScale = rotationMatrix * scaleMatrix;

        Vector3 transformedCamera0 =
            rotationScale.MultiplyPoint3x4(renderCameras[0].transform.position);

        Vector3 delta =
            referencePositions[0] - transformedCamera0;

        Matrix4x4 translationMatrix = Matrix4x4.Translate(delta);

        reconstructionMatrix =
            translationMatrix *
            rotationScale;

        ApplyCameraTransform();

        AttachToRoot();

        /*
        for (int i = 0; i < numCameras; i++)
        {
            UnityEngine.Debug.DrawRay(renderCameras[i].transform.position, renderCameras[i].transform.rotation * Vector3.forward * 0.2f, Color.blue, 100f);
            UnityEngine.Debug.DrawRay(renderCameras[i].transform.position, renderCameras[i].transform.rotation * Vector3.up * 0.2f, Color.green, 100f);
            UnityEngine.Debug.DrawRay(renderCameras[i].transform.position, renderCameras[i].transform.rotation * Vector3.right * 0.2f, Color.red, 100f);
        }
        */
    }


    public void NextFrame()
    {
        Stopwatch frameSW = Stopwatch.StartNew();
        currentFrame++;

        if (currentFrame >= frameCount)
            currentFrame = 0;

        LoadCurrentFrame();

        Vector3[] alignedPoints = new Vector3[pointCloud.Length];

        for (int i = 0; i < pointCloud.Length; i++)
        {
            alignedPoints[i] =
                    reconstructionMatrix.MultiplyPoint3x4(
                        pointCloud[i]);
        }

        NextSplat.GaussiansFromCloud(alignedPoints, gaussianSize);

        for (int i = 0; i < numCameras; i++)
        {
            
            if (colorFrames == null)
            {
                UnityEngine.Debug.LogError($"Camera {i} frames missing");
                continue;
            }

            if (currentFrame >= frameCount)
            {
                UnityEngine.Debug.LogError($"Frame overflow on camera {i}");
                continue;
            }

            ClearDepthMin(depthMinMaps[i]);

            GenerateGaussianDepth(
                renderCameras[i],
                NextSplat,
                depthMinMaps[i],
                i);

            ConvertDepth(
                depthMinMaps[i],
                depthMaps[i]);
        }

        RunGPUColouring(NextSplat);
        UnityEngine.Debug.Log(
            $"Frame generation time: {frameSW.ElapsedMilliseconds} ms " +
            $"({1000f / frameSW.ElapsedMilliseconds:F2} FPS)"
        );
    }

    void SwapSplats()
    {
        activeBuffer = 1 - activeBuffer;

    }
}