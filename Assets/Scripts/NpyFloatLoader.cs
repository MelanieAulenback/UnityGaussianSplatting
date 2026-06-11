using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class NpyFloatLoader
{
    public static float[,] Load2D(string path, out int width, out int height)
    {
        byte[] bytes = File.ReadAllBytes(path);

        // skip header safely by finding first '{'
        int headerStart = 10;
        int headerLen = BitConverter.ToInt32(bytes, 8);

        string header = Encoding.ASCII.GetString(bytes, 10, headerLen);

        int shapeStart = header.IndexOf("(");
        int shapeEnd = header.IndexOf(")");

        string[] shape = header.Substring(shapeStart + 1, shapeEnd - shapeStart - 1)
            .Split(',');

        height = int.Parse(shape[0]);
        width = int.Parse(shape[1]);

        int dataOffset = 10 + headerLen;

        int total = width * height;

        float[] flat = new float[total];

        Buffer.BlockCopy(bytes, dataOffset, flat, 0, total * 4);

        float[,] result = new float[height, width];

        int i = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                result[y, x] = flat[i++];

        return result;
    }

    static int[] ParseShape(string header)
    {
        int s = header.IndexOf("(");
        int e = header.IndexOf(")");

        string[] parts = header.Substring(s + 1, e - s - 1)
            .Split(',');

        int[] shape = new int[parts.Length];

        for (int i = 0; i < parts.Length; i++)
            shape[i] = int.Parse(parts[i].Trim());

        return shape;
    }
}