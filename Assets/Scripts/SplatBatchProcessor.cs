using System.Collections;
using System.IO;
using UnityEngine;
/*
public class SplatBatchProcessor : MonoBehaviour
{
    public SplatData splatData;
    public Camera renderCamera;

    public string colorFolder;
    public string depthFolder;

    public SplatAnimator animator;

    private Texture2D[] colorFrames;
    private Texture2D[] depthFrames;

    private int frameCount;

    private void Start()
    {
        SplatBatchProcessor.splatsProcessed = false;
    }

    public static bool splatsProcessed;

    public void StartBatch()
    {
        LoadFrames();
        StartCoroutine(ProcessSequence());
    }

    void LoadFrames()
    {
        string[] colorFiles = Directory.GetFiles(colorFolder, "*.png");
        string[] depthFiles = Directory.GetFiles(depthFolder, "*.png");

        System.Array.Sort(colorFiles);
        System.Array.Sort(depthFiles);

        frameCount = Mathf.Min(colorFiles.Length, depthFiles.Length);

        colorFrames = new Texture2D[frameCount];
        depthFrames = new Texture2D[frameCount];

        for (int i = 0; i < frameCount; i++)
        {
            colorFrames[i] = LoadTexture(colorFiles[i]);
            depthFrames[i] = LoadTexture(depthFiles[i]);
        }
    }

    IEnumerator ProcessSequence()
    {
        splatsProcessed = false;

        // IMPORTANT: initialize once
        animator.InitializePlayback(splatData);

        for (int i = 0; i < frameCount; i++)
        {
            splatData.UpdateFromDepthMap(
                colorFrames[i],
                depthFrames[i],
                renderCamera,
                1f,
                5f,
                1,
                0.01f,
                true
            );

            // GPU update ONLY (no rebuild)
            splatData.PushBuffersToGPU();

            yield return null;
        }

        splatsProcessed = true;
    }

    Texture2D LoadTexture(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);
        return tex;
    }
}*/