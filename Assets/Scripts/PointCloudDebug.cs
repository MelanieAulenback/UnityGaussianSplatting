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

        for (int i = 0; i < verts.Length; i += step)
        {
            Vector3 p = transform.TransformPoint(verts[i]);
            Gizmos.DrawSphere(p, 0.01f);
        }
    }
}