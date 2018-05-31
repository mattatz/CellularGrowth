using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CellularGrowth.Dim2
{

    public class Pointer : MonoBehaviour {

        [SerializeField, Range(0f, 1f)] protected float t = 1f;

        new protected Renderer renderer;
        protected MaterialPropertyBlock block;

        void Start () {
            renderer = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
        }
        
        void Update () {
            block.SetFloat("_T", t);
            renderer.SetPropertyBlock(block);
        }

        public void Interact(bool on, float dt)
        {
            t += dt * (on ? 1f : -1f);
            t = Mathf.Clamp01(t);
        }

    }

}


