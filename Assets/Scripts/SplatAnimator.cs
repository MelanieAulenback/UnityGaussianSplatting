using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
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

    public SplatData splat;
    public Texture2D[][] colorFrames;

    public int numCameras;
    public float fps = 30f;

    //public Slider loadingBar;

    private int currentFrame = 0;
    private float timer;

    public float targetSceneSize = 10f;

    // =====================================================
    // INIT FROM GLB + DATASET
    // =====================================================
    public void InitializeScene()
    {
        SetupGLBGeometry();
        CreateCamerasFromDataset();

        AttachToRoot();
        ApplyScale();

        importer.InitializeCameras();
        importer.cameras = renderCameras;

        importer.ApplyCameras(); 

        SetCamPositions();

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
        string camerasPath = Path.Combine(FileSelector.datasetRoot, "Cameras");

        string[] camFolders = Directory.GetDirectories(camerasPath)
            .OrderBy(f => f)
            .ToArray();

        numCameras = camFolders.Length;

        renderCameras = new Camera[numCameras];

        for (int i = 0; i < numCameras; i++)
        {
            GameObject camObj = new GameObject($"RenderCam_{i:000}");
            Camera cam = camObj.AddComponent<Camera>();
            camObj.AddComponent<DepthVisibility>(); 

            // optional tuning
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;

            renderCameras[i] = cam;
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
                Debug.Log($"Recovered Position: {pos}");
                Debug.Log($"Recovered Rotation: {rot.eulerAngles}");
                Debug.DrawRay(pos, rot * Vector3.forward * 0.2f, Color.blue, 100f);
                Debug.DrawRay(pos, rot * Vector3.up * 0.2f, Color.green, 100f);
                Debug.DrawRay(pos, rot * Vector3.right * 0.2f, Color.red, 100f);
            }

            //change just the position to the glb camera's position
            renderCameras[i].transform.position = pos;
            renderCameras[i].transform.rotation = rot;
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

        Texture2D[] images = new Texture2D[numCameras];

        for (int i = 0; i < numCameras; i++)
        {
            images[i] = colorFrames[i][0];
        }

        splat.GaussiansFromCloud(pointCloud, renderCameras, images, 0.01f);
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
            currentFrame++;
        }

        //if (loadingBar != null && colorFrames.Length > 0)
         //   loadingBar.value = (float)currentFrame / colorFrames[0].Length;
    }

    /*
    public void NextFrame()
    {
        int frameCount = colorFrames[0].Length;

        currentFrame++;

        if (currentFrame >= frameCount)
            currentFrame = 0;

        for (int i = 0; i < numCameras; i++)
        {
            
            if (colorFrames[i] == null || depthFrames[i] == null)
            {
                Debug.LogError($"Camera {i} frames missing");
                continue;
            }

            if (currentFrame >= colorFrames[i].Length)
            {
                Debug.LogError($"Frame overflow on camera {i}");
                continue;
            }

            splats[i].UpdateFromDepthMap(
                colorFrames[i][currentFrame],
                depthFrames[i][currentFrame],
                renderCameras[i],
                1f, 5f, 0.01f, true
            );
        }
    }
    */
}