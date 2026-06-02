using SFB;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class GaussianTrainer : MonoBehaviour
{
    public Camera renderCamera;

    private RenderTexture renderTexture;

    public ComputeShader diffShader;
    public ComputeShader gaussianUpdateShader;
    public ComputeShader contributionShader;
    public ComputeShader normalizeShader;
    public ComputeShader clearShader;

    public Texture2D referenceImage;

    public RawImage debugDiffImage;
    public RawImage debugRefImage;
    public RawImage debugCamImage;

    public SplatData splatData;

    private RenderTexture diffTexture;
    private RenderTexture accumColor;
    private RenderTexture accumWeight;
    private RenderTexture renderedTexture;

    private int kernelDiff;
    private int kernelUpdate;
    private int kernelContribution;
    private int kernelNormalize;
    private int kernelClear;

    private Matrix4x4 vp;
    private Matrix4x4 localToWorld;

    int w;
    int h;

    void Start()
    {
        //assign base dimensions to image size
        w = referenceImage.width;
        h = referenceImage.height;

        //create texture for image
        renderTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGBHalf);
        renderTexture.enableRandomWrite = false;
        renderTexture.Create();

        //make sure image and texture size match
        if (renderTexture.width != referenceImage.width ||
    renderTexture.height != referenceImage.height)
        {
            Debug.LogWarning("RenderTexture and ReferenceImage size mismatch!");
        }

        // -------------------------------------------------
        // ENSURE CAMERA TARGET IS VALID FOR RENDER GRAPH
        // -------------------------------------------------
        renderedTexture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBHalf);
        renderedTexture.enableRandomWrite = true;
        renderedTexture.Create();

        accumColor = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBFloat);
        accumColor.enableRandomWrite = true;
        accumColor.Create();

        accumWeight = new RenderTexture(w, h, 0, RenderTextureFormat.RFloat);
        accumWeight.enableRandomWrite = true;
        accumWeight.Create();

        diffTexture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGBFloat);
        diffTexture.enableRandomWrite = true;
        diffTexture.Create();

        debugDiffImage.texture = diffTexture;
        debugRefImage.texture = referenceImage;
        debugCamImage.texture = renderTexture;

        kernelDiff = diffShader.FindKernel("CSMain");
        kernelUpdate = gaussianUpdateShader.FindKernel("CSMain");
        kernelContribution = contributionShader.FindKernel("CSMain");
        kernelNormalize = normalizeShader.FindKernel("CSMain");
        kernelClear = clearShader.FindKernel("CSMain");

        gaussianUpdateShader.SetFloat("_ColorSpeed", 0.05f);

        splatData.InitializeBuffers();
    }

    void Update()
    {
        if (referenceImage == null) return;

        //convert world space to screen space
        localToWorld = transform.localToWorldMatrix;

        gaussianUpdateShader.SetMatrix("_LocalToWorld", localToWorld);
        contributionShader.SetMatrix("_LocalToWorld", localToWorld);

        vp =
            GL.GetGPUProjectionMatrix(renderCamera.projectionMatrix, false) *
            renderCamera.worldToCameraMatrix;

        Matrix4x4 invVP = vp.inverse;

        //tells gpu how to project splats in camera view
        gaussianUpdateShader.SetMatrix("_VP", vp);
        gaussianUpdateShader.SetMatrix("_InverseVP", invVP);
        contributionShader.SetMatrix("_VP", vp);

        // ORDER IS CRITICAL
        RunContributionPass();
        NormalizePass();
        //RunDiffPass();
        RunUpdatePass();

        splatData.SwapBuffers();
    }

    // -------------------------------------------------
    // DIFF (REFERENCE VS RENDERED IMAGE)
    // -------------------------------------------------
    void RunDiffPass()
    {
        diffShader.SetTexture(kernelDiff, "_Reference", referenceImage);
        diffShader.SetTexture(kernelDiff, "_Rendered", renderedTexture);
        diffShader.SetTexture(kernelDiff, "_Output", diffTexture);

        diffShader.Dispatch(
            kernelDiff,
            w / 8,
            h / 8,
            1
        );
    }

    // -------------------------------------------------
    // UPDATE GAUSSIANS (LEARNING STEP)
    // -------------------------------------------------
    void RunUpdatePass()
    {
        //update positions, colours, axes and weights
        gaussianUpdateShader.SetTexture(kernelUpdate, "_Reference", referenceImage);
        gaussianUpdateShader.SetTexture(kernelUpdate, "_Rendered", renderedTexture);
        gaussianUpdateShader.SetTexture(kernelUpdate, "_Diff", diffTexture);
        gaussianUpdateShader.SetTexture(kernelUpdate, "_TotalWeight", accumWeight);

        gaussianUpdateShader.SetBuffer(kernelUpdate, "_Positions", splatData.PositionsBuffer);
        gaussianUpdateShader.SetBuffer(kernelUpdate, "_PositionsOut", splatData.PositionsBufferOut);

        gaussianUpdateShader.SetBuffer(kernelUpdate, "_Colors", splatData.ColorsBuffer);
        gaussianUpdateShader.SetBuffer(kernelUpdate, "_ColorsOut", splatData.ColorsBufferOut);

        gaussianUpdateShader.SetBuffer(kernelUpdate, "_GaussianWeight", splatData.GaussianWeightBuffer);

        gaussianUpdateShader.SetBuffer(kernelUpdate, "_Axes", splatData.AxesBuffer);
        gaussianUpdateShader.SetBuffer(kernelUpdate, "_AxesOut", splatData.AxesBufferOut);

        gaussianUpdateShader.SetInt("_TextureSize", w);
        gaussianUpdateShader.SetInt("_TextureWidth", w);
        gaussianUpdateShader.SetInt("_TextureHeight", h);

        gaussianUpdateShader.SetMatrix("_VP", vp);
        gaussianUpdateShader.SetMatrix("_LocalToWorld", localToWorld);

        gaussianUpdateShader.SetTexture(kernelUpdate, "_AccumColor", accumColor);
        gaussianUpdateShader.SetTexture(kernelUpdate, "_AccumWeight", accumWeight);

        gaussianUpdateShader.Dispatch(
            kernelUpdate,
            Mathf.CeilToInt(splatData.Count / 64f),
            1,
            1
        );
    }

    // -------------------------------------------------
    // SPLAT CONTRIBUTION PASS
    // -------------------------------------------------
    void RunContributionPass()
    {
        //clear buffers
        clearShader.SetTexture(kernelClear, "_AccumColor", accumColor);
        clearShader.SetTexture(kernelClear, "_AccumWeight", accumWeight);

        clearShader.Dispatch(
            kernelClear,
            w / 8,
            h / 8,
            1
        );

        //project gaussians to screen space
        contributionShader.SetInt("_TextureSize", w);
        contributionShader.SetInt("_TextureWidth", w);
        contributionShader.SetInt("_TextureHeight", h);
        contributionShader.SetFloat("_InfluenceRadius", 1.5f);

        contributionShader.SetMatrix("_VP", vp);
        contributionShader.SetMatrix("_LocalToWorld", localToWorld);

        contributionShader.SetBuffer(kernelContribution, "_Positions", splatData.PositionsBuffer);
        contributionShader.SetBuffer(kernelContribution, "_Colors", splatData.ColorsBuffer);
        contributionShader.SetBuffer(kernelContribution, "_GaussianWeight", splatData.GaussianWeightBuffer);
        
        //adds colour per pixel
        contributionShader.SetTexture(kernelContribution, "_AccumColor", accumColor);
        contributionShader.SetTexture(kernelContribution, "_AccumWeight", accumWeight);

        contributionShader.Dispatch(
            kernelContribution,
            Mathf.CeilToInt(splatData.Count / 64f),
            1,
            1
        );
    }

    // -------------------------------------------------
    // NORMALIZE FINAL IMAGE
    // -------------------------------------------------
    void NormalizePass()
    {
        normalizeShader.SetTexture(kernelNormalize, "_AccumColor", accumColor);
        normalizeShader.SetTexture(kernelNormalize, "_AccumWeight", accumWeight);
        normalizeShader.SetTexture(kernelNormalize, "_Rendered", renderedTexture);

        normalizeShader.Dispatch(
            kernelNormalize,
            w / 8,
            h / 8,
            1
        );
    }
}