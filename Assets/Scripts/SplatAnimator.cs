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
using System.Threading.Tasks;

public class SplatAnimator : MonoBehaviour
{
    [Header("Other Scripts")]
    public DA3CameraImporter importer;
    public DepthDisplaySetup depthDisplay;

    [Header("GLB Root")]
    public Transform splatRoot;
    public Transform reconstructionRoot;

    [Header("Runtime")]
    public Vector3[] pointCloud;
    public Vector3[][] glbCameras;
    public Camera[] renderCameras;

    public Texture2D[] colorFrames;
    public RenderTexture[] depthMaps;
    public RenderTexture[] depthMinMaps;

    [Header("Counts")]
    public int numCameras;

    public float fps = 30f;
    public int frameCount;
    public int currentFrame = 0;
    private float timer;

    public float targetSceneSize = 10f;
    float currentScale = 1.0f;
    float gaussianSize;

    bool colourReady = false;
    bool callNextFrame = false;

    [Header("Transforms")]
    private Vector3[] referencePositions;
    Quaternion[] referenceRotations;
    Vector3 refCentroid;

    private Matrix4x4 reconstructionMatrix = Matrix4x4.identity;

    Vector3[] alignedPoints;

    [Header("cache")]
    const int CACHE_SIZE = 5;
    FrameCache[] frameCache;
    Task loadingTask;

    [Header("Buffers")]
    private int activeBuffer = 0;
    public SplatData[] splatBuffers = new SplatData[2];
    public SplatData CurrentSplat => splatBuffers[activeBuffer];
    public SplatData NextSplat => splatBuffers[1 - activeBuffer];

    public ComputeShader splatCompute;

    private void Start()
    {
        //scale gaussian size depending on scene size
        gaussianSize = 0.01f / targetSceneSize;
    }

    // =====================================================
    // PLAYBACK
    // =====================================================
    private void Update()
    {
        //exit if theres no frames yet
        if (colorFrames == null || colorFrames.Length == 0)
            return;

        timer += Time.deltaTime;

        //advance a frame according to the fps
        if (timer >= 1f / fps)
        {
            timer = 0f;
            //only advance if the colours loaded and start playback has started
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

    //called when start is pressed
    //starts the playback of frames
    public async void StartPlayback()
    {
        //initialize the scene
        InitializeScene();

        //preload first 5 (cache size) frames
        for (int i = 0; i < CACHE_SIZE && i < frameCount; i++)
            _ = LoadFrameAsync(i);

        // Wait until frame 0 is ready
        while (!frameCache[0].loaded)
            await Task.Yield();
        
        //load current frame
        LoadCurrentFrame();

        //check cameras are loaded
        if (renderCameras == null || renderCameras.Length == 0)
        {
            UnityEngine.Debug.LogError("renderCameras not initialized. Did you call InitializeScene()?");
            return;
        }

        //check colour frames are loaded
        if (colorFrames == null || colorFrames.Length == 0)
        {
            UnityEngine.Debug.LogError("Frames not loaded.");
            return;
        }

        //generate splat
        NextSplat.GaussiansFromCloud(pointCloud, gaussianSize);

        //generate gaussian depth maps
        for (int i = 0; i < numCameras; i++)
        {
            //reset integer buffer
            ClearDepthMin(depthMinMaps[i]);


            //write closest gaussian depths
            GenerateGaussianDepth(
                renderCameras[i],
                NextSplat,
                depthMinMaps[i],
                i
            );


            //convert integer mm -> float meters
            ConvertDepth(
                depthMinMaps[i],
                depthMaps[i]
            );
        }

        //colour the splat
        RunGPUColouring(NextSplat);

        //give ability to call next frame
        callNextFrame = true;
    }

    //loads the next frame
    public void NextFrame()
    {
        //Stopwatch sw = Stopwatch.StartNew();
        currentFrame++;

        //reset playback if it reaches the end (loop)
        if (currentFrame >= frameCount)
            currentFrame = 0;

        //load current frame
        LoadCurrentFrame();

        //create aligned points array if its null
        if (alignedPoints == null ||
           alignedPoints.Length != pointCloud.Length)
        {
            alignedPoints = new Vector3[pointCloud.Length];
        }

        //apply the camera transforms to the points
        for (int i = 0; i < pointCloud.Length; i++)
        {
            alignedPoints[i] =
                    reconstructionMatrix.MultiplyPoint3x4(
                        pointCloud[i]);
        }

        //generate the splat
        NextSplat.GaussiansFromCloud(alignedPoints, gaussianSize);

        //if there are colour frames, create a depth map per camera
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

            //create depth map
            GenerateGaussianDepth(
                renderCameras[i],
                NextSplat,
                depthMinMaps[i],
                i);

            ConvertDepth(
                depthMinMaps[i],
                depthMaps[i]);
        }


        //colour the gaussians
        RunGPUColouring(NextSplat);
        /*
        UnityEngine.Debug.Log(
            $"Frame generation time: {sw.ElapsedMilliseconds} ms " +
            $"({1000f / sw.ElapsedMilliseconds:F2} FPS)"
        );
        */
    }

    //loads the current frame
    void LoadCurrentFrame()
    {
        //Debug.Log("Frame: " + currentFrame);

        //get the current frame folder
        string frameFolder = FileSelector.frameFolders[currentFrame];

        //make sure frame is loaded
        int slot = currentFrame % CACHE_SIZE;

        if (!frameCache[slot].loaded)
        {
            UnityEngine.Debug.Log("Frame not ready.");
            return;
        }

        //access the point clouds for the current frame from the cache
        pointCloud = frameCache[slot].pointCloud;
        glbCameras = frameCache[slot].cameraVertices;

        //----------------------------------------------------
        // Zero out transforms on splat
        //----------------------------------------------------

        splatRoot.position = Vector3.zero;
        splatRoot.rotation = Quaternion.identity;
        splatRoot.localScale = Vector3.one;


        //----------------------------------------------------
        // Load colour images
        //----------------------------------------------------

        for (int cam = 0; cam < numCameras; cam++)
        {

            if (!frameCache[slot].loaded)
            {
                UnityEngine.Debug.Log("Frame not ready.");
                return;
            }

            colorFrames[cam].LoadRawTextureData(
                frameCache[slot].colourBytes[cam]);

            colorFrames[cam].Apply(false);

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

            //find center position of camera rig
            refCentroid = Vector3.zero;

            for (int i = 0; i < numCameras; i++)
                refCentroid += referencePositions[i];

            refCentroid /= numCameras;
        }

        //scale
        //get the distance between first two cameras
        float currSize = Vector3.Distance(renderCameras[0].transform.position, renderCameras[1].transform.position);
        float refSize = Vector3.Distance(referencePositions[0], referencePositions[1]);

        //get ratio of ref camera distances to current cam distances
        currentScale = refSize / currSize;

        //-------------------------
        // Rotation alignment
        //-------------------------

        Vector3 currentUp = Vector3.zero;
        Vector3 referenceUp = Vector3.zero;
        Vector3 currentCenter = Vector3.zero;

        //accumulate camera directions
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

        //yaw alignment
        Quaternion yaw =
            Quaternion.FromToRotation(
                currentDirection,
                referenceDirection);

        //fix roll using camera up vectors
        Vector3 rotatedUp =
            yaw * currentUp;

        Quaternion roll =
            Quaternion.FromToRotation(
                rotatedUp,
                referenceUp);

        Quaternion rotationOffset = roll * yaw;

        //-------------------------
        // Build matrices
        //-------------------------
        Matrix4x4 scaleMatrix = Matrix4x4.Scale(Vector3.one * currentScale);
        Matrix4x4 rotationMatrix = Matrix4x4.Rotate(rotationOffset);

        Matrix4x4 rotationScale = rotationMatrix * scaleMatrix;

        Vector3 transformedCamera0 =
            rotationScale.MultiplyPoint3x4(renderCameras[0].transform.position);

        Vector3 delta = referencePositions[0] - transformedCamera0;

        Matrix4x4 translationMatrix = Matrix4x4.Translate(delta);

        reconstructionMatrix =
            translationMatrix *
            rotationScale;

        //apply transforms to cameras
        ApplyCameraTransform();

        //parent splat and cameras to reconstruction root
        AttachToRoot();

        //preload next frame
        int preloadFrame = (currentFrame + CACHE_SIZE) % frameCount;

        if (loadingTask == null || loadingTask.IsCompleted)
        {
            loadingTask = LoadFrameAsync(preloadFrame);
        }

        /*
        for (int i = 0; i < numCameras; i++)
        {
            UnityEngine.Debug.DrawRay(renderCameras[i].transform.position, renderCameras[i].transform.rotation * Vector3.forward * 0.2f, Color.blue, 100f);
            UnityEngine.Debug.DrawRay(renderCameras[i].transform.position, renderCameras[i].transform.rotation * Vector3.up * 0.2f, Color.green, 100f);
            UnityEngine.Debug.DrawRay(renderCameras[i].transform.position, renderCameras[i].transform.rotation * Vector3.right * 0.2f, Color.red, 100f);
        }
        */
    }

    // =====================================================
    // 1. INIT FROM POINT CLOUDS + DATASET
    // =====================================================
    public void InitializeScene()
    {
        //create an array of 5 frame cache objects
        frameCache = new FrameCache[CACHE_SIZE];

        for (int i = 0; i < CACHE_SIZE; i++)
        {
            frameCache[i] = new FrameCache();
            frameCache[i].colourBytes = new byte[numCameras][];
            frameCache[i].loaded = false;
        }

        //create the cameras
        CreateCamerasFromDataset();

        //set camera intrinsics
        importer.InitializeCameras();

        //assign render cameras to importer cameras for consistency
        importer.cameras = renderCameras;

    }

    // -----------------------------------------------------
    // 2. Create Unity cameras from dataset count
    // -----------------------------------------------------
    void CreateCamerasFromDataset()
    {
        //get the camera folders for the current frame
        string[] camFolders = Directory.GetDirectories(FileSelector.frameFolders[currentFrame])
            .OrderBy(f => f)
            .ToArray();

        //num cameras is equal to teh number of camera folders
        numCameras = camFolders.Length;

        //initialize arrays
        renderCameras = new Camera[numCameras];
        depthMaps = new RenderTexture[numCameras];
        depthMinMaps = new RenderTexture[numCameras];

        //for the number of cameras
        for (int i = 0; i < numCameras; i++)
        {
            //create a camera
            GameObject camObj = new GameObject($"RenderCam_{i:000}");
            Camera cam = camObj.AddComponent<Camera>();

            //add it to the render cameras array
            renderCameras[i] = cam;

            //create a depth map for the current camera
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

    // -----------------------------------------------------
    // 3. Set camera positions from camera point clouds
    // -----------------------------------------------------
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


    // -------------------------------------------------------------------------
    // 4. Apply transform matrix between ref cam and current cam to current cam
    // -------------------------------------------------------------------------
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
    // 5. Attach splat and cameras to root
    // -----------------------------------------------------
    void AttachToRoot()
    {
        //create the reconstruction root object if it doesnt already exist
        if (reconstructionRoot == null)
        {
            GameObject reconstruction = new GameObject("ReconstructionRoot");
            reconstructionRoot = reconstruction.transform;
        }

        //attach splat to root
        splatRoot.SetParent(reconstructionRoot, false);

        //attach cameras to root
        foreach (var cam in renderCameras)
            cam.transform.SetParent(reconstructionRoot, false);

        //flip x position to removed mirroring
        Vector3 scaleX = new Vector3(-1, 1, 1);
        reconstructionRoot.localScale = scaleX * targetSceneSize;
    }

    // =====================================================
    // LOAD VERTICES FROM POINT CLOUD FILES
    // =====================================================
    public static Vector3[] LoadVertices(string path)
    {
        using FileStream stream = File.OpenRead(path);

        //read vertex count
        byte[] countBytes = new byte[4];
        stream.Read(countBytes, 0, 4);

        int count = System.BitConverter.ToInt32(countBytes, 0);

        //allocate output array
        Vector3[] verts = new Vector3[count];

        //read directly into the Vector3 array's memory
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

    //load camera vertices
    public static Vector3[][] LoadCameraVertices(string path)
    {
        //open the binary file for reading
        using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
        {
            //read how many cameras are stored in the file
            int cameraCount = reader.ReadInt32();

            //create an array to hold the vertices for each camera
            //each camera can have its own array of vertices
            Vector3[][] cameras = new Vector3[cameraCount][];

            //read the data for each camera
            for (int c = 0; c < cameraCount; c++)
            {
                //read how many vertices this camera contains
                int vertexCount = reader.ReadInt32();

                //create an array to store this camera's vertices
                cameras[c] = new Vector3[vertexCount];

                //read every vertex position
                for (int i = 0; i < vertexCount; i++)
                {
                    //each Vector3 is stored as three floats:
                    //x, y and z
                    cameras[c][i] = new Vector3(
                        reader.ReadSingle(),
                        reader.ReadSingle(),
                        reader.ReadSingle());
                }
            }

            //return all cameras and their vertex positions
            return cameras;
        }
    }

    // =====================================================
    // COLOUR GAUSSIANS + CREATE DEPTH MAP
    // =====================================================
    public void RunGPUColouring(SplatData targetSplat)
    {
        //get the number of gaussians that need to be coloured
        int count = targetSplat.Count;

        //find the compute shader kernel that projects colours
        //from the camera images onto the gaussians
        int kernel = splatCompute.FindKernel("ColourGaussians");

        //store the current frame so we know which frame
        //this colouring operation belongs to
        int requestedFrame = currentFrame;

        //mark colouring as still in progress
        colourReady = false;

        //clear any accumulated colours and contribution counts
        //from the previous colouring pass
        targetSplat.ResetAccumulation();

        //--------------------------------------------------------
        // Reset best camera scores
        //--------------------------------------------------------
        //create an array of the worst possible camera scores
        //every gaussian starts with no valid camera assigned
        float[] initialScores = new float[count];

        for (int i = 0; i < count; i++)
        {
            initialScores[i] = float.MaxValue;
        }

        //upload the initial scores to the GPU
        targetSplat.BestCameraScoreBuffer.SetData(initialScores);

        //--------------------------------------------------------
        // Project every camera onto the gaussians
        //-------------------------------------------------------

        for (int cam = 0; cam < numCameras; cam++)
        {
            //get this camera's colour image
            Texture2D image = colorFrames[cam];

            //tell the compute shader how many gaussians to process
            splatCompute.SetInt("_GaussianCount", count);

            //pass the colour image dimensions to the shader
            splatCompute.SetInt(
                "ColorTextureWidth",
                image.width
            );

            splatCompute.SetInt(
                "ColorTextureHeight",
                image.height
            );

            //pass the depth texture dimensions
            splatCompute.SetInt("TextureWidth", depthMaps[cam].width);
            splatCompute.SetInt("TextureHeight", depthMaps[cam].height);

            //buffer storing the best camera score for each gaussian
            splatCompute.SetBuffer(
                kernel,
                "_BestCameraScore",
                targetSplat.BestCameraScoreBuffer
            );

            //build the view-projection matrix for this camera
            //this converts world-space gaussian positions into
            //the camera's screen coordinates
            Matrix4x4 vp =
                GL.GetGPUProjectionMatrix(
                    renderCameras[cam].projectionMatrix,
                    true)
                *
                renderCameras[cam].worldToCameraMatrix;


            splatCompute.SetMatrix("_ViewProj", vp);

            //supply the gaussian positions
            splatCompute.SetBuffer(
                kernel,
                "_Positions",
                targetSplat.PositionsBuffer
            );

            //buffer that accumulates colour values from every camera
            splatCompute.SetBuffer(
                kernel,
                "_AccumColor",
                targetSplat.AccumColorBuffer
            );

            //buffer that counts how many cameras contributed to each gaussian
            splatCompute.SetBuffer(
                kernel,
                "_Contribution",
                targetSplat.ContributionBuffer
            );

            //optional debug buffer for inspecting shader values
            splatCompute.SetBuffer(
                kernel,
                "_DebugBuffer",
                targetSplat.DebugBuffer
            );

            //supply the colour image for this camera
            splatCompute.SetTexture(
                kernel,
                "_ColorTex",
                image
            );

            //supply the depth map used for occlusion testing
            splatCompute.SetTexture(
                kernel,
                "_DepthTex",
                 depthMaps[cam]
            );

            //calculate how many thread groups are needed
            //the compute shader processes 256 gaussians per group
            int groups = Mathf.CeilToInt(count / 256f);

            //execute the ColourGaussians kernel
            splatCompute.Dispatch(
                kernel,
                groups,
                1,
                1
            );


        }

        //--------------------------------------------------------
        // Average the accumulated colours
        //--------------------------------------------------------
        //run a second compute shader that converts the accumulated
        //colours into the final colour for each gaussian
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

        //dispatch the final averaging kernel
        int finalGroups = Mathf.CeilToInt(count / 256f);
        
        splatCompute.Dispatch(
            finalize,
            finalGroups,
            1,
            1
        );

        //--------------------------------------------------------
        // Copy the final colours back from the GPU
        //--------------------------------------------------------
        //request an asynchronous readback so Unity does not
        //stall while waiting for the GPU to finish
        AsyncGPUReadback.Request(
        targetSplat.FinalColorBuffer,
        request =>
        {
            //stop if the GPU readback failed
            if (request.hasError)
            {
                UnityEngine.Debug.LogError("Colour readback failed");
                return;
            }

            //read the GPU colour buffer
            var data = request.GetData<Vector4>();

            //convert the Vector4 values into Unity Color objects
            Color[] colors = new Color[data.Length];

            for (int i = 0; i < data.Length; i++)
                colors[i] = data[i];

            //store the colours on the CPU and upload them to the rendering buffers
            targetSplat.Colors = colors;
            targetSplat.UpdateColorsOnly(colors);

            //colouring has finished
            colourReady = true;

            //make the newly coloured splat the active one
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
    // CACHE
    // =====================================================
    //asynchronously loads all of the data needed for one animation frame
    //this includes the point cloud, camera vertices and colour images
    async Task LoadFrameAsync(int frame)
    {
        //determine which cache slot this frame should use
        //the cache acts as a circular buffer, so old frames are reused
        int slot = frame % CACHE_SIZE;

        //if this frame is already loaded into the cache,
        //there is nothing more to do
        if (frameCache[slot].loaded &&
            frameCache[slot].frameIndex == frame)
            return;

        //mark this cache slot as currently being loaded
        frameCache[slot].loaded = false;

        //get the folder containing all files for this frame
        string frameFolder = FileSelector.frameFolders[frame];

        //-------------------------------------------------
        // Start loading everything in parallel
        //-------------------------------------------------

        //begin loading the point cloud on a background thread
        //the main Unity thread can continue running while this loads
        Task<Vector3[]> pointTask =
            Task.Run(() =>
                LoadVertices(Path.Combine(frameFolder, "points.bin")));

        //begin loading the camera vertex data at the same time
        Task<Vector3[][]> cameraTask =
            Task.Run(() =>
                LoadCameraVertices(Path.Combine(frameFolder, "cameras.bin")));

        //create one loading task for each camera image
        Task<byte[]>[] colourTasks = new Task<byte[]>[numCameras];

        for (int cam = 0; cam < numCameras; cam++)
        {
            //build the path to this camera's colour image
            string file =
                Path.Combine(
                    frameFolder,
                    "Cameras",
                    $"Cam{cam:000}",
                    "Colour",
                    $"{cam:000000}.rgba");

            //begin loading the image bytes in parallel
            //every camera image starts loading immediately
            colourTasks[cam] =
                Task.Run(() => File.ReadAllBytes(file));
        }

        //-------------------------------------------------
        // Wait for point cloud and cameras
        //-------------------------------------------------
        //wait until the point cloud and camera vertices have finished loading,
        //then store it in the cache
        frameCache[slot].pointCloud = await pointTask;
        frameCache[slot].cameraVertices = await cameraTask;

        //-------------------------------------------------
        // Wait for every colour image
        //-------------------------------------------------

        await Task.WhenAll(colourTasks);

        //copy the loaded image bytes into the cache
        for (int cam = 0; cam < numCameras; cam++)
        {
            frameCache[slot].colourBytes[cam] =
                colourTasks[cam].Result;
        }

        //record which animation frame is stored in this cache slot
        frameCache[slot].frameIndex = frame;

        //mark this cache entry as completely loaded and ready to use
        frameCache[slot].loaded = true;
    }
    
    //swaps between 2 splat buffers
    void SwapSplats()
    {
        activeBuffer = 1 - activeBuffer;
    }
}