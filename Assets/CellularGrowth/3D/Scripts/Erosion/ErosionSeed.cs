using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;

namespace CellularGrowth.Dim3
{

    [StructLayout (LayoutKind.Sequential)]
    public struct SeedVertex
    {
        public Vector3 position;
        public float radius;
        public int index;
    };

    [StructLayout (LayoutKind.Sequential)]
    public struct SeedEdge
    {
        public int a, b;
        public int index;

        public bool Contains(int i)
        {
            return (i == a) || (i == b);
        }
    };

    [StructLayout (LayoutKind.Sequential)]
    public struct SeedFace
    {
        public int c0, c1, c2;
        public int e0, e1, e2;
    };

}


