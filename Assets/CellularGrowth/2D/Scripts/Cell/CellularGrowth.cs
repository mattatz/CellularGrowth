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

    public class CellularGrowth : MonoBehaviour {

        public ComputeBuffer CellsBuffer { get { return cellsBufferRead; } }
        public ComputeBuffer EdgesBuffer { get { return edgesBufferRead; } }

        [SerializeField] protected GUISkin skin;
        [SerializeField] protected Mesh mesh;
        [SerializeField] protected int cellsCount = 2 << 15; 
        [SerializeField] protected ComputeShader compute;

        [SerializeField] protected Material cellMaterial, edgeMaterial;
        [SerializeField] protected bool drawCell = true, drawEdge = true;
        [SerializeField] protected Gradient gradient;

        [SerializeField] protected bool dividable;
        [SerializeField] protected int threshold = 8000;
        [SerializeField, Range(0.0f, 1.0f)] protected float rate = 0.5f;
        [SerializeField, Range(0.1f, 3f)] protected float interval = 0.5f;
        [SerializeField, Range(1f, 10f)] protected float limit = 3.0f;
        [SerializeField, Range(0.5f, 0.99f)] protected float drag = 0.9f;
        [SerializeField, Range(1f, 10f)] protected float distance = 3f;

        [SerializeField] protected Membrane membrane;
        [SerializeField] protected Predators predators;
        [SerializeField] protected Pointer pointer;

        ComputeBuffer cellsBufferRead, cellsBufferWrite, cellsPoolBuffer, cellArgsBuffer;
        int[] cellArgs = new int[] { 0, 1, 0, 0 };

        ComputeBuffer edgesBufferRead, edgesBufferWrite, edgesPoolBuffer, edgeArgsBuffer;
        int[] edgeArgs = new int[] { 0, 1, 0, 0 };

        ComputeBuffer drawCellArgsBuffer;
        uint[] drawCellArgs = new uint[5] { 0, 0, 0, 0, 0 };

        protected Mesh line;
        ComputeBuffer drawEdgeArgsBuffer;
        uint[] drawEdgeArgs = new uint[5] { 0, 0, 0, 0, 0 };

        protected int cellPoolCount = 0, edgePoolCount = 0;

        protected Coroutine iDivider;
        protected Texture2D gradTexture;

        #region Kernels

        protected Kernel 
            initCellsKer, resetCellsKer, emitCellsKer, interactCellsKer, 
            updateCellsKer, divideCellsKer;

        protected Kernel 
            initEdgesKer, resetEdgesKer, updateEdgesKer, 
            removeEdgesKer, copyEdgesKer;

        protected Kernel
            removeCellsKer,
            removeCellsCircleKer, removeCellsLineKer;

        protected Kernel
            divideKer, wrapKer, huntKer;

        #endregion

        #region Monobehaviour functions

        protected void Start()
        {
            cellsBufferRead = new ComputeBuffer(cellsCount, Marshal.SizeOf(typeof(Cell)), ComputeBufferType.Default);
            cellsBufferWrite = new ComputeBuffer(cellsCount, Marshal.SizeOf(typeof(Cell)), ComputeBufferType.Default);

            cellsPoolBuffer = new ComputeBuffer(cellsCount, Marshal.SizeOf(typeof(int)), ComputeBufferType.Append);
            cellsPoolBuffer.SetCounterValue(0);
            cellArgsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
            cellArgsBuffer.SetData(cellArgs);

            drawCellArgs[0] = mesh.GetIndexCount(0);
            drawCellArgs[1] = (uint)cellsCount;
            drawCellArgsBuffer = new ComputeBuffer(1, sizeof(uint) * drawCellArgs.Length, ComputeBufferType.IndirectArguments);
            drawCellArgsBuffer.SetData(drawCellArgs);

            edgesBufferRead = new ComputeBuffer(cellsCount, Marshal.SizeOf(typeof(Edge)), ComputeBufferType.Default);
            edgesBufferWrite = new ComputeBuffer(cellsCount, Marshal.SizeOf(typeof(Edge)), ComputeBufferType.Default);

            edgesPoolBuffer = new ComputeBuffer(cellsCount, Marshal.SizeOf(typeof(int)), ComputeBufferType.Append);
            edgesPoolBuffer.SetCounterValue(0);
            edgeArgsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
            edgeArgsBuffer.SetData(edgeArgs);

            line = BuildLine();
            drawEdgeArgs[0] = line.GetIndexCount(0);
            drawEdgeArgs[1] = (uint)cellsCount;
            drawEdgeArgsBuffer = new ComputeBuffer(1, sizeof(uint) * drawEdgeArgs.Length, ComputeBufferType.IndirectArguments);
            drawEdgeArgsBuffer.SetData(drawEdgeArgs);

            initCellsKer = new Kernel(compute, "InitCells");
            resetCellsKer = new Kernel(compute, "ResetCells");
            emitCellsKer = new Kernel(compute, "EmitCells");
            interactCellsKer = new Kernel(compute, "InteractCells");
            updateCellsKer = new Kernel(compute, "UpdateCells");
            divideCellsKer = new Kernel(compute, "DivideCells");

            initEdgesKer = new Kernel(compute, "InitEdges");
            resetEdgesKer = new Kernel(compute, "ResetEdges");
            updateEdgesKer = new Kernel(compute, "UpdateEdges");
            removeEdgesKer = new Kernel(compute, "RemoveEdges");
            copyEdgesKer = new Kernel(compute, "CopyEdges");

            removeCellsKer = new Kernel(compute, "RemoveCells");
            removeCellsCircleKer = new Kernel(compute, "RemoveCellsCircle");
            removeCellsLineKer = new Kernel(compute, "RemoveCellsLine");

            divideKer = new Kernel(compute, "Divide");
            wrapKer = new Kernel(compute, "Wrap");
            huntKer = new Kernel(compute, "Hunt");

            gradTexture = CreateGradient(gradient);
            cellMaterial.SetTexture("_Gradient", gradTexture);

            InitCells(initCellsKer);
            InitEdges(initEdgesKer);
            UpdatePoolCount();

            EmitCells(emitCellsKer, 1);
            UpdatePoolCount();

            iDivider = StartCoroutine(IDivider());
        }

        protected void Update()
        {
            var dt = Time.deltaTime;
            var t = Time.timeSinceLevelLoad;

            UpdateEdges(updateEdgesKer, dt);
            InteractCells(interactCellsKer, cellPoolCount);
            UpdateCells(updateCellsKer, cellPoolCount, dt);

            if(membrane.gameObject.activeSelf) {
                Wrap(membrane, dt);
                membrane.Step(this, t, dt);
            }

            if(predators.gameObject.activeSelf)
            {
                Hunt(predators);
                predators.Wrap(membrane, dt);
                predators.Step(this, t, dt);
            }

            RemoveCells(removeCellsKer);
            RemoveEdges(removeEdgesKer);
            UpdatePoolCount();

            Render();

            var p = Input.mousePosition;
            var cam = Camera.main;
            var world = cam.ScreenToWorldPoint(new Vector3(p.x, p.y, cam.nearClipPlane + (cam.farClipPlane - cam.nearClipPlane) * 0.5f));
            pointer.transform.position = world;
            pointer.transform.localScale = Vector3.one * distance * 2f;
            pointer.Interact(Input.GetMouseButton(0), dt);
        }

        protected void OnDestroy()
        {
            cellsBufferRead.Release();
            cellsBufferWrite.Release();
            cellsPoolBuffer.Release();
            cellArgsBuffer.Release();

            edgesBufferRead.Release();
            edgesBufferWrite.Release();
            edgesPoolBuffer.Release();
            edgeArgsBuffer.Release();

            drawCellArgsBuffer.Release();
            drawEdgeArgsBuffer.Release();

            Destroy(gradTexture);
        }

        protected void OnGUI()
        {
            GUI.skin = skin;
            using(new GUILayout.HorizontalScope())
            {
                GUILayout.Space(10f);
                using(new GUILayout.VerticalScope())
                {
                    GUILayout.Space(10f);
                    if(GUILayout.Button("reset"))
                    {
                        Reset();
                    }
                    GUILayout.Label("cells: " + (cellsCount - cellPoolCount).ToString());
                    GUILayout.Label("edges: " + (cellsCount - edgePoolCount).ToString());
                    // dividable = GUILayout.Toggle(dividable, "dividable");
                    drawCell = GUILayout.Toggle(drawCell, "draw cell");
                    drawEdge = GUILayout.Toggle(drawEdge, "draw edge");

                    GUILayout.Space(5f);

                    using(new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("rate: " + rate.ToString("0.00"), GUILayout.Width(60f));
                        rate = GUILayout.HorizontalSlider(rate, 0.25f, 0.75f, GUILayout.Width(100f));
                    }

                }
            }
        }

        #endregion

        protected void Render()
        {
            if(drawCell)
            {
                cellMaterial.SetBuffer("_Cells", cellsBufferRead);
                cellMaterial.SetMatrix("_World2Local", transform.worldToLocalMatrix);
                cellMaterial.SetMatrix("_Local2World", transform.localToWorldMatrix);
                cellMaterial.SetPass(0);
                Graphics.DrawMeshInstancedIndirect(mesh, 0, cellMaterial, new Bounds(Vector3.zero, Vector3.one * 100f), drawCellArgsBuffer);
            }

            if(drawEdge)
            {
                edgeMaterial.SetBuffer("_Cells", cellsBufferRead);
                edgeMaterial.SetBuffer("_Edges", edgesBufferRead);
                edgeMaterial.SetMatrix("_World2Local", transform.worldToLocalMatrix);
                edgeMaterial.SetMatrix("_Local2World", transform.localToWorldMatrix);
                edgeMaterial.SetPass(0);
                Graphics.DrawMeshInstancedIndirect(line, 0, edgeMaterial, new Bounds(Vector3.zero, Vector3.one * 100f), drawEdgeArgsBuffer);
            }
        }

        protected void Reset()
        {
            ResetCells(resetCellsKer);
            ResetEdges(resetEdgesKer);
            UpdatePoolCount();

            EmitCells(emitCellsKer, 1);
            UpdatePoolCount();
        }

        protected IEnumerator IDivider()
        {
            yield return 0;
            while(true)
            {
                yield return new WaitForSeconds(interval);
                if(Input.GetMouseButton(0) && Dividable())
                {
                    DivideCells(divideCellsKer, Time.timeSinceLevelLoad);
                    CopyEdges(copyEdgesKer); 
                    Divide(divideKer, Time.timeSinceLevelLoad);
                    UpdatePoolCount();
                }
            }
        }

        protected Mesh BuildLine()
        {
            var mesh = new Mesh();
            mesh.vertices = new Vector3[2] { Vector3.zero, Vector3.up };
            mesh.uv = new Vector2[2] { new Vector2(0f, 0f), new Vector2(0f, 1f) };
            mesh.SetIndices(new int[2] { 0, 1 }, MeshTopology.Lines, 0);
            return mesh;
        }

        protected void SwapBuffer(ref ComputeBuffer buf0, ref ComputeBuffer buf1)
        {
            var tmp = buf0;
            buf0 = buf1;
            buf1 = tmp;
        }

        protected bool UpdatePoolCount()
        {
            var tmp = cellPoolCount;
            cellPoolCount = GetCellPoolCount();
            edgePoolCount = GetEdgePoolCount();
            return tmp != cellPoolCount;
        }

        protected int GetCellPoolCount()
        {
            cellArgsBuffer.SetData(cellArgs);
            ComputeBuffer.CopyCount(cellsPoolBuffer, cellArgsBuffer, 0);
            cellArgsBuffer.GetData(cellArgs);
            return cellArgs[0];
        }

        protected int GetEdgePoolCount()
        {
            edgeArgsBuffer.SetData(edgeArgs);
            ComputeBuffer.CopyCount(edgesPoolBuffer, edgeArgsBuffer, 0);
            edgeArgsBuffer.GetData(edgeArgs);
            return edgeArgs[0];
        }

        #region Cell kernels

        protected void InitCells(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferRead);
            compute.SetBuffer(ker.Index, "_CellPoolAppend", cellsPoolBuffer);
            compute.SetInt("_CellsCount", cellsCount);
            compute.Dispatch(ker.Index, Mathf.FloorToInt(cellsCount / (int)ker.ThreadX) + 1, 1, 1);
        }

        protected void ResetCells(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferRead);
            compute.SetBuffer(ker.Index, "_CellPoolAppend", cellsPoolBuffer);
            compute.SetInt("_CellsCount", cellsCount);
            compute.Dispatch(ker.Index, Mathf.FloorToInt(cellsCount / (int)ker.ThreadX) + 1, 1, 1);
        }

        protected void EmitCells(Kernel ker, int n, int type = 0)
        {
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferRead);
            compute.SetBuffer(ker.Index, "_CellPoolConsume", cellsPoolBuffer);
            compute.SetInt("_EmitCount", n);
            compute.SetInt("_EmitType", type);
            compute.Dispatch(ker.Index, n / (int)ker.ThreadX + 1, 1, 1);
        }

        protected void InteractCells(Kernel ker, int current)
        {
            compute.SetBuffer(ker.Index, "_CellsRead", cellsBufferRead);
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferWrite);
            compute.SetInt("_CellsCount", cellsCount);

            compute.SetBuffer(ker.Index, "_Edges", edgesBufferRead);
            compute.SetInt("_EdgesCount", cellsCount);
            compute.SetBuffer(ker.Index, "_EdgePoolConsume", edgesPoolBuffer);

            compute.Dispatch(ker.Index, Mathf.FloorToInt(cellsCount / (int)ker.ThreadX) + 1, 1, 1);

            SwapBuffer(ref cellsBufferRead, ref cellsBufferWrite);
        }

        protected void UpdateCells(Kernel ker, int current, float dt)
        {
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferRead);
            compute.SetBuffer(ker.Index, "_Edges", edgesBufferRead);
            compute.SetInt("_CellsCount", cellsCount);
            compute.SetInt("_EdgesCount", cellsCount);
            compute.SetFloat("_DT", dt);
            compute.SetFloat("_Limit", limit);
            compute.SetFloat("_Drag", drag);
            compute.SetVector("_Point", GetMousePoint());
            compute.Dispatch(ker.Index, Mathf.FloorToInt(cellsCount / (int)ker.ThreadX) + 1, 1, 1);
        }

        protected bool Dividable ()
        {
            return (dividable && (cellsCount - cellPoolCount) < threshold);
        }

        protected void DivideCells(Kernel ker, float time)
        {
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferRead);
            compute.SetBuffer(ker.Index, "_CellPoolConsume", cellsPoolBuffer);
            compute.SetInt("_CellsCount", cellsCount);

            compute.SetBuffer(ker.Index, "_Edges", edgesBufferRead);
            compute.SetBuffer(ker.Index, "_EdgePoolConsume", edgesPoolBuffer);
            compute.SetInt("_EdgesCount", cellsCount);

            compute.SetVector("_Point", GetMousePoint());
            compute.SetFloat("_Time", time);
            compute.SetFloat("_Rate", rate);
            compute.Dispatch(ker.Index, cellsCount / (int)ker.ThreadX + 1, 1, 1);
        }

        protected void Divide(Kernel ker, float time)
        {
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferRead);
            compute.SetInt("_CellsCount", cellsCount);
            compute.SetBuffer(ker.Index, "_CellPoolConsume", cellsPoolBuffer);

            compute.SetBuffer(ker.Index, "_EdgesRead", edgesBufferRead);
            compute.SetBuffer(ker.Index, "_Edges", edgesBufferWrite);
            compute.SetInt("_EdgesCount", cellsCount);
            compute.SetBuffer(ker.Index, "_EdgePoolConsume", edgesPoolBuffer);

            compute.SetVector("_Point", GetMousePoint());
            compute.SetFloat("_Time", time);
            compute.SetFloat("_Rate", rate);
            compute.Dispatch(ker.Index, Mathf.FloorToInt(cellsCount / (int)ker.ThreadX) + 1, 1, 1);

            SwapBuffer(ref edgesBufferRead, ref edgesBufferWrite);
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
            compute.SetBuffer(ker.Index, "_Edges", edgesBufferRead);
            compute.SetBuffer(ker.Index, "_EdgePoolAppend", edgesPoolBuffer);
            compute.SetInt("_EdgesCount", cellsCount);
            compute.Dispatch(ker.Index, cellsCount / (int)ker.ThreadX + 1, 1, 1);
        }

        protected void ResetEdges(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_Edges", edgesBufferRead);
            compute.SetBuffer(ker.Index, "_EdgePoolAppend", edgesPoolBuffer);
            compute.SetInt("_EdgesCount", cellsCount);
            compute.Dispatch(ker.Index, cellsCount / (int)ker.ThreadX + 1, 1, 1);
        }

        protected void UpdateEdges(Kernel ker, float dt)
        {
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferRead);
            compute.SetBuffer(ker.Index, "_Edges", edgesBufferRead);
            compute.SetInt("_EdgesCount", cellsCount);
            compute.SetFloat("_DT", dt);
            compute.Dispatch(ker.Index, cellsCount / (int)ker.ThreadX + 1, 1, 1);
        }

        protected void RemoveEdges(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferRead);
            compute.SetBuffer(ker.Index, "_Edges", edgesBufferRead);
            compute.SetBuffer(ker.Index, "_EdgePoolAppend", edgesPoolBuffer);
            compute.SetInt("_EdgesCount", cellsCount);
            compute.Dispatch(ker.Index, cellsCount / (int)ker.ThreadX + 1, 1, 1);
        }

        protected void CopyEdges(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_EdgesRead", edgesBufferRead);
            compute.SetBuffer(ker.Index, "_Edges", edgesBufferWrite);
            compute.SetInt("_EdgesCount", cellsCount);
            compute.Dispatch(ker.Index, cellsCount / (int)ker.ThreadX + 1, 1, 1);

            SwapBuffer(ref edgesBufferRead, ref edgesBufferWrite);
        }

        #endregion

        #region Remove cells

        public void Hunt(Predators predators)
        {
            var ker = huntKer;
            compute.SetBuffer(ker.Index, "_Predators", predators.PredatorsBuffer);
            compute.SetInt("_PredatorsCount", predators.PredatorsBuffer.count);
            RemoveCells(ker);
        }

        public void RemoveCellsCircle(Vector2 center, float radius)
        {
            compute.SetVector("_Center", center);
            compute.SetFloat("_Radius", radius);
            RemoveCells(removeCellsCircleKer);
        }

        public void RemoveCellsLine(Vector2 start, Vector2 end, float threshold)
        {
            compute.SetVector("_Start", start);
            compute.SetVector("_End", end);
            compute.SetFloat("_Threshold", threshold);
            RemoveCells(removeCellsLineKer);
        }

        public void RemoveCells(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferRead);
            compute.SetBuffer(ker.Index, "_CellPoolAppend", cellsPoolBuffer);
            compute.SetInt("_CellsCount", cellsCount);

            compute.SetBuffer(ker.Index, "_Edges", edgesBufferRead);
            compute.SetInt("_EdgesCount", cellsCount);
            compute.SetBuffer(ker.Index, "_EdgePoolAppend", edgesPoolBuffer);

            compute.Dispatch(ker.Index, cellsCount / (int)ker.ThreadX + 1, 1, 1);
        }

        #endregion

        #region Membrane

        public void Wrap(Membrane mem, float dt)
        {
            var ker = wrapKer;
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferRead);
            compute.SetInt("_CellsCount", cellsCount);

            compute.SetBuffer(ker.Index, "_MembraneNodes", mem.NodesBuffer);
            compute.SetBuffer(ker.Index, "_MembraneEdges", mem.EdgesBuffer);
            compute.SetInt("_MembraneNodesCount", mem.NodesBuffer.count);

            compute.SetFloat("_DT", dt);
            compute.SetFloat("_Tension", mem.Tension);
            compute.Dispatch(ker.Index, cellsCount / (int)ker.ThreadX + 1, 1, 1);
        }

        #endregion

        protected Texture2D CreateGradient(Gradient grad, int size = 128)
        {
            var tex = new Texture2D(size, 1, TextureFormat.RGBAFloat, false);
            tex.wrapMode = TextureWrapMode.Clamp;
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


