using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class DA3CameraImporter : MonoBehaviour
{
    [Header("Cameras")]
    public Camera[] cameras;

    [Header("Intrinsics")]
    public int imageWidth = 504;
    public int imageHeight = 448;

    [Header("DA3 Data")]
    private float[,,] extrinsics;
    private float[,,] intrinsics;

    // -----------------------------
    // INIT
    // -----------------------------
    public void InitializeCameras()
    {
        string extrinsicsPath = Path.Combine(FileSelector.datasetRoot, "Extrinsics.npy");
        string intrinsicsPath = Path.Combine(FileSelector.datasetRoot, "Intrinsics.npy");

        extrinsics = NpyLoader.LoadFloat32_3D(extrinsicsPath);
        intrinsics = NpyLoader.LoadFloat32_3D(intrinsicsPath);
    }

    // -----------------------------
    // APPLY ALL CAMERAS
    // -----------------------------
    public void ApplyCameras()
    {
        int count = Mathf.Min(cameras.Length, extrinsics.GetLength(0));

        for (int i = 0; i < count; i++)
            ApplyCamera(i);
    }

    // -----------------------------
    // CORE
    // -----------------------------
    void ApplyCamera(int i)
    {
        Camera cam = cameras[i];

        // -----------------------------
        // INTRINSICS (unchanged)
        // -----------------------------
        float fx = intrinsics[i, 0, 0];
        float fy = intrinsics[i, 1, 1];
        float cx = intrinsics[i, 0, 2];
        float cy = intrinsics[i, 1, 2];

        ApplyIntrinsics(cam, fx, fy, cx, cy, imageWidth, imageHeight);
    }

    // -----------------------------
    // LOAD EXTRINSICS
    // -----------------------------
    public Matrix4x4 LoadMatrix(int i)
    {
        Matrix4x4 m = Matrix4x4.identity;

        m.m00 = extrinsics[i, 0, 0];
        m.m01 = extrinsics[i, 0, 1];
        m.m02 = extrinsics[i, 0, 2];
        m.m03 = extrinsics[i, 0, 3];

        m.m10 = extrinsics[i, 1, 0];
        m.m11 = extrinsics[i, 1, 1];
        m.m12 = extrinsics[i, 1, 2];
        m.m13 = extrinsics[i, 1, 3];

        m.m20 = extrinsics[i, 2, 0];
        m.m21 = extrinsics[i, 2, 1];
        m.m22 = extrinsics[i, 2, 2];
        m.m23 = extrinsics[i, 2, 3];

        m.m30 = 0;
        m.m31 = 0;
        m.m32 = 0;
        m.m33 = 1;

        return m;
    }

    // -----------------------------
    // INTRINSICS
    // -----------------------------
    void ApplyIntrinsics(Camera cam, float fx, float fy, float cx, float cy, int width, int height)
    {
        float near = cam.nearClipPlane;
        float far = cam.farClipPlane;

        Matrix4x4 proj = Matrix4x4.zero;

        proj[0, 0] = 2f * fx / width;
        proj[0, 2] = 1f - (2f * cx / width);

        proj[1, 1] = 2f * fy / height;
        proj[1, 2] = (2f * cy / height) - 1f;

        proj[2, 2] = -(far + near) / (far - near);
        proj[2, 3] = -(2f * far * near) / (far - near);

        proj[3, 2] = -1f;

        cam.projectionMatrix = proj;
        cam.aspect = (float)width / height;
    }
}