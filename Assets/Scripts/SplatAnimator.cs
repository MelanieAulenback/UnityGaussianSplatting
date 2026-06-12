using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VFX;

public class SplatAnimator : MonoBehaviour
{
    public SplatData[] splats;
    public Camera[] renderCameras;

    public Texture2D colorImage;
    public Texture2D depthMap;

    [HideInInspector] public Texture2D[][] colorFrames;
    //[HideInInspector] public Texture2D[][] depthFrames;
    public List<List<float[,,]>> depthFrames =
        new List<List<float[,,]>>();
    private int currentFrame = 0;

    public float fps = 30f;

    private float timer;
    public bool IsReady;

    private int lastFrame = -1;
    public int numCameras = 2;

    public Slider loadingBar;

    private void Update()
    {
        if (colorFrames == null)
        {
            return;
        }
        if (colorFrames[0].Length == 0)
        {
            Debug.Log("No frames loaded");
            return;
        }
        else
        {
            IsReady = true;
            timer += Time.deltaTime;

            if (timer >= 1f / fps)
            {
                timer = 0f;
                NextFrame();
            }

            //update progress bar
            loadingBar.value = (float)currentFrame / colorFrames[0].Length;
        }
    }
    public void StartPlayback()
    {
        if (colorFrames == null || depthFrames == null)
        {
            Debug.LogError("Frames not loaded.");
            return;
        }

        string path = Path.Combine(Application.dataPath, "Data/unified_pointcloud.npy");

        // LOAD ONCE
        splats[0].referenceCam = renderCameras[0];
        splats[0].referenceImage = colorFrames[0][0];

        splats[0].LoadPointCloud(path);

        Vector3[] sharedPositions = splats[0].Positions;
        Color[] sharedColors = splats[0].Colors;
        Vector3[] sharedAxes = splats[0].Axes;

        for (int i = 1; i < numCameras; i++)
        {
            splats[i].Positions = sharedPositions;
            splats[i].Colors = sharedColors;
            splats[i].Axes = sharedAxes;

            splats[i].InitializeBuffers();
        }
    }

    public void NextFrame()
    {
        Debug.Log($"Frame {currentFrame}");
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

            /*splats[i].UpdateFromDepthNpy(
                colorFrames[i][currentFrame],
                depthFrames[i][currentFrame],
                renderCameras[i],
                0.1f,
                20f,
                false
            );*/

        }
    }
}