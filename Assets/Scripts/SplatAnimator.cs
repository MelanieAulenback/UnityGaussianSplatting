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
    }

    // Update is called once per frame
    void Update()
    {
        MoveGaussians();
        /*
        //accumulate the time
        timer += Time.deltaTime;

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
        //move all gaussians according to their specific functions and time
        for (int i = 0; i < splats[currFrame].Positions.Length; i++)
        {
            Vector3 newPos = splats[currFrame].Positions[i] + Deform(i, time);
        }

        //send new positions to gpu
        splats[currFrame].PositionsBuffer.SetData(newPos);
        
    }

    public void Deform(int gaussian, float time)
    {
        //the function for the current gaussian
        //normally would be embed parameter from pth file

    }
}
