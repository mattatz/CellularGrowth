using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;

namespace CellularGrowth.Dim3
{

    [StructLayout (LayoutKind.Sequential)]
    public struct Cell {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 force;
        public float radius;
        public float threshold;
        public float stress;
        public int type;
        public int links;
        public int faces;
        public uint dividable;
        public uint alive;
    }

}



