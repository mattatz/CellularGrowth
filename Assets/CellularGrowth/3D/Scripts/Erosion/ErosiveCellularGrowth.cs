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

    public class ErosiveCellularGrowth : CellularGrowth {

        [SerializeField] protected Mesh seed;
        [SerializeField] protected Vector3 seedScale = new Vector3(50f, 50f, 50f);

        protected override void Update()
        {
            base.Update();
        }

        protected override void Setup()
        {
            InitMesh(seed, seedScale);
            UpdatePoolCount();
        }

        protected override void Reset()
        {
            ResetCells(kernels[KernelType.ResetCells]);
            ResetEdges(kernels[KernelType.ResetEdges]);
            ResetFaces(kernels[KernelType.ResetFaces]);

            CopyCells(kernels[KernelType.CopyCells]);
            CopyEdges(kernels[KernelType.CopyEdges]);
            CopyFaces(kernels[KernelType.CopyFaces]);

            UpdatePoolCount();

            InitMesh(seed, seedScale);
            UpdatePoolCount();
        }

        protected void InitMesh(Mesh mesh, Vector3 scale)
        {
            var ker = new Kernel(compute, "InitMesh");

            compute.SetBuffer(ker.Index, "_Cells", cellsBufferRead);
            compute.SetBuffer(ker.Index, "_CellPoolConsume", cellsPoolBuffer);
            compute.SetBuffer(ker.Index, "_Edges", edgesBufferRead);
            compute.SetBuffer(ker.Index, "_EdgePoolConsume", edgesPoolBuffer);
            compute.SetBuffer(ker.Index, "_Faces", facesBufferRead);
            compute.SetBuffer(ker.Index, "_FacePoolConsume", facesPoolBuffer);

            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var triCount = triangles.Length;

            var seedVertices = new List<SeedVertex>();
            var seedEdges = new List<SeedEdge>();
            var seedFaces = new List<SeedFace>();

            for(int i = 0, n = mesh.vertexCount; i < n; i++)
            {
                var v = vertices[i];
                seedVertices.Add(new SeedVertex()
                {
                    position = Vector3.Scale(v, scale)
                });
            }

            for(int i = 0; i < triCount; i += 3)
            {
                int c0 = triangles[i];
                int c1 = triangles[i + 1];
                int c2 = triangles[i + 2];

                int e0 = SearchEdge(c0, c1, seedEdges);
                int e1 = SearchEdge(c1, c2, seedEdges);
                int e2 = SearchEdge(c2, c0, seedEdges);

                seedFaces.Add(new SeedFace()
                {
                    c0 = c0, c1 = c1, c2 = c2,
                    e0 = e0, e1 = e1, e2 = e2
                });
            }

            for(int i = 0, n = seedVertices.Count; i < n; i++)
            {
                var v = seedVertices[i];
                int count = 0;

                var radius = 0f;
                for(int j = 0, m = seedEdges.Count; j < m; j++)
                {
                    var e = seedEdges[j];
                    if(e.Contains(i))
                    {
                        radius += Vector3.Distance(seedVertices[e.a].position, seedVertices[e.b].position) * 0.5f;
                        count++;
                    }
                }
                v.radius = radius / count;
                seedVertices[i] = v;
            }

            var seedVerticesBuffer = new ComputeBuffer(mesh.vertexCount, Marshal.SizeOf(typeof(SeedVertex)));
            seedVerticesBuffer.SetData(seedVertices.ToArray());

            var seedEdgesBuffer = new ComputeBuffer(seedEdges.Count, Marshal.SizeOf(typeof(SeedEdge)));
            seedEdgesBuffer.SetData(seedEdges.ToArray());

            var seedFacesBuffer = new ComputeBuffer(seedFaces.Count, Marshal.SizeOf(typeof(SeedFace)));
            seedFacesBuffer.SetData(seedFaces.ToArray());

            compute.SetBuffer(ker.Index, "_SeedVertices", seedVerticesBuffer);
            compute.SetBuffer(ker.Index, "_SeedEdges", seedEdgesBuffer);
            compute.SetBuffer(ker.Index, "_SeedFaces", seedFacesBuffer);
            compute.SetInt("_SeedVerticesCount", seedVerticesBuffer.count);
            compute.SetInt("_SeedEdgesCount", seedEdgesBuffer.count);
            compute.SetInt("_SeedFacesCount", seedFacesBuffer.count);
            compute.Dispatch(ker.Index, 1, 1, 1);

            seedVerticesBuffer.Release();
            seedEdgesBuffer.Release();
            seedFacesBuffer.Release();
        }

        protected int SearchEdge(int a, int b, List<SeedEdge> seedEdges)
        {
            for(int i = 0, n = seedEdges.Count; i < n; i++)
            {
                var e = seedEdges[i];
                if(
                    (e.a == a && e.b == b) ||
                    (e.a == b && e.b == a)
                )
                {
                    return i;
                }
            }

            var ne = new SeedEdge() {
                a = a,
                b = b
            };
            seedEdges.Add(ne);
            return seedEdges.Count - 1;
        }

    }

}


