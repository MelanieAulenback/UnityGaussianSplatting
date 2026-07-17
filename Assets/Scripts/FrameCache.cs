using System;
using UnityEngine;

//cache class for preloading colour images and point clouds
public class FrameCache
{
    public byte[][] colourBytes;

    public Vector3[] pointCloud;
    public Vector3[][] cameraVertices;

    public bool loaded;
    public int frameIndex;
}