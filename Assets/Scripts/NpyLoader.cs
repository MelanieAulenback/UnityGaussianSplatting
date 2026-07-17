using System;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Unity.VisualScripting;
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

            //binary check
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

    //loads a NumPy .npy file containing a 2D array of float32 values.
    //returns the data as a float[,] that can be accessed as [x, y]
    public static float[,] LoadFloat32_2D(string path)
    {
        //read the entire .npy file into memory.
        byte[] bytes = File.ReadAllBytes(path);

        //create a stream so we can read the file sequentially
        using (MemoryStream ms = new MemoryStream(bytes))
        using (BinaryReader br = new BinaryReader(ms))
        {
            //skip the NumPy magic string
            //this is always 6 bytes: \x93NUMPY
            br.ReadBytes(6);

            //skip the major and minor version numbers
            //(Usually version 1.0 or 2.0.)
            br.ReadByte();
            br.ReadByte();

            //read the length of the header
            //the next two bytes tell us how many bytes long the header is.
            ushort headerLen = br.ReadUInt16();

            //read the header text
            //the header contains information such as:
            //- data type (float32)
            //- whether the data is stored in Fortran order
            //- the shape (dimensions) of the array
            string header =
                Encoding.ASCII.GetString(
                    br.ReadBytes(headerLen)
                );

            //extract the array dimensions from the header
            //example:
            //"(448, 504)" becomes {448, 504}
            int[] shape = ParseShape(header);

            int height = shape[0];
            int width = shape[1];

            //total number of float values in the file
            int total = width * height;

            //read all of the float data
            //each float occupies 4 bytes
            byte[] dataBytes =
                br.ReadBytes(total * sizeof(float));

            //create a 1D float array to hold the values
            float[] flat = new float[total];

            //convert the raw bytes into float values
            //this is much faster than reading one float at a time
            Buffer.BlockCopy(
                dataBytes,
                0,
                flat,
                0,
                dataBytes.Length
            );

            //create the final 2D array.
            //stored as [x, y] because that's how the rest of the project
            //indexes textures and images
            float[,] result =
                new float[width, height];

            int idx = 0;

            //copy the flattened array into the 2D array.
            //the NumPy data is stored row by row:
            //
            //flat:
            //0 1 2
            //3 4 5
            //
            //becomes:
            //result[0,0] result[1,0] result[2,0]
            //result[0,1] result[1,1] result[2,1]
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

    //reads the "shape" entry from a NumPy header.
    //example:
    //
    //"{'descr': '<f4', 'fortran_order': False, 'shape': (448, 504), }"
    //
    //returns:
    //{448, 504}
    private static int[] ParseShape(string header)
    {
        //find the opening and closing parentheses around the shape
        int start = header.IndexOf("(");
        int end = header.IndexOf(")");

        //extract everything inside the parentheses.
        //example:
        //"(448, 504)" -> "448, 504"
        string inside = header.Substring(start + 1, end - start - 1);

        //split the dimensions wherever there is a comma
        string[] parts = inside.Split(',');

        //convert each dimension from text into an integer
        int[] shape = new int[parts.Length];

        for (int i = 0; i < parts.Length; i++)
            shape[i] = int.Parse(parts[i].Trim());

        return shape;
    }
}