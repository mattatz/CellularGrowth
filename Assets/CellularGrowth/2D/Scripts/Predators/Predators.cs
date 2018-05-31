using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace CellularGrowth.Dim2
{

    public class Predators : MonoBehaviour {

        public ComputeBuffer PredatorsBuffer { get { return predatorsBufferRead; } }

        [SerializeField] protected Mesh mesh;
        [SerializeField] protected int count = 32; 
        [SerializeField] protected ComputeShader compute;

        [SerializeField] protected Material material;

        [SerializeField, Range(1f, 10f)] protected float limit = 3.0f;
        [SerializeField, Range(0.5f, 0.99f)] protected float drag = 0.9f;

        ComputeBuffer predatorsBufferRead, predatorsBufferWrite, predatorsPoolBuffer, predatorArgsBuffer;
        int[] predatorArgs = new int[] { 0, 1, 0, 0 };

        ComputeBuffer drawPredatorArgsBuffer;
        uint[] drawPredatorArgs = new uint[5] { 0, 0, 0, 0, 0 };

        protected int predatorPoolCount = 0;

        protected Kernel 
            initPredatorsKer, emitPredatorsKer, interactPredatorsKer, 
            updatePredatorsKer;

        protected Kernel
            wrapKer;

        #region Monobehaviour functions

        protected void Start()
        {
            predatorsBufferRead = new ComputeBuffer(count, Marshal.SizeOf(typeof(Predator)), ComputeBufferType.Default);
            predatorsBufferWrite = new ComputeBuffer(count, Marshal.SizeOf(typeof(Predator)), ComputeBufferType.Default);

            predatorsPoolBuffer = new ComputeBuffer(count, Marshal.SizeOf(typeof(int)), ComputeBufferType.Append);
            predatorsPoolBuffer.SetCounterValue(0);
            predatorArgsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
            predatorArgsBuffer.SetData(predatorArgs);

            drawPredatorArgs[0] = mesh.GetIndexCount(0);
            drawPredatorArgs[1] = (uint)count;
            drawPredatorArgsBuffer = new ComputeBuffer(1, sizeof(uint) * drawPredatorArgs.Length, ComputeBufferType.IndirectArguments);
            drawPredatorArgsBuffer.SetData(drawPredatorArgs);

            initPredatorsKer = new Kernel(compute, "InitPredators");
            emitPredatorsKer = new Kernel(compute, "EmitPredators");
            interactPredatorsKer = new Kernel(compute, "InteractPredators");
            updatePredatorsKer = new Kernel(compute, "UpdatePredators");
            wrapKer = new Kernel(compute, "Wrap");

            InitPredators(initPredatorsKer);
            EmitPredators(emitPredatorsKer, count);
            UpdatePoolCount();
        }

        protected void Update()
        {
            Render();
        }

        public void Step(CellularGrowth cell, float t, float dt)
        {
            InteractPredators(interactPredatorsKer);
            UpdatePredators(updatePredatorsKer, cell, t, dt);
            // UpdatePoolCount();
        }

        protected void OnDestroy()
        {
            predatorsBufferRead.Release();
            predatorsBufferWrite.Release();
            predatorsPoolBuffer.Release();
            predatorArgsBuffer.Release();
            drawPredatorArgsBuffer.Release();
        }

        #endregion

        protected void Render()
        {
            material.SetBuffer("_Predators", predatorsBufferRead);
            material.SetMatrix("_World2Local", transform.worldToLocalMatrix);
            material.SetMatrix("_Local2World", transform.localToWorldMatrix);
            material.SetPass(0);
            Graphics.DrawMeshInstancedIndirect(mesh, 0, material, new Bounds(Vector3.zero, Vector3.one * 100f), drawPredatorArgsBuffer);
        }

        protected void SwapBuffer(ref ComputeBuffer buf0, ref ComputeBuffer buf1)
        {
            var tmp = buf0;
            buf0 = buf1;
            buf1 = tmp;
        }

        protected void UpdatePoolCount()
        {
            predatorPoolCount = GetPredatorPoolCount();
        }

        protected int GetPredatorPoolCount()
        {
            predatorArgsBuffer.SetData(predatorArgs);
            ComputeBuffer.CopyCount(predatorsPoolBuffer, predatorArgsBuffer, 0);
            predatorArgsBuffer.GetData(predatorArgs);
            return predatorArgs[0];
        }

        #region Predator kernels

        protected void InitPredators(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_Predators", predatorsBufferRead);
            compute.SetBuffer(ker.Index, "_PredatorPoolAppend", predatorsPoolBuffer);
            compute.SetInt("_PredatorsCount", count);
            compute.Dispatch(ker.Index, count / (int)ker.ThreadX + 1, 1, 1);
        }

        protected void EmitPredators(Kernel ker, int n)
        {
            compute.SetBuffer(ker.Index, "_Predators", predatorsBufferRead);
            compute.SetBuffer(ker.Index, "_PredatorPoolConsume", predatorsPoolBuffer);
            compute.SetInt("_EmitCount", n);
            compute.Dispatch(ker.Index, n / (int)ker.ThreadX + 1, 1, 1);

            predatorPoolCount = GetPredatorPoolCount();
        }

        protected void InteractPredators(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_PredatorsRead", predatorsBufferRead);
            compute.SetBuffer(ker.Index, "_Predators", predatorsBufferWrite);
            compute.SetInt("_PredatorsCount", count);
            compute.Dispatch(ker.Index, count / (int)ker.ThreadX + 1, 1, 1);

            SwapBuffer(ref predatorsBufferRead, ref predatorsBufferWrite);
        }

        protected void UpdatePredators(Kernel ker, CellularGrowth cell, float t, float dt)
        {
            compute.SetBuffer(ker.Index, "_Predators", predatorsBufferRead);
            compute.SetInt("_PredatorsCount", count);

            compute.SetFloat("_DT", dt);
            compute.SetFloat("_Time", t);
            compute.SetFloat("_Limit", limit);
            compute.SetFloat("_Drag", drag);
            compute.SetVector("_Point", GetMousePoint());
            compute.Dispatch(ker.Index, count / (int)ker.ThreadX + 1, 1, 1);
        }

        public void Wrap(Membrane mem, float dt)
        {
            var ker = wrapKer;
            compute.SetBuffer(ker.Index, "_Predators", predatorsBufferRead);
            compute.SetInt("_PredatorsCount", count);

            compute.SetBuffer(ker.Index, "_MembraneNodes", mem.NodesBuffer);
            compute.SetBuffer(ker.Index, "_MembraneEdges", mem.EdgesBuffer);
            compute.SetInt("_MembraneNodesCount", mem.NodesBuffer.count);

            compute.SetFloat("_DT", dt);
            compute.SetFloat("_Tension", mem.Tension);
            compute.Dispatch(ker.Index, count / (int)ker.ThreadX + 1, 1, 1);
        }

        protected Vector4 GetMousePoint()
        {
            var p = Input.mousePosition;
            var world = Camera.main.ScreenToWorldPoint(new Vector3(p.x, p.y, Camera.main.nearClipPlane));
            var local = transform.InverseTransformPoint(world);
            const float distance = 3f;
            return new Vector4(local.x, local.y, distance, 1f / distance);
        }

        #endregion

    }

}


