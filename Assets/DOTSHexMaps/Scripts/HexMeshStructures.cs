using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Rendering;

namespace DOTSHexagons
{
	public enum MeshMaterial
	{
		Terrian,
		River,
		Water,
		WaterShore,
		Estuaries,
		Roads,
		Walls,
	}

	public struct HexMeshContainer : INativeDisposable
	{
		public Mesh mesh;
		public NativeArray<float3> verticesCollider;
		public NativeArray<int3> trianglesCollider;
		public MeshMaterial material;
		public Entity entity;


		public HexMeshContainer(MeshMaterial material)
		{
			this.material = material;
			mesh = null;
			verticesCollider = new NativeArray<float3>(0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			verticesCollider.Dispose();
			trianglesCollider = new NativeArray<int3>(0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			trianglesCollider.Dispose();
			entity = Entity.Null;
		}
		public HexMeshContainer(MeshMaterial material, Mesh mesh)
		{
			this.material = material;
			this.mesh = mesh;
			verticesCollider = new NativeArray<float3>(0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			verticesCollider.Dispose();
			trianglesCollider = new NativeArray<int3>(0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			trianglesCollider.Dispose();
			entity = Entity.Null;
		}
		public HexMeshContainer(MeshMaterial material, Mesh mesh, Entity entity)
		{
			this.material = material;
			this.mesh = mesh;
			this.mesh.RecalculateNormals();
			this.mesh.RecalculateBounds();
			verticesCollider = new NativeArray<float3>(0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			verticesCollider.Dispose();
			trianglesCollider = new NativeArray<int3>(0, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			trianglesCollider.Dispose();
			this.entity = entity;
		}
		public HexMeshContainer(Mesh mesh, NativeArray<float3> colliderVerts,NativeArray<int3> colliderTris)
		{
			this.material = MeshMaterial.Terrian;
			this.mesh = mesh;
			verticesCollider = colliderVerts;
			trianglesCollider = colliderTris;
			this.entity = Entity.Null;
		}

		public void Dispose()
        {
			verticesCollider.Dispose();
			trianglesCollider.Dispose();
		}
		public JobHandle Dispose(JobHandle inputDeps)
		{
			JobHandle outputDepsV = verticesCollider.Dispose(inputDeps);
			return trianglesCollider.Dispose(outputDepsV);
		}
	}

    public struct Triangle
	{
		public int t1;
		public int t2;
		public int t3;
		public int t4;

		public Triangle(int t1,int t2,int t3,int t4)
		{
			this.t1 = t1;
			this.t2 = t2;
			this.t3 = t3;
			this.t4 = t4;
		}
    }

	public struct VertexWeightIndice
	{
		public float3 vertex;
		public float4 cellWeights;
		public float3 cellIndices;

		public VertexWeightIndice(float3 v, float4 weights, float3 indices)
		{
			vertex = v;
			cellWeights = weights;
			cellIndices = indices;
		}
	}

	public struct VertexWeightUVIndice
	{

		public float3 vertex;
		public float3 cellIndices;
		public float4 cellWeights;
		public float2 uv;
		public VertexWeightUVIndice(float3 v, float2 uv, float4 weights, float3 indices)
		{
			vertex = v;
			cellWeights = weights;
			this.uv = uv;
			cellIndices = indices;
		}
	}

	public struct VertexWeightUV1UV2Indice
	{
		public float3 vertex;
		public float3 cellIndices;
		public float4 cellWeights;
		public float2 uv1;
		public float2 uv2;

		public VertexWeightUV1UV2Indice(float3 v, float2 uv1, float2 uv2, float4 weights, float3 indices)
		{
			vertex = v;
			cellWeights = weights;
			this.uv1 = uv1;
			this.uv2 = uv2;
			cellIndices = indices;
		}
	}

	#region Containers
	public struct ContainerWeightIndiceTriangle
	{
		public VertexWeightIndice v1;
		public VertexWeightIndice v2;
		public VertexWeightIndice v3;

		public ContainerWeightIndiceTriangle(VertexWeightIndice v1, VertexWeightIndice v2, VertexWeightIndice v3)
		{
			this.v1 = v1;
			this.v2 = v2;
			this.v3 = v3;
		}
	}
	public struct ContainerWeightIndiceQuad
	{
		public VertexWeightIndice v1;
		public VertexWeightIndice v2;
		public VertexWeightIndice v3;
		public VertexWeightIndice v4;

		public readonly bool Quad;
		public ContainerWeightIndiceQuad(VertexWeightIndice v1, VertexWeightIndice v2, VertexWeightIndice v3)
		{
			this.v1 = v1;
			this.v2 = v2;
			this.v3 = v3;
			this.v4 = v3;
			Quad = false;
		}
		public ContainerWeightIndiceQuad(VertexWeightIndice v1, VertexWeightIndice v2, VertexWeightIndice v3, VertexWeightIndice v4)
		{
			this.v1 = v1;
			this.v2 = v2;
			this.v3 = v3;
			this.v4 = v4;
			Quad = true;
		}
	}

	public struct ContainerWeightUVIndiceTriangle
	{
		public VertexWeightUVIndice v1;
		public VertexWeightUVIndice v2;
		public VertexWeightUVIndice v3;

		public ContainerWeightUVIndiceTriangle(VertexWeightUVIndice v1, VertexWeightUVIndice v2, VertexWeightUVIndice v3)
		{
			this.v1 = v1;
			this.v2 = v2;
			this.v3 = v3;
		}
	}

	public struct ContainerWeightUV1UV2IndiceTriangle
	{
		public VertexWeightUV1UV2Indice v1;
		public VertexWeightUV1UV2Indice v2;
		public VertexWeightUV1UV2Indice v3;

		public ContainerWeightUV1UV2IndiceTriangle(VertexWeightUV1UV2Indice v1, VertexWeightUV1UV2Indice v2, VertexWeightUV1UV2Indice v3)
		{
			this.v1 = v1;
			this.v2 = v2;
			this.v3 = v3;
		}
	}

    #endregion

}
