using UnityEngine;

public class DA3CameraImporter : MonoBehaviour
{
    public Camera[] cameras;

    public string extrinsicsPath;
    public string intrinsicsPath;

    public int imageWidth = 504;
    public int imageHeight = 448;

    private float[,,] extrinsics;
    private float[,,] intrinsics;

    void Start()
    {
        Debug.Log("Loading DA3 camera data...");

        extrinsics = NpyLoader.LoadFloat32(extrinsicsPath);
        intrinsics = NpyLoader.LoadFloat32(intrinsicsPath);

        Debug.Log($"Extrinsics shape: {extrinsics.GetLength(0)} x {extrinsics.GetLength(1)} x {extrinsics.GetLength(2)}");
        Debug.Log($"Intrinsics shape: {intrinsics.GetLength(0)} x {intrinsics.GetLength(1)} x {intrinsics.GetLength(2)}");

        ApplyCameras();
    }

    void ApplyCameras()
    {
        int count = Mathf.Min(cameras.Length, extrinsics.GetLength(0));

        for (int i = 0; i < count; i++)
            ApplyCamera(i);
    }

    void ApplyCamera(int i)
    {
        Camera cam = cameras[i];

        // --------------------------
        // EXTRINSICS (assume 3x4)
        // --------------------------
        Matrix4x4 w2c = Matrix4x4.identity;

        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 4; c++)
                w2c[r, c] = extrinsics[i, r, c];

        Matrix4x4 c2w = w2c.inverse;

        cam.transform.position = c2w.GetColumn(3);
        cam.transform.rotation = Quaternion.LookRotation(
            c2w.GetColumn(2),
            c2w.GetColumn(1)
        );

        // --------------------------
        // INTRINSICS (safe indexing)
        // --------------------------
        float fx = intrinsics[i, 0, 0];
        float fy = intrinsics[i, 1, 1];
        float cx = intrinsics[i, 0, 2];
        float cy = intrinsics[i, 1, 2];

        ApplyIntrinsics(cam, fx, fy, cx, cy);

        Debug.Log($"Camera {i} applied");
    }

    void ApplyIntrinsics(Camera cam, float fx, float fy, float cx, float cy)
    {
        float near = cam.nearClipPlane;
        float far = cam.farClipPlane;

        Matrix4x4 proj = Matrix4x4.zero;

        proj[0, 0] = 2f * fx / imageWidth;
        proj[0, 2] = 1f - (2f * cx / imageWidth);

        proj[1, 1] = 2f * fy / imageHeight;
        proj[1, 2] = (2f * cy / imageHeight) - 1f;

        proj[2, 2] = -(far + near) / (far - near);
        proj[2, 3] = -(2f * far * near) / (far - near);

        proj[3, 2] = -1f;

        cam.projectionMatrix = proj;
        cam.aspect = (float)imageWidth / imageHeight;
    }
}