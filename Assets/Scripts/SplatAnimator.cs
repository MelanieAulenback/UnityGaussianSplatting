using UnityEngine;
using UnityEngine.UI;

public class SplatAnimator : MonoBehaviour
{
    public SplatData[] splats;
    public Camera[] renderCameras;

    public Texture2D[][] colorFrames;

    // camera → single depth map (CURRENT DESIGN)
    public float[][,] depthFramesNpy;

    private int currentFrame;
    public float fps = 30f;
    private float timer;

    public int numCameras = 2;
    public Slider loadingBar;
    public DepthInspector depthInspector;

    private void Update()
    {
        if (colorFrames == null || depthFramesNpy == null)
            return;

        timer += Time.deltaTime;

        if (timer >= 1f / fps)
        {
            timer = 0f;
            NextFrame();
        }

        loadingBar.value = (float)currentFrame / colorFrames[0].Length;
    }

    public void StartPlayback()
    {
        depthInspector.depthFramesNpy = depthFramesNpy;

        for (int i = 0; i < numCameras; i++)
        {
            float[,] depth = depthFramesNpy[i];

            splats[i].SetDepthFrame(depth);

            splats[i].GenerateFromDepthMap(
                colorFrames[i][0],
                renderCameras[i],
                1f,
                5f,
                1,
                0.01f,
                false
            );
        }

        depthInspector.Inspect(0, 0);
    }

    public void NextFrame()
    {
        currentFrame = (currentFrame + 1) % colorFrames[0].Length;

        for (int i = 0; i < numCameras; i++)
        {
            float[,] depth = depthFramesNpy[i];

            splats[i].SetDepthFrame(depth);

            splats[i].UpdateFromDepthMap(
                colorFrames[i][currentFrame],
                renderCameras[i],
                1f,
                5f,
                0.01f,
                false
            );
        }
    }
}