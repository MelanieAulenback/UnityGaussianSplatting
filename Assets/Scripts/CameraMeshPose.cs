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

        //--------------------------------------------------
        // Build unique vertex set -
        // there are normally 16 points overlapping in the same 5 positions, just want the 5 positions
        //--------------------------------------------------

        List<Vector3> unique = new List<Vector3>();
        const float eps = 0.0001f;

        foreach (Vector3 v in verts)
        {
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
        //DebugUniqueVertices(unique);
        //--------------------------------------------------
        // Find tip of camera by coplanarity
        //--------------------------------------------------

        const float coplanarTolerance = 1e-4f;

        int tipIndex = -1;

        for (int candidate = 0; candidate < unique.Count; candidate++)
        {
            // Collect the other four points
            List<Vector3> others = new List<Vector3>();

            for (int i = 0; i < unique.Count; i++)
            {
                if (i != candidate)
                    others.Add(unique[i]);
            }

            // Plane defined by first three points
            Vector3 a = others[0];
            Vector3 b = others[1];
            Vector3 c = others[2];
            Vector3 d = others[3];

            Vector3 normal = Vector3.Cross(b - a, c - a);

            // Degenerate plane
            if (normal.sqrMagnitude < 1e-8f)
                continue;

            normal.Normalize();

            // Distance of 4th point from the plane
            float distance = Mathf.Abs(Vector3.Dot(d - a, normal));

            if (distance < coplanarTolerance)
            {
                tipIndex = candidate;
                break;
            }
        }

        if (tipIndex == -1)
        {
            Debug.LogError("Could not determine camera tip.");
            return false;
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
    private static void DebugUniqueVertices(List<Vector3> unique)
    {
        Color[] vertexColors =
        {
        Color.red,       // unique 0
        Color.green,     // unique 1
        Color.blue,      // unique 2
        Color.magenta,   // unique 3
        Color.yellow     // unique 4
    };

        for (int i = 0; i < unique.Count; i++)
        {
            GameObject sphere = GameObject.CreatePrimitive(
                PrimitiveType.Sphere
            );

            sphere.name = $"UniqueVertex_{i}";

            // Mirror only for visualization
            Vector3 debugPos = unique[i];
            debugPos.x *= -1;

            sphere.transform.position = debugPos;
            sphere.transform.localScale = Vector3.one * 0.05f;

            Renderer renderer = sphere.GetComponent<Renderer>();

            if (renderer != null)
            {
                Material mat = new Material(
                    Shader.Find("Universal Render Pipeline/Lit")
                );

                mat.color = vertexColors[i];
                renderer.material = mat;
            }

            Collider col = sphere.GetComponent<Collider>();
            if (col != null)
                Object.Destroy(col);


            Debug.Log(
                $"Unique Vertex {i}: Original={unique[i]}, Mirrored={debugPos}"
            );
        }
    }
}