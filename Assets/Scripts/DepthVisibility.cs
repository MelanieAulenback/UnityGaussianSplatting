using UnityEngine;

public class DepthVisibility : MonoBehaviour
{
    public Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    public bool IsVisible(Vector3 worldPos)
    {
        Vector3 view = cam.WorldToViewportPoint(worldPos);

        if (view.z <= 0f)
            return false;

        if (view.x < 0f || view.x > 1f ||
            view.y < 0f || view.y > 1f)
            return false;

        // stronger geometric rejection (important)
        Vector3 dir = (worldPos - cam.transform.position).normalized;
        float facing = Vector3.Dot(cam.transform.forward, dir);

        if (facing < 0.25f)
            return false;

        return true;
    }

    float SampleSceneDepth(Vector2 uv)
    {
        // Built-in Unity depth texture
        // NOTE: requires DepthTextureMode.Depth enabled

        RenderTexture rt = Shader.GetGlobalTexture("_CameraDepthTexture") as RenderTexture;
        if (rt == null)
            return 1f; // fail-safe

        Texture2D tex = TextureFromRT(rt);

        return tex.GetPixelBilinear(uv.x, uv.y).r;
    }

    Texture2D TextureFromRT(RenderTexture rt)
    {
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RFloat, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        return tex;
    }
}