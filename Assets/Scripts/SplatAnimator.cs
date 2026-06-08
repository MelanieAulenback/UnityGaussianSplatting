using UnityEngine;
using UnityEngine.UI;

public class SplatAnimator : MonoBehaviour
{
    public SplatData splat;
    public Camera[] renderCameras;

    [HideInInspector] public Texture2D[][] colorFrames;
    [HideInInspector] public Texture2D[][] depthFrames;

    private int currentFrame = 0;

    public float fps = 30f;
    private float timer;

    public int numCameras = 2;

    public Slider loadingBar;

    private bool initialized = false;

    public void StartPlayback()
    {
        Debug.Log("StartPlayback called");

        if (colorFrames == null || depthFrames == null)
            return;

        splat.InitializeBuffers();

        for (int i = 0; i < numCameras; i++)
        {
            splat.GenerateFromDepthMap(
                colorFrames[i][0],
                depthFrames[i][0],
                renderCameras[i].transform.localToWorldMatrix,
                renderCameras[i],
                1f,
                5f,
                2,
                0.01f,
                true
            );
        }

        Debug.Log("FINAL COUNT = " + splat.Count);

        initialized = true;
    }

    private void Update()
    {
        if (!initialized) return;

        timer += Time.deltaTime;

        if (timer < 1f / fps)
            return;

        timer = 0f;

        NextFrame();

        if (loadingBar != null)
            loadingBar.value =
                (float)currentFrame / colorFrames[0].Length;
    }

    public void NextFrame()
    {
        int frameCount = colorFrames[0].Length;

        currentFrame++;
        if (currentFrame >= frameCount)
            currentFrame = 0;

        for (int i = 0; i < numCameras; i++)
        {
            splat.UpdateFromDepthMap(
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