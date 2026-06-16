using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SplatAnimator : MonoBehaviour
{
    public Transform reconstructionRoot;

    public GameObject pointCloud;
    public GameObject[] glbCameras;

    public SplatData splat;
    public Camera[] renderCameras;

    public Texture2D[][] colorFrames;

    public int numCameras = 2;
    public float fps = 30f;

    public Slider loadingBar;

    private int currentFrame = 0;
    private float timer;
    public bool IsReady;

    public float targetSceneSize = 10f;

    private void Start()
    {
        // =====================================================
        // STEP 3: ATTACH EVERYTHING TO SAME ROOT
        // =====================================================

        if (reconstructionRoot != null)
        {
            pointCloud.transform.SetParent(reconstructionRoot);

            foreach (var cam in glbCameras)
            {
                cam.transform.SetParent(reconstructionRoot);
            }

            foreach (var cam in renderCameras)
            {
                cam.transform.SetParent(reconstructionRoot);
            }
        }
    }

    private void Update()
    {
        if (colorFrames == null)
            return;

        if (colorFrames.Length == 0 || colorFrames[0].Length == 0)
            return;

        IsReady = true;

        timer += Time.deltaTime;
        if (timer >= 1f / fps)
        {
            timer = 0f;
            // NextFrame();
        }

        loadingBar.value = (float)currentFrame / colorFrames[0].Length;
    }

    public void StartPlayback()
    {
        if (colorFrames == null || colorFrames.Length == 0)
        {
            Debug.LogError("Frames not loaded.");
            return;
        }

        ApplyScale();

        Texture2D[] images = new Texture2D[numCameras];

        for (int i = 0; i < numCameras; i++)
            images[i] = colorFrames[i][0];

        splat.GaussiansFromCloud(pointCloud, renderCameras, images, 0.01f);
    }

    void ApplyScale()
    {
        Mesh mesh = pointCloud.GetComponent<MeshFilter>().sharedMesh;

        Bounds b = mesh.bounds;
        float maxExtent = Mathf.Max(b.size.x, b.size.y, b.size.z);

        float scale = targetSceneSize / maxExtent;

        reconstructionRoot.localScale = Vector3.one * scale;
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