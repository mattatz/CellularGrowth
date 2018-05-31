using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;

namespace CellularGrowth.Dim2
{

    [StructLayout (LayoutKind.Sequential)]
    public struct MembraneEdge {
        public int a, b;
        public Vector2 fa, fb;
        public uint alive;
    }

}


