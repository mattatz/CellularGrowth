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

    public class Membrane : MonoBehaviour {

        public ComputeBuffer NodesBuffer { get { return nodesBufferRead; } }
        public ComputeBuffer EdgesBuffer { get { return edgesBuffer; } }
        public float Tension { get { return tension; } }

        [SerializeField] protected int nodesCount = 128;
        [SerializeField] protected ComputeShader compute;

        [SerializeField] protected Material material;

        [SerializeField, Range(3f, 15f)] protected float radius = 7.0f;
        protected float current;

        [SerializeField, Range(1f, 10f)] protected float limit = 3.0f;
        [SerializeField, Range(0.5f, 0.99f)] protected float drag = 0.9f;
        [SerializeField, Range(1f, 10f)] protected float distance = 5f;
        [SerializeField, Range(0.1f, 0.95f)] protected float tension = 0.7f;

        ComputeBuffer nodesBufferRead, nodesBufferWrite, nodesPoolBuffer, nodeArgsBuffer;
        int[] nodeArgs = new int[] { 0, 1, 0, 0 };

        ComputeBuffer edgesBuffer, edgesPoolBuffer, edgeArgsBuffer;
        int[] edgeArgs = new int[] { 0, 1, 0, 0 };

        protected Mesh line;
        ComputeBuffer drawEdgeArgsBuffer;
        uint[] drawEdgeArgs = new uint[5] { 0, 0, 0, 0, 0 };

        protected int nodePoolCount = 0, edgePoolCount = 0;

        protected Kernel
            initNodesKer, interactNodesKer, updateNodesKer;

        protected Kernel
            initEdgesKer, relaxKer;

        protected Kernel
            emitKer, stretchKer, expandKer;

        protected Texture2D gradTexture;

        #region Monobehaviour functions

        protected void Start()
        {
            nodesBufferRead = new ComputeBuffer(nodesCount, Marshal.SizeOf(typeof(MembraneNode)), ComputeBufferType.Default);
            nodesBufferWrite = new ComputeBuffer(nodesCount, Marshal.SizeOf(typeof(MembraneNode)), ComputeBufferType.Default);

            nodesPoolBuffer = new ComputeBuffer(nodesCount, Marshal.SizeOf(typeof(int)), ComputeBufferType.Append);
            nodesPoolBuffer.SetCounterValue(0);
            nodeArgsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
            nodeArgsBuffer.SetData(nodeArgs);

            edgesBuffer = new ComputeBuffer(nodesCount, Marshal.SizeOf(typeof(Edge)), ComputeBufferType.Default);
            edgesPoolBuffer = new ComputeBuffer(nodesCount, Marshal.SizeOf(typeof(int)), ComputeBufferType.Append);
            edgesPoolBuffer.SetCounterValue(0);
            edgeArgsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
            edgeArgsBuffer.SetData(edgeArgs);

            line = BuildInstancingLine();
            // line = BuildPseudoInstancingLine(nodesCount);
            drawEdgeArgs[0] = line.GetIndexCount(0);
            drawEdgeArgs[1] = (uint)nodesCount;
            drawEdgeArgsBuffer = new ComputeBuffer(1, sizeof(uint) * drawEdgeArgs.Length, ComputeBufferType.IndirectArguments);
            drawEdgeArgsBuffer.SetData(drawEdgeArgs);

            initNodesKer = new Kernel(compute, "InitNodes");
            initEdgesKer = new Kernel(compute, "InitEdges");

            interactNodesKer = new Kernel(compute, "InteractNodes");
            updateNodesKer = new Kernel(compute, "UpdateNodes");

            emitKer = new Kernel(compute, "Emit");
            stretchKer = new Kernel(compute, "Stretch");
            expandKer = new Kernel(compute, "Expand");
            relaxKer = new Kernel(compute, "Relax");

            InitNodes(initNodesKer);
            InitEdges(initEdgesKer);

            current = radius;
            Emit(emitKer, nodesCount, radius);

            UpdatePoolCount();
        }

        protected void Update()
        {
            Render();
        }

        public void Step(CellularGrowth cell, float t, float dt)
        {
            current = Mathf.Lerp(current, radius, dt * 10f);
            InteractNodes(interactNodesKer, nodePoolCount);
            Stretch(stretchKer, dt);
            Expand(cell, expandKer, dt);
            UpdateNodes(updateNodesKer, dt);
            Relax(relaxKer, dt);
        }

        protected void OnDestroy()
        {
            nodesBufferRead.Release();
            nodesBufferWrite.Release();
            nodesPoolBuffer.Release();
            nodeArgsBuffer.Release();

            edgesBuffer.Release();
            edgesPoolBuffer.Release();
            edgeArgsBuffer.Release();

            drawEdgeArgsBuffer.Release();

            Destroy(gradTexture);
        }

        protected void OnGUI()
        {
        }

        #endregion

        protected void Render()
        {
            material.SetBuffer("_Nodes", nodesBufferRead);
            material.SetBuffer("_Edges", edgesBuffer);
            material.SetMatrix("_World2Local", transform.worldToLocalMatrix);
            material.SetMatrix("_Local2World", transform.localToWorldMatrix);
            material.SetPass(0);
            Graphics.DrawMeshInstancedIndirect(line, 0, material, new Bounds(Vector3.zero, Vector3.one * 100f), drawEdgeArgsBuffer);
            // Graphics.DrawMesh(line, transform.localToWorldMatrix, material, 0);
        }

        protected Mesh BuildInstancingLine()
        {
            var mesh = new Mesh();
            mesh.vertices = new Vector3[2] { Vector3.zero, Vector3.up };
            mesh.uv = new Vector2[2] { new Vector2(0f, 0f), new Vector2(0f, 1f) };
            mesh.SetIndices(new int[2] { 0, 1 }, MeshTopology.Lines, 0);
            return mesh;
        }

        protected Mesh BuildPseudoInstancingLine(int count)
        {
            var mesh = new Mesh();
            mesh.vertices = new Vector3[count];
            var uv = new Vector2[count];
            var indices = new int[count];
            var inv = 1f / (count - 1);
            for(int i = 0; i < count; i++)
            {
                uv[i] = new Vector2(0f, inv * i);
                indices[i] = i;
            }
            mesh.uv = uv;
            // mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.SetIndices(indices, MeshTopology.LineStrip, 0);
            return mesh;
        }

        protected void SwapBuffer(ref ComputeBuffer buf0, ref ComputeBuffer buf1)
        {
            var tmp = buf0;
            buf0 = buf1;
            buf1 = tmp;
        }

        protected void UpdatePoolCount()
        {
            nodePoolCount = GetNodePoolCount();
            edgePoolCount = GetEdgePoolCount();
        }

        protected int GetNodePoolCount()
        {
            nodeArgsBuffer.SetData(nodeArgs);
            ComputeBuffer.CopyCount(nodesPoolBuffer, nodeArgsBuffer, 0);
            nodeArgsBuffer.GetData(nodeArgs);
            return nodeArgs[0];
        }

        protected int GetEdgePoolCount()
        {
            edgeArgsBuffer.SetData(edgeArgs);
            ComputeBuffer.CopyCount(edgesPoolBuffer, edgeArgsBuffer, 0);
            edgeArgsBuffer.GetData(edgeArgs);
            return edgeArgs[0];
        }

        protected float GetEdgeLength()
        {
            var len = (2f * current * Mathf.PI) / nodesCount;
            return Mathf.Max(len, 0.1f);
        }

        #region Node kernels

        protected void InitNodes(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_Nodes", nodesBufferRead);
            compute.SetBuffer(ker.Index, "_NodePoolAppend", nodesPoolBuffer);
            compute.SetInt("_NodesCount", nodesCount);
            compute.Dispatch(ker.Index, nodesCount / (int)ker.ThreadX + 1, 1, 1);
        }

        protected void Emit(Kernel ker, int n, float radius)
        {
            compute.SetBuffer(ker.Index, "_Nodes", nodesBufferRead);
            compute.SetBuffer(ker.Index, "_NodePoolConsume", nodesPoolBuffer);

            compute.SetBuffer(ker.Index, "_Edges", edgesBuffer);
            compute.SetBuffer(ker.Index, "_EdgePoolConsume", edgesPoolBuffer);

            compute.SetInt("_EmitCount", n);
            compute.SetFloat("_EmitRadius", radius);
            compute.Dispatch(ker.Index, 1, 1, 1);
        }

        protected void InteractNodes(Kernel ker, int current)
        {
            compute.SetBuffer(ker.Index, "_NodesRead", nodesBufferRead);
            compute.SetBuffer(ker.Index, "_Nodes", nodesBufferWrite);
            compute.SetInt("_NodesCount", nodesCount);
            compute.Dispatch(ker.Index, nodesCount / (int)ker.ThreadX + 1, 1, 1);

            SwapBuffer(ref nodesBufferRead, ref nodesBufferWrite);
        }

        protected void UpdateNodes(Kernel ker, float dt)
        {
            compute.SetBuffer(ker.Index, "_Nodes", nodesBufferRead);
            compute.SetBuffer(ker.Index, "_Edges", edgesBuffer);
            compute.SetInt("_NodesCount", nodesCount);
            compute.SetInt("_EdgesCount", nodesCount);

            compute.SetFloat("_EdgeLength", GetEdgeLength());
            compute.SetFloat("_Limit", limit);
            compute.SetFloat("_Drag", drag);
            compute.SetVector("_Point", GetMousePoint());
            compute.SetFloat("_DT", dt);
            compute.Dispatch(ker.Index, nodesCount / (int)ker.ThreadX + 1, 1, 1);
        }

        protected Vector4 GetMousePoint()
        {
            var p = Input.mousePosition;
            var world = Camera.main.ScreenToWorldPoint(new Vector3(p.x, p.y, Camera.main.nearClipPlane));
            var local = transform.InverseTransformPoint(world);
            return new Vector4(local.x, local.y, distance, 1f / distance);
        }

        #endregion

        #region Edge kernels

        protected void InitEdges(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_Edges", edgesBuffer);
            compute.SetBuffer(ker.Index, "_EdgePoolAppend", edgesPoolBuffer);
            compute.SetInt("_EdgesCount", nodesCount);
            compute.Dispatch(ker.Index, nodesCount / (int)ker.ThreadX + 1, 1, 1);
        }

        protected void Stretch(Kernel ker, float dt)
        {
            compute.SetBuffer(ker.Index, "_Nodes", nodesBufferRead);
            compute.SetBuffer(ker.Index, "_Edges", edgesBuffer);
            compute.SetInt("_EdgesCount", nodesCount);
            compute.SetFloat("_EdgeLength", GetEdgeLength());
            compute.SetFloat("_DT", dt);
            compute.Dispatch(ker.Index, nodesCount / (int)ker.ThreadX + 1, 1, 1);
        }

        protected void Expand(CellularGrowth cell, Kernel ker, float dt) {
            compute.SetBuffer(ker.Index, "_Cells", cell.CellsBuffer);
            compute.SetInt("_CellsCount", cell.CellsBuffer.count);

            compute.SetBuffer(ker.Index, "_Nodes", nodesBufferRead);
            compute.SetBuffer(ker.Index, "_Edges", edgesBuffer);
            compute.SetInt("_EdgesCount", nodesCount);

            compute.SetFloat("_Tension", tension);
            compute.SetFloat("_DT", dt);

            compute.Dispatch(ker.Index, nodesCount / (int)ker.ThreadX + 1, 1, 1);
        }

        protected void Relax(Kernel ker, float dt)
        {
            compute.SetBuffer(ker.Index, "_Edges", edgesBuffer);
            compute.SetInt("_EdgesCount", nodesCount);
            compute.SetFloat("_DT", dt);
            compute.Dispatch(ker.Index, nodesCount / (int)ker.ThreadX + 1, 1, 1);
        }

        protected void RemoveEdges(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_Nodes", nodesBufferRead);
            compute.SetBuffer(ker.Index, "_Edges", edgesBuffer);
            compute.SetBuffer(ker.Index, "_EdgePoolAppend", edgesPoolBuffer);
            compute.SetInt("_EdgesCount", nodesCount);
            compute.Dispatch(ker.Index, nodesCount / (int)ker.ThreadX + 1, 1, 1);
        }

        #endregion

        protected Texture2D CreateGradient(Gradient grad, int size = 128)
        {
            var tex = new Texture2D(size, 1, TextureFormat.RGBAFloat, false);
            var inv = 1f / (size - 1);
            for(int x = 0; x < size; x++)
            {
                var t = x * inv;
                tex.SetPixel(x, 0, grad.Evaluate(t));
            }
            tex.Apply();
            return tex;
        }

    }

}


