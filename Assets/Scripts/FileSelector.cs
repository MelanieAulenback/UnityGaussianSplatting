using SFB;
using UnityEngine;
using System.IO;
using System.Linq;

public class FileSelector : MonoBehaviour
{
    public Canvas menu;
    public SplatAnimator animator;

    private string colorFolder;
    private string depthFolder;

    public void SelectColorFolder()
    {
        string[] paths = StandaloneFileBrowser.OpenFolderPanel(
            "Select Color Folder",
            "",
            false
        );

        if (paths.Length > 0)
            colorFolder = paths[0];
    }

    public void SelectDepthFolder()
    {
        string[] paths = StandaloneFileBrowser.OpenFolderPanel(
            "Select Depth Folder",
            "",
            false
        );

        if (paths.Length > 0)
            depthFolder = paths[0];
    }

    public void StartAnimation()
    {
        animator.colorFrames = LoadFolder(colorFolder);
        animator.depthFrames = LoadFolder(depthFolder);

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