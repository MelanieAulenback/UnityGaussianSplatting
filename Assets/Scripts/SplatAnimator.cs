using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.UI;

public class SplatAnimator : MonoBehaviour
{
    public SplatData[] splats;
    public Camera[] renderCameras;

    public Texture2D colorImage;
    public Texture2D depthMap;

    [HideInInspector] public Texture2D[][] colorFrames;
    [HideInInspector] public Texture2D[][] depthFrames;

    private int currentFrame = 0;

    public float fps = 30f;

    private float timer;
    public bool IsReady;

    private int lastFrame = -1;
    public int numCameras = 2;

    public Slider loadingBar;

    private void Start()
    {
        Debug.Log($"SplatAnimator START on: {gameObject.name}");
    }

    private void Update()
    {
        Debug.Log($"SplatAnimator UPDATE on: {gameObject.name}");
        if (colorFrames == null || depthFrames == null)
        {
            return;
        }
        if (colorFrames[0].Length == 0 || depthFrames[0].Length == 0)
        {
            Debug.Log("No frames loaded");
            return;
        }
        else
        {
            IsReady = true;
            timer += Time.deltaTime;

            Debug.Log($"Timer: {timer}");

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
        Debug.Log($"StartPlayback called on: {gameObject.name}");

        Debug.Log($"colorFrames null? {colorFrames == null}");
        Debug.Log($"depthFrames null? {depthFrames == null}");

        if (colorFrames == null || depthFrames == null)
        {
            Debug.LogError("Frames not loaded.");
            return;
        }

        if (colorFrames[0].Length == 0 || depthFrames[0].Length == 0)
        {
            Debug.LogError("No images found.");
            return;
        }

        for (int i = 0; i < numCameras; i++)
        {
            splats[i].GenerateFromDepthMap(
                colorFrames[i][0],
                depthFrames[i][0],
                renderCameras[i],
                1f,
                5f,
                1,
                0.01f,
                true
            );
        }
    }

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
                1f,
                5f,
                0.01f,
                true
            );
        }
    }
}