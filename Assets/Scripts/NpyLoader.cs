using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class NpyLoader
{
    public static float[,,] LoadFloat32_3D(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);

        using (MemoryStream ms = new MemoryStream(bytes))
        using (BinaryReader br = new BinaryReader(ms))
        {
            // -------------------------
            // 1. Check magic string
            // -------------------------
            byte[] magic = br.ReadBytes(6);

            //Debug.Log("Loading file: " + path);
            //Debug.Log("Exists: " + File.Exists(path));

            // safer binary check (NO string conversion)
            if (magic[0] != 0x93 || magic[1] != (byte)'N')
            {
                throw new Exception("Not a valid NPY file (bad magic header)");
            }

            // -------------------------
            // 2. version
            // -------------------------
            byte vMajor = br.ReadByte();
            byte vMinor = br.ReadByte();

            // -------------------------
            // 3. header length
            // -------------------------
            ushort headerLen = br.ReadUInt16();

            string header = Encoding.ASCII.GetString(br.ReadBytes(headerLen));

            // -------------------------
            // 4. extract shape
            // -------------------------
            int[] shape = ParseShape(header);

            int total = shape[0] * shape[1] * shape[2];

            // -------------------------
            // 5. read float32 data safely
            // -------------------------
            byte[] dataBytes = br.ReadBytes(total * sizeof(float));

            if (dataBytes.Length != total * 4)
            {
                throw new Exception(
                    $"NPY size mismatch. Expected {total * 4} bytes but got {dataBytes.Length}"
                );
            }

            float[] flat = new float[total];
            Buffer.BlockCopy(dataBytes, 0, flat, 0, dataBytes.Length);

            // -------------------------
            // 6. reshape
            // -------------------------
            float[,,] result = new float[shape[0], shape[1], shape[2]];

            int idx = 0;
            for (int i = 0; i < shape[0]; i++)
                for (int j = 0; j < shape[1]; j++)
                    for (int k = 0; k < shape[2]; k++)
                        result[i, j, k] = flat[idx++];

            return result;
        }
    }

    public static float[,] LoadFloat32_2D(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);

        using (MemoryStream ms = new MemoryStream(bytes))
        using (BinaryReader br = new BinaryReader(ms))
        {
            br.ReadBytes(6);

            br.ReadByte();
            br.ReadByte();

            ushort headerLen = br.ReadUInt16();

            string header =
                Encoding.ASCII.GetString(
                    br.ReadBytes(headerLen)
                );

            int[] shape = ParseShape(header);

            int height = shape[0];
            int width = shape[1];

            int total = width * height;

            byte[] dataBytes =
                br.ReadBytes(total * sizeof(float));

            float[] flat = new float[total];

            Buffer.BlockCopy(
                dataBytes,
                0,
                flat,
                0,
                dataBytes.Length
            );

            float[,] result =
                new float[width, height];

            int idx = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    result[x, y] = flat[idx++];
                }
            }

            return result;
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