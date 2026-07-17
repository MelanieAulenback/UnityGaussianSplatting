using UnityEngine;

public class DepthDisplaySetup : MonoBehaviour
{
    public Material depthMaterial;
    public Material colourMaterial;
    public SplatAnimator splatAnimator;

    //applies depth map and coloured image on the two planes
    //for comparing
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

    }
}