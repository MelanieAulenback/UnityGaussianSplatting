using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class NpyLoader
{
    // Generic loader that supports 2D or 3D float32 NPY
    public static float[,,] LoadFloat32(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);

        using (MemoryStream ms = new MemoryStream(bytes))
        using (BinaryReader br = new BinaryReader(ms))
        {
            byte[] magic = br.ReadBytes(6);

            if (magic[0] != 0x93 || magic[1] != (byte)'N')
                throw new Exception("Invalid NPY file");

            byte vMajor = br.ReadByte();
            byte vMinor = br.ReadByte();

            ushort headerLen = br.ReadUInt16();
            string header = Encoding.ASCII.GetString(br.ReadBytes(headerLen));

            Debug.Log(header);

            int[] shape = ParseShape(header);

            // =========================
            // CASE 1: 2D (H, W)
            // =========================
            if (shape.Length == 2)
            {
                int h = shape[0];
                int w = shape[1];

                int total = h * w;
                byte[] dataBytes = br.ReadBytes(total * 4);

                float[] flat = new float[total];
                Buffer.BlockCopy(dataBytes, 0, flat, 0, dataBytes.Length);

                float[,,] result = new float[h, w, 1];

                int idx = 0;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        result[y, x, 0] = flat[idx++];

                return result;
            }

            // =========================
            // CASE 3: 3D (N, H, W)
            // =========================
            if (shape.Length == 3)
            {
                int d0 = shape[0];
                int d1 = shape[1];
                int d2 = shape[2];

                int total = d0 * d1 * d2;
                byte[] dataBytes = br.ReadBytes(total * 4);

                float[] flat = new float[total];
                Buffer.BlockCopy(dataBytes, 0, flat, 0, dataBytes.Length);

                float[,,] result = new float[d0, d1, d2];

                int idx = 0;
                for (int i = 0; i < d0; i++)
                    for (int j = 0; j < d1; j++)
                        for (int k = 0; k < d2; k++)
                            result[i, j, k] = flat[idx++];

                return result;
            }

            throw new Exception("Unsupported NPY shape: " + shape.Length);
        }
    }

    public static float[] LoadFloatArray(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);

        using (MemoryStream ms = new MemoryStream(bytes))
        using (BinaryReader br = new BinaryReader(ms))
        {
            byte[] magic = br.ReadBytes(6);

            if (magic[0] != 0x93 || magic[1] != (byte)'N')
                throw new Exception("Invalid NPY file");

            br.ReadByte(); // version
            br.ReadByte();

            ushort headerLen = br.ReadUInt16();
            string header = Encoding.ASCII.GetString(br.ReadBytes(headerLen));

            int[] shape = ParseShape(header);

            int total = 1;
            foreach (int s in shape)
                total *= s;

            byte[] dataBytes = br.ReadBytes(total * 4);

            float[] flat = new float[total];
            Buffer.BlockCopy(dataBytes, 0, flat, 0, dataBytes.Length);

            return flat;
        }
    }

    private static int[] ParseShape(string header)
    {
        int start = header.IndexOf("(");
        int end = header.IndexOf(")");

        string inside = header.Substring(start + 1, end - start - 1);
        string[] parts = inside.Split(',');

        int[] shape = new int[parts.Length];

        for (int i = 0; i < parts.Length; i++)
            shape[i] = int.Parse(parts[i].Trim());

        return shape;
    }
}