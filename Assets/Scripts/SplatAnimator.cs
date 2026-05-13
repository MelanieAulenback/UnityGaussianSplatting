using UnityEngine;
using UnityEngine.VFX;

public class SplatAnimator : MonoBehaviour
{
    //array of splats
    public SplatData[] splats;
    //splat object
    public float animationLength;
    private int currFrame;
    private float frameDur;
    private float timer;
    private SplatGizmoDrawer gizmoDrawer;
    private SplatDataBinder binder;
    private VisualEffect vfx;

    //base transforms
    public Vector3[] BasePositions;
    public Quaternion[] BaseRotations;
    public Vector3[] BaseScales;
    public Vector3[] BaseAxes;

    //gaussian transforms
    Vector3 newPos;
    Quaternion newRot;
    Vector3 newScale;

    //embeddings for each gaussian
    Vector4[] Embedding;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //evenly divide frames/num of splats by animation length
        frameDur = animationLength / splats.Length;

        //set the starting frame/splat
        currFrame = 0;

        //set timer to 0
        timer = 0;

        //get components
        gizmoDrawer = GetComponent<SplatGizmoDrawer>();
        binder = GetComponent<SplatDataBinder>();
        vfx = GetComponent<VisualEffect>();

        //assign the first splat
        ChangeFrame(splats[currFrame]);

        //assign the embeddings array
        Embedding = new Vector4[splats[0].Count];

        //assign the base position of the gaussian
        BasePositions = (Vector3[])splats[0].Positions.Clone();

        //allocate scale and rotation arrays
        BaseRotations = new Quaternion[splats[0].Count];
        BaseScales = new Vector3[splats[0].Count];
        BaseAxes = new Vector3[splats[0].Count * 3];

        //extract rotation + scale from axes
        for (int i = 0; i < splats[0].Count; i++)
        {
            //get axis
            Vector3 axisX = splats[0].Axes[i * 3 + 0];
            Vector3 axisY = splats[0].Axes[i * 3 + 1];
            Vector3 axisZ = splats[0].Axes[i * 3 + 2];

            BaseAxes[i * 3 + 0] = axisX;
            BaseAxes[i * 3 + 1] = axisY;
            BaseAxes[i * 3 + 2] = axisZ;

            //extract scale
            float scaleX = axisX.magnitude;
            float scaleY = axisY.magnitude;
            float scaleZ = axisZ.magnitude;

            BaseScales[i] = new Vector3(
                scaleX,
                scaleY,
                scaleZ
            );

            //extract rotation
            Vector3 right = axisX.normalized;
            Vector3 up = axisY.normalized;
            Vector3 forward = axisZ.normalized;

            BaseRotations[i] = Quaternion.LookRotation(
                forward,
                up
            );

            //set embedding
            Embedding[i] = new Vector4(
                Random.value,
                Random.value,
                Random.value,
                Random.value
            );
        }
    }

    // Update is called once per frame
    void Update()
    {   
        //accumulate the time
        timer += Time.deltaTime;

        //move the gaussians
        MoveGaussians(timer);

        /*
        //change the splat after the frame duration
        while (timer >= frameDur)
        {
            timer -= frameDur;

            //if its not the last frame
            if (currFrame != splats.Length - 1)
            {
                currFrame += 1;
            }

            ChangeFrame(splats[currFrame]);
        }
        */
    }

    //changes the splat parameters to the current frame splat data
    public void ChangeFrame(SplatData splat)
    {
        //set the splat data
        gizmoDrawer.SplatData = splat;

        //set the vfx bindings
        binder.Data = splat;

        //re-initiate the vfx graph to reload visuals
        vfx.Reinit();
    }

    //moves gaussians over time
    //position = base position + deform(gaussian function, time)
    public void MoveGaussians(float time)
    {
        SplatData data = splats[0];

        //move all gaussians according to the shared functions and time
        for (int i = 0; i < data.Positions.Length; i++)
        {
            Vector3 s = DeformScale(i, time);

            Vector3 axisX = BaseAxes[i * 3 + 0];
            Vector3 axisY = BaseAxes[i * 3 + 1];
            Vector3 axisZ = BaseAxes[i * 3 + 2];

            data.Axes[i * 3 + 0] = axisX * s.x;
            data.Axes[i * 3 + 1] = axisY * s.y;
            data.Axes[i * 3 + 2] = axisZ * s.z;

            //get the current gaussians density
            float density = DeformDensity(i, time);

            Color c = data.Colors[i];
            c.a = density;
            data.Colors[i] = c;
        }

        //send new positions to gpu
        data.PositionsBuffer.SetData(data.Positions);
        data.AxesBuffer.SetData(data.Axes);
        data.ColorsBuffer.SetData(data.Colors);
        
    }

    //the function that scales the gaussians
    public Vector3 DeformScale(int i, float t)
    {
        Vector4 e = Embedding[i];
        Vector3 p = BasePositions[i];

        // per-axis frequencies (like learned deformation field)
        float fx = 1f + e.x * 2f;
        float fy = 1f + e.y * 2f;
        float fz = 1f + e.z * 2f;

        // smooth temporal oscillation
        float sx = 1f + 0.1f * Mathf.Sin(t * fx + p.x);
        float sy = 1f + 0.1f * Mathf.Sin(t * fy + p.y);
        float sz = 1f + 0.1f * Mathf.Sin(t * fz + p.z);

        return new Vector3(sx, sy, sz);
    }
    
    public float DeformDensity(int i, float t)
    {
       Vector3 p = BasePositions[i];
        Vector4 e = Embedding[i];

        // spatial hash-like variation (replaces weak dot product)
        float spatial =
            Mathf.Sin(p.x * 1.7f + e.x * 10f) *
            Mathf.Sin(p.y * 1.3f + e.y * 10f) *
            Mathf.Sin(p.z * 1.1f + e.z * 10f);

        // temporal activation (multi-frequency like learned Fourier features)
        float temporal =
            Mathf.Sin(t * (1f + e.x * 3f)) +
            0.5f * Mathf.Sin(t * (2f + e.y * 5f)) +
            0.25f * Mathf.Sin(t * (4f + e.z * 7f));

        // combine like a small MLP would
        float d = spatial + temporal + e.w;

        // squash into 0–1
        return Mathf.SmoothStep(0f, 1f, d * 0.5f + 0.5f);
    }
}
