using SFB;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class FileSelector : MonoBehaviour
{
    public Canvas menu;
    public SplatAnimator animator;

    private List<string> colourFolders = new List<string>();
    //private List<string> depthFolders = new List<string>();

    public void SelectColorFolder()
    {
        string[] paths = StandaloneFileBrowser.OpenFolderPanel(
            "Select Color Folder",
            "",
            false
        );

        if (paths.Length > 0)
        {
            colourFolders.Add(paths[0]);
            Debug.Log($"Added color folder {colourFolders.Count - 1}: {paths[0]}");
        }
    }

    /*
    public void SelectDepthFolder()
    {
        string[] paths = StandaloneFileBrowser.OpenFolderPanel(
            "Select Depth Folder",
            "",
            false
        );

        if (paths.Length > 0)
        {
            depthFolders.Add(paths[0]);
            Debug.Log($"Added depth folder {depthFolders.Count - 1}: {paths[0]}");
        }
    }
    */
    public void StartAnimation()
    {
        int camCount = animator.numCameras;

        if (colourFolders.Count < camCount)
        {
            Debug.LogError($"Not enough color folders. Need {camCount}");
            return;
        }
        /*
        if (depthFolders.Count < camCount)
        {
            Debug.LogError($"Not enough depth folders. Need {camCount}");
            return;
        }
        */
        animator.colorFrames = new Texture2D[camCount][];
        //animator.depthFrames = new Texture2D[camCount][];
        //animator.depthFrames = new List<List<float[,]>>();

        for (int i = 0; i < camCount; i++)
        {
            animator.colorFrames[i] = LoadFolder(colourFolders[i]);
            //animator.depthFrames[i] = LoadFolder(depthFolders[i]);
            //animator.depthFrames.Add(LoadDepthFolder(depthFolders[i]));
        }

        animator.StartPlayback();

        if (menu != null)
            menu.enabled = false;
    }

    // =====================================================
    // COLOR LOADING
    // =====================================================
    Texture2D[] LoadColorFolder(string folder)
    {
        string[] files = Directory.GetFiles(folder, "*.png")
            .OrderBy(f => f)
            .ToArray();

        Texture2D[] textures = new Texture2D[files.Length];

        for (int i = 0; i < files.Length; i++)
        {
            byte[] bytes = File.ReadAllBytes(files[i]);

            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);

            textures[i] = tex;
        }

        return textures;
    }

    // =====================================================
    // DEPTH LOADING (PNG OR NPY)
    // =====================================================
    List<float[,]> LoadDepthFolder(string folder)
    {
        string[] files = Directory.GetFiles(folder)
            .Where(f => f.EndsWith(".npy") || f.EndsWith(".png"))
            .OrderBy(f => f)
            .ToArray();

        List<float[,]> frames = new List<float[,]>();

        foreach (string file in files)
        {
            if (file.EndsWith(".npy"))
            {
                float[,] depth = NpyLoader.LoadFloat32_2D(file);
                frames.Add(depth);

                Debug.Log($"Loaded NPY depth: {Path.GetFileName(file)}");
            }
            else if (file.EndsWith(".png"))
            {
                float[,] depth = LoadDepthPNG(file);
                frames.Add(depth);

                Debug.Log($"Loaded PNG depth: {Path.GetFileName(file)}");
            }
        }

        return frames;
    }

    // =====================================================
    // PNG DEPTH → float[,]
    // Treats RED channel as distance along ray (Z depth)
    // =====================================================
    float[,] LoadDepthPNG(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);

        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(bytes);

        int width = tex.width;
        int height = tex.height;

        float[,] depth = new float[width, height];

        Color[] pixels = tex.GetPixels(); // faster + correct grayscale handling

        for (int y = 0; y < height; y++)
        {
            int row = y * width;

            for (int x = 0; x < width; x++)
            {
                Color c = pixels[row + x];

                // grayscale-safe extraction (works whether PNG is RGB or true grayscale)
                float d = c.r;

                depth[x, y] = d;
            }
        }

        return depth;
    }

    Texture2D[] LoadFolder(string folder)
    {
        string[] files = Directory.GetFiles(folder, "*.png")
            .OrderBy(f => f)
            .ToArray();

        Texture2D[] textures = new Texture2D[files.Length];

        for (int i = 0; i < files.Length; i++)
        {
            byte[] bytes = File.ReadAllBytes(files[i]);

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(bytes);

            Debug.Log(
                $"Loaded: {Path.GetFileName(files[i])} | " +
                $"{tex.width}x{tex.height}"
            );

            textures[i] = tex;
        }

        Debug.Log($"Loaded {textures.Length} PNGs from {folder}");

        return textures;
    }
}