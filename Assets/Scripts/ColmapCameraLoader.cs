using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class ColmapCameraLoader : MonoBehaviour
{
    [Header("COLMAP")]
    public string imagesTxtPath;

    [Header("Image Names")]
    public string frontImageName = "Front/Reading Front 223.png";
    public string angle45ImageName = "45/Reading 45 223.png";

    [Header("Unity Cameras")]
    public Camera frontCamera;
    public Camera angle45Camera;

    [ContextMenu("Load Cameras")]
    public void LoadCameras()
    {
        LoadCamera(frontImageName, frontCamera);
        LoadCamera(angle45ImageName, angle45Camera);
    }

    private void LoadCamera(string imageName, Camera targetCamera)
    {
        if (!File.Exists(imagesTxtPath))
        {
            Debug.LogError($"images.txt not found: {imagesTxtPath}");
            return;
        }

        string[] lines = File.ReadAllLines(imagesTxtPath);

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("#"))
                continue;

            if (!line.Contains(imageName))
                continue;

            string[] parts = line.Split(' ');

            if (parts.Length < 10)
                continue;

            double qw = double.Parse(parts[1], CultureInfo.InvariantCulture);
            double qx = double.Parse(parts[2], CultureInfo.InvariantCulture);
            double qy = double.Parse(parts[3], CultureInfo.InvariantCulture);
            double qz = double.Parse(parts[4], CultureInfo.InvariantCulture);

            double tx = double.Parse(parts[5], CultureInfo.InvariantCulture);
            double ty = double.Parse(parts[6], CultureInfo.InvariantCulture);
            double tz = double.Parse(parts[7], CultureInfo.InvariantCulture);

            ApplyColmapPose(
                targetCamera.transform,
                qw, qx, qy, qz,
                tx, ty, tz
            );

            Debug.Log($"Loaded pose for {imageName}");
            return;
        }

        Debug.LogError($"Could not find image: {imageName}");
    }

    private void ApplyColmapPose(
        Transform target,
        double qw,
        double qx,
        double qy,
        double qz,
        double tx,
        double ty,
        double tz)
    {
        Quaternion colmapRotation =
            new Quaternion(
                (float)qx,
                (float)qy,
                (float)qz,
                (float)qw);

        Matrix4x4 R = Matrix4x4.Rotate(colmapRotation);

        Vector3 T = new Vector3(
            (float)tx,
            (float)ty,
            (float)tz);

        Matrix4x4 Rt = R.transpose;

        Vector3 cameraPosition =
            -(Rt.MultiplyVector(T));

        Quaternion cameraRotation =
            Quaternion.Inverse(colmapRotation);

        // COLMAP (right-handed) -> Unity (left-handed)
        cameraPosition = new Vector3(
            cameraPosition.x,
            cameraPosition.y,
            -cameraPosition.z);

        cameraRotation = new Quaternion(
            -cameraRotation.x,
            -cameraRotation.y,
            cameraRotation.z,
            cameraRotation.w);

        target.position = cameraPosition;
        target.rotation = cameraRotation;
    }
}
