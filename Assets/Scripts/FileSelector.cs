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

    //sets up the database, loads folders, counts frames and cameras
    public void SelectDataset()
    {
        //select dataset folder
        string[] paths = StandaloneFileBrowser.OpenFolderPanel(
            "Select Dataset Folder",
            "",
            false
        );

        if (paths.Length == 0)
            return;

        datasetRoot = paths[0];

        //get the frame folders
        frameFolders = Directory.GetDirectories(datasetRoot)
        .OrderBy(f => f)
        .ToArray();

        if (frameFolders.Length == 0)
        {
            Debug.LogError("No frame folders found!");
            return;
        }

        //get the camera folders
        string camerasPath = Path.Combine(frameFolders[0], "Cameras");

        string[] camFolders = Directory.GetDirectories(camerasPath)
            .OrderBy(f => f)
            .ToArray();

        animator.numCameras = camFolders.Length;
        animator.frameCount = frameFolders.Length;

        //create array of colour image textures
        animator.colorFrames = new Texture2D[camFolders.Length];

        for (int i = 0; i < camFolders.Length; i++)
        {
            animator.colorFrames[i] =
                new Texture2D(
                    1080,
                    958,
                    TextureFormat.RGBA32,
                    false);
        }

        //create array of depth map textures
        animator.depthMaps = new RenderTexture[camFolders.Length];

        //ONLY LOAD DATA (colour images) — DO NOT START PLAYBACK
        for (int i = 0; i < camFolders.Length; i++)
        {
            string colourPath = Path.Combine(camFolders[i], "Colour");
            //string depthPath = Path.Combine(camFolders[i], "Depth");

            if (!Directory.Exists(colourPath))
            {
                Debug.LogError($"Missing Colour folder: {colourPath}");
                continue;
            }

            LoadSingleImage(colourPath, i, animator.colorFrames[i]);
        }

        //enable animation to start
        UIManager.startAnimation = true;
        Debug.Log("Dataset loaded. Ready to start animation.");
    }

    //starts the animation when the start button is pressed
    public void StartAnimation()
    {
        if (animator.colorFrames == null || animator.colorFrames.Length == 0)
        {
            Debug.LogError("No dataset loaded!");
            return;
        }

        animator.frameCount = frameFolders.Length;
        animator.StartPlayback();

        //disable menu
        if (menu != null)
            menu.enabled = false;
    }

    //accesses the current colour image from folder directory
    public static void LoadSingleImage(string folder, int cam, Texture2D tex)
    {
        string file = Path.Combine(
                folder,
                $"{cam:000000}.rgba"
            );

        if (file == null)
            return;

        byte[] bytes = File.ReadAllBytes(file);

        tex.LoadRawTextureData(bytes);

        //pass texture changes from cpu to gpu and keep readable copy on cpu
        tex.Apply(false);

    }
}