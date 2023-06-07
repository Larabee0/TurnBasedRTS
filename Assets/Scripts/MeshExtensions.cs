using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

public static class MeshExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddTriangleInfoUnperturbed(this MeshBasic wallMesh, float3 v1, float3 v2, float3 v3)
    {
        uint vertexIndex = wallMesh.VertexIndex;
        wallMesh.verticesInternalTri[0] = new float3x2(v1,float3.zero);
        wallMesh.verticesInternalTri[1] = new float3x2(v2,float3.zero);
        wallMesh.verticesInternalTri[2] = new float3x2(v3,float3.zero);
        wallMesh.trianglesInternalTri[0] = vertexIndex;
        wallMesh.trianglesInternalTri[1] = vertexIndex + 1;
        wallMesh.trianglesInternalTri[2] = vertexIndex + 2;
        wallMesh.ApplyTriangle();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddQuadInfoUnperturbed(this MeshBasic wallMesh, float3 v1, float3 v2, float3 v3, float3 v4)
    {
        uint vertexIndex = wallMesh.VertexIndex;
        wallMesh.verticesInternalQuad[0] = new float3x2(v1,float3.zero);
        wallMesh.verticesInternalQuad[1] = new float3x2(v2,float3.zero);
        wallMesh.verticesInternalQuad[2] = new float3x2(v3,float3.zero);
        wallMesh.verticesInternalQuad[3] = new float3x2(v4,float3.zero);
        wallMesh.trianglesInternalQuad[0] = vertexIndex;
        wallMesh.trianglesInternalQuad[1] = vertexIndex + 2;
        wallMesh.trianglesInternalQuad[2] = vertexIndex + 1;
        wallMesh.trianglesInternalQuad[3] = vertexIndex + 1;
        wallMesh.trianglesInternalQuad[4] = vertexIndex + 2;
        wallMesh.trianglesInternalQuad[5] = vertexIndex + 3;
        wallMesh.ApplyQuad();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddTriangleInfo(this MeshData mesh, float3 v1, float3 v2, float3 v3, float3 indices, float4 weights1, float4 weights2, float4 weights3)
    {
        uint vertexIndex = mesh.VertexIndex;
        mesh.verticesInternalTri[0] = new float3x2(HexMetrics.Perturb(mesh.noiseColours, v1, mesh.wrapSize), float3.zero);
        mesh.verticesInternalTri[1] = new float3x2(HexMetrics.Perturb(mesh.noiseColours, v2, mesh.wrapSize), float3.zero);
        mesh.verticesInternalTri[2] = new float3x2(HexMetrics.Perturb(mesh.noiseColours, v3, mesh.wrapSize), float3.zero);
        AddTriangle(mesh, vertexIndex);
        AddCellIndicesTriangle(mesh, indices);
        AddCellWeightsTriangle(mesh, weights1, weights2, weights3);
        mesh.ApplyTriangle();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddTriangleInfoUnperturbed(this MeshData mesh, float3 v1, float3 v2, float3 v3, float3 indices, float4 weights1, float4 weights2, float4 weights3)
    {
        uint vertexIndex = mesh.VertexIndex;
        mesh.verticesInternalTri[0] = new float3x2(v1, float3.zero);
        mesh.verticesInternalTri[1] = new float3x2(v2, float3.zero);
        mesh.verticesInternalTri[2] = new float3x2(v3, float3.zero);
        AddTriangle(mesh, vertexIndex);
        AddCellIndicesTriangle(mesh, indices);
        AddCellWeightsTriangle(mesh, weights1, weights2, weights3);
        mesh.ApplyTriangle();
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddQuadInfo(this MeshData mesh, float3 v1, float3 v2, float3 v3, float3 v4, float3 indices, float4 weights1, float4 weights2, float4 weights3, float4 weights4)
    {
        uint vertexIndex = mesh.VertexIndex;
        mesh.verticesInternalQuad[0] = new float3x2(HexMetrics.Perturb(mesh.noiseColours, v1, mesh.wrapSize), float3.zero);
        mesh.verticesInternalQuad[1] = new float3x2(HexMetrics.Perturb(mesh.noiseColours, v2, mesh.wrapSize), float3.zero);
        mesh.verticesInternalQuad[2] = new float3x2(HexMetrics.Perturb(mesh.noiseColours, v3, mesh.wrapSize), float3.zero);
        mesh.verticesInternalQuad[3] = new float3x2(HexMetrics.Perturb(mesh.noiseColours, v4, mesh.wrapSize), float3.zero);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddQuad(this MeshUV mesh, uint vertexIndex)
    {
        mesh.trianglesInternalQuad[0] = vertexIndex;
        mesh.trianglesInternalQuad[1] = vertexIndex + 2;
        mesh.trianglesInternalQuad[2] = vertexIndex + 1;
        mesh.trianglesInternalQuad[3] = vertexIndex + 1;
        mesh.trianglesInternalQuad[4] = vertexIndex + 2;
        mesh.trianglesInternalQuad[5] = vertexIndex + 3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddCellIndicesQuad(this MeshUV mesh, float3 indices)
    {
        mesh.cellIndicesInternalQuad[0] = indices;
        mesh.cellIndicesInternalQuad[1] = indices;
        mesh.cellIndicesInternalQuad[2] = indices;
        mesh.cellIndicesInternalQuad[3] = indices;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddCellWeightsQuad(this MeshUV mesh, float4 weights1, float4 weights2, float4 weights3, float4 weights4)
    {
        mesh.cellWeightsInternalQuad[0] = weights1;
        mesh.cellWeightsInternalQuad[1] = weights2;
        mesh.cellWeightsInternalQuad[2] = weights3;
        mesh.cellWeightsInternalQuad[3] = weights4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddUVQuad(this MeshUV mesh, float2 uv1, float2 uv2, float2 uv3, float2 uv4)
    {
        mesh.uvInternalQuad[0] = uv1;
        mesh.uvInternalQuad[1] = uv2;
        mesh.uvInternalQuad[2] = uv3;
        mesh.uvInternalQuad[3] = uv4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddQuadInfoUV(this MeshUV mesh, float3 v1, float3 v2, float3 v3, float3 v4, float2 uv1, float2 uv2, float2 uv3, float2 uv4, float3 indices, float4 weights1, float4 weights2, float4 weights3, float4 weights4)
    {
        uint vertexIndex = mesh.VertexIndex;
        mesh.verticesInternalQuad[0] = new(HexMetrics.Perturb(mesh.noiseColours, v1, mesh.wrapSize), float3.zero);
        mesh.verticesInternalQuad[1] = new(HexMetrics.Perturb(mesh.noiseColours, v2, mesh.wrapSize), float3.zero);
        mesh.verticesInternalQuad[2] = new(HexMetrics.Perturb(mesh.noiseColours, v3, mesh.wrapSize), float3.zero);
        mesh.verticesInternalQuad[3] = new(HexMetrics.Perturb(mesh.noiseColours, v4, mesh.wrapSize), float3.zero);
        AddQuad(mesh, vertexIndex);
        AddCellIndicesQuad(mesh, indices);
        AddCellWeightsQuad(mesh, weights1, weights2, weights3, weights4);
        AddUVQuad(mesh, uv1, uv2, uv3, uv4);
        mesh.ApplyQuad();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddQuadInfoUVUnperturbed(this MeshUV mesh, float3 v1, float3 v2, float3 v3, float3 v4, float2 uv1, float2 uv2, float2 uv3, float2 uv4, float3 indices, float4 weights1, float4 weights2, float4 weights3, float4 weights4)
    {
        uint vertexIndex = mesh.VertexIndex;
        mesh.verticesInternalQuad[0] = new(v1, float3.zero);
        mesh.verticesInternalQuad[1] = new(v2, float3.zero);
        mesh.verticesInternalQuad[2] = new(v3, float3.zero);
        mesh.verticesInternalQuad[3] = new(v4, float3.zero);
        AddQuad(mesh, vertexIndex);
        AddCellIndicesQuad(mesh, indices);
        AddCellWeightsQuad(mesh, weights1, weights2, weights3, weights4);
        AddUVQuad(mesh, uv1, uv2, uv3, uv4);
        mesh.ApplyQuad();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddTriangleInfoUV(this MeshUV mesh, float3 v1, float3 v2, float3 v3, float2 uv1, float2 uv2, float2 uv3, float3 indices, float4 weights1, float4 weights2, float4 weights3)
    {
        uint vertexIndex = mesh.VertexIndex;
        mesh.verticesInternalTri[0] = new(HexMetrics.Perturb(mesh.noiseColours, v1, mesh.wrapSize), float3.zero);
        mesh.verticesInternalTri[1] = new(HexMetrics.Perturb(mesh.noiseColours, v2, mesh.wrapSize), float3.zero);
        mesh.verticesInternalTri[2] = new(HexMetrics.Perturb(mesh.noiseColours, v3, mesh.wrapSize), float3.zero);
        mesh.trianglesInternalTri[0] = vertexIndex;
        mesh.trianglesInternalTri[1] = vertexIndex + 1;
        mesh.trianglesInternalTri[2] = vertexIndex + 2;
        mesh.cellIndicesInternalTri[0] = indices;
        mesh.cellIndicesInternalTri[1] = indices;
        mesh.cellIndicesInternalTri[2] = indices;
        mesh.cellWeightsInternalTri[0] = weights1;
        mesh.cellWeightsInternalTri[1] = weights2;
        mesh.cellWeightsInternalTri[2] = weights3;
        mesh.uvInternalTri[0] = uv1;
        mesh.uvInternalTri[1] = uv2;
        mesh.uvInternalTri[2] = uv3;
        mesh.ApplyTriangle();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddTrianlgeInfoUVaUVb(this Mesh2UV mesh, float3 v1, float3 v2, float3 v3, float2 uvA1, float2 uvA2, float2 uvA3, float2 uvB1, float2 uvB2, float2 uvB3, float3 indices, float4 weights1, float4 weights2, float4 weights3)
    {
        uint vertexIndex = mesh.VertexIndex;
        mesh.verticesInternalTri[0] = new(HexMetrics.Perturb(mesh.noiseColours, v1, mesh.wrapSize),float3.zero);
        mesh.verticesInternalTri[1] = new(HexMetrics.Perturb(mesh.noiseColours, v2, mesh.wrapSize),float3.zero);
        mesh.verticesInternalTri[2] = new(HexMetrics.Perturb(mesh.noiseColours, v3, mesh.wrapSize),float3.zero);
        mesh.trianglesInternalTri[0] = vertexIndex;
        mesh.trianglesInternalTri[1] = vertexIndex + 1;
        mesh.trianglesInternalTri[2] = vertexIndex + 2;
        mesh.cellIndicesInternalTri[0] = indices;
        mesh.cellIndicesInternalTri[1] = indices;
        mesh.cellIndicesInternalTri[2] = indices;
        mesh.cellWeightsInternalTri[0] = weights1;
        mesh.cellWeightsInternalTri[1] = weights2;
        mesh.cellWeightsInternalTri[2] = weights3;
        mesh.uvInternalTri[0] = new float4(uvA1, uvB1);
        mesh.uvInternalTri[1] = new float4(uvA2, uvB2);
        mesh.uvInternalTri[2] = new float4(uvA3, uvB3);
        mesh.ApplyTriangle();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddQuadInfoUVaUVb(this Mesh2UV mesh, float3 v1, float3 v2, float3 v3, float3 v4, float2 uvA1, float2 uvA2, float2 uvA3, float2 uvA4, float2 uvB1, float2 uvB2, float2 uvB3, float2 uvB4, float3 indices, float4 weights1, float4 weights2, float4 weights3, float4 weights4)
    {
        uint vertexIndex = mesh.VertexIndex;
        mesh.verticesInternalQuad[0] = new (HexMetrics.Perturb(mesh.noiseColours, v1, mesh.wrapSize),float3.zero);
        mesh.verticesInternalQuad[1] = new (HexMetrics.Perturb(mesh.noiseColours, v2, mesh.wrapSize),float3.zero);
        mesh.verticesInternalQuad[2] = new (HexMetrics.Perturb(mesh.noiseColours, v3, mesh.wrapSize),float3.zero);
        mesh.verticesInternalQuad[3] = new (HexMetrics.Perturb(mesh.noiseColours, v4, mesh.wrapSize),float3.zero);
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
        mesh.uvInternalQuad[0] = new float4(uvA1, uvB1);
        mesh.uvInternalQuad[1] = new float4(uvA2, uvB2);
        mesh.uvInternalQuad[2] = new float4(uvA3, uvB3);
        mesh.uvInternalQuad[3] = new float4(uvA4, uvB4);
        mesh.ApplyQuad();
    }
}

public struct MeshWrapper
{
    public NativeList<HexFeatureRequest> featureRequests;
    public MeshData terrianMesh;
    public MeshUV riverMesh;
    public MeshData waterMesh;
    public MeshUV waterShoreMesh;
    public Mesh2UV estuaryMesh;
    public MeshUV roadMesh;
    public MeshBasic wallMesh;

    public MeshWrapper(NativeArray<float4> noiseColours, int wrapSize)
    {
        featureRequests = new NativeList<HexFeatureRequest>(Allocator.Temp);
        terrianMesh = new MeshData(noiseColours, wrapSize, 0);
        riverMesh = new MeshUV(noiseColours, wrapSize, 0);
        waterMesh = new MeshData(noiseColours, wrapSize, 0);
        waterShoreMesh = new MeshUV(noiseColours, wrapSize, 0);
        estuaryMesh = new Mesh2UV(noiseColours, wrapSize, 0);
        roadMesh = new MeshUV(noiseColours, wrapSize, 0);
        wallMesh = new MeshBasic(0);
    }
}

public struct MeshDataWrapper
{
    public double TimeStamp;
    public UnsafeParallelHashSet<int> chunksIncluded;
    public Mesh.MeshDataArray meshDataArray;

    public MeshDataWrapper(double timeStamp, UnsafeParallelHashSet<int> chunksIncluded, Mesh.MeshDataArray meshDataArray)
    {
        TimeStamp = timeStamp;
        this.chunksIncluded = chunksIncluded;
        this.meshDataArray = meshDataArray;
    }
}

public struct MeshBasic
{
    private NativeArray<VertexAttributeDescriptor> VertexDescriptors;
    public NativeList<float3x2> vertices;
    public NativeList<uint> triangles;
    public uint VertexIndex { get { return (uint)vertices.Length; } }

    public MeshBasic(int capacity = 0)
    {
        VertexDescriptors = new NativeArray<VertexAttributeDescriptor>(2, Allocator.Temp);
        VertexDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
        VertexDescriptors[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0);
        vertices = new NativeList<float3x2>(capacity, Allocator.Temp);
        triangles = new NativeList<uint>(capacity, Allocator.Temp);

        vertices = new NativeList<float3x2>(capacity, Allocator.Temp);
        triangles = new NativeList<uint>(capacity, Allocator.Temp);

        verticesInternalTri = new NativeArray<float3x2>(3, Allocator.Temp);
        trianglesInternalTri = new NativeArray<uint>(3, Allocator.Temp);

        verticesInternalQuad = new NativeArray<float3x2>(4, Allocator.Temp);
        trianglesInternalQuad = new NativeArray<uint>(6, Allocator.Temp);
    }

    public NativeArray<float3x2> verticesInternalTri;
    public NativeArray<uint> trianglesInternalTri;

    public void ApplyTriangle()
    {
        vertices.AddRange(verticesInternalTri);
        triangles.AddRange(trianglesInternalTri);
    }

    public NativeArray<float3x2> verticesInternalQuad;
    public NativeArray<uint> trianglesInternalQuad;

    public void ApplyQuad()
    {
        vertices.AddRange(verticesInternalQuad);
        triangles.AddRange(trianglesInternalQuad);
    }

    private void CalculateNormals()
    {
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int ia = (int)triangles[i + 0];
            int ib = (int)triangles[i + 1];
            int ic = (int)triangles[i + 2];
            float3x2 va = vertices[ia];
            float3x2 vb = vertices[ib];
            float3x2 vc = vertices[ic];
            float3 p = math.cross(vb.c0 - va.c0, vc.c0 - va.c0);
            va.c1 += p;
            vb.c1 += p;
            vc.c1 += p;
            vertices[ia] = va;
            vertices[ib] = vb;
            vertices[ic] = vc;
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            float3x2 v = vertices[i];
            v.c1 = math.normalize(v.c1);
            vertices[i] = v;
        }
    }


    public void ApplyMesh(Mesh.MeshData meshData)
    {
        CalculateNormals();
        meshData.SetVertexBufferParams(vertices.Length, VertexDescriptors);
        meshData.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);
        meshData.GetVertexData<float3x2>(0).CopyFrom(vertices.AsArray());
        meshData.GetIndexData<uint>().CopyFrom(triangles.AsArray());
        meshData.subMeshCount = 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangles.Length, MeshTopology.Triangles));
    }

}

public struct MeshData
{
    public int wrapSize;
    public NativeArray<float4> noiseColours;
    private NativeArray<VertexAttributeDescriptor> VertexDescriptors;
    public NativeList<float3x2> vertices;
    public NativeList<float3> cellIndices;
    public NativeList<float4> cellWeights;
    public NativeList<uint> triangles;
    public uint VertexIndex => (uint)vertices.Length;

    public MeshData(NativeArray<float4> noiseColours, int wrapSize = 0, int capacity = 0)
    {
        this.wrapSize = wrapSize;
        this.noiseColours = noiseColours;
        VertexDescriptors = new NativeArray<VertexAttributeDescriptor>(4, Allocator.Temp);
        VertexDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
        VertexDescriptors[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0);
        VertexDescriptors[2] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, 1);
        VertexDescriptors[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 3, 2);
        vertices = new NativeList<float3x2>(capacity, Allocator.Temp);
        cellIndices = new NativeList<float3>(capacity, Allocator.Temp);
        cellWeights = new NativeList<float4>(capacity, Allocator.Temp);
        triangles = new NativeList<uint>(capacity, Allocator.Temp);

        verticesInternalTri = new NativeArray<float3x2>(3, Allocator.Temp);
        cellIndicesInternalTri = new NativeArray<float3>(3, Allocator.Temp);
        cellWeightsInternalTri = new NativeArray<float4>(3, Allocator.Temp);
        trianglesInternalTri = new NativeArray<uint>(3, Allocator.Temp);

        verticesInternalQuad = new NativeArray<float3x2>(4, Allocator.Temp);
        cellIndicesInternalQuad = new NativeArray<float3>(4, Allocator.Temp);
        cellWeightsInternalQuad = new NativeArray<float4>(4, Allocator.Temp);
        trianglesInternalQuad = new NativeArray<uint>(6, Allocator.Temp);
    }

    public NativeArray<float3x2> verticesInternalTri;
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

    public NativeArray<float3x2> verticesInternalQuad;
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

    private void CalculateNormals()
    {
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int ia = (int)triangles[i + 0];
            int ib = (int)triangles[i + 1];
            int ic = (int)triangles[i + 2];
            float3x2 va = vertices[ia];
            float3x2 vb = vertices[ib];
            float3x2 vc = vertices[ic];
            float3 p = math.cross(vb.c0 - va.c0, vc.c0 - va.c0);
            va.c1 += p;
            vb.c1 += p;
            vc.c1 += p;
            vertices[ia] = va;
            vertices[ib] = vb;
            vertices[ic] = vc;
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            float3x2 v = vertices[i];
            v.c1 = math.normalize(v.c1);
            vertices[i] = v;
        }
    }

    public void ApplyMesh(Mesh.MeshData meshData)
    {
        CalculateNormals();
        meshData.SetVertexBufferParams(vertices.Length, VertexDescriptors);
        meshData.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);
        meshData.GetVertexData<float3x2>(0).CopyFrom(vertices.AsArray());
        meshData.GetVertexData<float4>(1).CopyFrom(cellWeights.AsArray());
        meshData.GetVertexData<float3>(2).CopyFrom(cellIndices.AsArray());
        meshData.GetIndexData<uint>().CopyFrom(triangles.AsArray());
        meshData.subMeshCount = 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangles.Length, MeshTopology.Triangles));
    }


}

public struct MeshUV
{
    public int wrapSize;
    public NativeArray<float4> noiseColours;
    private NativeArray<VertexAttributeDescriptor> VertexDescriptors;
    public NativeList<float3x2> vertices;
    public NativeList<float3> cellIndices;
    public NativeList<float4> cellWeights;
    public NativeList<float2> uvs;
    public NativeList<uint> triangles;
    public uint VertexIndex => (uint)vertices.Length;

    public MeshUV(NativeArray<float4> noiseColours, int wrapSize = 0, int capacity = 0)
    {
        this.wrapSize = wrapSize;
        this.noiseColours = noiseColours;
        VertexDescriptors = new NativeArray<VertexAttributeDescriptor>(5, Allocator.Temp);
        VertexDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
        VertexDescriptors[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0);
        VertexDescriptors[2] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, 1);
        VertexDescriptors[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 3);
        VertexDescriptors[4] = new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 3, 2);
        vertices = new NativeList<float3x2>(capacity, Allocator.Temp);
        cellIndices = new NativeList<float3>(capacity, Allocator.Temp);
        cellWeights = new NativeList<float4>(capacity, Allocator.Temp);
        uvs = new NativeList<float2>(capacity, Allocator.Temp);
        triangles = new NativeList<uint>(capacity, Allocator.Temp);

        verticesInternalTri = new NativeArray<float3x2>(3, Allocator.Temp);
        cellIndicesInternalTri = new NativeArray<float3>(3, Allocator.Temp);
        cellWeightsInternalTri = new NativeArray<float4>(3, Allocator.Temp);
        uvInternalTri = new NativeArray<float2>(3, Allocator.Temp);
        trianglesInternalTri = new NativeArray<uint>(3, Allocator.Temp);

        verticesInternalQuad = new NativeArray<float3x2>(4, Allocator.Temp);
        cellIndicesInternalQuad = new NativeArray<float3>(4, Allocator.Temp);
        cellWeightsInternalQuad = new NativeArray<float4>(4, Allocator.Temp);
        uvInternalQuad = new NativeArray<float2>(4, Allocator.Temp);
        trianglesInternalQuad = new NativeArray<uint>(6, Allocator.Temp);
    }

    public NativeArray<float3x2> verticesInternalTri;
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

    public NativeArray<float3x2> verticesInternalQuad;
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


    private void CalculateNormals()
    {
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int ia = (int)triangles[i + 0];
            int ib = (int)triangles[i + 1];
            int ic = (int)triangles[i + 2];
            float3x2 va = vertices[ia];
            float3x2 vb = vertices[ib];
            float3x2 vc = vertices[ic];
            float3 p = math.cross(vb.c0 - va.c0, vc.c0 - va.c0);
            va.c1 += p;
            vb.c1 += p;
            vc.c1 += p;
            vertices[ia] = va;
            vertices[ib] = vb;
            vertices[ic] = vc;
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            float3x2 v = vertices[i];
            v.c1 = math.normalize(v.c1);
            vertices[i] = v;
        }
    }

    public void ApplyMesh(Mesh.MeshData meshData)
    {
        CalculateNormals();
        meshData.SetVertexBufferParams(vertices.Length, VertexDescriptors);
        meshData.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);
        meshData.GetVertexData<float3x2>(0).CopyFrom(vertices.AsArray());
        meshData.GetVertexData<float4>(1).CopyFrom(cellWeights.AsArray());
        meshData.GetVertexData<float3>(2).CopyFrom(cellIndices.AsArray());
        meshData.GetVertexData<float2>(3).CopyFrom(uvs.AsArray());
        meshData.GetIndexData<uint>().CopyFrom(triangles.AsArray());
        meshData.subMeshCount = 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangles.Length, MeshTopology.Triangles));
    }
}

public struct Mesh2UV
{
    public int wrapSize;
    public NativeArray<float4> noiseColours;
    private NativeArray<VertexAttributeDescriptor> VertexDescriptors;
    public NativeList<float3x2> vertices;
    public NativeList<float3> cellIndices;
    public NativeList<float4> cellWeights;
    public NativeList<float4> uvs;
    public NativeList<uint> triangles;
    public uint VertexIndex => (uint)vertices.Length;

    public Mesh2UV(NativeArray<float4> noiseColours, int wrapSize = 0, int capacity = 0)
    {
        this.wrapSize = wrapSize;
        this.noiseColours = noiseColours;
        VertexDescriptors = new NativeArray<VertexAttributeDescriptor>(6, Allocator.Temp);
        VertexDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
        VertexDescriptors[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0);
        VertexDescriptors[2] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, 1);
        VertexDescriptors[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 3);
        VertexDescriptors[4] = new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2, 3);
        VertexDescriptors[5] = new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 3, 2);
        vertices = new NativeList<float3x2>(capacity, Allocator.Temp);
        cellIndices = new NativeList<float3>(capacity, Allocator.Temp);
        cellWeights = new NativeList<float4>(capacity, Allocator.Temp);
        uvs = new NativeList<float4>(capacity, Allocator.Temp);
        triangles = new NativeList<uint>(capacity, Allocator.Temp);

        verticesInternalTri = new NativeArray<float3x2>(3, Allocator.Temp);
        cellIndicesInternalTri = new NativeArray<float3>(3, Allocator.Temp);
        cellWeightsInternalTri = new NativeArray<float4>(3, Allocator.Temp);
        uvInternalTri = new NativeArray<float4>(3, Allocator.Temp);
        trianglesInternalTri = new NativeArray<uint>(3, Allocator.Temp);

        verticesInternalQuad = new NativeArray<float3x2>(4, Allocator.Temp);
        cellIndicesInternalQuad = new NativeArray<float3>(4, Allocator.Temp);
        cellWeightsInternalQuad = new NativeArray<float4>(4, Allocator.Temp);
        uvInternalQuad = new NativeArray<float4>(4, Allocator.Temp);
        trianglesInternalQuad = new NativeArray<uint>(6, Allocator.Temp);
    }

    public NativeArray<float3x2> verticesInternalTri;
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

    public NativeArray<float3x2> verticesInternalQuad;
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

    private void CalculateNormals()
    {
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int ia = (int)triangles[i + 0];
            int ib = (int)triangles[i + 1];
            int ic = (int)triangles[i + 2];
            float3x2 va = vertices[ia];
            float3x2 vb = vertices[ib];
            float3x2 vc = vertices[ic];
            float3 p = math.cross(vb.c0 - va.c0, vc.c0 - va.c0);
            va.c1 += p;
            vb.c1 += p;
            vc.c1 += p;
            vertices[ia] = va;
            vertices[ib] = vb;
            vertices[ic] = vc;
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            float3x2 v = vertices[i];
            v.c1 = math.normalize(v.c1);
            vertices[i] = v;
        }
    }

    public void ApplyMesh(Mesh.MeshData meshData)
    {
        CalculateNormals();
        meshData.SetVertexBufferParams(vertices.Length, VertexDescriptors);
        meshData.SetIndexBufferParams(triangles.Length, IndexFormat.UInt32);
        meshData.GetVertexData<float3x2>(0).CopyFrom(vertices.AsArray());
        meshData.GetVertexData<float4>(1).CopyFrom(cellWeights.AsArray());
        meshData.GetVertexData<float3>(2).CopyFrom(cellIndices.AsArray());
        meshData.GetVertexData<float4>(3).CopyFrom(uvs.AsArray());
        meshData.GetIndexData<uint>().CopyFrom(triangles.AsArray());
        meshData.subMeshCount = 1;
        meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangles.Length, MeshTopology.Triangles));
    }
}