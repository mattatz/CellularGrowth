using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;

namespace CellularGrowth.Dim3
{

    [StructLayout (LayoutKind.Sequential)]
    public class Edge {
        public int a, b;
        public Vector3 fa, fb;
        public uint removable;
        public uint alive;
    }

}




