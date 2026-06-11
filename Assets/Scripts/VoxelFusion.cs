using System.Collections.Generic;
using UnityEngine;

public static class VoxelFusion
{
    struct VoxelData
    {
        public Vector3 positionSum;
        public Color colorSum;
        public Vector3 axis0Sum;
        public Vector3 axis1Sum;
        public Vector3 axis2Sum;
        public int count;
    }

    public static void Fuse(
        List<SplatData> inputSplats,
        SplatData outputSplat,
        float voxelSize)
    {
        Dictionary<Vector3Int, VoxelData> voxels =
            new Dictionary<Vector3Int, VoxelData>();

        foreach (var splat in inputSplats)
        {
            if (splat == null || splat.Count == 0)
                continue;

            for (int i = 0; i < splat.Count; i++)
            {
                Vector3 pos = splat.Positions[i];

                Vector3Int voxel =
                    new Vector3Int(
                        Mathf.FloorToInt(pos.x / voxelSize),
                        Mathf.FloorToInt(pos.y / voxelSize),
                        Mathf.FloorToInt(pos.z / voxelSize)
                    );

                VoxelData data;

                if (!voxels.TryGetValue(voxel, out data))
                {
                    data = new VoxelData();
                }

                data.positionSum += pos;
                data.colorSum += splat.Colors[i];

                data.axis0Sum += splat.Axes[i * 3 + 0];
                data.axis1Sum += splat.Axes[i * 3 + 1];
                data.axis2Sum += splat.Axes[i * 3 + 2];

                data.count++;

                voxels[voxel] = data;
            }
        }

        List<Vector3> positions = new();
        List<Color> colors = new();
        List<Vector3> axes = new();

        foreach (var kvp in voxels)
        {
            VoxelData v = kvp.Value;

            float inv = 1.0f / v.count;

            positions.Add(v.positionSum * inv);

            colors.Add(new Color(
                v.colorSum.r * inv,
                v.colorSum.g * inv,
                v.colorSum.b * inv,
                v.colorSum.a * inv));

            axes.Add(v.axis0Sum * inv);
            axes.Add(v.axis1Sum * inv);
            axes.Add(v.axis2Sum * inv);
        }

        outputSplat.Dispose();

        outputSplat.Positions = positions.ToArray();
        outputSplat.Colors = colors.ToArray();
        outputSplat.Axes = axes.ToArray();

        outputSplat.InitializeBuffers();

        Debug.Log(
            $"Voxel Fusion Complete: " +
            $"{positions.Count} fused splats");
    }
}
