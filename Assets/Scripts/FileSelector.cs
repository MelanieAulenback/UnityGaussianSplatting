using SFB;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class FileSelector : MonoBehaviour
{
    public Canvas menu;
    public SplatAnimator animator;

    public static string datasetRoot;
    public static string[] frameFolders;

    public void SelectDataset()
    {
        string[] paths = StandaloneFileBrowser.OpenFolderPanel(
            "Select Dataset Folder",
            "",
            false
        );

        if (paths.Length == 0)
            return;

        datasetRoot = paths[0];


        frameFolders = Directory.GetDirectories(datasetRoot)
        .OrderBy(f => f)
        .ToArray();

        if (frameFolders.Length == 0)
        {
            Debug.LogError("No frame folders found!");
            return;
        }


        string camerasPath = Path.Combine(frameFolders[0], "Cameras");

        string[] camFolders = Directory.GetDirectories(camerasPath)
            .OrderBy(f => f)
            .ToArray();

        animator.numCameras = camFolders.Length;
        animator.frameCount = frameFolders.Length;

        animator.colorFrames = new Texture2D[camFolders.Length];
        animator.depthFrames = new Texture2D[camFolders.Length]; 
        animator.depthMaps = new RenderTexture[camFolders.Length];

        // ONLY LOAD DATA — DO NOT START PLAYBACK
        for (int i = 0; i < camFolders.Length; i++)
        {
            string colourPath = Path.Combine(camFolders[i], "Colour");
            string depthPath = Path.Combine(camFolders[i], "Depth");

            if (!Directory.Exists(colourPath))
            {
                Debug.LogError($"Missing Colour folder: {colourPath}");
                continue;
            }

            animator.colorFrames[i] = LoadSingleImage(colourPath);
            animator.depthFrames[i] = LoadSingleImage(depthPath);
        }

        Debug.Log("Dataset loaded. Ready to start animation.");
    }

    // 🔥 NEW: call this from your UI button
    public void StartAnimation()
    {
        if (animator.colorFrames == null || animator.colorFrames.Length == 0)
        {
            Debug.LogError("No dataset loaded!");
            return;
        }

        animator.frameCount = frameFolders.Length;
        animator.StartPlayback();

        if (menu != null)
            menu.enabled = false;
    }

    Texture2D[] LoadFolder(string folder)
    {
        string[] files = Directory.GetFiles(folder)
            .Where(f =>
                f.EndsWith(".png") ||
                f.EndsWith(".jpg") ||
                f.EndsWith(".jpeg"))
            .OrderBy(f => f)
            .ToArray();

        Texture2D[] textures = new Texture2D[files.Length];

        for (int i = 0; i < files.Length; i++)
        {
            byte[] bytes = File.ReadAllBytes(files[i]);

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(bytes);

            textures[i] = tex;
        }

        return textures;
    }

    public static Texture2D LoadSingleImage(string folder)
    {
        string file = Directory.GetFiles(folder)
            .FirstOrDefault(f =>
                f.EndsWith(".png") ||
                f.EndsWith(".jpg") ||
                f.EndsWith(".jpeg"));

        if (file == null)
            return null;

        byte[] bytes = File.ReadAllBytes(file);

        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(bytes);

        return tex;
    }
}