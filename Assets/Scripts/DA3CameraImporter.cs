using UnityEngine;

public class DA3CameraImporter : MonoBehaviour
{
    public Camera[] cameras;

    public string extrinsicsPath;
    public string intrinsicsPath;

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
        int count = Mathf.Min(cameras.Length, extrinsics.GetLength(0));

        for (int i = 0; i < count; i++)
            ApplyCamera(i);
    }

    void ApplyCamera(int i)
    {
        Camera cam = cameras[i];

        Matrix4x4 w2c = Matrix4x4.identity;

        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 4; c++)
                w2c[r, c] = extrinsics[i, r, c];

        Matrix4x4 c2w = w2c.inverse;

        Vector3 pos = c2w.GetColumn(3);
        Vector3 forward = c2w.GetColumn(2);
        Vector3 up = c2w.GetColumn(1);

        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation(forward, up);

        Debug.Log($"Camera {i} applied");
    }
}