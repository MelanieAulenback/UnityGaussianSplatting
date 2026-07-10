using UnityEngine;

public class DepthDisplaySetup : MonoBehaviour
{
    public Material depthMaterial;
    public Material colourMaterial;
    public SplatAnimator splatAnimator;

    public void SetupDepthTexture()
    {

        if (splatAnimator.depthMaps == null ||
            splatAnimator.depthMaps.Length == 0)
        {
            Debug.LogError("Depth maps not created yet!");
            return;
        }


        depthMaterial.SetTexture(
            "_DepthTex",
            splatAnimator.depthMaps[0]
        );
        colourMaterial.SetTexture(
            "_ColTex",
            splatAnimator.colorFrames[0]
        );

        colourMaterial.SetFloat("_Opacity", 0.5f);
        depthMaterial.SetFloat(
            "_MaxDepth",
            10f
        );

        /*
        float aspect = 0;

        if (gameObject.name == "Depth texture")
        {
            aspect =
                (float)splatAnimator.depthMaps[splatAnimator.currentFrame].width /
                splatAnimator.depthMaps[splatAnimator.currentFrame].height;
        }
        else if (gameObject.name == "Colour Texture")
        {
            aspect =
                (float)splatAnimator.colorFrames[splatAnimator.currentFrame].width /
                splatAnimator.colorFrames[splatAnimator.currentFrame].height;
        }
        Debug.Log("depth width: " + splatAnimator.depthMaps[splatAnimator.currentFrame].width + " depth height: " + splatAnimator.depthMaps[splatAnimator.currentFrame].height);
        Debug.Log("colour width: " + splatAnimator.colorFrames[splatAnimator.currentFrame].width + " colour height: " + splatAnimator.colorFrames[splatAnimator.currentFrame].height);

        */

        Debug.Log("Depth texture assigned!");
    }
}