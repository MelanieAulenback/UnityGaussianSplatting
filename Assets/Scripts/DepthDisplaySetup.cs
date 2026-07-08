using UnityEngine;

public class DepthDisplaySetup : MonoBehaviour
{
    public Material depthMaterial;
    public Material colourMaterial;
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
        colourMaterial.SetTexture(
            "_ColTex",
            splatAnimator.colorFrames[1][0]
        );

        colourMaterial.SetFloat("_Opacity", 0.5f);
        depthMaterial.SetFloat(
            "_MaxDepth",
            10f
        );

        float aspect = 0;

        if (gameObject.name == "Depth texture")
        {
            aspect =
                (float)splatAnimator.depthMaps[1].width /
                splatAnimator.depthMaps[1].height;
        }
        else if (gameObject.name == "Colour Texture")
        {
            aspect =
                (float)splatAnimator.colorFrames[1][0].width /
                splatAnimator.colorFrames[1][0].height;
        }
        Debug.Log("depth width: " + splatAnimator.depthMaps[1].width + " depth height: " + splatAnimator.depthMaps[1].height);
        Debug.Log("colour width: " + splatAnimator.colorFrames[1][0].width + " colour height: " + splatAnimator.colorFrames[1][0].height);



        Debug.Log("Depth texture assigned!");
    }
}