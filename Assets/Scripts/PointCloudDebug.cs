using UnityEngine;

public class PointCloudDebug : MonoBehaviour
{
    //creates coloured spheres at vertex locations, used for visualizing point clouds
    void OnDrawGizmos()
    {
        //get the vertices
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return;

        Vector3[] verts = mf.sharedMesh.vertices;

        //set teh colour to green
        Gizmos.color = Color.green;

        int step = Mathf.Max(1, verts.Length / 5000);

        //draw a green sphere at every vertex location
        for (int i = 0; i < verts.Length; i += step)
        {
            //if it's the first vertex, make it red
            if (i == 0)
            {
                Gizmos.color = Color.red;
            }

            Vector3 p = transform.TransformPoint(verts[i]);
            Gizmos.DrawSphere(p, 0.01f);
        }
    }
}