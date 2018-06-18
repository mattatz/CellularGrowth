using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;

namespace CellularGrowth.Dim3
{

    [StructLayout (LayoutKind.Sequential)]
    public struct Face {
        public int c0, c1, c2;
        public int e0, e1, e2;
        public uint removable;
        public uint alive;
    }

}


