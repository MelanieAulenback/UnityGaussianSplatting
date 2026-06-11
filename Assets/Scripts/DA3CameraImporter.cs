using UnityEngine;

public class DA3CameraImporter : MonoBehaviour
{
    public Camera[] cameras;

    public string extrinsicsPath;
    public string intrinsicsPath;

    // DA3 image size
    public int imageWidth = 504;
    public int imageHeight = 448;

    private float[,,] extrinsics;
    private float[,,] intrinsics;

    void Start()
    {
        Debug.Log("Loading DA3 camera data...");

        extrinsics = NpyLoader.LoadFloat32_3D(extrinsicsPath);
        intrinsics = NpyLoader.LoadFloat32_3D(intrinsicsPath);

        ApplyCameras();
    }

    void ApplyCameras()
    {
        int count = Mathf.Min(
            cameras.Length,
            extrinsics.GetLength(0),
            intrinsics.GetLength(0)
        );

        for (int i = 0; i < count; i++)
            ApplyCamera(i);
    }

    void ApplyCamera(int i)
    {
        Camera cam = cameras[i];

        // ==========================
        // EXTRINSICS
        // ==========================
        Matrix4x4 w2c = Matrix4x4.identity;

        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                w2c[r, c] = extrinsics[i, r, c];
            }
        }

        Matrix4x4 c2w = w2c.inverse;

        Vector3 position = c2w.GetColumn(3);
        Vector3 forward = c2w.GetColumn(2);
        Vector3 up = c2w.GetColumn(1);

        cam.transform.position = position;
        cam.transform.rotation = Quaternion.LookRotation(forward, up);

        // ==========================
        // INTRINSICS
        // ==========================
        float fx = intrinsics[i, 0, 0];
        float fy = intrinsics[i, 1, 1];
        float cx = intrinsics[i, 0, 2];
        float cy = intrinsics[i, 1, 2];

        ApplyIntrinsics(
            cam,
            fx,
            fy,
            cx,
            cy,
            imageWidth,
            imageHeight
        );

        Debug.Log($"Camera {i} applied");
        Debug.Log($"fx={fx} fy={fy} cx={cx} cy={cy}");
    }

    void ApplyIntrinsics(
        Camera cam,
        float fx,
        float fy,
        float cx,
        float cy,
        int width,
        int height)
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

        // Optional: set inspector values close to reality
        cam.aspect = (float)width / height;

        float fovY =
            2f *
            Mathf.Atan(height / (2f * fy)) *
            Mathf.Rad2Deg;

        cam.fieldOfView = fovY;
    }
}