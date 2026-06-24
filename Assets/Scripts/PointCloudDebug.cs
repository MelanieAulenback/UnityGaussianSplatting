using UnityEngine;

public class PointCloudDebug : MonoBehaviour
{
    void OnDrawGizmos()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return;

        Vector3[] verts = mf.sharedMesh.vertices;

        Gizmos.color = Color.green;

        int step = Mathf.Max(1, verts.Length / 5000);

        //Debug.Log("num vertices " + verts.Length);

        for (int i = 0; i < verts.Length; i += step)
        {
            if (i == 0)
            {
                Gizmos.color = Color.red;
            }

            Vector3 p = transform.TransformPoint(verts[i]);
            Gizmos.DrawSphere(p, 0.01f);
        }
    }
}

/*using System.Collections.Generic;
using UnityEngine;

public class PointCloudDebug : MonoBehaviour
{
    void OnDrawGizmos()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return;

        Vector3[] verts = mf.sharedMesh.vertices;

        Debug.Log("raw vertex count: " + verts.Length);

        // =====================================================
        // STEP 1: TRANSFORM TO WORLD SPACE
        // =====================================================

        Vector3[] world = new Vector3[verts.Length];

        for (int i = 0; i < verts.Length; i++)
            world[i] = transform.TransformPoint(verts[i]);

        // =====================================================
        // STEP 2: DEDUPLICATE VERTICES
        // =====================================================

        List<Vector3> unique = new List<Vector3>();

        float eps = 0.0001f;

        for (int i = 0; i < world.Length; i++)
        {
            bool found = false;

            for (int j = 0; j < unique.Count; j++)
            {
                if (Vector3.Distance(world[i], unique[j]) < eps)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                unique.Add(world[i]);
        }

        Debug.Log("unique vertex count: " + unique.Count);

        // =====================================================
        // STEP 3: DRAW UNIQUE VERTICES
        // =====================================================

        for (int i = 0; i < unique.Count; i++)
        {
            Vector3 p = unique[i];

            if (i == 0)
                Gizmos.color = Color.red;
            else if (i == 1)
                Gizmos.color = Color.blue;
            else if (i == 2)
                Gizmos.color = Color.yellow;
            else if (i == 3)
                Gizmos.color = new Color(1f, 0.5f, 0f); // orange
            else if (i == 4)
                Gizmos.color = new Color(0.6f, 0f, 0.8f); // purple
            else
                Gizmos.color = Color.green;

            Gizmos.DrawSphere(p, 0.015f);
        }
    }
}
*/