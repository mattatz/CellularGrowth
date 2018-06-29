using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace CellularGrowth.Dim3
{

    public class CellularGrowth : MonoBehaviour {

        [SerializeField] protected GUISkin skin;
        [SerializeField] protected Mesh mesh;
        [SerializeField] protected int cellsCount = 2 << 15;
        [SerializeField] protected ComputeShader compute;

        [SerializeField] protected Material cellMaterial, edgeMaterial, faceMaterial;
        [SerializeField] protected bool drawCell = false, drawEdge = false, drawFace = true;
        [SerializeField] protected ShadowCastingMode casting = ShadowCastingMode.On;
        [SerializeField] protected bool receiveShadow = true;

        [SerializeField] protected bool dividable;
        [SerializeField] protected int threshold = 8000;
        [SerializeField, Range(0.0f, 1.0f)] protected float rate = 0.5f;
        [SerializeField, Range(1, 80)] protected int frequency = 30;
        [SerializeField, Range(7, 12)] protected int maxLink = 8;
        [SerializeField, Range(2, 10)] protected int maxInterval = 8;

        [SerializeField, Range(1f, 20f)] protected float growSpeed = 5f;

        [SerializeField, Range(1f, 10f)] protected float limit = 3.0f;
        [SerializeField, Range(0.5f, 0.99f)] protected float drag = 0.9f;
        [SerializeField, Range(1f, 10f)] protected float distance = 3f;

        protected ComputeBuffer cellsBufferRead, cellsBufferWrite, cellsPoolBuffer, cellArgsBuffer;
        protected int[] cellArgs = new int[] { 0, 1, 0, 0 };

        protected int edgesCount;
        protected ComputeBuffer edgesBufferRead, edgesBufferWrite, edgesPoolBuffer, edgeArgsBuffer;
        protected int[] edgeArgs = new int[] { 0, 1, 0, 0 };

        protected int facesCount;
        protected ComputeBuffer facesBufferRead, facesBufferWrite, facesPoolBuffer, faceArgsBuffer;
        protected int[] faceArgs = new int[] { 0, 1, 0, 0 };

        protected ComputeBuffer drawCellArgsBuffer;
        protected uint[] drawCellArgs = new uint[5] { 0, 0, 0, 0, 0 };

        protected Mesh line;
        protected ComputeBuffer drawEdgeArgsBuffer;
        protected uint[] drawEdgeArgs = new uint[5] { 0, 0, 0, 0, 0 };

        protected Mesh triangle;
        protected ComputeBuffer drawFaceArgsBuffer;
        protected uint[] drawFaceArgs = new uint[5] { 0, 0, 0, 0, 0 };

        protected int cellPoolCount = 0, edgePoolCount = 0, facePoolCount = 0;

        #region Kernels

        protected enum KernelType
        {
            InitCells,
            ResetCells,
            InteractCells,
            UpdateCells,
            CopyCells,

            InitEdges,
            ResetEdges,
            UpdateEdges, 
            RemoveEdges,
            CopyEdges,

            InitFaces,
            ResetFaces,
            RemoveFaces,
            CopyFaces,

            Activate,
            Check,
            Divide,
        };

        protected Dictionary<KernelType, Kernel> kernels;

        #endregion

        #region Monobehaviour functions

        protected virtual void Start()
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

            edgesCount = cellsCount * 3;
            edgesBufferRead = new ComputeBuffer(edgesCount, Marshal.SizeOf(typeof(Edge)), ComputeBufferType.Default);
            edgesBufferWrite = new ComputeBuffer(edgesCount, Marshal.SizeOf(typeof(Edge)), ComputeBufferType.Default);

            edgesPoolBuffer = new ComputeBuffer(edgesCount, Marshal.SizeOf(typeof(int)), ComputeBufferType.Append);
            edgesPoolBuffer.SetCounterValue(0);
            edgeArgsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
            edgeArgsBuffer.SetData(edgeArgs);

            line = BuildLine();
            drawEdgeArgs[0] = line.GetIndexCount(0);
            drawEdgeArgs[1] = (uint)edgesCount;
            drawEdgeArgsBuffer = new ComputeBuffer(1, sizeof(uint) * drawEdgeArgs.Length, ComputeBufferType.IndirectArguments);
            drawEdgeArgsBuffer.SetData(drawEdgeArgs);

            facesCount = cellsCount * 2;
            facesBufferRead = new ComputeBuffer(facesCount, Marshal.SizeOf(typeof(Face)), ComputeBufferType.Default);
            facesBufferWrite = new ComputeBuffer(facesCount, Marshal.SizeOf(typeof(Face)), ComputeBufferType.Default);

            facesPoolBuffer = new ComputeBuffer(facesCount, Marshal.SizeOf(typeof(int)), ComputeBufferType.Append);
            facesPoolBuffer.SetCounterValue(0);
            faceArgsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
            faceArgsBuffer.SetData(faceArgs);

            triangle = BuildTriangle();
            drawFaceArgs[0] = triangle.GetIndexCount(0);
            drawFaceArgs[1] = (uint)facesCount;
            drawFaceArgsBuffer = new ComputeBuffer(1, sizeof(uint) * drawFaceArgs.Length, ComputeBufferType.IndirectArguments);
            drawFaceArgsBuffer.SetData(drawFaceArgs);

            kernels = new Dictionary<KernelType, Kernel>();
            foreach(var key in Enum.GetNames(typeof(KernelType)))
            {
                kernels.Add((KernelType)Enum.Parse(typeof(KernelType), key, true), new Kernel(compute, key));
            }

            InitCells(kernels[KernelType.InitCells]);
            InitEdges(kernels[KernelType.InitEdges]);
            InitFaces(kernels[KernelType.InitFaces]);
            UpdatePoolCount();

            Setup();
        }

        protected virtual void Setup()
        {
            InitTetrahedron();
            // InitHexahedron();
            UpdatePoolCount();
        }

        protected virtual void Update()
        {
            var dt = Mathf.Min(Time.deltaTime, 0.05f);
            var t = Time.timeSinceLevelLoad;                

            UpdateEdges(kernels[KernelType.UpdateEdges], dt);
            InteractCells(kernels[KernelType.InteractCells], cellPoolCount);
            UpdateCells(kernels[KernelType.UpdateCells], cellPoolCount, dt);

            if(Time.frameCount % frequency == 0 && Dividable()) Divide();

            Render();
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

            facesBufferRead.Release();
            facesBufferWrite.Release();
            facesPoolBuffer.Release();
            faceArgsBuffer.Release();

            drawCellArgsBuffer.Release();
            drawEdgeArgsBuffer.Release();
            drawFaceArgsBuffer.Release();
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
                    GUILayout.Label("cells: " + (cellsCount - cellPoolCount).ToString() + "/" + cellsCount.ToString());
                    // GUILayout.Label("edges: " + (edgesCount - edgePoolCount).ToString() + "/" + edgesCount.ToString());
                    // GUILayout.Label("faces: " + (facesCount - facePoolCount).ToString() + "/" + facesCount.ToString());

                    drawCell = GUILayout.Toggle(drawCell, "draw cell");
                    drawEdge = GUILayout.Toggle(drawEdge, "draw edge");
                    drawFace = GUILayout.Toggle(drawFace, "draw face");

                    GUILayout.Space(5f);

                    dividable = GUILayout.Toggle(dividable, "dividable");

                    GUILayout.Space(4f);

                    using(new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("rate: " + rate.ToString("0.00"), GUILayout.Width(60f));
                        rate = GUILayout.HorizontalSlider(rate, 0.25f, 0.75f, GUILayout.Width(100f));
                    }

                    using(new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("frequency: " + frequency, GUILayout.Width(60f));
                        frequency = Mathf.FloorToInt(GUILayout.HorizontalSlider(frequency, 1, 50, GUILayout.Width(100f)));
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

            if(drawFace)
            {
                faceMaterial.SetBuffer("_Cells", cellsBufferRead);
                faceMaterial.SetBuffer("_Edges", edgesBufferRead);
                faceMaterial.SetBuffer("_Faces", facesBufferRead);
                faceMaterial.SetMatrix("_World2Local", transform.worldToLocalMatrix);
                faceMaterial.SetMatrix("_Local2World", transform.localToWorldMatrix);
                faceMaterial.SetPass(0);
                Graphics.DrawMeshInstancedIndirect(triangle, 0, faceMaterial, new Bounds(Vector3.zero, Vector3.one * 100f), drawFaceArgsBuffer, 0, null, casting, receiveShadow);
            }
        }

        protected virtual void Reset()
        {
            ResetCells(kernels[KernelType.ResetCells]);
            ResetEdges(kernels[KernelType.ResetEdges]);
            ResetFaces(kernels[KernelType.ResetFaces]);

            CopyCells(kernels[KernelType.CopyCells]);
            CopyEdges(kernels[KernelType.CopyEdges]);
            CopyFaces(kernels[KernelType.CopyFaces]);

            UpdatePoolCount();

            InitTetrahedron();
            UpdatePoolCount();
        }

        protected IEnumerator IDivider(float interval)
        {
            yield return 0;
            while(true)
            {
                yield return new WaitForSeconds(interval);
                // if(Input.GetMouseButton(0) && Dividable())
                if(Dividable())
                {
                    Divide();
                }
            }
        }

        public void Divide()
        {
            Activate(kernels[KernelType.Activate], Time.timeSinceLevelLoad);

            CopyCells(kernels[KernelType.CopyCells]);
            Check(kernels[KernelType.Check], Time.timeSinceLevelLoad);

            CopyCells(kernels[KernelType.CopyCells]);
            CopyEdges(kernels[KernelType.CopyEdges]);
            CopyFaces(kernels[KernelType.CopyFaces]);
            Divide(kernels[KernelType.Divide], Time.timeSinceLevelLoad);

            RemoveEdges(kernels[KernelType.RemoveEdges]);
            RemoveFaces(kernels[KernelType.RemoveFaces]);
            UpdatePoolCount();
        }

        protected Mesh BuildLine()
        {
            var mesh = new Mesh();
            mesh.vertices = new Vector3[2] { Vector3.zero, Vector3.up };
            mesh.uv = new Vector2[2] { new Vector2(0f, 0f), new Vector2(0f, 1f) };
            mesh.SetIndices(new int[2] { 0, 1 }, MeshTopology.Lines, 0);
            return mesh;
        }

        protected Mesh BuildTriangle()
        {
            var mesh = new Mesh();
            mesh.vertices = new Vector3[3];
            mesh.uv = new Vector2[3];
            mesh.SetIndices(new int[3] { 0, 1, 2 }, MeshTopology.Triangles, 0);
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
            // edgePoolCount = GetEdgePoolCount();
            // facePoolCount = GetFacePoolCount();
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

        protected int GetFacePoolCount()
        {
            faceArgsBuffer.SetData(faceArgs);
            ComputeBuffer.CopyCount(facesPoolBuffer, faceArgsBuffer, 0);
            faceArgsBuffer.GetData(faceArgs);
            return faceArgs[0];
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

        protected void InteractCells(Kernel ker, int current)
        {
            compute.SetBuffer(ker.Index, "_CellsRead", cellsBufferRead);
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferWrite);
            compute.SetInt("_CellsCount", cellsCount);

            compute.SetBuffer(ker.Index, "_Edges", edgesBufferRead);
            compute.SetInt("_EdgesCount", edgesCount);
            compute.SetBuffer(ker.Index, "_EdgePoolConsume", edgesPoolBuffer);

            compute.Dispatch(ker.Index, Mathf.FloorToInt(cellsCount / (int)ker.ThreadX) + 1, 1, 1);

            SwapBuffer(ref cellsBufferRead, ref cellsBufferWrite);
        }

        protected void UpdateCells(Kernel ker, int current, float dt)
        {
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferRead);
            compute.SetBuffer(ker.Index, "_Edges", edgesBufferRead);
            compute.SetBuffer(ker.Index, "_Faces", facesBufferRead);
            compute.SetInt("_CellsCount", cellsCount);
            compute.SetInt("_EdgesCount", edgesCount);
            compute.SetInt("_FacesCount", facesCount);
            compute.SetFloat("_DT", dt);
            compute.SetFloat("_GrowSpeed", growSpeed);
            compute.SetFloat("_Limit", limit);
            compute.SetFloat("_Drag", drag);

            compute.SetVector("_Point", GetMousePoint());
            compute.SetVector("_ViewDirection", Vector3.forward);

            compute.Dispatch(ker.Index, Mathf.FloorToInt(cellsCount / (int)ker.ThreadX) + 1, 1, 1);
        }

        protected bool Dividable ()
        {
            return (dividable && (cellsCount - cellPoolCount) < threshold);
        }

        protected void CopyCells(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_CellsRead", cellsBufferRead);
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferWrite);
            compute.SetInt("_CellsCount", cellsCount);
            compute.Dispatch(ker.Index, cellsCount / (int)ker.ThreadX + 1, 1, 1);

            SwapBuffer(ref cellsBufferRead, ref cellsBufferWrite);
        }

        protected void Activate(Kernel ker, float time)
        {
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferRead);
            compute.SetInt("_CellsCount", cellsCount);

            compute.SetInt("_MaxLink", maxLink);
            compute.SetInt("_MaxInterval", maxInterval);
            compute.SetFloat("_Time", time);
            compute.SetFloat("_Rate", rate);
            compute.Dispatch(ker.Index, Mathf.FloorToInt(cellsCount / (int)ker.ThreadX) + 1, 1, 1);
        }

        protected void Check(Kernel ker, float time)
        {
            compute.SetBuffer(ker.Index, "_CellsRead", cellsBufferRead);
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferWrite);
            compute.SetInt("_CellsCount", cellsCount);

            compute.SetBuffer(ker.Index, "_Edges", edgesBufferRead);
            compute.SetInt("_EdgesCount", edgesCount);

            compute.SetFloat("_Time", time);
            compute.Dispatch(ker.Index, Mathf.FloorToInt(edgesCount / (int)ker.ThreadX) + 1, 1, 1);

            SwapBuffer(ref cellsBufferRead, ref cellsBufferWrite);
        }

        protected void Divide(Kernel ker, float time)
        {
            compute.SetBuffer(ker.Index, "_CellsRead", cellsBufferRead);
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferWrite);
            compute.SetInt("_CellsCount", cellsCount);
            compute.SetFloat("_InvCellsCount", 1f / cellsCount);
            compute.SetBuffer(ker.Index, "_CellPoolConsume", cellsPoolBuffer);

            compute.SetBuffer(ker.Index, "_EdgesRead", edgesBufferRead);
            compute.SetBuffer(ker.Index, "_Edges", edgesBufferWrite);
            compute.SetInt("_EdgesCount", edgesCount);
            compute.SetBuffer(ker.Index, "_EdgePoolConsume", edgesPoolBuffer);

            compute.SetBuffer(ker.Index, "_FacesRead", facesBufferRead);
            compute.SetBuffer(ker.Index, "_Faces", facesBufferWrite);
            compute.SetInt("_FacesCount", facesCount);
            compute.SetBuffer(ker.Index, "_FacePoolConsume", facesPoolBuffer);

            compute.SetFloat("_Time", time);
            compute.Dispatch(ker.Index, Mathf.FloorToInt(cellsCount / (int)ker.ThreadX) + 1, 1, 1);

            compute.SetInt("_MaxLink", maxLink);
            compute.SetInt("_MaxInterval", maxInterval);

            SwapBuffer(ref cellsBufferRead, ref cellsBufferWrite);
            SwapBuffer(ref edgesBufferRead, ref edgesBufferWrite);
            SwapBuffer(ref facesBufferRead, ref facesBufferWrite);
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
            compute.SetInt("_EdgesCount", edgesCount);
            compute.Dispatch(ker.Index, edgesCount / (int)ker.ThreadX + 1, 1, 1);
        }

        protected void ResetEdges(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_Edges", edgesBufferRead);
            compute.SetBuffer(ker.Index, "_EdgePoolAppend", edgesPoolBuffer);
            compute.SetInt("_EdgesCount", edgesCount);
            compute.Dispatch(ker.Index, edgesCount / (int)ker.ThreadX + 1, 1, 1);
        }

        protected void UpdateEdges(Kernel ker, float dt)
        {
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferRead);
            compute.SetBuffer(ker.Index, "_Edges", edgesBufferRead);
            compute.SetInt("_EdgesCount", edgesCount);
            compute.SetFloat("_DT", dt);
            compute.Dispatch(ker.Index, edgesCount / (int)ker.ThreadX + 1, 1, 1);
        }

        protected void RemoveEdges(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_Edges", edgesBufferRead);
            compute.SetBuffer(ker.Index, "_EdgePoolAppend", edgesPoolBuffer);
            compute.SetInt("_EdgesCount", edgesCount);
            compute.Dispatch(ker.Index, edgesCount / (int)ker.ThreadX + 1, 1, 1);
        }

        protected void CopyEdges(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_EdgesRead", edgesBufferRead);
            compute.SetBuffer(ker.Index, "_Edges", edgesBufferWrite);
            compute.SetInt("_EdgesCount", edgesCount);
            compute.Dispatch(ker.Index, edgesCount / (int)ker.ThreadX + 1, 1, 1);

            SwapBuffer(ref edgesBufferRead, ref edgesBufferWrite);
        }

        #endregion

        #region Face kernels

        protected void InitFaces(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_Faces", facesBufferRead);
            compute.SetBuffer(ker.Index, "_FacePoolAppend", facesPoolBuffer);
            compute.SetInt("_FacesCount", facesCount);
            compute.Dispatch(ker.Index, facesCount / (int)ker.ThreadX + 1, 1, 1);
        }

        protected void ResetFaces(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_Faces", facesBufferRead);
            compute.SetBuffer(ker.Index, "_FacePoolAppend", facesPoolBuffer);
            compute.SetInt("_FacesCount", facesCount);
            compute.Dispatch(ker.Index, facesCount / (int)ker.ThreadX + 1, 1, 1);
        }

        protected void RemoveFaces(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_Faces", facesBufferRead);
            compute.SetBuffer(ker.Index, "_FacePoolAppend", facesPoolBuffer);
            compute.SetInt("_FacesCount", facesCount);
            compute.Dispatch(ker.Index, facesCount / (int)ker.ThreadX + 1, 1, 1);
        }

        protected void CopyFaces(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_FacesRead", facesBufferRead);
            compute.SetBuffer(ker.Index, "_Faces", facesBufferWrite);
            compute.SetInt("_FacesCount", facesCount);
            compute.Dispatch(ker.Index, facesCount / (int)ker.ThreadX + 1, 1, 1);

            SwapBuffer(ref facesBufferRead, ref facesBufferWrite);
        }

        #endregion

        protected void InitTetrahedron()
        {
            var ker = new Kernel(compute, "InitTetrahedron");
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferRead);
            compute.SetBuffer(ker.Index, "_CellPoolConsume", cellsPoolBuffer);
            compute.SetBuffer(ker.Index, "_Edges", edgesBufferRead);
            compute.SetBuffer(ker.Index, "_EdgePoolConsume", edgesPoolBuffer);
            compute.SetBuffer(ker.Index, "_Faces", facesBufferRead);
            compute.SetBuffer(ker.Index, "_FacePoolConsume", facesPoolBuffer);
            compute.Dispatch(ker.Index, 1, 1, 1);
        }

        protected void InitHexahedron()
        {
            var ker = new Kernel(compute, "InitHexahedron");
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferRead);
            compute.SetBuffer(ker.Index, "_CellPoolConsume", cellsPoolBuffer);
            compute.SetBuffer(ker.Index, "_Edges", edgesBufferRead);
            compute.SetBuffer(ker.Index, "_EdgePoolConsume", edgesPoolBuffer);
            compute.SetBuffer(ker.Index, "_Faces", facesBufferRead);
            compute.SetBuffer(ker.Index, "_FacePoolConsume", facesPoolBuffer);
            compute.Dispatch(ker.Index, 1, 1, 1);
        }

        #region Remove cells

        public void RemoveCells(Kernel ker)
        {
            compute.SetBuffer(ker.Index, "_Cells", cellsBufferRead);
            compute.SetBuffer(ker.Index, "_CellPoolAppend", cellsPoolBuffer);
            compute.SetInt("_CellsCount", cellsCount);

            compute.SetBuffer(ker.Index, "_Edges", edgesBufferRead);
            compute.SetInt("_EdgesCount", edgesCount);
            compute.SetBuffer(ker.Index, "_EdgePoolAppend", edgesPoolBuffer);

            compute.Dispatch(ker.Index, cellsCount / (int)ker.ThreadX + 1, 1, 1);
        }

        #endregion

    }

}



