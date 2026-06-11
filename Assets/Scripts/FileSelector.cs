using SFB;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class FileSelector : MonoBehaviour
{
    public Canvas menu;
    public SplatAnimator animator;

    private string colorFolder;
    private string depthFolder;

    private List<string> colourFolders = new List<string>();
    private List<string> depthFolders = new List<string>();

    public void SelectColorFolder()
    {
        string[] paths = StandaloneFileBrowser.OpenFolderPanel(
            "Select Color Folder",
            "",
            false
        );

        if (paths.Length > 0)
        {
            colorFolder = paths[0];
            colourFolders.Add(colorFolder);
            Debug.Log($"Added color folder {colourFolders.Count - 1}: {colorFolder}");
        }
    }

    public void SelectDepthFolder()
    {
        string[] paths = StandaloneFileBrowser.OpenFolderPanel(
            "Select Depth Folder",
            "",
            false
        );

        if (paths.Length > 0)
        {
            depthFolder = paths[0];
            depthFolders.Add(depthFolder);
            Debug.Log($"Added depth folder {depthFolders.Count - 1}: {depthFolder}");
        }
    }

    public void StartAnimation()
    {
        Debug.Log("StartAnimation called");
        Debug.Log($"Animator in FileSelector: {animator.name}");
        int camCount = animator.numCameras;

        if (colourFolders.Count < camCount)
        {
            Debug.LogError($"Not enough folders selected. Needed {camCount}, got {colourFolders.Count} color");
            return;
        }

        /*
        if (colourFolders.Count < camCount || depthFolders.Count < camCount)
        {
            Debug.LogError($"Not enough folders selected. Needed {camCount}, got {colourFolders.Count} color and {depthFolders.Count} depth.");
            return;
        }
        */
        animator.colorFrames = new Texture2D[animator.numCameras][];
        Debug.Log("Allocated colorFrames");

        //animator.depthFrames = new Texture2D[animator.numCameras][];
        //Debug.Log("Allocated depthFrames");

        for (int i = 0; i < camCount; i++)
        {
            animator.colorFrames[i] = LoadFolder(colourFolders[i]);
            //animator.depthFrames[i] = LoadFolder(depthFolders[i]);
        }

        animator.StartPlayback();

        if (menu != null)
            menu.enabled = false;
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

            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);

            textures[i] = tex;
        }

        Debug.Log($"Loaded {textures.Length} images from {folder}");

        return textures;
    }
}