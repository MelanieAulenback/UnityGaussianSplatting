using Siccity.GLTFUtility;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class SplatAnimator : MonoBehaviour
{
    public DA3CameraImporter importer;

    public Transform reconstructionRoot;

    [Header("GLB Root")]
    public GameObject glbRoot;

    [Header("Runtime")]
    public GameObject pointCloud;
    public Camera[] renderCameras;
    public GameObject[] glbCameras;

    public RenderTexture[] depthMaps;
    public RenderTexture[] depthMinMaps;
    public RenderTexture[] linearDepthMaps;
    public Texture2D[] cpuDepthMaps;

    public SplatData splat;
    public Texture2D[] colorFrames;
    public Texture2D[] depthFrames;

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

    public DepthDisplaySetup depthDisplay;

    bool colourReady = false;

    public void RunGPUColouring()
    {
        int count = splat.Count;

        int kernel = splatCompute.FindKernel("ColourGaussians");

        int requestedFrame = currentFrame;

        colourReady = false;

        splat.ResetAccumulation();

        // Reset best camera scores
        float[] initialScores = new float[count];

        for (int i = 0; i < count; i++)
        {
            initialScores[i] = float.MaxValue;
        }

        splat.BestCameraScoreBuffer.SetData(initialScores);

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
                splat.BestCameraScoreBuffer
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
                splat.PositionsBuffer
            );

            splatCompute.SetBuffer(
                kernel,
                "_AccumColor",
                splat.AccumColorBuffer
            );

            splatCompute.SetBuffer(
                kernel,
                "_Contribution",
                splat.ContributionBuffer
            );

            splatCompute.SetBuffer(
                kernel,
                "_DebugBuffer",
                splat.DebugBuffer
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

            splatCompute.SetMatrix(
                "_WorldToCamera",
                renderCameras[cam].worldToCameraMatrix
            );


            int groups = Mathf.CeilToInt(count / 256f);

            splatCompute.Dispatch(
                kernel,
                groups,
                1,
                1
            );

            Vector4[] debug = new Vector4[3];

            splat.DebugBuffer.GetData(debug);

            Debug.Log(
                $"Gaussian depth={debug[0].x}  depth map depth={debug[0].y}  difference={debug[0].z}  Score={debug[0].w}"
            );

            Debug.Log(
                "UV: " + debug[1].x +
                ", " + debug[1].y +
                " Pixel: " + debug[1].z +
                ", " + debug[1].w
            );

            Debug.Log(
                "Depth size: " + debug[2].x + "," + debug[2].y +
                " Color size: " + debug[2].z + "," + debug[2].w
            );
        }


        // finalize average colors
        int finalize = splatCompute.FindKernel("FinalizeColours");

        splatCompute.SetInt("_GaussianCount", count);

        splatCompute.SetBuffer(
            finalize,
            "_AccumColor",
            splat.AccumColorBuffer
        );

        splatCompute.SetBuffer(
            finalize,
            "_Contribution",
            splat.ContributionBuffer
        );

        splatCompute.SetBuffer(
            finalize,
            "_FinalColor",
            splat.FinalColorBuffer
        );


        int finalGroups = Mathf.CeilToInt(count / 256f);
        
        splatCompute.Dispatch(
            finalize,
            finalGroups,
            1,
            1
        );


        AsyncGPUReadback.Request(
        splat.FinalColorBuffer,
        request =>
        {
            if (request.hasError)
            {
                Debug.LogError("Colour readback failed");
                return;
            }


            var data = request.GetData<Vector4>();

            Color[] colors = new Color[data.Length];

            for (int i = 0; i < data.Length; i++)
                colors[i] = data[i];


            splat.Colors = colors;
            splat.UpdateColorsOnly(colors);

            colourReady = true;
        });
    }

    public void GenerateGaussianDepth(
    Camera cam,
    SplatData splatData,
    RenderTexture depthMinTexture,
    int cameraIndex)
    {
        Debug.Log($"Generating depth for {cam.name}");
        int kernel = splatCompute.FindKernel("WriteDepth");

        splatCompute.SetTexture(
            kernel,
            "DepthTexture",
            depthMinTexture
        );

        splatCompute.SetTexture(
            kernel,
            "_DA3DepthTex",
            depthFrames[cameraIndex]
        );

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
                splat.DebugBuffer
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
    void SetupGLBGeometry()
    {
        if (glbRoot == null)
        {
            Debug.LogError("GLB Root not assigned!");
            return;
        }

        List<Transform> geometries = new List<Transform>();

        foreach (Transform child in glbRoot.transform)
        {
            if (child.name.StartsWith("geometry_"))
                geometries.Add(child);
        }

        geometries = geometries
            .OrderBy(g => g.name)
            .ToList();

        if (geometries.Count == 0)
        {
            Debug.LogError("No geometry_* found in GLB!");
            return;
        }

        // geometry_0 = point cloud
        pointCloud = geometries[0].gameObject;

        // geometry_1+ = camera anchors
        glbCameras = geometries.Skip(1).Select(g => g.gameObject).ToArray();

        Debug.Log($"GLB parsed: 1 pointcloud + {glbCameras.Length} cameras");
        
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

        Debug.Log($"Created {numCameras} Unity cameras");

    }

    public void SetCamPositions()
    {
        
        for (int i = 0;i < renderCameras.Length;i++)
        {
            GameObject glbCam = glbCameras[i];
            Mesh mesh = glbCam.GetComponent<MeshFilter>().sharedMesh;
            MeshFilter mf = glbCam.GetComponent<MeshFilter>();
            Transform t = glbCam.transform;

            /*
            for (int j = 0; j < mesh.vertexCount; j++)
            {
                Debug.Log($"cam{i} vertex {j}: {mesh.vertices[j]}");
                Debug.Log($"cam{i} vertex {j} world space: {t.TransformPoint(mesh.vertices[j])}");
            }
            */

            if (CameraMeshPose.TryGetPose(importer, i, mf, out Vector3 pos, out Quaternion rot))
            {
                Debug.DrawRay(pos, rot * Vector3.forward * 0.2f, Color.blue, 100f);
                Debug.DrawRay(pos, rot * Vector3.up * 0.2f, Color.green, 100f);
                Debug.DrawRay(pos, rot * Vector3.right * 0.2f, Color.red, 100f);

            }

            //change just the position to the glb camera's position
            renderCameras[i].transform.position = pos;
            renderCameras[i].transform.rotation = rot;

            renderCameras[i].aspect = (float)colorFrames[i].width / colorFrames[i].height;
        }
    }
    // -----------------------------------------------------
    // 3. Attach everything to root
    // -----------------------------------------------------
    void AttachToRoot()
    {
        if (reconstructionRoot == null)
            return;

        if (pointCloud != null)
            pointCloud.transform.SetParent(reconstructionRoot);

        foreach (var cam in glbCameras)
            cam.transform.SetParent(reconstructionRoot);

        foreach (var cam in renderCameras)
            cam.transform.SetParent(reconstructionRoot);
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
            Debug.LogError("renderCameras not initialized. Did you call InitializeScene()?");
            return;
        }

        if (colorFrames == null || colorFrames.Length == 0)
        {
            Debug.LogError("Frames not loaded.");
            return;
        }


        splat.GaussiansFromCloud(pointCloud, 0.01f);

        // Generate Gaussian depth maps
        for (int i = 0; i < numCameras; i++)
        {
            // reset integer buffer
            ClearDepthMin(depthMinMaps[i]);


            // write closest gaussian depths
            GenerateGaussianDepth(
                renderCameras[i],
                splat,
                depthMinMaps[i],
                i
            );


            // convert integer mm -> float meters
            ConvertDepth(
                depthMinMaps[i],
                depthMaps[i]
            );
        }


        RunGPUColouring();

        callNextFrame = true;


    }

    // =====================================================
    // SCALE
    // =====================================================
    void ApplyScale()
    {
        Mesh mesh = pointCloud.GetComponent<MeshFilter>().sharedMesh;

        Bounds b = mesh.bounds;
        float maxExtent = Mathf.Max(b.size.x, b.size.y, b.size.z);

        float scale = targetSceneSize / maxExtent;

        reconstructionRoot.localScale = Vector3.one * scale;

        Debug.Log(reconstructionRoot.lossyScale);
        Debug.Log(reconstructionRoot.localScale);
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

        Debug.Log("Current frame: " + currentFrame);
        //if (loadingBar != null && colorFrames.Length > 0)
        //   loadingBar.value = (float)currentFrame / colorFrames[0].Length;
    }

    void LoadCurrentFrame()
    {
        string frameFolder = FileSelector.frameFolders[currentFrame];

        // ---------- Load GLB ----------
        string glbPath = Directory.GetFiles(frameFolder, "*.glb")
            .FirstOrDefault();

        if (glbPath == null)
        {
            Debug.LogError($"No GLB found in {frameFolder}");
            return;
        }

        // Destroy previous reconstruction
        if (glbRoot != null)
        {
            Destroy(glbRoot);
        }

        glbRoot = Importer.LoadFromFile(glbPath);

        if (glbRoot == null)
        {
            Debug.LogError("Failed to import GLB.");
            return;
        }

        reconstructionRoot = glbRoot.transform;

        //----------------------------------------------------
        // Parse GLB
        //----------------------------------------------------

        SetupGLBGeometry();

        AttachToRoot();

        ApplyScale();

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

            colorFrames[cam] = FileSelector.LoadSingleImage(colourFolder);
            depthFrames[cam] = FileSelector.LoadSingleImage(depthFolder);
        }

        //----------------------------------------------------
        // Update camera poses
        //----------------------------------------------------

        importer.ApplyCameras();

        SetCamPositions();
    }



    public void NextFrame()
    {

        currentFrame++;

        if (currentFrame >= frameCount)
            currentFrame = 0;

        LoadCurrentFrame();

        splat.GaussiansFromCloud(pointCloud, 0.01f);

        for (int i = 0; i < numCameras; i++)
        {
            
            if (colorFrames == null || depthFrames == null)
            {
                Debug.LogError($"Camera {i} frames missing");
                continue;
            }

            if (currentFrame >= frameCount)
            {
                Debug.LogError($"Frame overflow on camera {i}");
                continue;
            }

            ClearDepthMin(depthMinMaps[i]);

            GenerateGaussianDepth(
                renderCameras[i],
                splat,
                depthMinMaps[i],
                i);

            ConvertDepth(
                depthMinMaps[i],
                depthMaps[i]);
        }

        RunGPUColouring();
    }
    
}