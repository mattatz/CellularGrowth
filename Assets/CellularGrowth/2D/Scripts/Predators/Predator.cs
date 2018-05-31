using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CellularGrowth.Dim2
{

    public struct Predator {
        public Vector2 position;
        public Vector2 velocity;
        public Vector2 force;
        public float radius;
        public float stress;
        public bool alive;
    }

}


