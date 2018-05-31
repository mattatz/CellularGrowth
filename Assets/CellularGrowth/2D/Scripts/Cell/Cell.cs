using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;

namespace CellularGrowth.Dim2
{

    [StructLayout (LayoutKind.Sequential)]
    public struct Cell {
        public Vector2 position;
        public Vector2 velocity;
        public Vector2 force;
        public float radius;
        public float threshold;
        public float stress;
        public int type;
        public int links;
        public int membrane;
        public uint alive;
    }

}


