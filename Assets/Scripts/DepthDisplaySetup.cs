using UnityEngine;

public class DepthDisplaySetup : MonoBehaviour
{
    public Material depthMaterial;
    public SplatAnimator splatAnimator;

    public void SetupDepthTexture()
    {
        Debug.Log(splatAnimator.depthMaps[1].GetInstanceID());

        if (splatAnimator.depthMaps == null ||
            splatAnimator.depthMaps.Length == 0)
        {
            Debug.LogError("Depth maps not created yet!");
            return;
        }


        depthMaterial.SetTexture(
            "_DepthTex",
            splatAnimator.depthMaps[1]
        );


        depthMaterial.SetFloat(
            "_MaxDepth",
            10f
        );


        float aspect =
            (float)splatAnimator.depthMaps[1].width /
            splatAnimator.depthMaps[1].height;


        transform.localScale =
            new Vector3(
                aspect,
                1,
                1
            );


        Debug.Log("Depth texture assigned!");
    }
}