using UnityEngine;

public class DA3CameraImporter : MonoBehaviour
{
    public Camera[] cameras;
    public GameObject[] glbCameraMeshes;

    public string extrinsicsPath;
    public string intrinsicsPath;

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
            glbCameraMeshes.Length,
            extrinsics.GetLength(0),
            intrinsics.GetLength(0)
        );

        for (int i = 0; i < count; i++)
            ApplyCamera(i);
    }

    void ApplyCamera(int i)
    {
        Camera cam = cameras[i];

        // =====================================================
        // GLB CAMERA MESH → POSE EXTRACTION
        // =====================================================

        GameObject glbCam = glbCameraMeshes[i];
        Mesh mesh = glbCam.GetComponent<MeshFilter>().sharedMesh;
        Transform t = glbCam.transform;

        Vector3[] vertices = mesh.vertices;

        Vector3[] world = new Vector3[vertices.Length];
        for (int j = 0; j < vertices.Length; j++)
            world[j] = t.TransformPoint(vertices[j]);

        // -----------------------------
        // Camera position (centroid fallback)
        // -----------------------------
        Vector3 center = Vector3.zero;
        for (int j = 0; j < world.Length; j++)
            center += world[j];
        center /= world.Length;

        // -----------------------------
        // Stable forward direction (geometry-based)
        // -----------------------------
        int farthest = 0;
        float maxDist = 0f;

        for (int j = 0; j < world.Length; j++)
        {
            float d = (world[j] - center).sqrMagnitude;
            if (d > maxDist)
            {
                maxDist = d;
                farthest = j;
            }
        }

        Vector3 forward = (world[farthest] - center).normalized;
        forward = -forward;

        // -----------------------------
        // Build stable orthonormal basis
        // -----------------------------
        Vector3 tempUp = Vector3.up;

        if (Mathf.Abs(Vector3.Dot(tempUp, forward)) > 0.9f)
            tempUp = Vector3.right;

        Vector3 right = Vector3.Cross(tempUp, forward).normalized;
        Vector3 up = Vector3.Cross(forward, right).normalized;

        // -----------------------------
        // Apply transform
        // -----------------------------
        cam.transform.position = center;
        cam.transform.rotation = Quaternion.LookRotation(forward, up);

        // =====================================================
        // INTRINSICS
        // =====================================================

        float fx = intrinsics[i, 0, 0];
        float fy = intrinsics[i, 1, 1];
        float cx = intrinsics[i, 0, 2];
        float cy = intrinsics[i, 1, 2];

        ApplyIntrinsics(cam, fx, fy, cx, cy, imageWidth, imageHeight);

        Debug.Log($"Camera {i} applied");
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

        cam.aspect = (float)width / height;

        cam.fieldOfView =
            2f *
            Mathf.Atan(height / (2f * fy)) *
            Mathf.Rad2Deg;
    }
}