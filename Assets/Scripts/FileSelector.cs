using SFB;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class FileSelector : MonoBehaviour
{
    public Canvas menu;
    public SplatAnimator animator;

    private List<string> colourFolders = new();
    private List<string> depthFolders = new();

    public void SelectColorFolder()
    {
        var paths = StandaloneFileBrowser.OpenFolderPanel("Select Color Folder", "", false);
        if (paths.Length > 0) colourFolders.Add(paths[0]);
    }

    public void SelectDepthFolder()
    {
        var paths = StandaloneFileBrowser.OpenFolderPanel("Select Depth Folder", "", false);
        if (paths.Length > 0) depthFolders.Add(paths[0]);
    }

    public void StartAnimation()
    {
        int camCount = animator.numCameras;

        animator.colorFrames = new Texture2D[camCount][];
        animator.depthFramesNpy = new float[camCount][,];

        for (int i = 0; i < camCount; i++)
        {
            animator.colorFrames[i] = LoadColorFolder(colourFolders[i]);
            animator.depthFramesNpy[i] = LoadDepthFolder(depthFolders[i]);
        }

        animator.StartPlayback();

        if (menu != null)
            menu.enabled = false;
    }

    Texture2D[] LoadColorFolder(string folder)
    {
        return Directory.GetFiles(folder, "*.png")
            .OrderBy(f => f)
            .Select(f =>
            {
                var bytes = File.ReadAllBytes(f);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                return tex;
            })
            .ToArray();
    }

    // IMPORTANT: one depth frame per camera (THIS is your current design)
    float[,] LoadDepthFolder(string folder)
    {
        var files = Directory.GetFiles(folder, "*.npy")
            .OrderBy(f => f)
            .ToArray();

        int w, h;
        return NpyFloatLoader.Load2D(files[0], out w, out h);
    }
}