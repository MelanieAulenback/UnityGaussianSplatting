using System.Collections.Generic;
using UnityEngine;

public static class CameraMeshPose
{
    public static bool TryGetPose(
    Vector3[] verts,
    out Vector3 position,
    out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        /*
        if (mf == null || mf.sharedMesh == null)
            return false;

        Transform t = mf.transform;
        Vector3[] verts = mf.sharedMesh.vertices;
        */
        //--------------------------------------------------
        // Build unique vertex set
        //--------------------------------------------------

        List<Vector3> unique = new List<Vector3>();
        const float eps = 0.0001f;

        foreach (Vector3 v in verts)
        {
            //Vector3 w = t.TransformPoint(v);
            Vector3 w = v;

            bool exists = false;
            foreach (Vector3 u in unique)
            {
                if ((u - w).sqrMagnitude < eps * eps)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
                unique.Add(w);
        }

        if (unique.Count != 5)
        {
            Debug.LogError($"Expected 5 unique vertices, found {unique.Count}");
            return false;
        }

        //--------------------------------------------------
        // Find tip
        //--------------------------------------------------

        int tipIndex = 0;
        float best = -1f;

        for (int i = 0; i < unique.Count; i++)
        {
            float sum = 0f;

            for (int j = 0; j < unique.Count; j++)
            {
                if (i == j) continue;
                sum += Vector3.Distance(unique[i], unique[j]);
            }

            if (sum > best)
            {
                best = sum;
                tipIndex = i;
            }
        }

        Vector3 tip = unique[tipIndex];

        //--------------------------------------------------
        // Collect image plane vertices
        //--------------------------------------------------

        List<Vector3> plane = new List<Vector3>();

        for (int i = 0; i < unique.Count; i++)
        {
            if (i != tipIndex)
                plane.Add(unique[i]);
        }

        Vector3 center = Vector3.zero;

        foreach (Vector3 p in plane)
            center += p;

        center /= plane.Count;

        //--------------------------------------------------
        // Forward
        //--------------------------------------------------

        Vector3 forward = (center - tip).normalized;

        //--------------------------------------------------
        // Build upright camera
        //--------------------------------------------------

        Vector3 worldUp = Vector3.up;

        if (Mathf.Abs(Vector3.Dot(worldUp, forward)) > 0.99f)
            worldUp = Vector3.right;

        Vector3 right = Vector3.Cross(worldUp, forward).normalized;
        Vector3 up = Vector3.Cross(forward, right).normalized;

        //--------------------------------------------------
        // Compute all pairwise distances
        //--------------------------------------------------

        List<(Vector3 a, Vector3 b, float len)> pairs =
            new List<(Vector3, Vector3, float)>();

        for (int i = 0; i < 4; i++)
        {
            for (int j = i + 1; j < 4; j++)
            {
                float len = Vector3.Distance(plane[i], plane[j]);
                pairs.Add((plane[i], plane[j], len));
            }
        }

        pairs.Sort((x, y) => y.len.CompareTo(x.len));

        //--------------------------------------------------
        // Skip the two diagonals.
        // Use the two longest remaining edges.
        //--------------------------------------------------

        Vector3 edge1 = (pairs[2].b - pairs[2].a).normalized;
        Vector3 edge2 = (pairs[3].b - pairs[3].a).normalized;

        // Make them point the same direction
        if (Vector3.Dot(edge1, edge2) < 0f)
            edge2 = -edge2;

        Vector3 imageRight = (edge1 + edge2).normalized;

        imageRight =
            Vector3.ProjectOnPlane(imageRight, forward).normalized;

        //--------------------------------------------------
        // Prevent 180° flips
        //--------------------------------------------------

        if (Vector3.Dot(imageRight, right) < 0f)
            imageRight = -imageRight;

        //--------------------------------------------------
        // Roll
        //--------------------------------------------------

        float roll =
            Vector3.SignedAngle(
                right,
                imageRight,
                forward);

        Quaternion rollRotation =
            Quaternion.AngleAxis(roll, forward);

        right = rollRotation * right;
        up = rollRotation * up;

        //--------------------------------------------------
        // Output
        //--------------------------------------------------

        position = tip;
        rotation = Quaternion.LookRotation(forward, up);

        return true;
    }
}