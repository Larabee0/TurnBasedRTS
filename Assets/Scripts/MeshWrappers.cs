using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Rendering;
using System.Runtime.CompilerServices;

public static class MeshWrapperExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddTriangleInfo(this MeshData mesh, float3 v1, float3 v2, float3 v3, float3 indices, float4 weights1, float4 weights2, float4 weights3)
    {
        uint vertexIndex = mesh.VertexIndex;
        mesh.verticesInternalTri[0] = HexMetrics.Perturb(mesh.noiseColours, v1, mesh.wrapSize);
        mesh.verticesInternalTri[1] = HexMetrics.Perturb(mesh.noiseColours, v2, mesh.wrapSize);
        mesh.verticesInternalTri[2] = HexMetrics.Perturb(mesh.noiseColours, v3, mesh.wrapSize);
        AddTriangle(mesh, vertexIndex);
        AddCellIndicesTriangle(mesh, indices);
        AddCellWeightsTriangle(mesh, weights1, weights2, weights3);
        mesh.ApplyTriangle();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddTriangleInfoUnperturbed(this MeshData mesh, float3 v1, float3 v2, float3 v3, float3 indices, float4 weights1, float4 weights2, float4 weights3)
    {
        uint vertexIndex = mesh.VertexIndex;
        mesh.verticesInternalTri[0] = v1;
        mesh.verticesInternalTri[1] = v2;
        mesh.verticesInternalTri[2] = v3;
        AddTriangle(mesh, vertexIndex);
        AddCellIndicesTriangle(mesh, indices);
        AddCellWeightsTriangle(mesh, weights1, weights2, weights3);
        mesh.ApplyTriangle();
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddQuadInfo(this MeshData mesh, float3 v1, float3 v2, float3 v3, float3 v4, float3 indices, float4 weights1, float4 weights2, float4 weights3, float4 weights4)
    {
        uint vertexIndex = mesh.VertexIndex;
        mesh.verticesInternalQuad[0] = HexMetrics.Perturb(mesh.noiseColours, v1, mesh.wrapSize);
        mesh.verticesInternalQuad[1] = HexMetrics.Perturb(mesh.noiseColours, v2, mesh.wrapSize);
        mesh.verticesInternalQuad[2] = HexMetrics.Perturb(mesh.noiseColours, v3, mesh.wrapSize);
        mesh.verticesInternalQuad[3] = HexMetrics.Perturb(mesh.noiseColours, v4, mesh.wrapSize);
        mesh.trianglesInternalQuad[0] = vertexIndex;
        mesh.trianglesInternalQuad[1] = vertexIndex + 2;
        mesh.trianglesInternalQuad[2] = vertexIndex + 1;
        mesh.trianglesInternalQuad[3] = vertexIndex + 1;
        mesh.trianglesInternalQuad[4] = vertexIndex + 2;
        mesh.trianglesInternalQuad[5] = vertexIndex + 3;
        mesh.cellIndicesInternalQuad[0] = indices;
        mesh.cellIndicesInternalQuad[1] = indices;
        mesh.cellIndicesInternalQuad[2] = indices;
        mesh.cellIndicesInternalQuad[3] = indices;
        mesh.cellWeightsInternalQuad[0] = weights1;
        mesh.cellWeightsInternalQuad[1] = weights2;
        mesh.cellWeightsInternalQuad[2] = weights3;
        mesh.cellWeightsInternalQuad[3] = weights4;
        mesh.ApplyQuad();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddTriangle(this MeshData mesh, uint vertexIndex)
    {
        mesh.trianglesInternalTri[0] = vertexIndex;
        mesh.trianglesInternalTri[1] = vertexIndex + 1;
        mesh.trianglesInternalTri[2] = vertexIndex + 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddCellIndicesTriangle(this MeshData mesh, float3 indices)
    {
        mesh.cellIndicesInternalTri[0] = indices;
        mesh.cellIndicesInternalTri[1] = indices;
        mesh.cellIndicesInternalTri[2] = indices;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddCellWeightsTriangle(this MeshData mesh, float4 weights1, float4 weights2, float4 weights3)
    {
        mesh.cellWeightsInternalTri[0] = weights1;
        mesh.cellWeightsInternalTri[1] = weights2;
        mesh.cellWeightsInternalTri[2] = weights3;
    }

}

public struct MeshBasic
{
    public NativeList<float3> vertices;
    public NativeList<uint> triangles;
    public uint VertexIndex { get { return (uint)vertices.Length; } }

    public MeshBasic(int capacity = 0)
    {
        vertices = new NativeList<float3>(capacity, Allocator.Temp);
        triangles = new NativeList<uint>(capacity, Allocator.Temp);

        verticesInternalTri = new NativeArray<float3>(3, Allocator.Temp);
        trianglesInternalTri = new NativeArray<uint>(3, Allocator.Temp);

        verticesInternalQuad = new NativeArray<float3>(4, Allocator.Temp);
        trianglesInternalQuad = new NativeArray<uint>(6, Allocator.Temp);
    }

    public NativeArray<float3> verticesInternalTri;
    public NativeArray<uint> trianglesInternalTri;

    public void ApplyTriangle()
    {
        vertices.AddRange(verticesInternalTri);
        triangles.AddRange(trianglesInternalTri);
    }

    public NativeArray<float3> verticesInternalQuad;
    public NativeArray<uint> trianglesInternalQuad;

    public void ApplyQuad()
    {
        vertices.AddRange(verticesInternalQuad);
        triangles.AddRange(trianglesInternalQuad);
    }
}

public struct MeshData
{
    public int wrapSize;
    public NativeArray<float4> noiseColours;
    private NativeArray<VertexAttributeDescriptor> VertexDescriptors;
    public NativeList<float3> vertices;
    public NativeList<float3> cellIndices;
    public NativeList<float4> cellWeights;
    public NativeList<uint> triangles;
    public uint VertexIndex { get { return (uint)vertices.Length; } }

    public MeshData(NativeArray<float4> noiseColours,int wrapSize = 0,int capacity = 0)
    {
        this.wrapSize = wrapSize;
        this.noiseColours = noiseColours;
        VertexDescriptors = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp);
        VertexDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
        VertexDescriptors[1] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, 1);
        VertexDescriptors[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 3, 2);
        vertices = new NativeList<float3>(capacity, Allocator.Temp);
        cellIndices = new NativeList<float3>(capacity, Allocator.Temp);
        cellWeights = new NativeList<float4>(capacity, Allocator.Temp);
        triangles = new NativeList<uint>(capacity, Allocator.Temp);

        verticesInternalTri = new NativeArray<float3>(3, Allocator.Temp);
        cellIndicesInternalTri = new NativeArray<float3>(3, Allocator.Temp);
        cellWeightsInternalTri = new NativeArray<float4>(3, Allocator.Temp);
        trianglesInternalTri = new NativeArray<uint>(3, Allocator.Temp);

        verticesInternalQuad = new NativeArray<float3>(4, Allocator.Temp);
        cellIndicesInternalQuad = new NativeArray<float3>(4, Allocator.Temp);
        cellWeightsInternalQuad = new NativeArray<float4>(4, Allocator.Temp);
        trianglesInternalQuad = new NativeArray<uint>(6, Allocator.Temp);
    }

    public NativeArray<float3> verticesInternalTri;
    public NativeArray<float3> cellIndicesInternalTri;
    public NativeArray<float4> cellWeightsInternalTri;
    public NativeArray<uint> trianglesInternalTri;

    public void ApplyTriangle()
    {
        vertices.AddRange(verticesInternalTri);
        cellIndices.AddRange(cellIndicesInternalTri);
        cellWeights.AddRange(cellWeightsInternalTri);
        triangles.AddRange(trianglesInternalTri);
    }

    public NativeArray<float3> verticesInternalQuad;
    public NativeArray<float3> cellIndicesInternalQuad;
    public NativeArray<float4> cellWeightsInternalQuad;
    public NativeArray<uint> trianglesInternalQuad;

    public void ApplyQuad()
    {
        vertices.AddRange(verticesInternalQuad);
        cellIndices.AddRange(cellIndicesInternalQuad);
        cellWeights.AddRange(cellWeightsInternalQuad);
        triangles.AddRange(trianglesInternalQuad);
    }


    public void ApplyMesh(Mesh.MeshData meshData)
    {
        meshData.SetVertexBufferParams(vertices.Length, VertexDescriptors);
        meshData.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);
        meshData.GetVertexData<float3>(0).CopyFrom(vertices);
        meshData.GetVertexData<float4>(1).CopyFrom(cellWeights);
        meshData.GetVertexData<float3>(2).CopyFrom(cellIndices);
        meshData.GetIndexData<uint>().CopyFrom(triangles);
        meshData.subMeshCount = 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangles.Length, MeshTopology.Triangles));
    }


}

public struct MeshUV
{
    public NativeList<float3> vertices;
    public NativeList<float3> cellIndices;
    public NativeList<float4> cellWeights;
    public NativeList<float2> uvs;
    public NativeList<uint> triangles;
    public uint VertexIndex { get { return (uint)vertices.Length; } }

    public MeshUV(int capacity = 0)
    {
        vertices = new NativeList<float3>(capacity, Allocator.Temp);
        cellIndices = new NativeList<float3>(capacity, Allocator.Temp);
        cellWeights = new NativeList<float4>(capacity, Allocator.Temp);
        uvs = new NativeList<float2>(capacity, Allocator.Temp);
        triangles = new NativeList<uint>(capacity, Allocator.Temp);

        verticesInternalTri = new NativeArray<float3>(3, Allocator.Temp);
        cellIndicesInternalTri = new NativeArray<float3>(3, Allocator.Temp);
        cellWeightsInternalTri = new NativeArray<float4>(3, Allocator.Temp);
        uvInternalTri = new NativeArray<float2>(3, Allocator.Temp);
        trianglesInternalTri = new NativeArray<uint>(3, Allocator.Temp);

        verticesInternalQuad = new NativeArray<float3>(4, Allocator.Temp);
        cellIndicesInternalQuad = new NativeArray<float3>(4, Allocator.Temp);
        cellWeightsInternalQuad = new NativeArray<float4>(4, Allocator.Temp);
        uvInternalQuad = new NativeArray<float2>(4, Allocator.Temp);
        trianglesInternalQuad = new NativeArray<uint>(6, Allocator.Temp);
    }

    public NativeArray<float3> verticesInternalTri;
    public NativeArray<float3> cellIndicesInternalTri;
    public NativeArray<float4> cellWeightsInternalTri;
    public NativeArray<float2> uvInternalTri;
    public NativeArray<uint> trianglesInternalTri;

    public void ApplyTriangle()
    {
        vertices.AddRange(verticesInternalTri);
        cellIndices.AddRange(cellIndicesInternalTri);
        cellWeights.AddRange(cellWeightsInternalTri);
        uvs.AddRange(uvInternalTri);
        triangles.AddRange(trianglesInternalTri);
    }

    public NativeArray<float3> verticesInternalQuad;
    public NativeArray<float3> cellIndicesInternalQuad;
    public NativeArray<float4> cellWeightsInternalQuad;
    public NativeArray<float2> uvInternalQuad;
    public NativeArray<uint> trianglesInternalQuad;

    public void ApplyQuad()
    {
        vertices.AddRange(verticesInternalQuad);
        cellIndices.AddRange(cellIndicesInternalQuad);
        cellWeights.AddRange(cellWeightsInternalQuad);
        uvs.AddRange(uvInternalQuad);
        triangles.AddRange(trianglesInternalQuad);
    }
}

public struct Mesh2UV
{
    public NativeList<float3> vertices;
    public NativeList<float3> cellIndices;
    public NativeList<float4> cellWeights;
    public NativeList<float4> uvs;
    public NativeList<uint> triangles;
    public uint VertexIndex { get { return (uint)vertices.Length; } }

    public Mesh2UV(int capacity = 0)
    {
        vertices = new NativeList<float3>(capacity, Allocator.Temp);
        cellIndices = new NativeList<float3>(capacity, Allocator.Temp);
        cellWeights = new NativeList<float4>(capacity, Allocator.Temp);
        uvs = new NativeList<float4>(capacity, Allocator.Temp);
        triangles = new NativeList<uint>(capacity, Allocator.Temp);

        verticesInternalTri = new NativeArray<float3>(3, Allocator.Temp);
        cellIndicesInternalTri = new NativeArray<float3>(3, Allocator.Temp);
        cellWeightsInternalTri = new NativeArray<float4>(3, Allocator.Temp);
        uvInternalTri = new NativeArray<float4>(3, Allocator.Temp);
        trianglesInternalTri = new NativeArray<uint>(3, Allocator.Temp);

        verticesInternalQuad = new NativeArray<float3>(4, Allocator.Temp);
        cellIndicesInternalQuad = new NativeArray<float3>(4, Allocator.Temp);
        cellWeightsInternalQuad = new NativeArray<float4>(4, Allocator.Temp);
        uvInternalQuad = new NativeArray<float4>(4, Allocator.Temp);
        trianglesInternalQuad = new NativeArray<uint>(6, Allocator.Temp);
    }

    public NativeArray<float3> verticesInternalTri;
    public NativeArray<float3> cellIndicesInternalTri;
    public NativeArray<float4> cellWeightsInternalTri;
    public NativeArray<float4> uvInternalTri;
    public NativeArray<uint> trianglesInternalTri;

    public void ApplyTriangle()
    {
        vertices.AddRange(verticesInternalTri);
        cellIndices.AddRange(cellIndicesInternalTri);
        cellWeights.AddRange(cellWeightsInternalTri);
        uvs.AddRange(uvInternalTri);
        triangles.AddRange(trianglesInternalTri);
    }

    public NativeArray<float3> verticesInternalQuad;
    public NativeArray<float3> cellIndicesInternalQuad;
    public NativeArray<float4> cellWeightsInternalQuad;
    public NativeArray<float4> uvInternalQuad;
    public NativeArray<uint> trianglesInternalQuad;

    public void ApplyQuad()
    {
        vertices.AddRange(verticesInternalQuad);
        cellIndices.AddRange(cellIndicesInternalQuad);
        cellWeights.AddRange(cellWeightsInternalQuad);
        uvs.AddRange(uvInternalQuad);
        triangles.AddRange(trianglesInternalQuad);
    }
}