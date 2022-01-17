using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;
using Unity.Transforms;
using UnityEngine.Rendering;

namespace DOTSHexagonsV2
{
	[BurstCompile]
	public struct TriangulatorEverything : IJobEntityBatch
	{
		private static readonly float4 weights1 = new float4(1f, 0f, 0f, 0f);
		private static readonly float4 weights2 = new float4(0f, 1f, 0f, 0f);
		private static readonly float4 weights3 = new float4(0f, 0f, 1f, 0f);
		[ReadOnly]
		public NativeArray<float4> noiseColours;
		[ReadOnly]
		public EntityTypeHandle chunkEntityType;
		[ReadOnly]
		public ComponentTypeHandle<HexGridChunkComponent> hexGridChunkComponentType;
		[ReadOnly]
		public BufferTypeHandle<HexGridCellBuffer> hexGridCellBufferType;
		[ReadOnly]
		public BufferFromEntity<HexCell> hexCellBufferType;
		[ReadOnly]
		public BufferFromEntity<HexGridChild> childBufferType;
		[ReadOnly]
		public ComponentDataFromEntity<FeatureGridEntities> featureDataComponentType;
		[ReadOnly]
		public ComponentDataFromEntity<HexGridComponent> gridDataComponentType;
		[ReadOnly]
		public BufferFromEntity<HexHash> hexHashData;

		public EntityCommandBuffer.ParallelWriter ecbEnd;
		public EntityCommandBuffer.ParallelWriter ecbBegin;

		public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
		{
			BufferAccessor<HexGridCellBuffer> hGCBAccessor = batchInChunk.GetBufferAccessor(hexGridCellBufferType);
			NativeArray<HexGridChunkComponent> hGCC = batchInChunk.GetNativeArray(hexGridChunkComponentType);
			NativeArray<Entity> chunkEntities = batchInChunk.GetNativeArray(chunkEntityType);
			for (int i = 0; i < hGCC.Length; i++)
			{
				HexGridChunkComponent chunkComp = hGCC[i];
				MeshData terrianMesh = new MeshData(0);
				MeshUV riverMesh = new MeshUV(0);
				MeshData waterMesh = new MeshData(0);
				MeshUV waterShoreMesh = new MeshUV(0);
				Mesh2UV estuaryMesh = new Mesh2UV(0);
				MeshUV roadMesh = new MeshUV(0);
				MeshBasic wallMesh = new MeshBasic(0);

				NativeList<PossibleFeaturePosition> features = new NativeList<PossibleFeaturePosition>(Allocator.Temp);
				DynamicBuffer<HexCell> cells = hexCellBufferType[chunkComp.gridEntity];
				NativeArray<HexGridCellBuffer> chunkCells = hGCBAccessor[i].AsNativeArray();
				for (int chunkCellIndex = 0; chunkCellIndex < chunkCells.Length; chunkCellIndex++)
				{
					HexGridCellBuffer chunkCell = chunkCells[chunkCellIndex];
					ExecuteMesh(cells, chunkCell.cellIndex, terrianMesh, riverMesh, waterMesh, waterShoreMesh, estuaryMesh, roadMesh, wallMesh, features);
				}

				ecbBegin.SetBuffer<HexGridVertex>(batchIndex, chunkComp.entityTerrian).CopyFrom(terrianMesh.vertices);
				ecbBegin.SetBuffer<HexGridTriangles>(batchIndex, chunkComp.entityTerrian).CopyFrom(terrianMesh.triangles);
				ecbBegin.SetBuffer<HexGridWeights>(batchIndex, chunkComp.entityTerrian).CopyFrom(terrianMesh.cellWeights);
				ecbBegin.SetBuffer<HexGridIndices>(batchIndex, chunkComp.entityTerrian).CopyFrom(terrianMesh.cellIndices);
				ecbBegin.AddComponent<RepaintScheduled>(batchIndex, chunkComp.entityTerrian);

				ecbBegin.SetBuffer<HexGridVertex>(batchIndex, chunkComp.entityRiver).CopyFrom(riverMesh.vertices.AsArray().Reinterpret<HexGridVertex>());
				ecbBegin.SetBuffer<HexGridTriangles>(batchIndex, chunkComp.entityRiver).CopyFrom(riverMesh.triangles.AsArray().Reinterpret<HexGridTriangles>());
				ecbBegin.SetBuffer<HexGridWeights>(batchIndex, chunkComp.entityRiver).CopyFrom(riverMesh.cellWeights.AsArray().Reinterpret<HexGridWeights>());
				ecbBegin.SetBuffer<HexGridIndices>(batchIndex, chunkComp.entityRiver).CopyFrom(riverMesh.cellIndices.AsArray().Reinterpret<HexGridIndices>());
				ecbBegin.SetBuffer<HexGridUV2>(batchIndex, chunkComp.entityRiver).CopyFrom(riverMesh.uvs.AsArray().Reinterpret<HexGridUV2>());
				ecbBegin.AddComponent<RepaintScheduled>(batchIndex, chunkComp.entityRiver);

				ecbBegin.SetBuffer<HexGridVertex>(batchIndex, chunkComp.entityWater).CopyFrom(waterMesh.vertices.AsArray());
				ecbBegin.SetBuffer<HexGridTriangles>(batchIndex, chunkComp.entityWater).CopyFrom(waterMesh.triangles.AsArray());
				ecbBegin.SetBuffer<HexGridWeights>(batchIndex, chunkComp.entityWater).CopyFrom(waterMesh.cellWeights.AsArray());
				ecbBegin.SetBuffer<HexGridIndices>(batchIndex, chunkComp.entityWater).CopyFrom(waterMesh.cellIndices.AsArray());
				ecbBegin.AddComponent<RepaintScheduled>(batchIndex, chunkComp.entityWater);

				ecbBegin.SetBuffer<HexGridVertex>(batchIndex, chunkComp.entityWaterShore).CopyFrom(waterShoreMesh.vertices.AsArray().Reinterpret<HexGridVertex>());
				ecbBegin.SetBuffer<HexGridTriangles>(batchIndex, chunkComp.entityWaterShore).CopyFrom(waterShoreMesh.triangles.AsArray().Reinterpret<HexGridTriangles>());
				ecbBegin.SetBuffer<HexGridWeights>(batchIndex, chunkComp.entityWaterShore).CopyFrom(waterShoreMesh.cellWeights.AsArray().Reinterpret<HexGridWeights>());
				ecbBegin.SetBuffer<HexGridIndices>(batchIndex, chunkComp.entityWaterShore).CopyFrom(waterShoreMesh.cellIndices.AsArray().Reinterpret<HexGridIndices>());
				ecbBegin.SetBuffer<HexGridUV2>(batchIndex, chunkComp.entityWaterShore).CopyFrom(waterShoreMesh.uvs.AsArray().Reinterpret<HexGridUV2>());
				ecbBegin.AddComponent<RepaintScheduled>(batchIndex, chunkComp.entityWaterShore);

				ecbBegin.SetBuffer<HexGridVertex>(batchIndex, chunkComp.entityEstuaries).CopyFrom(estuaryMesh.vertices.AsArray().Reinterpret<HexGridVertex>());
				ecbBegin.SetBuffer<HexGridTriangles>(batchIndex, chunkComp.entityEstuaries).CopyFrom(estuaryMesh.triangles.AsArray().Reinterpret<HexGridTriangles>());
				ecbBegin.SetBuffer<HexGridWeights>(batchIndex, chunkComp.entityEstuaries).CopyFrom(estuaryMesh.cellWeights.AsArray().Reinterpret<HexGridWeights>());
				ecbBegin.SetBuffer<HexGridIndices>(batchIndex, chunkComp.entityEstuaries).CopyFrom(estuaryMesh.cellIndices.AsArray().Reinterpret<HexGridIndices>());
				ecbBegin.SetBuffer<HexGridUV4>(batchIndex, chunkComp.entityEstuaries).CopyFrom(estuaryMesh.uvs.AsArray().Reinterpret<HexGridUV4>());
				ecbBegin.AddComponent<RepaintScheduled>(batchIndex, chunkComp.entityEstuaries);

				ecbBegin.SetBuffer<HexGridVertex>(batchIndex, chunkComp.entityRoads).CopyFrom(roadMesh.vertices.AsArray().Reinterpret<HexGridVertex>());
				ecbBegin.SetBuffer<HexGridTriangles>(batchIndex, chunkComp.entityRoads).CopyFrom(roadMesh.triangles.AsArray().Reinterpret<HexGridTriangles>());
				ecbBegin.SetBuffer<HexGridWeights>(batchIndex, chunkComp.entityRoads).CopyFrom(roadMesh.cellWeights.AsArray().Reinterpret<HexGridWeights>());
				ecbBegin.SetBuffer<HexGridIndices>(batchIndex, chunkComp.entityRoads).CopyFrom(roadMesh.cellIndices.AsArray().Reinterpret<HexGridIndices>());
				ecbBegin.SetBuffer<HexGridUV2>(batchIndex, chunkComp.entityRoads).CopyFrom(roadMesh.uvs.AsArray().Reinterpret<HexGridUV2>());
				ecbBegin.AddComponent<RepaintScheduled>(batchIndex, chunkComp.entityRoads);

				ecbBegin.SetBuffer<HexGridVertex>(batchIndex, chunkComp.entityWalls).CopyFrom(wallMesh.vertices.AsArray().Reinterpret<HexGridVertex>());
				ecbBegin.SetBuffer<HexGridTriangles>(batchIndex, chunkComp.entityWalls).CopyFrom(wallMesh.triangles.AsArray().Reinterpret<HexGridTriangles>());
				ecbBegin.AddComponent<RepaintScheduled>(batchIndex, chunkComp.entityWalls);

				terrianMesh.Dispose();
				riverMesh.Dispose();
				waterMesh.Dispose();
				waterShoreMesh.Dispose();
				estuaryMesh.Dispose();
				roadMesh.Dispose();
				wallMesh.Dispose();
				ecbBegin.SetBuffer<PossibleFeaturePosition>(batchIndex, chunkComp.FeatureContainer).CopyFrom(features);
				features.Dispose();
				ecbBegin.AddComponent<RefreshCellFeatures>(batchIndex, chunkComp.FeatureContainer);
				ecbEnd.RemoveComponent<RefreshChunk>(batchIndex, chunkEntities[i]);
			}
		}

		private void ExecuteMesh(DynamicBuffer<HexCell> cells, int cellIndex, MeshData terrianMesh, MeshUV riverMesh, MeshData waterMesh, MeshUV waterShoreMesh, Mesh2UV estuariesMesh, MeshUV roadMesh, MeshBasic wallMesh, NativeList<PossibleFeaturePosition> features)
		{
			HexCell cell = cells[cellIndex];

			for (HexDirection direction = HexDirection.NE; direction <= HexDirection.NW; direction++)
			{
				Triangulate(cells, direction, cell, terrianMesh, riverMesh, waterMesh, waterShoreMesh, estuariesMesh, roadMesh, wallMesh, features);
			}

			switch (cell.IsUnderwater)
			{
				case false:
					switch (!cell.HasRiver && !cell.HasRoads)
					{
						case true:
							AddFeature(features, cell.Index, cell.Position, 0f, FeatureType.None);
							break;
					}
					switch (cell.IsSpeical)
					{
						case true:
							AddFeature(features, cell.Index, cell.Position, 0f, FeatureType.Special);
							break;
					}
					break;
			}
		}

		private void Triangulate(DynamicBuffer<HexCell> cells, HexDirection direction, HexCell cell, MeshData terrianMesh, MeshUV riverMesh, MeshData waterMesh, MeshUV waterShoreMesh, Mesh2UV estuariesMesh, MeshUV roadMesh, MeshBasic wallMesh, NativeList<PossibleFeaturePosition> features)
		{
			float3 centre = cell.Position;
			EdgeVertices e = new EdgeVertices(centre + HexFunctions.GetFirstSolidCorner(direction), centre + HexFunctions.GetSecondSolidCorner(direction));

			switch (cell.HasRiver)
			{
				case true:
					switch (HexCell.HasRiverThroughEdge(cell, direction))
					{
						case true:
							e.v3.y = cell.StreamBedY;
							switch (cell.HasRiverBeginOrEnd)
							{
								case true:
									TriangulateWithRiverBeginOrEnd(cell.wrapSize, terrianMesh, riverMesh, roadMesh, cell, centre, e);
									break;
								case false:
									TriangulateWithRiver(terrianMesh, riverMesh, roadMesh, direction, cell, centre, e);
									break;
							}
							break;
						case false:
							TriangulateAdjacentToRiver(terrianMesh, roadMesh, features, direction, cell, centre, e);
							break;
					}
					break;
				case false:
					TriangulateWithoutRiver(terrianMesh, roadMesh, direction, cell, centre, e);
					switch (!cell.IsUnderwater && !HexCell.HasRoadThroughEdge(cell, direction))
					{
						case true:
							AddFeature(features, cell.Index, (centre + e.v1 + e.v5) * (1f / 3f), 0f, FeatureType.None);
							break;
					}
					break;
			}

			switch (direction <= HexDirection.SE)
			{
				case true:
					TriangulateConnection(cells, terrianMesh, riverMesh, roadMesh, wallMesh, features, direction, cell, e);
					break;
			}
			switch (cell.IsUnderwater)
			{
				case true:
					TriangulateWater(cells, waterMesh, waterShoreMesh, estuariesMesh, direction, cell, centre);
					break;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TriangulateWater(DynamicBuffer<HexCell> cells, MeshData waterMesh, MeshUV waterShoreMesh, Mesh2UV estuariesMesh, HexDirection direction, HexCell cell, float3 centre)
		{
			centre.y = cell.WaterSurfaceY;
			HexCell neighbour = HexCell.GetNeighbour(cell,cells, direction);
			if (neighbour)
			{
				if (!neighbour.IsUnderwater)
				{
					TriangulateWaterShore(cells, waterMesh, waterShoreMesh, estuariesMesh, direction, cell, neighbour, centre);
				}
				else
				{
					TriangulateOpenWater(cells, waterMesh, direction, cell, neighbour.Index, centre);
				}
			}
			else
			{
				TriangulateOpenWater(cells, waterMesh, direction, cell, neighbour.Index, centre);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TriangulateOpenWater(DynamicBuffer<HexCell> cells, MeshData waterMesh, HexDirection direction, HexCell cell, int neighbourIndex, float3 centre)
		{
			float3 c1 = centre + HexFunctions.GetFirstWaterCorner(direction);
			float3 c2 = centre + HexFunctions.GetSecondWaterCorner(direction);
			int wrapSize = cell.wrapSize;
			float3 indices = new float3(cell.Index);
			AddTriangleInfo(wrapSize, waterMesh, centre, c1, c2, indices, weights1, weights1, weights1);

			switch (direction <= HexDirection.SE && neighbourIndex != int.MinValue)
			{
				case true:
					float3 bridge = HexFunctions.GetWaterBridge(direction);
					float3 e1 = c1 + bridge;
					float3 e2 = c2 + bridge;

					indices.y = neighbourIndex;
					AddQuadInfo(wrapSize, waterMesh, c1, c2, e1, e2, indices, weights1, weights1, weights2, weights2);

					switch (direction <= HexDirection.E)
					{
						case true:
							HexCell nextNeighbour = HexCell.GetNeighbour(cell,cells, direction.Next());
							switch ((bool)nextNeighbour)
							{
								case true:
									switch (nextNeighbour.IsUnderwater)
									{
										case true:
											indices.z = nextNeighbour.Index;
											AddTriangleInfo(wrapSize, waterMesh, c2, e2, c2 + HexFunctions.GetWaterBridge(direction.Next()), indices, weights1, weights2, weights3);
											break;
									}
									break;
							}
							break;
					}
					break;
			}
		}

		private void TriangulateWaterShore(DynamicBuffer<HexCell> cells, MeshData waterMesh, MeshUV waterShoreMesh, Mesh2UV estuariesMesh, HexDirection direction, HexCell cell, HexCell neighbour, float3 centre)
		{
			EdgeVertices e1 = new EdgeVertices(centre + HexFunctions.GetFirstWaterCorner(direction), centre + HexFunctions.GetSecondWaterCorner(direction));
			float3 indices = new float3(cell.Index)
			{
				y = neighbour.Index
			};
			int wrapSize = cell.wrapSize;
			AddTriangleInfo(wrapSize, waterMesh, centre, e1.v1, e1.v2, indices, weights1, weights1, weights1);
			AddTriangleInfo(wrapSize, waterMesh, centre, e1.v2, e1.v3, indices, weights1, weights1, weights1);
			AddTriangleInfo(wrapSize, waterMesh, centre, e1.v3, e1.v4, indices, weights1, weights1, weights1);
			AddTriangleInfo(wrapSize, waterMesh, centre, e1.v4, e1.v5, indices, weights1, weights1, weights1);

			float3 centre2 = neighbour.Position;
			switch (neighbour.ColumnIndex < cell.ColumnIndex - 1)
			{
				case true:
					centre2.x += cell.wrapSize * HexFunctions.innerDiameter;
					break;
				case false:
					switch (neighbour.ColumnIndex > cell.ColumnIndex + 1)
					{
						case true:
							centre2.x -= cell.wrapSize * HexFunctions.innerDiameter;
							break;
					}
					break;
			}

			centre2.y = centre.y;
			EdgeVertices e2 = new EdgeVertices(centre2 + HexFunctions.GetSecondSolidCorner(direction.Opposite()), centre2 + HexFunctions.GetFirstSolidCorner(direction.Opposite()));
			switch (HexCell.HasRiverThroughEdge(cell, direction))
			{
				case true:
					TriangulateEstruary(wrapSize, waterShoreMesh, estuariesMesh, e1, e2, cell.IncomingRiver == direction, indices);
					break;
				case false:
					float2x4 waterShoreUV = new float2x4
					{
						c0 = 0f,
						c1 = 0f,
						c2 = new float2(0f, 1f),
						c3 = new float2(0f, 1f),
					};

					AddQuadInfoUV(wrapSize, waterShoreMesh, e1.v1, e1.v2, e2.v1, e2.v2, waterShoreUV.c0, waterShoreUV.c1, waterShoreUV.c2, waterShoreUV.c3, indices, weights1, weights1, weights2, weights2);
					AddQuadInfoUV(wrapSize, waterShoreMesh, e1.v2, e1.v3, e2.v2, e2.v3, waterShoreUV.c0, waterShoreUV.c1, waterShoreUV.c2, waterShoreUV.c3, indices, weights1, weights1, weights2, weights2);
					AddQuadInfoUV(wrapSize, waterShoreMesh, e1.v3, e1.v4, e2.v3, e2.v4, waterShoreUV.c0, waterShoreUV.c1, waterShoreUV.c2, waterShoreUV.c3, indices, weights1, weights1, weights2, weights2);
					AddQuadInfoUV(wrapSize, waterShoreMesh, e1.v4, e1.v5, e2.v4, e2.v5, waterShoreUV.c0, waterShoreUV.c1, waterShoreUV.c2, waterShoreUV.c3, indices, weights1, weights1, weights2, weights2);
					break;
			}

			HexCell nextNeighbor = HexCell.GetNeighbour(cell,cells, direction.Next());
			switch ((bool)nextNeighbor)
			{
				case true:
					float3 centre3 = nextNeighbor.Position;
					switch (nextNeighbor.ColumnIndex < cell.ColumnIndex - 1)
					{
						case true:
							centre3.x += cell.wrapSize * HexFunctions.innerDiameter;
							break;
						case false:
							switch (nextNeighbor.ColumnIndex > cell.ColumnIndex + 1)
							{
								case true:
									centre3.x -= cell.wrapSize * HexFunctions.innerDiameter;
									break;
							}
							break;
					}
					float3 v3 = centre3 + (nextNeighbor.IsUnderwater ? HexFunctions.GetFirstWaterCorner(direction.Previous()) : HexFunctions.GetFirstSolidCorner(direction.Previous()));
					v3.y = centre.y;
					AddTriangleInfoUV(wrapSize, waterShoreMesh, e1.v5, e2.v5, v3, 0f, new float2(0f, 1f), new float2(0f, nextNeighbor.IsUnderwater ? 0f : 1f), indices, weights1, weights2, weights3);
					break;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TriangulateEstruary(int wrapSize, MeshUV waterShoreMesh, Mesh2UV estuariesMesh, EdgeVertices e1, EdgeVertices e2, bool incomingRiver, float3 indices)
		{
			AddTriangleInfoUV(wrapSize, waterShoreMesh, e2.v1, e1.v2, e1.v1, new float2(0f, 1f), 0f, 0f, indices, weights2, weights1, weights1);
			AddTriangleInfoUV(wrapSize, waterShoreMesh, e2.v5, e1.v5, e1.v4, new float2(0f, 1f), 0f, 0f, indices, weights2, weights1, weights1);

			float2x3 EstuariesTriangleUV1 = new float2x3
			{
				c0 = 0f,
				c1 = 1f,
				c2 = 1f,
			};
			float2x4 EstuariesQuadUVA1 = new float2x4
			{
				c0 = new float2(0f, 1f),
				c1 = 0f,
				c2 = 1f,
				c3 = 0f,
			};
			float2x4 EstuariesQuadUVA2 = new float2x4
			{
				c0 = 0f,
				c1 = 0f,
				c2 = 1f,
				c3 = new float2(0f, 1f),
			};
			float2x3 EstuariesTriangleUV2;
			float2x4 EstuariesQuadUVB1;
			float2x4 EstuariesQuadUVB2;
			switch (incomingRiver)
			{
				case true:
					EstuariesQuadUVB1.c0 = new float2(1.5f, 1f);
					EstuariesQuadUVB1.c1 = new float2(0.7f, 1.15f);
					EstuariesQuadUVB1.c2 = new float2(1f, 0.8f);
					EstuariesQuadUVB1.c3 = new float2(0.5f, 1.1f);

					EstuariesTriangleUV2.c0 = new float2(0.5f, 1.1f);
					EstuariesTriangleUV2.c1 = new float2(1f, 0.8f);
					EstuariesTriangleUV2.c2 = new float2(0f, 0.8f);

					EstuariesQuadUVB2.c0 = new float2(0.5f, 1.1f);
					EstuariesQuadUVB2.c1 = new float2(0.3f, 1.15f);
					EstuariesQuadUVB2.c2 = new float2(0f, 0.8f);
					EstuariesQuadUVB2.c3 = new float2(-0.5f, 1f);
					break;
				case false:
					EstuariesQuadUVB1.c0 = new float2(-0.5f, -0.2f);
					EstuariesQuadUVB1.c1 = new float2(0.3f, -0.35f);
					EstuariesQuadUVB1.c2 = 0f;
					EstuariesQuadUVB1.c3 = new float2(0.5f, -0.3f);

					EstuariesTriangleUV2.c0 = new float2(0.5f, -0.3f);
					EstuariesTriangleUV2.c1 = 0f;
					EstuariesTriangleUV2.c2 = new float2(1f, 0f);

					EstuariesQuadUVB2.c0 = new float2(0.5f, -0.3f);
					EstuariesQuadUVB2.c1 = new float2(0.7f, -0.35f);
					EstuariesQuadUVB2.c2 = new float2(1f, 0f);
					EstuariesQuadUVB2.c3 = new float2(1.5f, -0.2f);
					break;
			}

			AddQuadInfoUVaUVb(wrapSize, estuariesMesh, e2.v1, e1.v2, e2.v2, e1.v3, EstuariesQuadUVA1.c0, EstuariesQuadUVA1.c1, EstuariesQuadUVA1.c2, EstuariesQuadUVA1.c3, EstuariesQuadUVB1.c0, EstuariesQuadUVB1.c1, EstuariesQuadUVB1.c2, EstuariesQuadUVB1.c3, indices, weights2, weights1, weights2, weights1);
			AddTrianlgeInfoUVaUVb(wrapSize, estuariesMesh, e1.v3, e2.v2, e2.v4, EstuariesTriangleUV1.c0, EstuariesTriangleUV1.c1, EstuariesTriangleUV1.c2, EstuariesTriangleUV2.c0, EstuariesTriangleUV2.c1, EstuariesTriangleUV2.c2, indices, weights1, weights2, weights2);
			AddQuadInfoUVaUVb(wrapSize, estuariesMesh, e1.v3, e1.v4, e2.v4, e2.v5, EstuariesQuadUVA2.c0, EstuariesQuadUVA2.c1, EstuariesQuadUVA2.c2, EstuariesQuadUVA2.c3, EstuariesQuadUVB2.c0, EstuariesQuadUVB2.c1, EstuariesQuadUVB2.c2, EstuariesQuadUVB2.c3, indices, weights1, weights1, weights2, weights2);
		}

		private void TriangulateConnection(DynamicBuffer<HexCell> cells, MeshData terrianMesh, MeshUV riverMesh, MeshUV roadMesh, MeshBasic wallMesh, NativeList<PossibleFeaturePosition> features, HexDirection direction, HexCell cell, EdgeVertices e1)
		{
			HexCell neighbour = HexCell.GetNeighbour(cell, cells, direction);
			int wrapSize = cell.wrapSize;
			switch ((bool)neighbour)
			{
				case false:
					return;
			}

			float3 bridge = HexFunctions.GetBridge(direction);

			bridge.y = neighbour.Position.y - cell.Position.y;
			EdgeVertices e2 = new EdgeVertices(e1.v1 + bridge, e1.v5 + bridge);

			bool hasRiver = HexCell.HasRiverThroughEdge(cell, direction);
			bool hasRoad = HexCell.HasRoadThroughEdge(cell, direction);

			switch (hasRiver)
			{
				case true:
					e2.v3.y = neighbour.StreamBedY;
					float3 indices = cell.Index;
					indices.y = neighbour.Index;
					switch (cell.IsUnderwater)
					{
						case false:
							switch (neighbour.IsUnderwater)
							{
								case false:
									TriangulateRiverQuad(wrapSize, riverMesh, e1.v2, e1.v4, e2.v2, e2.v4, cell.RiverSurfaceY, neighbour.RiverSurfaceY, 0.8f, cell.HasIncomingRiver && cell.IncomingRiver == direction, indices);
									break;
								case true:
									switch (cell.Elevation > neighbour.WaterLevel)
									{
										case true:
											TriangulateWaterfallInWater(cell.wrapSize, riverMesh, e1.v2, e1.v4, e2.v2, e2.v4, cell.RiverSurfaceY, neighbour.RiverSurfaceY, neighbour.WaterSurfaceY, indices);
											break;
									}
									break;
							}
							break;
						case true:
							switch (!neighbour.IsUnderwater && neighbour.Elevation > cell.WaterLevel)
							{
								case true:
									TriangulateWaterfallInWater(cell.wrapSize, riverMesh, e2.v4, e2.v2, e1.v4, e1.v2, neighbour.RiverSurfaceY, cell.RiverSurfaceY, cell.WaterSurfaceY, indices);
									break;
							}
							break;
					}
					break;
			}

			switch (HexCell.GetEdgeType(cell, neighbour) == HexEdgeType.Slope)
			{
				case true:
					TriangulateEdgeTerraces(terrianMesh, roadMesh, e1, cell, e2, neighbour, hasRoad);
					break;
				case false:
					TriangulateEdgeStrip(wrapSize, terrianMesh, roadMesh, e1, weights1, cell.Index, e2, weights2, neighbour.Index, hasRoad);
					break;
			}
			AddWall(wallMesh, features, cell.Index, e1, cell, e2, neighbour, hasRiver, hasRoad);

			HexCell nextNeighbour = HexCell.GetNeighbour(cell, cells, direction.Next());
			switch ((bool)nextNeighbour)
			{
				case false:
					return;
			}
			switch (direction <= HexDirection.E)
			{
				case true:
					float3 v5 = e1.v5 + HexFunctions.GetBridge(direction.Next());
					v5.y = nextNeighbour.Position.y;
					switch (cell.Elevation <= neighbour.Elevation)
					{
						case true:
							switch (cell.Elevation <= nextNeighbour.Elevation)
							{
								case true:
									TriangulateCorner(terrianMesh, wallMesh, features, e1.v5, cell, e2.v5, neighbour, v5, nextNeighbour);
									break;
								case false:
									TriangulateCorner(terrianMesh, wallMesh, features, v5, nextNeighbour, e1.v5, cell, e2.v5, neighbour);
									break;
							}
							break;
						case false:
							switch (neighbour.Elevation <= nextNeighbour.Elevation)
							{
								case true:
									TriangulateCorner(terrianMesh, wallMesh, features, e2.v5, neighbour, v5, nextNeighbour, e1.v5, cell);
									break;
								case false:
									TriangulateCorner(terrianMesh, wallMesh, features, v5, nextNeighbour, e1.v5, cell, e2.v5, neighbour);
									break;
							}
							break;
					}
					break;
			}
		}

		private void TriangulateCorner(MeshData terrianMesh, MeshBasic wallMesh, NativeList<PossibleFeaturePosition> features, float3 bottom, HexCell bottomCell, float3 left, HexCell leftCell, float3 right, HexCell rightCell)
		{
			HexEdgeType leftEdgeType = HexCell.GetEdgeType(bottomCell, leftCell);
			HexEdgeType rightEdgeType = HexCell.GetEdgeType(bottomCell, rightCell);

			switch (leftEdgeType == HexEdgeType.Slope)
			{
				case true:
					switch (rightEdgeType == HexEdgeType.Slope)
					{
						case true:
							TriangulateCornerTerraces(terrianMesh, bottom, bottomCell, left, leftCell, right, rightCell);
							break;
						case false:
							switch (rightEdgeType == HexEdgeType.Flat)
							{
								case true:
									TriangulateCornerTerraces(terrianMesh, left, leftCell, right, rightCell, bottom, bottomCell);
									break;
								case false:
									TriangulateCornerTerracesCliff(terrianMesh, bottom, bottomCell, left, leftCell, right, rightCell);
									break;
							}
							break;
					}
					break;
				case false:
					switch (rightEdgeType == HexEdgeType.Slope)
					{
						case true:
							switch (leftEdgeType == HexEdgeType.Flat)
							{
								case true:
									TriangulateCornerTerraces(terrianMesh, right, rightCell, bottom, bottomCell, left, leftCell);
									break;
								case false:
									TriangulateCornerCliffTerraces(terrianMesh, bottom, bottomCell, left, leftCell, right, rightCell);
									break;
							}
							break;
						case false:
							switch (HexCell.GetEdgeType(leftCell, rightCell) == HexEdgeType.Slope)
							{
								case true:
									switch (leftCell.Elevation < rightCell.Elevation)
									{
										case true:
											TriangulateCornerCliffTerraces(terrianMesh, right, rightCell, bottom, bottomCell, left, leftCell);
											break;
										case false:
											TriangulateCornerTerracesCliff(terrianMesh, left, leftCell, right, rightCell, bottom, bottomCell);
											break;
									}
									break;
								case false:
									float3 indices = new float3(bottomCell.Index, leftCell.Index, rightCell.Index);
									AddTriangleInfo(bottomCell.wrapSize, terrianMesh, bottom, left, right, indices, weights1, weights2, weights3);
									break;
							}
							break;
					}
					break;
			}

			AddWall(wallMesh, features, bottomCell.Index, bottom, bottomCell, left, leftCell, right, rightCell, 1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TriangulateCornerCliffTerraces(MeshData terrianMesh, float3 begin, HexCell beginCell, float3 left, HexCell leftCell, float3 right, HexCell rightCell)
		{
			float b = 1f / (leftCell.Elevation - beginCell.Elevation);
			switch (b < 0)
			{
				case true:
					b = -b;
					break;
			}
			float3 boundary = math.lerp(HexFunctions.Perturb(noiseColours, begin, beginCell.wrapSize), HexFunctions.Perturb(noiseColours, left, beginCell.wrapSize), b);
			float4 boundaryWeights = math.lerp(weights1, weights2, b);
			float3 indices = new float3(beginCell.Index, leftCell.Index, rightCell.Index);

			TriangulateBoundaryTriangle(beginCell.wrapSize, terrianMesh, right, weights3, begin, weights1, boundary, boundaryWeights, indices);

			switch (HexCell.GetEdgeType(leftCell, rightCell) == HexEdgeType.Slope)
			{
				case true:
					TriangulateBoundaryTriangle(beginCell.wrapSize, terrianMesh, left, weights2, right, weights3, boundary, boundaryWeights, indices);
					break;
				case false:
					AddTriangleInfoUnperturbed(terrianMesh, HexFunctions.Perturb(noiseColours, left, beginCell.wrapSize), HexFunctions.Perturb(noiseColours, right, beginCell.wrapSize), boundary, indices, weights2, weights3, boundaryWeights);
					break;
			}
		}

		private void TriangulateCornerTerracesCliff(MeshData terrianMesh, float3 begin, HexCell beginCell, float3 left, HexCell leftCell, float3 right, HexCell rightCell)
		{
			float b = 1f / (rightCell.Elevation - beginCell.Elevation);
			switch (b < 0)
			{
				case true:
					b = -b;
					break;
			}
			float3 boundary = math.lerp(HexFunctions.Perturb(noiseColours, begin, beginCell.wrapSize), HexFunctions.Perturb(noiseColours, right, beginCell.wrapSize), b);
			float4 boundaryWeight = math.lerp(weights1, weights3, b);
			float3 indices = new float3(beginCell.Index, leftCell.Index, rightCell.Index);
			TriangulateBoundaryTriangle(beginCell.wrapSize, terrianMesh, begin, weights1, left, weights2, boundary, boundaryWeight, indices);

			switch (HexCell.GetEdgeType(leftCell, rightCell) == HexEdgeType.Slope)
			{
				case true:
					TriangulateBoundaryTriangle(beginCell.wrapSize, terrianMesh, left, weights2, right, weights3, boundary, boundaryWeight, indices);
					break;
				case false:
					AddTriangleInfoUnperturbed(terrianMesh, HexFunctions.Perturb(noiseColours, left, beginCell.wrapSize), HexFunctions.Perturb(noiseColours, right, beginCell.wrapSize), boundary, indices, weights2, weights3, boundaryWeight);
					break;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TriangulateBoundaryTriangle(int wrapSize, MeshData terrianMesh, float3 begin, float4 beginWeights, float3 left, float4 leftWeights, float3 boundary, float4 boundaryWeights, float3 indices)
		{
			float3 v2 = HexFunctions.Perturb(noiseColours, HexFunctions.TerraceLerp(begin, left, 1), wrapSize);
			float4 w2 = HexFunctions.TerraceLerp(beginWeights, leftWeights, 1);

			AddTriangleInfoUnperturbed(terrianMesh, HexFunctions.Perturb(noiseColours, begin, wrapSize), v2, boundary, indices, beginWeights, w2, boundaryWeights);
			for (int i = 2; i < HexFunctions.terraceSteps; i++)
			{
				float3 v1 = v2;
				float4 w1 = w2;
				v2 = HexFunctions.Perturb(noiseColours, HexFunctions.TerraceLerp(begin, left, i), wrapSize);
				w2 = HexFunctions.TerraceLerp(beginWeights, leftWeights, i);
				AddTriangleInfoUnperturbed(terrianMesh, v1, v2, boundary, indices, w1, w2, boundaryWeights);
			}

			AddTriangleInfoUnperturbed(terrianMesh, v2, HexFunctions.Perturb(noiseColours, left, wrapSize), boundary, indices, w2, leftWeights, boundaryWeights);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TriangulateCornerTerraces(MeshData terrianMesh, float3 begin, HexCell beginCell, float3 left, HexCell leftCell, float3 right, HexCell rightCell)
		{
			float3 v3 = HexFunctions.TerraceLerp(begin, left, 1);
			float3 v4 = HexFunctions.TerraceLerp(begin, right, 1);
			float4 w3 = HexFunctions.TerraceLerp(weights1, weights2, 1);
			float4 w4 = HexFunctions.TerraceLerp(weights1, weights3, 1);
			float3 indices = new float3(beginCell.Index, leftCell.Index, rightCell.Index);

			int wrapSize = beginCell.wrapSize;
			AddTriangleInfo(wrapSize, terrianMesh, begin, v3, v4, indices, weights1, w3, w4);
			for (int i = 2; i < HexFunctions.terraceSteps; i++)
			{
				float3 v1 = v3;
				float3 v2 = v4;
				float4 w1 = w3;
				float4 w2 = w4;
				v3 = HexFunctions.TerraceLerp(begin, left, i);
				v4 = HexFunctions.TerraceLerp(begin, right, i);
				w3 = HexFunctions.TerraceLerp(weights1, weights2, i);
				w4 = HexFunctions.TerraceLerp(weights1, weights3, i);
				AddQuadInfo(wrapSize, terrianMesh, v1, v2, v3, v4, indices, w1, w2, w3, w4);
			}
			AddQuadInfo(wrapSize, terrianMesh, v3, v4, left, right, indices, w3, w4, weights2, weights3);
		}

		private void TriangulateEdgeTerraces(MeshData terrianMesh, MeshUV roadMesh, EdgeVertices begin, HexCell beginCell, EdgeVertices end, HexCell endCell, bool hasRoad)
		{
			EdgeVertices e2 = EdgeVertices.TerraceLerp(begin, end, 1);
			float4 w2 = HexFunctions.TerraceLerp(weights1, weights2, 1);
			float i1 = beginCell.Index;
			float i2 = endCell.Index;
			int wrapSize = beginCell.wrapSize;
			TriangulateEdgeStrip(wrapSize, terrianMesh, roadMesh, begin, weights1, i1, e2, w2, i2, hasRoad);

			for (int i = 2; i < HexFunctions.terraceSteps; i++)
			{
				EdgeVertices e1 = e2;
				float4 w1 = w2;
				e2 = EdgeVertices.TerraceLerp(begin, end, i);
				w2 = HexFunctions.TerraceLerp(weights1, weights2, i);
				TriangulateEdgeStrip(wrapSize, terrianMesh, roadMesh, e1, w1, i1, e2, w2, i2, hasRoad);
			}
			TriangulateEdgeStrip(wrapSize, terrianMesh, roadMesh, e2, w2, i1, end, weights2, i2, hasRoad);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TriangulateWaterfallInWater(int wrapSize, MeshUV riverMesh, float3 v1, float3 v2, float3 v3, float3 v4, float y1, float y2, float waterY, float3 indices)
		{
			v1.y = v2.y = y1;
			v3.y = v4.y = y2;
			v1 = HexFunctions.Perturb(noiseColours, v1, wrapSize);
			v2 = HexFunctions.Perturb(noiseColours, v2, wrapSize);
			v3 = HexFunctions.Perturb(noiseColours, v3, wrapSize);
			v4 = HexFunctions.Perturb(noiseColours, v4, wrapSize);
			float t = (waterY - y2) / (y1 - y2);
			v3 = math.lerp(v3, v1, t);
			v4 = math.lerp(v4, v2, t);
			AddQuadInfoUnperturbedUV(riverMesh, v1, v2, v3, v4, new float2(0f, 0.8f), new float2(1f, 0.8f), new float2(0f, 1f), new float2(1f, 1f), indices, weights1, weights1, weights2, weights2);
		}

		private void TriangulateWithoutRiver(MeshData terrianMesh, MeshUV roadMesh, HexDirection direction, HexCell cell, float3 centre, EdgeVertices e)
		{
			int wrapSize = cell.wrapSize;
			TriangulateEdgeFan(wrapSize, terrianMesh, centre, e, cell.Index);

			switch (cell.HasRoads)
			{
				case true:
					float2 interpolators = GetRoadInterpolators(direction, cell);
					TriangulateRoad(wrapSize, roadMesh, centre, math.lerp(centre, e.v1, interpolators.x), math.lerp(centre, e.v5, interpolators.y), e, HexCell.HasRoadThroughEdge(cell, direction), cell.Index);
					break;
			}
		}

		private void TriangulateRoadAdjacentToRiver(MeshUV roadMesh, NativeList<PossibleFeaturePosition> features, HexDirection direction, HexCell cell, float3 centre, EdgeVertices e)
		{
			bool hasRoadThroughEdge = HexCell.HasRoadThroughEdge(cell, direction);
			bool previousHasRiver = HexCell.HasRiverThroughEdge(cell, direction.Previous());
			bool nextHasRiver = HexCell.HasRiverThroughEdge(cell, direction.Next());
			float2 interpolators = GetRoadInterpolators(direction, cell);
			float3 roadCentre = centre;
			switch (cell.HasRiverBeginOrEnd)
			{
				case true:
					roadCentre += HexFunctions.GetSolidEdgeMiddle(cell.RiverBeginOrEndDirection.Opposite()) * (1f / 3f);
					break;
				case false:
					switch (cell.incomingRiver == cell.OutgoingRiver.Opposite())
					{
						case true:
							float3 corner;
							switch (previousHasRiver)
							{
								case true:
									switch (!hasRoadThroughEdge && !HexCell.HasRoadThroughEdge(cell, direction.Next()))
									{
										case true:
											return;
									}
									corner = HexFunctions.GetSecondSolidCorner(direction);
									break;
								case false:
									switch (!hasRoadThroughEdge && !HexCell.HasRoadThroughEdge(cell, direction.Previous()))
									{
										case true:
											return;
									}
									corner = HexFunctions.GetFirstSolidCorner(direction);
									break;
							}
							roadCentre += corner * 0.5f;
							switch (cell.IncomingRiver == direction.Next() && (HexCell.HasRoadThroughEdge(cell, direction.Next2()) || HexCell.HasRoadThroughEdge(cell, direction.Opposite())))
							{
								case true:
									AddFeature(features, cell.Index, roadCentre, centre - corner * 0.5f, FeatureType.Bridge);
									break;
							}
							centre += corner * 0.25f;
							break;
						case false:
							switch (cell.IncomingRiver == cell.OutgoingRiver.Previous())
							{
								case true:
									roadCentre -= HexFunctions.GetSecondCorner(cell.IncomingRiver) * 0.2f;
									break;
								case false:
									switch (cell.IncomingRiver == cell.OutgoingRiver.Next())
									{
										case true:
											roadCentre -= HexFunctions.GetFirstCorner(cell.IncomingRiver) * 0.2f;
											break;
										case false:
											switch (previousHasRiver && nextHasRiver)
											{
												case true:
													switch (hasRoadThroughEdge)
													{
														case false:
															return;
													}
													float3 offset = HexFunctions.GetSolidEdgeMiddle(direction) * HexFunctions.innerToOuter;
													roadCentre += offset * 0.7f;
													centre += offset * 0.5f;
													break;
												case false:
													HexDirection middle;
													switch (previousHasRiver)
													{
														case true:
															middle = direction.Next();
															break;
														case false:
															switch (nextHasRiver)
															{
																case true:
																	middle = direction.Previous();
																	break;
																case false:
																	middle = direction;
																	break;
															}
															break;
													}
													switch (!HexCell.HasRoadThroughEdge(cell, middle) && !HexCell.HasRoadThroughEdge(cell, middle.Previous()) && !HexCell.HasRoadThroughEdge(cell, middle.Next()))
													{
														case true:
															return;
													}
													float3 offsetOther = HexFunctions.GetSolidEdgeMiddle(middle);
													roadCentre += offsetOther * 0.25f;
													switch (direction == middle && HexCell.HasRoadThroughEdge(cell, direction.Opposite()))
													{
														case true:
															AddFeature(features, cell.Index, roadCentre, centre - offsetOther * (HexFunctions.innerToOuter * 0.7f), FeatureType.Bridge);
															break;
													}
													break;
											}
											break;
									}
									break;
							}
							break;
					}
					break;
			}
			int wrapSize = cell.wrapSize;
			float3 mL = math.lerp(roadCentre, e.v1, interpolators.x);
			float3 mR = math.lerp(roadCentre, e.v5, interpolators.y);
			TriangulateRoad(wrapSize, roadMesh, roadCentre, mL, mR, e, hasRoadThroughEdge, cell.Index);
			switch (previousHasRiver)
			{
				case true:
					TriangulateRoadEdge(wrapSize, roadMesh, roadCentre, centre, mL, cell.Index);
					break;
			}
			switch (nextHasRiver)
			{
				case true:
					TriangulateRoadEdge(wrapSize, roadMesh, roadCentre, mR, centre, cell.Index);
					break;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float2 GetRoadInterpolators(HexDirection direction, HexCell cell)
		{
			float2 interpolators;
			switch (HexCell.HasRoadThroughEdge(cell, direction))
			{
				case true:
					interpolators.x = interpolators.y = 0.5f;
					return interpolators;
				case false:
					interpolators.x = HexCell.HasRoadThroughEdge(cell, direction.Previous()) ? 0.5f : 0.25f;
					interpolators.y = HexCell.HasRoadThroughEdge(cell, direction.Next()) ? 0.5f : 0.25f;
					return interpolators;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TriangulateRoad(int wrapSize, MeshUV roadMesh, float3 centre, float3 mL, float3 mR, EdgeVertices e, bool hasRoadThroughCellEdge, float index)
		{
			switch (hasRoadThroughCellEdge)
			{
				case true:
					float3 indices = new float3(index);
					float3 mC = math.lerp(mL, mR, 0.5f);
					TriangulateRoadSegment(wrapSize, roadMesh, mL, mC, mR, e.v2, e.v3, e.v4, weights1, weights1, indices);

					AddTriangleInfoUV(wrapSize, roadMesh, centre, mL, mC, new float2(1f, 0f), new float2(0f), new float2(1f, 0f), indices, weights1, weights1, weights1);
					AddTriangleInfoUV(wrapSize, roadMesh, centre, mC, mR, new float2(1f, 0f), new float2(1f, 0f), new float2(0f), indices, weights1, weights1, weights1);
					break;
				case false:
					TriangulateRoadEdge(wrapSize, roadMesh, centre, mL, mR, index);
					break;
			}
		}

		private void TriangulateAdjacentToRiver(MeshData terrianMesh, MeshUV roadMesh, NativeList<PossibleFeaturePosition> features, HexDirection direction, HexCell cell, float3 centre, EdgeVertices e)
		{
			switch (cell.HasRoads)
			{
				case true:
					TriangulateRoadAdjacentToRiver(roadMesh, features, direction, cell, centre, e);
					break;
			}

			switch (HexCell.HasRiverThroughEdge(cell, direction.Next()))
			{
				case true:
					switch (HexCell.HasRiverThroughEdge(cell, direction.Previous()))
					{
						case true:
							centre += HexFunctions.GetSolidEdgeMiddle(direction) * (HexFunctions.innerToOuter * 0.5f);
							break;
						case false:
							switch (HexCell.HasRiverThroughEdge(cell, direction.Previous2()))
							{
								case true:
									centre += HexFunctions.GetFirstSolidCorner(direction) * 0.25f;
									break;
							}
							break;
					}
					break;
				case false:
					switch (HexCell.HasRiverThroughEdge(cell, direction.Previous()) && HexCell.HasRiverThroughEdge(cell, direction.Next2()))
					{
						case true:
							centre += HexFunctions.GetSecondSolidCorner(direction) * 0.25f;
							break;
					}
					break;
			}
			EdgeVertices m = new EdgeVertices(math.lerp(centre, e.v1, 0.5f), math.lerp(centre, e.v5, 0.5f));
			int wrapSize = cell.wrapSize;
			TriangulateEdgeStrip(wrapSize, terrianMesh, roadMesh, m, weights1, cell.Index, e, weights1, cell.Index);
			TriangulateEdgeFan(wrapSize, terrianMesh, centre, m, cell.Index);
			switch (!cell.IsUnderwater && !HexCell.HasRoadThroughEdge(cell, direction))
			{
				case true:
					AddFeature(features, cell.Index, (centre + e.v1 + e.v5) * (1f / 3f), 0f, FeatureType.None);
					break;
			}
		}

		private void TriangulateWithRiver(MeshData terrianMesh, MeshUV riverMesh, MeshUV roadMesh, HexDirection direction, HexCell cell, float3 centre, EdgeVertices e)
		{
			float3 centreL;
			float3 centreR;
			int wrapSize = cell.wrapSize;
			switch (HexCell.HasRiverThroughEdge(cell, direction.Opposite()))
			{
				case true:
					centreL = centre + HexFunctions.GetFirstSolidCorner(direction.Previous()) * 0.25f;
					centreR = centre + HexFunctions.GetSecondSolidCorner(direction.Next()) * 0.25f;
					break;
				case false:
					switch (HexCell.HasRiverThroughEdge(cell, direction.Next()))
					{
						case true:
							centreL = centre;
							centreR = math.lerp(centre, e.v5, 2f / 3f);
							break;
						case false:
							switch (HexCell.HasRiverThroughEdge(cell, direction.Previous()))
							{
								case true:
									centreL = math.lerp(centre, e.v1, 2f / 3f);
									centreR = centre;
									break;
								case false:
									switch (HexCell.HasRiverThroughEdge(cell, direction.Next2()))
									{
										case true:
											centreL = centre;
											centreR = centre + HexFunctions.GetSolidEdgeMiddle(direction.Next()) * (0.5f * HexFunctions.innerToOuter);
											break;
										case false:
											centreL = centre + HexFunctions.GetSolidEdgeMiddle(direction.Previous()) * (0.5f * HexFunctions.innerToOuter);
											centreR = centre;
											break;
									}
									break;
							}
							break;
					}
					break;
			}

			centre = math.lerp(centreL, centreR, 0.5f);
			EdgeVertices m = new EdgeVertices(math.lerp(centreL, e.v1, 0.5f), math.lerp(centreR, e.v5, 0.5f), 1f / 6f);
			m.v3.y = centre.y = e.v3.y;

			float3 indices = cell.Index;
			TriangulateEdgeStrip(wrapSize, terrianMesh, roadMesh, m, weights1, cell.Index, e, weights1, cell.Index);
			AddTriangleInfo(wrapSize, terrianMesh, centreL, m.v1, m.v2, indices, weights1, weights1, weights1);
			AddQuadInfo(wrapSize, terrianMesh, centreL, centre, m.v2, m.v3, indices, weights1, weights1, weights1, weights1);
			AddQuadInfo(wrapSize, terrianMesh, centre, centreR, m.v3, m.v4, indices, weights1, weights1, weights1, weights1);
			AddTriangleInfo(wrapSize, terrianMesh, centreR, m.v4, m.v5, indices, weights1, weights1, weights1);

			switch (cell.IsUnderwater)
			{
				case false:
					bool reversed = cell.IncomingRiver == direction;
					TriangulateRiverQuad(wrapSize, riverMesh, centreL, centreR, m.v2, m.v4, cell.RiverSurfaceY, 0.4f, reversed, indices);
					TriangulateRiverQuad(wrapSize, riverMesh, m.v2, m.v4, e.v2, e.v4, cell.RiverSurfaceY, 0.6f, reversed, indices);
					break;
			}
		}

		private void TriangulateWithRiverBeginOrEnd(int wrapSize, MeshData terrianMesh, MeshUV riverMesh, MeshUV roadMesh, HexCell cell, float3 centre, EdgeVertices e)
		{
			EdgeVertices m = new EdgeVertices(math.lerp(centre, e.v1, 0.5f), math.lerp(centre, e.v5, 0.5f));
			m.v3.y = e.v3.y;

			TriangulateEdgeStrip(wrapSize, terrianMesh, roadMesh, m, weights1, cell.Index, e, weights1, cell.Index);
			TriangulateEdgeFan(wrapSize, terrianMesh, centre, m, cell.Index);

			switch (cell.IsUnderwater)
			{
				case false:
					bool reversed = cell.HasIncomingRiver;
					float3 indices = cell.Index;
					TriangulateRiverQuad(wrapSize, riverMesh, m.v2, m.v4, e.v2, e.v4, cell.RiverSurfaceY, 0.6f, reversed, indices);
					centre.y = m.v2.y = m.v4.y = cell.RiverSurfaceY;
					float2x3 riverUVs;
					switch (reversed)
					{
						case true:
							riverUVs.c0 = new float2(0.5f, 0.4f);
							riverUVs.c1 = new float2(1f, 0.2f);
							riverUVs.c2 = new float2(0f, 0.2f);
							break;
						case false:
							riverUVs.c0 = new float2(0.5f, 0.4f);
							riverUVs.c1 = new float2(0f, 0.6f);
							riverUVs.c2 = new float2(1f, 0.6f);
							break;
					}
					AddTriangleInfoUV(wrapSize, riverMesh, centre, m.v2, m.v4, riverUVs.c0, riverUVs.c1, riverUVs.c2, indices, weights1, weights1, weights1);
					break;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TriangulateEdgeStrip(int wrapSize, MeshData terrianMesh, MeshUV roadMesh, EdgeVertices e1, float4 w1, float index1, EdgeVertices e2, float4 w2, float index2, bool hasRoad = false)
		{

			float3 indices = index1;
			indices.y = index2;
			AddQuadInfo(wrapSize, terrianMesh, e1.v1, e1.v2, e2.v1, e2.v2, indices, w1, w1, w2, w2);
			AddQuadInfo(wrapSize, terrianMesh, e1.v2, e1.v3, e2.v2, e2.v3, indices, w1, w1, w2, w2);
			AddQuadInfo(wrapSize, terrianMesh, e1.v3, e1.v4, e2.v3, e2.v4, indices, w1, w1, w2, w2);
			AddQuadInfo(wrapSize, terrianMesh, e1.v4, e1.v5, e2.v4, e2.v5, indices, w1, w1, w2, w2);
			switch (hasRoad)
			{
				case true:
					TriangulateRoadSegment(wrapSize, roadMesh, e1.v2, e1.v3, e1.v4, e2.v2, e2.v3, e2.v4, w1, w2, indices);
					break;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TriangulateEdgeFan(int wrapSize, MeshData terrianMesh, float3 centre, EdgeVertices edge, float index)
		{
			float3 indices = index;
			AddTriangleInfo(wrapSize, terrianMesh, centre, edge.v1, edge.v2, indices, weights1, weights1, weights1);
			AddTriangleInfo(wrapSize, terrianMesh, centre, edge.v2, edge.v3, indices, weights1, weights1, weights1);
			AddTriangleInfo(wrapSize, terrianMesh, centre, edge.v3, edge.v4, indices, weights1, weights1, weights1);
			AddTriangleInfo(wrapSize, terrianMesh, centre, edge.v4, edge.v5, indices, weights1, weights1, weights1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TriangulateRoadEdge(int wrapSize, MeshUV roadMesh, float3 centre, float3 mL, float3 mR, float index)
		{
			float3 indices = new float3(index);
			AddTriangleInfoUV(wrapSize, roadMesh, centre, mL, mR, new float2(1f, 0), new float2(0f), new float2(0f), indices, weights1, weights1, weights1);
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TriangulateRoadSegment(int wrapSize, MeshUV roadMesh, float3 v1, float3 v2, float3 v3, float3 v4, float3 v5, float3 v6, float4 w1, float4 w2, float3 indices)
		{
			float2x4 QuadUVs = new float2x4
			{
				c0 = 0f,
				c1 = new float2(1f, 0f),
				c2 = 0f,
				c3 = new float2(1f, 0f),
			};
			AddQuadInfoUV(wrapSize, roadMesh, v1, v2, v4, v5, QuadUVs.c0, QuadUVs.c1, QuadUVs.c2, QuadUVs.c3, indices, w1, w1, w2, w2);
			QuadUVs.c0 = new float2(1f, 0f);
			QuadUVs.c1 = 0f;
			QuadUVs.c2 = new float2(1f, 0f);
			QuadUVs.c3 = 0f;
			AddQuadInfoUV(wrapSize, roadMesh, v2, v3, v5, v6, QuadUVs.c0, QuadUVs.c1, QuadUVs.c2, QuadUVs.c3, indices, w1, w1, w2, w2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TriangulateRiverQuad(int wrapSize, MeshUV riverMesh, float3 v1, float3 v2, float3 v3, float3 v4, float y, float v, bool reversed, float3 indices)
		{
			TriangulateRiverQuad(wrapSize, riverMesh, v1, v2, v3, v4, y, y, v, reversed, indices);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void TriangulateRiverQuad(int wrapSize, MeshUV riverMesh, float3 v1, float3 v2, float3 v3, float3 v4, float y1, float y2, float v, bool reversed, float3 indices)
		{
			v1.y = v2.y = y1;
			v3.y = v4.y = y2;
			float2x4 riverUV;
			switch (reversed)
			{
				case true:
					riverUV.c0 = new float2(1f, 0.8f - v);
					riverUV.c1 = new float2(0f, 0.8f - v);
					riverUV.c2 = new float2(1f, 0.6f - v);
					riverUV.c3 = new float2(0f, 0.6f - v);
					break;
				case false:
					riverUV.c0 = new float2(0f, v);
					riverUV.c1 = new float2(1f, v);
					riverUV.c2 = new float2(0f, v + 0.2f);
					riverUV.c3 = new float2(1f, v + 0.2f);
					break;
			}
			AddQuadInfoUV(wrapSize, riverMesh, v1, v2, v3, v4, riverUV.c0, riverUV.c1, riverUV.c2, riverUV.c3, indices, weights1, weights1, weights2, weights2);
		}

		#region AddWithCellWeightsAndIndicesOnly
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddTriangleInfo(int wrapSize, MeshData mesh, float3 v1, float3 v2, float3 v3, float3 indices, float4 weights1, float4 weights2, float4 weights3)
		{
			uint vertexIndex = mesh.VertexIndex;
			mesh.verticesInternalTri[0] = HexFunctions.Perturb(noiseColours, v1, wrapSize);
			mesh.verticesInternalTri[1] = HexFunctions.Perturb(noiseColours, v2, wrapSize);
			mesh.verticesInternalTri[2] = HexFunctions.Perturb(noiseColours, v3, wrapSize);
			AddTriangle(mesh, vertexIndex);
			AddCellIndicesTriangle(mesh, indices);
			AddCellWeightsTriangle(mesh, weights1, weights2, weights3);
			mesh.ApplyTriangle();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddTriangleInfoUnperturbed(MeshData mesh, float3 v1, float3 v2, float3 v3, float3 indices, float4 weights1, float4 weights2, float4 weights3)
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
		private void AddQuadInfo(int wrapSize, MeshData mesh, float3 v1, float3 v2, float3 v3, float3 v4, float3 indices, float4 weights1, float4 weights2, float4 weights3, float4 weights4)
		{
			uint vertexIndex = mesh.VertexIndex;
			mesh.verticesInternalQuad[0] = HexFunctions.Perturb(noiseColours, v1, wrapSize);
			mesh.verticesInternalQuad[1] = HexFunctions.Perturb(noiseColours, v2, wrapSize);
			mesh.verticesInternalQuad[2] = HexFunctions.Perturb(noiseColours, v3, wrapSize);
			mesh.verticesInternalQuad[3] = HexFunctions.Perturb(noiseColours, v4, wrapSize);
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
		#endregion

		#region AddWithCellWeightsAndIndicesAndUVs
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddTriangleInfoUV(int wrapSize, MeshUV mesh, float3 v1, float3 v2, float3 v3, float2 uv1, float2 uv2, float2 uv3, float3 indices, float4 weights1, float4 weights2, float4 weights3)
		{
			uint vertexIndex = mesh.VertexIndex;
			mesh.verticesInternalTri[0] = HexFunctions.Perturb(noiseColours, v1, wrapSize);
			mesh.verticesInternalTri[1] = HexFunctions.Perturb(noiseColours, v2, wrapSize);
			mesh.verticesInternalTri[2] = HexFunctions.Perturb(noiseColours, v3, wrapSize);
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
		private void AddQuadInfoUV(int wrapSize, MeshUV mesh, float3 v1, float3 v2, float3 v3, float3 v4, float2 uv1, float2 uv2, float2 uv3, float2 uv4, float3 indices, float4 weights1, float4 weights2, float4 weights3, float4 weights4)
		{
			uint vertexIndex = mesh.VertexIndex;
			mesh.verticesInternalQuad[0] = HexFunctions.Perturb(noiseColours, v1, wrapSize);
			mesh.verticesInternalQuad[1] = HexFunctions.Perturb(noiseColours, v2, wrapSize);
			mesh.verticesInternalQuad[2] = HexFunctions.Perturb(noiseColours, v3, wrapSize);
			mesh.verticesInternalQuad[3] = HexFunctions.Perturb(noiseColours, v4, wrapSize);
			AddQuad(mesh, vertexIndex);
			AddCellIndicesQuad(mesh, indices);
			AddCellWeightsQuad(mesh, weights1, weights2, weights3, weights4);
			AddUVQuad(mesh, uv1, uv2, uv3, uv4);
			mesh.ApplyQuad();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddQuadInfoUnperturbedUV(MeshUV mesh, float3 v1, float3 v2, float3 v3, float3 v4, float2 uv1, float2 uv2, float2 uv3, float2 uv4, float3 indices, float4 weights1, float4 weights2, float4 weights3, float4 weights4)
		{
			uint vertexIndex = mesh.VertexIndex;
			mesh.verticesInternalQuad[0] = v1;
			mesh.verticesInternalQuad[1] = v2;
			mesh.verticesInternalQuad[2] = v3;
			mesh.verticesInternalQuad[3] = v4;
			AddQuad(mesh, vertexIndex);
			AddCellIndicesQuad(mesh, indices);
			AddCellWeightsQuad(mesh, weights1, weights2, weights3, weights4);
			AddUVQuad(mesh, uv1, uv2, uv3, uv4);
			mesh.ApplyQuad();
		}
		#endregion

		#region AddWithCellWeightsAndIndicesAndUVAUVB
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddTrianlgeInfoUVaUVb(int wrapSize, Mesh2UV mesh, float3 v1, float3 v2, float3 v3, float2 uvA1, float2 uvA2, float2 uvA3, float2 uvB1, float2 uvB2, float2 uvB3, float3 indices, float4 weights1, float4 weights2, float4 weights3)
		{
			uint vertexIndex = mesh.VertexIndex;
			mesh.verticesInternalTri[0] = HexFunctions.Perturb(noiseColours, v1, wrapSize);
			mesh.verticesInternalTri[1] = HexFunctions.Perturb(noiseColours, v2, wrapSize);
			mesh.verticesInternalTri[2] = HexFunctions.Perturb(noiseColours, v3, wrapSize);
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
		private void AddQuadInfoUVaUVb(int wrapSize, Mesh2UV mesh, float3 v1, float3 v2, float3 v3, float3 v4, float2 uvA1, float2 uvA2, float2 uvA3, float2 uvA4, float2 uvB1, float2 uvB2, float2 uvB3, float2 uvB4, float3 indices, float4 weights1, float4 weights2, float4 weights3, float4 weights4)
		{
			uint vertexIndex = mesh.VertexIndex;
			mesh.verticesInternalQuad[0] = HexFunctions.Perturb(noiseColours, v1, wrapSize);
			mesh.verticesInternalQuad[1] = HexFunctions.Perturb(noiseColours, v2, wrapSize);
			mesh.verticesInternalQuad[2] = HexFunctions.Perturb(noiseColours, v3, wrapSize);
			mesh.verticesInternalQuad[3] = HexFunctions.Perturb(noiseColours, v4, wrapSize);
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
		#endregion

		#region Walls

		private void AddWall(MeshBasic wallMesh, NativeList<PossibleFeaturePosition> features, int hexGridCell, EdgeVertices near, HexCell nearCell, EdgeVertices far, HexCell farCell, bool hasRiver, bool hasRoad)
		{
			int wrapSize = nearCell.wrapSize;
			if (nearCell.Walled != farCell.Walled && !nearCell.IsUnderwater && !farCell.IsUnderwater && HexCell.GetEdgeType(nearCell, farCell) != HexEdgeType.Cliff)
			{
				AddWallSegment(wrapSize, wallMesh, features, hexGridCell, near.v1, far.v1, near.v2, far.v2);
				if (hasRiver || hasRoad)
				{
					AddWallCap(wrapSize, wallMesh, near.v2, far.v2);
					AddWallCap(wrapSize, wallMesh, far.v4, near.v4);
				}
				else
				{
					AddWallSegment(wrapSize, wallMesh, features, hexGridCell, near.v2, far.v2, near.v3, far.v3);
					AddWallSegment(wrapSize, wallMesh, features, hexGridCell, near.v3, far.v3, near.v4, far.v4);
				}

				AddWallSegment(wrapSize, wallMesh, features, hexGridCell, near.v4, far.v4, near.v5, far.v5);
			}
		}

		private void AddWall(MeshBasic wallMesh, NativeList<PossibleFeaturePosition> features, int hexGridCell, float3 c1, HexCell cell1, float3 c2, HexCell cell2, float3 c3, HexCell cell3, int towerIndex = 0
			)
		{
			if (cell1.Walled)
			{
				if (cell2.Walled)
				{
					if (!cell3.Walled)
					{
						AddWallSegment(wallMesh, features, hexGridCell, c3, cell3, c1, cell1, c2, cell2, towerIndex);
					}
				}
				else if (cell3.Walled)
				{
					AddWallSegment(wallMesh, features, hexGridCell, c2, cell2, c3, cell3, c1, cell1, towerIndex);
				}
				else
				{
					AddWallSegment(wallMesh, features, hexGridCell, c1, cell1, c2, cell2, c3, cell3, towerIndex);
				}
			}
			else if (cell2.Walled)
			{
				if (cell3.Walled)
				{
					AddWallSegment(wallMesh, features, hexGridCell, c1, cell1, c2, cell2, c3, cell3, towerIndex);
				}
				else
				{
					AddWall(wallMesh, features, hexGridCell, c2, cell2, c3, cell3, c1, cell1, towerIndex);
				}
			}
			else if (cell3.Walled)
			{
				AddWallSegment(wallMesh, features, hexGridCell, c3, cell3, c1, cell1, c2, cell2, towerIndex);
			}
		}

		private void AddWallSegment(MeshBasic wallMesh, NativeList<PossibleFeaturePosition> features, int hexGridCell, float3 pivot, HexCell pivotCell, float3 left, HexCell leftCell, float3 right, HexCell rightCell, int towerIndex = 0)
		{
			if (pivotCell.IsUnderwater)
			{
				return;
			}

			int wrapSize = pivotCell.wrapSize;
			bool hasLeftWall = !leftCell.IsUnderwater && HexCell.GetEdgeType(pivotCell, leftCell) != HexEdgeType.Cliff;
			bool hasRightWall = !rightCell.IsUnderwater && HexCell.GetEdgeType(pivotCell, rightCell) != HexEdgeType.Cliff;
			if (hasLeftWall)
			{
				if (hasRightWall)
				{
					bool hasTower = false;
					if (leftCell.Elevation == rightCell.Elevation)
					{
						HexHash hash = HexFunctions.SampleHashGrid(hexHashData[pivotCell.grid].AsNativeArray(), (pivot + left + right) * (1f / 3f));
						hasTower = hash.e < HexFunctions.wallTowerThreshold;
					}
					AddWallSegment(wrapSize, wallMesh, features, hexGridCell, pivot, left, pivot, right, hasTower);
				}
				else if (leftCell.Elevation < rightCell.Elevation)
				{
					AddWallWedge(wrapSize, wallMesh, pivot, left, right);
				}
				else
				{
					AddWallCap(wrapSize, wallMesh, pivot, left);
				}
			}
			else if (hasRightWall)
			{
				if (rightCell.Elevation < leftCell.Elevation)
				{
					AddWallWedge(wrapSize, wallMesh, right, pivot, left);
				}
				else
				{
					AddWallCap(wrapSize, wallMesh, right, pivot);
				}
			}
		}

		private void AddWallSegment(int wrapSize, MeshBasic wallMesh, NativeList<PossibleFeaturePosition> features, int cellIndex, float3 nearLeft, float3 farLeft, float3 nearRight, float3 farRight, bool addTower = false)
		{
			nearLeft = HexFunctions.Perturb(noiseColours, nearLeft, wrapSize);
			farLeft = HexFunctions.Perturb(noiseColours, farLeft, wrapSize);
			nearRight = HexFunctions.Perturb(noiseColours, nearRight, wrapSize);
			farRight = HexFunctions.Perturb(noiseColours, farRight, wrapSize);

			float3 left = HexFunctions.WallLerp(nearLeft, farLeft);
			float3 right = HexFunctions.WallLerp(nearRight, farRight);

			float3 leftThicknessOffset = HexFunctions.WallThicknessOffset(nearLeft, farLeft);
			float3 rightThicknessOffset = HexFunctions.WallThicknessOffset(nearRight, farRight);

			float leftTop = left.y + HexFunctions.wallHeight;
			float rightTop = right.y + HexFunctions.wallHeight;

			float3 v1, v2, v3, v4;
			v1 = v3 = left - leftThicknessOffset;
			v2 = v4 = right - rightThicknessOffset;
			v3.y = leftTop;
			v4.y = rightTop;
			WallsAddQuadInfoUnperturbed(wallMesh, v1, v2, v3, v4);

			float3 t1 = v3, t2 = v4;

			v1 = v3 = left + leftThicknessOffset;
			v2 = v4 = right + rightThicknessOffset;
			v3.y = leftTop;
			v4.y = rightTop;
			WallsAddQuadInfoUnperturbed(wallMesh, v2, v1, v4, v3);

			WallsAddQuadInfoUnperturbed(wallMesh, t1, t2, v3, v4);

			if (addTower)
			{
				float3 rightDirection = right - left;
				rightDirection.y = 0f;
				AddFeature(features, cellIndex, (left + right) * 0.5f, rightDirection, FeatureType.WallTower);
			}
		}

		private void AddWallWedge(int wrapSize, MeshBasic wallMesh, float3 near, float3 far, float3 point)
		{
			near = HexFunctions.Perturb(noiseColours, near, wrapSize);
			far = HexFunctions.Perturb(noiseColours, far, wrapSize);
			point = HexFunctions.Perturb(noiseColours, point, wrapSize);

			float3 centre = HexFunctions.WallLerp(near, far);
			float3 thickness = HexFunctions.WallThicknessOffset(near, far);

			float3 v1, v2, v3, v4;
			float3 pointTop = point;
			point.y = centre.y;

			v1 = v3 = centre - thickness;
			v2 = v4 = centre + thickness;
			v3.y = v4.y = centre.y + HexFunctions.wallHeight;

			WallsAddQuadInfoUnperturbed(wallMesh, v1, point, v3, pointTop);
			WallsAddQuadInfoUnperturbed(wallMesh, point, v2, pointTop, v4);
			WallsAddTriangleInfoUnperturbed(wrapSize, wallMesh, pointTop, v3, v4);
		}

		private void AddWallCap(int wrapSize, MeshBasic wallMesh, float3 near, float3 far)
		{
			near = HexFunctions.Perturb(noiseColours, near, wrapSize);
			far = HexFunctions.Perturb(noiseColours, far, wrapSize);
			float3 centre = HexFunctions.WallLerp(near, far);
			float3 thickness = HexFunctions.WallThicknessOffset(near, far);
			float3 v1, v2, v3, v4;
			v1 = v3 = centre - thickness;
			v2 = v4 = centre + thickness;
			v3.y = v4.y = centre.y + HexFunctions.wallHeight;
			WallsAddQuadInfoUnperturbed(wallMesh, v1, v2, v3, v4);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void WallsAddTriangleInfoUnperturbed(int wrapSize, MeshBasic wallMesh, float3 v1, float3 v2, float3 v3)
		{
			uint vertexIndex = wallMesh.VertexIndex;
			wallMesh.verticesInternalTri[0] = HexFunctions.Perturb(noiseColours, v1, wrapSize);
			wallMesh.verticesInternalTri[1] = HexFunctions.Perturb(noiseColours, v2, wrapSize);
			wallMesh.verticesInternalTri[2] = HexFunctions.Perturb(noiseColours, v3, wrapSize);
			wallMesh.trianglesInternalTri[0] = vertexIndex;
			wallMesh.trianglesInternalTri[1] = vertexIndex + 1;
			wallMesh.trianglesInternalTri[2] = vertexIndex + 2;
			wallMesh.ApplyTriangle();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void WallsAddQuadInfoUnperturbed(MeshBasic wallMesh, float3 v1, float3 v2, float3 v3, float3 v4)
		{
			uint vertexIndex = wallMesh.VertexIndex;
			wallMesh.verticesInternalQuad[0] = v1;
			wallMesh.verticesInternalQuad[1] = v2;
			wallMesh.verticesInternalQuad[2] = v3;
			wallMesh.verticesInternalQuad[3] = v4;
			wallMesh.trianglesInternalQuad[0] = vertexIndex;
			wallMesh.trianglesInternalQuad[1] = vertexIndex + 2;
			wallMesh.trianglesInternalQuad[2] = vertexIndex + 1;
			wallMesh.trianglesInternalQuad[3] = vertexIndex + 1;
			wallMesh.trianglesInternalQuad[4] = vertexIndex + 2;
			wallMesh.trianglesInternalQuad[5] = vertexIndex + 3;
			wallMesh.ApplyQuad();
		}
		#endregion

		#region ListAdding
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddTriangle(MeshData mesh, uint vertexIndex)
		{
			mesh.trianglesInternalTri[0] = vertexIndex;
			mesh.trianglesInternalTri[1] = vertexIndex + 1;
			mesh.trianglesInternalTri[2] = vertexIndex + 2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddQuad(MeshUV mesh, uint vertexIndex)
		{
			mesh.trianglesInternalQuad[0] = vertexIndex;
			mesh.trianglesInternalQuad[1] = vertexIndex + 2;
			mesh.trianglesInternalQuad[2] = vertexIndex + 1;
			mesh.trianglesInternalQuad[3] = vertexIndex + 1;
			mesh.trianglesInternalQuad[4] = vertexIndex + 2;
			mesh.trianglesInternalQuad[5] = vertexIndex + 3;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddCellIndicesTriangle(MeshData mesh, float3 indices)
		{
			mesh.cellIndicesInternalTri[0] = indices;
			mesh.cellIndicesInternalTri[1] = indices;
			mesh.cellIndicesInternalTri[2] = indices;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddCellIndicesQuad(MeshUV mesh, float3 indices)
		{
			mesh.cellIndicesInternalQuad[0] = indices;
			mesh.cellIndicesInternalQuad[1] = indices;
			mesh.cellIndicesInternalQuad[2] = indices;
			mesh.cellIndicesInternalQuad[3] = indices;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddCellWeightsTriangle(MeshData mesh, float4 weights1, float4 weights2, float4 weights3)
		{
			mesh.cellWeightsInternalTri[0] = weights1;
			mesh.cellWeightsInternalTri[1] = weights2;
			mesh.cellWeightsInternalTri[2] = weights3;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddCellWeightsQuad(MeshUV mesh, float4 weights1, float4 weights2, float4 weights3, float4 weights4)
		{
			mesh.cellWeightsInternalQuad[0] = weights1;
			mesh.cellWeightsInternalQuad[1] = weights2;
			mesh.cellWeightsInternalQuad[2] = weights3;
			mesh.cellWeightsInternalQuad[3] = weights4;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddUVQuad(MeshUV mesh, float2 uv1, float2 uv2, float2 uv3, float2 uv4)
		{
			mesh.uvInternalQuad[0] = uv1;
			mesh.uvInternalQuad[1] = uv2;
			mesh.uvInternalQuad[2] = uv3;
			mesh.uvInternalQuad[3] = uv4;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddFeature(NativeList<PossibleFeaturePosition> features, int cellIndex, float3 position, float3 direction, FeatureType reservedFor)
		{
			features.Add(new PossibleFeaturePosition { cellIndex = cellIndex, position = position, direction = direction, ReservedFor = reservedFor });
		}
		#endregion


		private struct MeshBasic : INativeDisposable
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

			public JobHandle Dispose(JobHandle inputDeps)
			{
				inputDeps = vertices.Dispose(inputDeps);
				inputDeps = triangles.Dispose(inputDeps);

				inputDeps = verticesInternalTri.Dispose(inputDeps);
				inputDeps = trianglesInternalTri.Dispose(inputDeps);

				inputDeps = verticesInternalQuad.Dispose(inputDeps);
				return trianglesInternalQuad.Dispose(inputDeps);

			}

			public void Dispose()
			{
				vertices.Dispose();
				triangles.Dispose();

				verticesInternalTri.Dispose();
				trianglesInternalTri.Dispose();

				verticesInternalQuad.Dispose();
				trianglesInternalQuad.Dispose();
			}
		}

		private struct MeshData : INativeDisposable
		{
			public NativeList<HexGridVertex> vertices;
			public NativeList<HexGridIndices> cellIndices;
			public NativeList<HexGridWeights> cellWeights;
			public NativeList<HexGridTriangles> triangles;
			public uint VertexIndex { get { return (uint)vertices.Length; } }

			public MeshData(int capacity = 0)
			{
				vertices = new NativeList<HexGridVertex>(capacity, Allocator.Temp);
				cellIndices = new NativeList<HexGridIndices>(capacity, Allocator.Temp);
				cellWeights = new NativeList<HexGridWeights>(capacity, Allocator.Temp);
				triangles = new NativeList<HexGridTriangles>(capacity, Allocator.Temp);

				verticesInternalTri = new NativeArray<HexGridVertex>(3, Allocator.Temp);
				cellIndicesInternalTri = new NativeArray<HexGridIndices>(3, Allocator.Temp);
				cellWeightsInternalTri = new NativeArray<HexGridWeights>(3, Allocator.Temp);
				trianglesInternalTri = new NativeArray<HexGridTriangles>(3, Allocator.Temp);

				verticesInternalQuad = new NativeArray<HexGridVertex>(4, Allocator.Temp);
				cellIndicesInternalQuad = new NativeArray<HexGridIndices>(4, Allocator.Temp);
				cellWeightsInternalQuad = new NativeArray<HexGridWeights>(4, Allocator.Temp);
				trianglesInternalQuad = new NativeArray<HexGridTriangles>(6, Allocator.Temp);
			}

			public NativeArray<HexGridVertex> verticesInternalTri;
			public NativeArray<HexGridIndices> cellIndicesInternalTri;
			public NativeArray<HexGridWeights> cellWeightsInternalTri;
			public NativeArray<HexGridTriangles> trianglesInternalTri;

			public void ApplyTriangle()
			{
				vertices.AddRange(verticesInternalTri);
				cellIndices.AddRange(cellIndicesInternalTri);
				cellWeights.AddRange(cellWeightsInternalTri);
				triangles.AddRange(trianglesInternalTri);
			}

			public NativeArray<HexGridVertex> verticesInternalQuad;
			public NativeArray<HexGridIndices> cellIndicesInternalQuad;
			public NativeArray<HexGridWeights> cellWeightsInternalQuad;
			public NativeArray<HexGridTriangles> trianglesInternalQuad;

			public void ApplyQuad()
			{
				vertices.AddRange(verticesInternalQuad);
				cellIndices.AddRange(cellIndicesInternalQuad);
				cellWeights.AddRange(cellWeightsInternalQuad);
				triangles.AddRange(trianglesInternalQuad);
			}

			public JobHandle Dispose(JobHandle inputDeps)
			{
				inputDeps = vertices.Dispose(inputDeps);
				inputDeps = cellIndices.Dispose(inputDeps);
				inputDeps = cellWeights.Dispose(inputDeps);
				inputDeps = triangles.Dispose(inputDeps);

				inputDeps = verticesInternalTri.Dispose(inputDeps);
				inputDeps = cellIndicesInternalTri.Dispose(inputDeps);
				inputDeps = cellWeightsInternalTri.Dispose(inputDeps);
				inputDeps = trianglesInternalTri.Dispose(inputDeps);

				inputDeps = verticesInternalQuad.Dispose(inputDeps);
				inputDeps = cellIndicesInternalQuad.Dispose(inputDeps);
				inputDeps = cellWeightsInternalQuad.Dispose(inputDeps);
				return trianglesInternalQuad.Dispose(inputDeps);

			}

			public void Dispose()
			{
				vertices.Dispose();
				cellIndices.Dispose();
				cellWeights.Dispose();
				triangles.Dispose();

				verticesInternalTri.Dispose();
				cellIndicesInternalTri.Dispose();
				cellWeightsInternalTri.Dispose();
				trianglesInternalTri.Dispose();

				verticesInternalQuad.Dispose();
				cellIndicesInternalQuad.Dispose();
				cellWeightsInternalQuad.Dispose();
				trianglesInternalQuad.Dispose();
			}
		}

		private struct MeshUV : INativeDisposable
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

			public JobHandle Dispose(JobHandle inputDeps)
			{
				inputDeps = vertices.Dispose(inputDeps);
				inputDeps = cellIndices.Dispose(inputDeps);
				inputDeps = cellWeights.Dispose(inputDeps);
				inputDeps = triangles.Dispose(inputDeps);

				inputDeps = verticesInternalTri.Dispose(inputDeps);
				inputDeps = cellIndicesInternalTri.Dispose(inputDeps);
				inputDeps = cellWeightsInternalTri.Dispose(inputDeps);
				inputDeps = uvInternalTri.Dispose(inputDeps);
				inputDeps = trianglesInternalTri.Dispose(inputDeps);

				inputDeps = verticesInternalQuad.Dispose(inputDeps);
				inputDeps = cellIndicesInternalQuad.Dispose(inputDeps);
				inputDeps = cellWeightsInternalQuad.Dispose(inputDeps);
				inputDeps = uvInternalQuad.Dispose(inputDeps);
				return trianglesInternalQuad.Dispose(inputDeps);

			}

			public void Dispose()
			{
				vertices.Dispose();
				cellIndices.Dispose();
				cellWeights.Dispose();
				triangles.Dispose();

				verticesInternalTri.Dispose();
				cellIndicesInternalTri.Dispose();
				cellWeightsInternalTri.Dispose();
				uvInternalTri.Dispose();
				trianglesInternalTri.Dispose();

				verticesInternalQuad.Dispose();
				cellIndicesInternalQuad.Dispose();
				cellWeightsInternalQuad.Dispose();
				uvInternalQuad.Dispose();
				trianglesInternalQuad.Dispose();
			}
		}

		private struct Mesh2UV : INativeDisposable
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

			public JobHandle Dispose(JobHandle inputDeps)
			{
				inputDeps = vertices.Dispose(inputDeps);
				inputDeps = cellIndices.Dispose(inputDeps);
				inputDeps = cellWeights.Dispose(inputDeps);
				inputDeps = triangles.Dispose(inputDeps);

				inputDeps = verticesInternalTri.Dispose(inputDeps);
				inputDeps = cellIndicesInternalTri.Dispose(inputDeps);
				inputDeps = cellWeightsInternalTri.Dispose(inputDeps);
				inputDeps = uvInternalTri.Dispose(inputDeps);
				inputDeps = trianglesInternalTri.Dispose(inputDeps);

				inputDeps = verticesInternalQuad.Dispose(inputDeps);
				inputDeps = cellIndicesInternalQuad.Dispose(inputDeps);
				inputDeps = cellWeightsInternalQuad.Dispose(inputDeps);
				inputDeps = uvInternalQuad.Dispose(inputDeps);
				return trianglesInternalQuad.Dispose(inputDeps);

			}

			public void Dispose()
			{
				vertices.Dispose();
				cellIndices.Dispose();
				cellWeights.Dispose();
				triangles.Dispose();

				verticesInternalTri.Dispose();
				cellIndicesInternalTri.Dispose();
				cellWeightsInternalTri.Dispose();
				uvInternalTri.Dispose();
				trianglesInternalTri.Dispose();

				verticesInternalQuad.Dispose();
				cellIndicesInternalQuad.Dispose();
				cellWeightsInternalQuad.Dispose();
				uvInternalQuad.Dispose();
				trianglesInternalQuad.Dispose();
			}
		}
	}
}
