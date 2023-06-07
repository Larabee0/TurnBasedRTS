using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
public struct MeshConnectivityBuilder
{
    const float k_MergeCoplanarTrianglesTolerance = 1e-4f;

    public Vertex[] Vertices;
    public Triangle[] Triangles;
    public Edge[] Edges;

    /// Vertex.
    public struct Vertex
    {
        /// Number of triangles referencing this vertex, or, equivalently, number of edge starting from this vertex.
        public int Cardinality;

        /// true if the vertex is on the boundary, false otherwise.
        /// Note: if true the first edge of the ring is naked.
        /// Conditions: number of naked edges in the 1-ring is greater than 0.
        public bool Boundary;

        /// true if the vertex is on the border, false otherwise.
        /// Note: if true the first edge of the ring is naked.
        /// Conditions: number of naked edges in the 1-ring is equal to 1.
        public bool Border;

        /// true is the vertex 1-ring is manifold.
        /// Conditions: number of naked edges in the 1-ring is less than 2 and cardinality is greater than 0.
        public bool Manifold;

        /// Index of the first edge.
        public int FirstEdge;
    }

    /// (Half) Edge.
    public struct Edge
    {
        public static Edge Invalid() => new() { IsValid = false };

        // Triangle index
        public int Triangle;

        // Starting vertex index
        public int Start;

        public bool IsValid;
    }

    public struct Triangle
    {
        public void Clear()
        {
            IsValid = false;
            Edge0.IsValid = false;
            Edge1.IsValid = false;
            Edge2.IsValid = false;
        }

        // Broken up rather than an array because we need native containers of triangles elsewhere in the code, and
        // nested native containers aren't supported.
        public Edge Edge0;
        public Edge Edge1;
        public Edge Edge2;

        public Edge Links(int edge)
        {
            MeshColliderCreate.CheckIndexAndThrow(edge, 3);
            return edge switch
            {
                0 => Edge0,
                1 => Edge1,
                2 => Edge2,
                _ => default,
            };
        }

        public void SetLinks(int edge, Edge newEdge)
        {
            MeshColliderCreate.CheckIndexAndThrow(edge, 3);
            switch (edge)
            {
                case 0:
                    Edge0 = newEdge;
                    break;
                case 1:
                    Edge1 = newEdge;
                    break;
                case 2:
                    Edge2 = newEdge;
                    break;
            }
        }

        internal bool IsValid;
    }

    public static float3[] WeldVertices(int3[] indices, float3[] vertices)
    {
        int numVertices = vertices.Length;
        var verticesAndHashes = new VertexWithHash[numVertices];
        for (int i = 0; i < numVertices; i++)
        {
            verticesAndHashes[i] = new VertexWithHash()
            {
                Index = i,
                Vertex = vertices[i],
                Hash = SpatialHash(vertices[i])
            };
        }

        var uniqueVertices = new List<float3>();
        var remap = new int[numVertices];
        Array.Sort(verticesAndHashes, new SortVertexWithHashByHash());

        for (int i = 0; i < numVertices; i++)
        {
            if (verticesAndHashes[i].Index == int.MaxValue)
            {
                continue;
            }

            uniqueVertices.Add(vertices[verticesAndHashes[i].Index]);
            remap[verticesAndHashes[i].Index] = uniqueVertices.Count - 1;

            for (int j = i + 1; j < numVertices; j++)
            {
                if (verticesAndHashes[j].Index == int.MaxValue)
                {
                    continue;
                }

                if (verticesAndHashes[i].Hash == verticesAndHashes[j].Hash)
                {
                    if (verticesAndHashes[i].Vertex.x == verticesAndHashes[j].Vertex.x &&
                        verticesAndHashes[i].Vertex.y == verticesAndHashes[j].Vertex.y &&
                        verticesAndHashes[i].Vertex.z == verticesAndHashes[j].Vertex.z)
                    {
                        remap[verticesAndHashes[j].Index] = remap[verticesAndHashes[i].Index];

                        verticesAndHashes[j] = new VertexWithHash()
                        {
                            Index = int.MaxValue,
                            Vertex = verticesAndHashes[j].Vertex,
                            Hash = verticesAndHashes[j].Hash
                        };
                    }
                }
                else
                {
                    break;
                }
            }
        }

        for (int i = 0; i < indices.Length; i++)
        {
            int3 tri = indices[i];
            tri.x = remap[tri.x];
            tri.y = remap[tri.y];
            tri.z = remap[tri.z];
            indices[i] = tri;
        }

        return uniqueVertices.ToArray();
    }
    private static ulong SpatialHash(float3 vertex)
    {
        uint x, y, z;
        unsafe
        {
            float* tmp = &vertex.x;
            x = *((uint*)tmp);

            tmp = &vertex.y;
            y = *((uint*)tmp);

            tmp = &vertex.z;
            z = *((uint*)tmp);
        }

        const ulong p1 = 73856093;
        const ulong p2 = 19349663;
        const ulong p3 = 83492791;

        return (x * p1) ^ (y * p2) ^ (z * p3);
    }

    private struct SortVertexWithHashByHash : IComparer<VertexWithHash>
    {
        public int Compare(VertexWithHash x, VertexWithHash y)
        {
            return x.Hash.CompareTo(y.Hash);
        }
    }

    private struct VertexWithHash
    {
        internal float3 Vertex;
        internal ulong Hash;
        internal int Index;
    }
}
