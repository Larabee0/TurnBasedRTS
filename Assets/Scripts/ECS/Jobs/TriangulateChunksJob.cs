using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// This is a big job, but its actually a lot bigger than you think it is <see cref="MeshWrapper"/> hides a lot of code.
/// Imagine adding *everything* in the <see cref="MeshExtensions"/> file to this job, not just the class but the whole file, thats how
/// long it *really* is.
/// 
/// This job takes the cell information from a chunk and generates several meshes based on it. It also decides what and where
/// features should be placed if the cell calls for feature. These are then instantiated by the <see cref="HexFeatureSystem"/>.
/// </summary>
[BurstCompile, WithAll(typeof(HexChunkCellDataCompleted), typeof(HexChunkRefresh))]
public partial struct TriangulateChunksJob : IJobEntity
{
    private static readonly float4 weights1 = new(1f, 0f, 0f, 0f);
    private static readonly float4 weights2 = new(0f, 1f, 0f, 0f);
    private static readonly float4 weights3 = new(0f, 0f, 1f, 0f);
    public int wrapSize;

    public HexFeatureCollectionComponent featureCollections;

    [ReadOnly, DeallocateOnJobCompletion]
    public NativeArray<HexFeatureSpecialPrefab> specialPrefabs;

    [ReadOnly]
    public NativeArray<HexHash> hashGrid;

    [ReadOnly]
    public NativeArray<float4> noiseColours;

    [ReadOnly]
    public NativeParallelHashMap<int2, int> chunkToMeshMap;

    [NativeDisableParallelForRestriction]
    public Mesh.MeshDataArray meshDataArray;

    public EntityCommandBuffer.ParallelWriter ecbEnd;

    /// <summary>
    /// internal struct used to store the absolute mesh indices in <see cref="meshDataArray"/> for all meshes in the current chunk.
    /// The source of these indices is <see cref="chunkToMeshMap"/>
    /// </summary>
    private struct MeshIndices
    {
        public int terrainIndex;
        public int riversIndex;
        public int waterIndex;
        public int waterShoreIndex;
        public int estuariesIndex;
        public int roadsIndex;
        public int wallsIndex;
    }

    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity chunkEntity, ref DynamicBuffer<HexFeatureRequest> featureRequests,
        ref DynamicBuffer<HexChunkCellWrapper> wrappedCells, in DynamicBuffer<HexCellReference> chunkCells, in HexChunkTag chunkTag,
        in HexChunkMeshEntities meshEntities)
    {
        MeshIndices indices = new()
        {
            terrainIndex = chunkToMeshMap[new int2(chunkTag.Index, 0)],
            riversIndex = chunkToMeshMap[new int2(chunkTag.Index, 1)],
            waterIndex = chunkToMeshMap[new int2(chunkTag.Index, 2)],
            waterShoreIndex = chunkToMeshMap[new int2(chunkTag.Index, 3)],
            estuariesIndex = chunkToMeshMap[new int2(chunkTag.Index, 4)],
            roadsIndex = chunkToMeshMap[new int2(chunkTag.Index, 5)],
            wallsIndex = chunkToMeshMap[new int2(chunkTag.Index, 6)]
        };

        NativeArray<HexChunkCellWrapper> allChunkCells = wrappedCells.AsNativeArray();
        allChunkCells.Sort(new WrappedCellIndexSorter());

        // this struct is *massive*
        MeshWrapper meshWrapper = new(noiseColours, allChunkCells, wrapSize);

        // Triangulation (mesh generation)
        Triangulate(chunkCells, meshWrapper);

        /// Copying the mesh data in <see cref="meshWrapper"/> to the <see cref="meshDataArray"/>
        ApplyMeshes(jobChunkIndex, meshEntities, indices, meshWrapper);

        // process feature changes
        UpdateFeatureRequests(jobChunkIndex, chunkEntity, featureRequests, meshWrapper.featureRequests);

        // prevent the job re-running next frame.
        ecbEnd.RemoveComponent<HexChunkCellWrapper>(jobChunkIndex, chunkEntity);
        ecbEnd.RemoveComponent<HexChunkCellDataCompleted>(jobChunkIndex, chunkEntity);
    }

    /// <summary>
    /// Process all the feature requests generated during triangulation. it compares them with the current featureRequests in the HexFeatureRequest buffer.
    /// If there are any changes it will modify the buffer then add <see cref="HexFeatureUpdate"/> component to the chunk.
    /// </summary>
    /// <param name="jobChunkIndex"></param>
    /// <param name="chunkEntity"></param>
    /// <param name="featureRequests"></param>
    /// <param name="meshWrapperFeatureRequests"></param>
    private void UpdateFeatureRequests(int jobChunkIndex, Entity chunkEntity, DynamicBuffer<HexFeatureRequest> featureRequests, NativeList<HexFeatureRequest> meshWrapperFeatureRequests)
    {
        // if both the list and buffer are empty we don't need to run this block.
        if (featureRequests.Length > 0 || meshWrapperFeatureRequests.Length > 0)
        {
            // if the new requests are 0 and we got here, we can assume that we need to remove all the features currently
            // in the chunk.
            if (meshWrapperFeatureRequests.Length == 0)
            {
                featureRequests.Clear();
                ecbEnd.AddComponent<HexFeatureUpdate>(jobChunkIndex, chunkEntity);
            }
            // we can assume that both collections contain something here, so we just need to see if they are identical using a HashSet.
            // if they are not identical we need to request a featuer update.
            else if (meshWrapperFeatureRequests.Length == featureRequests.Length)
            {
                NativeParallelHashSet<uint> featureRequestSet = new(featureRequests.Length, Allocator.Temp);
                for (int i = 0; i < featureRequests.Length; i++)
                {
                    featureRequestSet.Add(featureRequests[i].Hash);
                }
                for (int i = 0; i < meshWrapperFeatureRequests.Length; i++)
                {
                    if (!featureRequestSet.Contains(meshWrapperFeatureRequests[i].Hash))
                    {
                        featureRequests.Clear();
                        featureRequests.AddRange(meshWrapperFeatureRequests.AsArray());
                        ecbEnd.AddComponent<HexFeatureUpdate>(jobChunkIndex, chunkEntity);
                        break;
                    }
                }
            }
            // if we get here its easiest to just clear the buffer re add the new requests and request a feature update.
            else
            {
                featureRequests.Clear();
                featureRequests.AddRange(meshWrapperFeatureRequests.AsArray());
                ecbEnd.AddComponent<HexFeatureUpdate>(jobChunkIndex, chunkEntity);
            }
        }
    }

    /// <summary>
    /// This method is used to copy the mesh data from the working <see cref="MeshWrapper"/> into the <see cref="meshDataArray"/>
    /// for access outside this job by the <see cref="HexChunkMeshApplicatorSystem"/>.
    /// </summary>
    /// <param name="jobChunkIndex">sortKey for the entity command buffer</param>
    /// <param name="meshEntities">entities that will host the meshes</param>
    /// <param name="indices">aboslute mesh indices in <see cref="meshDataArray"/></param>
    /// <param name="meshWrapper">working mesh data to be copied from</param>
    private void ApplyMeshes(int jobChunkIndex, HexChunkMeshEntities meshEntities, MeshIndices indices, MeshWrapper meshWrapper)
    {
        /// here each mesh has ApplyMesh called and a the correct <see cref="Mesh.MeshData"/> from <see cref="meshDataArray"/>
        /// is provided. The struct then interally copies its arrays into the the MeshData struct using the advanced meshAPI.
        meshWrapper.terrianMesh.ApplyMesh(meshDataArray[indices.terrainIndex]);
        meshWrapper.riverMesh.ApplyMesh(meshDataArray[indices.riversIndex]);
        meshWrapper.waterMesh.ApplyMesh(meshDataArray[indices.waterIndex]);
        meshWrapper.waterShoreMesh.ApplyMesh(meshDataArray[indices.waterShoreIndex]);
        meshWrapper.estuaryMesh.ApplyMesh(meshDataArray[indices.estuariesIndex]);
        meshWrapper.roadMesh.ApplyMesh(meshDataArray[indices.roadsIndex]);
        meshWrapper.wallMesh.ApplyMesh(meshDataArray[indices.wallsIndex]);

        /// <see cref="HexChunkMeshApplicatorSystem"/> needs to know the absolute meshIndex for each mesh entity. so we will add that now.
        ecbEnd.AddComponent(jobChunkIndex, meshEntities.Terrain, new HexMeshChunkIndex { meshArrayIndex = indices.terrainIndex });
        ecbEnd.AddComponent(jobChunkIndex, meshEntities.Rivers, new HexMeshChunkIndex { meshArrayIndex = indices.riversIndex });
        ecbEnd.AddComponent(jobChunkIndex, meshEntities.Water, new HexMeshChunkIndex { meshArrayIndex = indices.waterIndex });
        ecbEnd.AddComponent(jobChunkIndex, meshEntities.WaterShore, new HexMeshChunkIndex { meshArrayIndex = indices.waterShoreIndex });
        ecbEnd.AddComponent(jobChunkIndex, meshEntities.Estuaries, new HexMeshChunkIndex { meshArrayIndex = indices.estuariesIndex });
        ecbEnd.AddComponent(jobChunkIndex, meshEntities.Roads, new HexMeshChunkIndex { meshArrayIndex = indices.roadsIndex });
        ecbEnd.AddComponent(jobChunkIndex, meshEntities.Walls, new HexMeshChunkIndex { meshArrayIndex = indices.wallsIndex });

        // begin the process of rebuilding the chunk's collider.
        ecbEnd.AddComponent<HexChunkColliderRebuild>(jobChunkIndex, meshEntities.Terrain);
    }

    /// <summary>
    /// First Triangulate overload, this runs over each cell in the chunk (the 4x4 chunk cells not including the neighbouring cells)
    /// For each cell it calls <see cref="Triangulate(MeshWrapper, HexDirection, HexChunkCellWrapper)"/> and then it looks at adding 
    /// features & special features to teh central cell position.
    /// </summary>
    /// <param name="chunkCells">4x4 cells in the chunk</param>
    /// <param name="meshWrapper">MeshWrapper containing all working data</param>
    private void Triangulate(DynamicBuffer<HexCellReference> chunkCells, MeshWrapper meshWrapper)
    {
        for (int c = 0; c < chunkCells.Length; c++)
        {
            if (meshWrapper.GetCell(chunkCells[c].Index, out HexChunkCellWrapper cell))
            {
                for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                {
                    Triangulate(meshWrapper, d, cell);
                }
                if (!cell.cellTerrain.IsUnderwater)
                {
                    if (!cell.cellTerrain.HasRiver && !cell.cellTerrain.HasRoads)
                    {
                        AddFeature(meshWrapper, cell, cell.Position);
                    }
                    if (cell.cellTerrain.IsSpeical)
                    {
                        AddSpecialFeature(meshWrapper, cell, cell.Position);
                    }
                }
            }
        }
    }

    private void Triangulate(MeshWrapper wrapper,  HexDirection d, HexChunkCellWrapper cell)
    {
        float3 center = cell.Position;
        EdgeVertices e = new(center + HexMetrics.GetFirstSolidCorner(d), center + HexMetrics.GetSecondSolidCorner(d));
        if (cell.cellTerrain.HasRiver)
        {
            if (cell.cellTerrain.HasRiverThroughEdge(d))
            {
                e.c2.y = cell.cellTerrain.StreamBedY;
                if (cell.cellTerrain.HasRiverBeginOrEnd)
                {
                    TriangulateWithRiverBeginOrEnd(wrapper, cell, center, e);
                }
                else
                {
                    TriangulateWithRiver(wrapper, d, cell, center, e);
                }
            }
            else
            {
                TriangulateAdjacentToRiver(wrapper, d, cell, center, e);
            }
        }
        else
        {
            TriangulateWithoutRiver(wrapper, d, cell, center, e);

            if (!cell.cellTerrain.IsUnderwater && !cell.cellTerrain.HasRoadThroughEdge(d))
            {
                AddFeature(wrapper, cell, (center + e.c0 + e.c4) * (1f / 3f));
            }
        }

        if (d <= HexDirection.SE)
        {
            TriangulateConnection(wrapper, d, cell, e);
        }

        if (cell.cellTerrain.IsUnderwater)
        {
            TriangulateWater(wrapper, d, cell, center);
        }
    }

    private void TriangulateWater(MeshWrapper wrapper,  HexDirection d, HexChunkCellWrapper cell, float3 center)
    {
        center.y = cell.cellTerrain.WaterSurfaceY;

        int neighbourIndex = wrapper.GetNeighbourIndex(d, cell);
        if (neighbourIndex < 0)
        {
            TriangulateOpenWater(wrapper, d, cell, neighbourIndex, center);
            return;
        }

        HexChunkCellWrapper neighbor = wrapper.GetCell(neighbourIndex);
        if (!neighbor.cellTerrain.IsUnderwater)
        {
            TriangulateWaterShore(wrapper, d, cell, neighbor, center);
        }
        else
        {
            TriangulateOpenWater(wrapper, d, cell, neighbourIndex, center);
        }
    }

    private void TriangulateOpenWater(MeshWrapper wrapper, HexDirection direction, HexChunkCellWrapper cell, int neighbourIndex, float3 center)
    {
        float3 c1 = center + HexMetrics.GetFirstWaterCorner(direction);
        float3 c2 = center + HexMetrics.GetSecondWaterCorner(direction);

        float3 indices = new(cell.Index);
        wrapper.waterMesh.AddTriangleInfo(center, c1, c2, indices, weights1, weights1, weights1);

        if (direction <= HexDirection.SE && neighbourIndex >= 0)
        {
            HexChunkCellWrapper neighbour = wrapper.GetCell(neighbourIndex);
            float3 bridge = HexMetrics.GetWaterBridge(direction);
            float3 e1 = c1 + bridge;
            float3 e2 = c2 + bridge;

            indices.y = neighbour.Index;
            wrapper.waterMesh.AddQuadInfo(c1, c2, e1, e2, indices, weights1, weights1, weights2, weights2);

            if (direction <= HexDirection.E)
            {
                if (wrapper.GetNeighbourCell(direction.Next(),cell,out HexChunkCellWrapper nextNeighbour))
                {
                    if (!nextNeighbour.cellTerrain.IsUnderwater)
                    {
                        return;
                    }
                    indices.z = nextNeighbour.Index;
                    wrapper.waterMesh.AddTriangleInfo(c2, e2, c2 + HexMetrics.GetWaterBridge(direction.Next()), indices, weights1, weights2, weights3);
                }
            }
        }
    }

    private void TriangulateWaterShore(MeshWrapper wrapper, HexDirection direction, HexChunkCellWrapper cell, HexChunkCellWrapper neighbor, float3 center)
    {
        EdgeVertices e1 = new(center + HexMetrics.GetFirstWaterCorner(direction), center + HexMetrics.GetSecondWaterCorner(direction));

        float3 indices = new(cell.Index, neighbor.Index, cell.Index);
        indices.x = indices.z = cell.Index;
        indices.y = neighbor.Index;
        wrapper.waterMesh.AddTriangleInfo(center, e1.c0, e1.c1, indices, weights1, weights1, weights1);
        wrapper.waterMesh.AddTriangleInfo(center, e1.c1, e1.c2, indices, weights1, weights1, weights1);
        wrapper.waterMesh.AddTriangleInfo(center, e1.c2, e1.c3, indices, weights1, weights1, weights1);
        wrapper.waterMesh.AddTriangleInfo(center, e1.c3, e1.c4, indices, weights1, weights1, weights1);

        float3 center2 = neighbor.Position;
        if (neighbor.cellBasic.ColumnIndex < cell.cellBasic.ColumnIndex - 1)
        {
            center2.x += wrapSize * HexMetrics.innerDiameter;
        }
        else if (neighbor.cellBasic.ColumnIndex > cell.cellBasic.ColumnIndex + 1)
        {
            center2.x -= wrapSize * HexMetrics.innerDiameter;
        }
        center2.y = center.y;
        EdgeVertices e2 = new(center2 + HexMetrics.GetSecondSolidCorner(direction.Opposite()), center2 + HexMetrics.GetFirstSolidCorner(direction.Opposite()));

        if (cell.cellTerrain.HasRiverThroughEdge(direction))
        {
            TriangulateEstuary(wrapper, e1, e2, cell.cellTerrain.hasIncomingRiver && cell.cellTerrain.IncomingRiver == direction, indices);
        }
        else
        {
            float2x4 waterShoreUV = new()
            {
                c0 = 0f,
                c1 = 0f,
                c2 = new float2(0f, 1f),
                c3 = new float2(0f, 1f),
            };

            wrapper.waterShoreMesh.AddQuadInfoUV(e1.c0, e1.c1, e2.c0, e2.c1, waterShoreUV.c0, waterShoreUV.c1, waterShoreUV.c2, waterShoreUV.c3, indices, weights1, weights1, weights2, weights2);
            wrapper.waterShoreMesh.AddQuadInfoUV(e1.c1, e1.c2, e2.c1, e2.c2, waterShoreUV.c0, waterShoreUV.c1, waterShoreUV.c2, waterShoreUV.c3, indices, weights1, weights1, weights2, weights2);
            wrapper.waterShoreMesh.AddQuadInfoUV(e1.c2, e1.c3, e2.c2, e2.c3, waterShoreUV.c0, waterShoreUV.c1, waterShoreUV.c2, waterShoreUV.c3, indices, weights1, weights1, weights2, weights2);
            wrapper.waterShoreMesh.AddQuadInfoUV(e1.c3, e1.c4, e2.c3, e2.c4, waterShoreUV.c0, waterShoreUV.c1, waterShoreUV.c2, waterShoreUV.c3, indices, weights1, weights1, weights2, weights2);

        }

        if (wrapper.GetNeighbourCell(direction.Next(),cell,out HexChunkCellWrapper nextNeighbor))
        {
            float3 center3 = nextNeighbor.Position;
            if (nextNeighbor.cellBasic.ColumnIndex < cell.cellBasic.ColumnIndex - 1)
            {
                center3.x += wrapSize * HexMetrics.innerDiameter;
            }
            else if (nextNeighbor.cellBasic.ColumnIndex > cell.cellBasic.ColumnIndex + 1)
            {
                center3.x -= wrapSize * HexMetrics.innerDiameter;
            }
            float3 v3 = center3 + (nextNeighbor.cellTerrain.IsUnderwater ? HexMetrics.GetFirstWaterCorner(direction.Previous()) : HexMetrics.GetFirstSolidCorner(direction.Previous()));
            v3.y = center.y;

            indices.z = nextNeighbor.Index;
            wrapper.waterShoreMesh.AddTriangleInfoUV(e1.c4, e2.c4, v3, 0f, new float2(0f, 1f), new float2(0f, nextNeighbor.cellTerrain.IsUnderwater ? 0f : 1f), indices, weights1, weights2, weights3);
        }
    }

    private void TriangulateEstuary(MeshWrapper wrapper, EdgeVertices e1, EdgeVertices e2, bool incomingRiver, float3 indices)
    {
        wrapper.waterShoreMesh.AddTriangleInfoUV(e2.c0, e1.c1, e1.c0, new float2(0f, 1f), new float2(0f, 0f), new float2(0f, 0f), indices, weights2, weights1, weights1);
        wrapper.waterShoreMesh.AddTriangleInfoUV(e2.c4, e1.c4, e1.c3, new float2(0f, 1f), new float2(0f, 0f), new float2(0f, 0f), indices, weights2, weights1, weights1);

        wrapper.waterShoreMesh.AddQuadInfoUV(e2.c0, e1.c1, e2.c1, e1.c2, new float2(0f, 1f), new float2(0f, 0f), new float2(1f, 1f), new float2(0f, 0f), indices, weights2, weights1, weights2, weights1);
        wrapper.waterShoreMesh.AddTriangleInfoUV(e1.c2, e2.c1, e2.c3, new float2(0f, 0f), new float2(1f, 1f), new float2(1f, 1f), indices, weights1, weights2, weights2);
        wrapper.waterShoreMesh.AddQuadInfoUV(e1.c2, e1.c3, e2.c3, e2.c4, new float2(0f, 0f), new float2(0f, 0f), new float2(1f, 1f), new float2(0f, 1f), indices, weights1, weights1, weights2, weights2);

        float2x3 EstuariesTriangleUV1 = new()
        {
            c0 = 0f,
            c1 = 1f,
            c2 = 1f,
        };
        float2x4 EstuariesQuadUVA1 = new()
        {
            c0 = new float2(0f, 1f),
            c1 = 0f,
            c2 = 1f,
            c3 = 0f,
        };
        float2x4 EstuariesQuadUVA2 = new()
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

        wrapper.estuaryMesh.AddQuadInfoUVaUVb(e2.c0, e1.c1, e2.c1, e1.c2, EstuariesQuadUVA1.c0, EstuariesQuadUVA1.c1, EstuariesQuadUVA1.c2, EstuariesQuadUVA1.c3, EstuariesQuadUVB1.c0, EstuariesQuadUVB1.c1, EstuariesQuadUVB1.c2, EstuariesQuadUVB1.c3, indices, weights2, weights1, weights2, weights1);
        wrapper.estuaryMesh.AddTrianlgeInfoUVaUVb(e1.c2, e2.c1, e2.c3, EstuariesTriangleUV1.c0, EstuariesTriangleUV1.c1, EstuariesTriangleUV1.c2, EstuariesTriangleUV2.c0, EstuariesTriangleUV2.c1, EstuariesTriangleUV2.c2, indices, weights1, weights2, weights2);
        wrapper.estuaryMesh.AddQuadInfoUVaUVb(e1.c2, e1.c3, e2.c3, e2.c4, EstuariesQuadUVA2.c0, EstuariesQuadUVA2.c1, EstuariesQuadUVA2.c2, EstuariesQuadUVA2.c3, EstuariesQuadUVB2.c0, EstuariesQuadUVB2.c1, EstuariesQuadUVB2.c2, EstuariesQuadUVB2.c3, indices, weights1, weights1, weights2, weights2);
    }

    private void TriangulateConnection(MeshWrapper wrapper, HexDirection d, HexChunkCellWrapper cell, EdgeVertices e1)
    {
        if(!wrapper.GetNeighbourCell(d,cell,out HexChunkCellWrapper neighbour))
        {
            return;
        }

        float3 bridge = HexMetrics.GetBridge(d);
        bridge.y = neighbour.Position.y - cell.Position.y;

        EdgeVertices e2 = new(e1.c0 + bridge, e1.c4 + bridge);

        bool hasRoad = cell.cellTerrain.HasRoadThroughEdge(d);
        bool hasRiver = cell.cellTerrain.HasRiverThroughEdge(d);
        if (hasRiver)
        {
            e2.c2.y = neighbour.cellTerrain.StreamBedY;
            float3 indices = new(cell.Index, neighbour.Index, cell.Index);

            if (!cell.cellTerrain.IsUnderwater)
            {
                if (!neighbour.cellTerrain.IsUnderwater)
                {
                    TriangulateRiverQuad(wrapper,
                        e1.c1, e1.c3, e2.c1, e2.c3,
                        cell.cellTerrain.RiverSurfaceY, neighbour.cellTerrain.RiverSurfaceY, 0.8f,
                        cell.cellTerrain.hasIncomingRiver && cell.cellTerrain.IncomingRiver == d,
                        indices);
                }
                else if (cell.Elevation > neighbour.cellTerrain.waterLevel)
                {
                    TriangulateWaterfallInWater(wrapper,
                        e1.c1, e1.c3, e2.c1, e2.c3,
                        cell.cellTerrain.RiverSurfaceY, neighbour.cellTerrain.RiverSurfaceY,
                        neighbour.cellTerrain.WaterSurfaceY, indices);
                }
            }
            else if (!neighbour.cellTerrain.IsUnderwater && neighbour.Elevation > cell.cellTerrain.waterLevel)
            {
                TriangulateWaterfallInWater(wrapper,
                    e2.c3, e2.c1, e1.c3, e1.c1,
                    neighbour.cellTerrain.RiverSurfaceY, cell.cellTerrain.RiverSurfaceY,
                    cell.cellTerrain.WaterSurfaceY, indices);
            }
        }

        switch (cell.cellTerrain.GetEdgeType(neighbour.cellTerrain))
        {
            case HexEdgeType.Slope:
                TriangulateEdgeTerraces(wrapper, cell, e1, neighbour, e2, hasRoad);
                break;
            default:
                TriangulateEdgeStrip(wrapper, e1, weights1, cell.Index, e2, weights2, neighbour.Index, hasRoad);
                break;
        }

        AddWall(wrapper, e1, cell, e2, neighbour, hasRiver, hasRoad);

        if (d <= HexDirection.E && wrapper.GetNeighbourCell(d.Next(),cell, out HexChunkCellWrapper nextNeighbour))
        {
            float3 c4 = e1.c4 + HexMetrics.GetBridge(d.Next());
            c4.y = nextNeighbour.Position.y;

            if (cell.Elevation <= neighbour.Elevation)
            {
                if (cell.Elevation <= nextNeighbour.Elevation)
                {
                    TriangulateCorner(wrapper, e1.c4, cell, e2.c4, neighbour, c4, nextNeighbour);
                }
                else
                {
                    TriangulateCorner(wrapper, c4, nextNeighbour, e1.c4, cell, e2.c4, neighbour);
                }
            }
            else if (neighbour.Elevation <= nextNeighbour.Elevation)
            {
                TriangulateCorner(wrapper, e2.c4, neighbour, c4, nextNeighbour, e1.c4, cell);
            }
            else
            {
                TriangulateCorner(wrapper, c4, nextNeighbour, e1.c4, cell, e2.c4, neighbour);
            }
        }
    }

    private void TriangulateEdgeFan(MeshWrapper wrapper, float3 center, EdgeVertices edge, float index)
    {
        wrapper.terrianMesh.AddTriangleInfo(center, edge.c0, edge.c1, index, weights1, weights1, weights1);
        wrapper.terrianMesh.AddTriangleInfo(center, edge.c1, edge.c2, index, weights1, weights1, weights1);
        wrapper.terrianMesh.AddTriangleInfo(center, edge.c2, edge.c3, index, weights1, weights1, weights1);
        wrapper.terrianMesh.AddTriangleInfo(center, edge.c3, edge.c4, index, weights1, weights1, weights1);
    }

    private void TriangulateEdgeStrip(MeshWrapper wrapper, EdgeVertices e1, float4 w1, float index1, EdgeVertices e2, float4 w2, float index2, bool hasRoad = false)
    {
        float3 indices = new(index1, index2, index1);
        wrapper.terrianMesh.AddQuadInfo(e1.c0, e1.c1, e2.c0, e2.c1, indices, w1, w1, w2, w2);
        wrapper.terrianMesh.AddQuadInfo(e1.c1, e1.c2, e2.c1, e2.c2, indices, w1, w1, w2, w2);
        wrapper.terrianMesh.AddQuadInfo(e1.c2, e1.c3, e2.c2, e2.c3, indices, w1, w1, w2, w2);
        wrapper.terrianMesh.AddQuadInfo(e1.c3, e1.c4, e2.c3, e2.c4, indices, w1, w1, w2, w2);

        if (hasRoad)
        {
            TriangulateRoadSegment(wrapper, e1.c1, e1.c2, e1.c3, e2.c1, e2.c2, e2.c3, w1, w2, indices);
        }
    }

    private void TriangulateEdgeTerraces(MeshWrapper wrapper, HexChunkCellWrapper beginCell, EdgeVertices begin, HexChunkCellWrapper endCell, EdgeVertices end, bool hasRoad)
    {
        EdgeVertices e2 = HexMetrics.TerraceLerp(begin, end, 1);
        float4 w2 = HexMetrics.TerraceLerp(weights1, weights2, 1);

        float i1 = beginCell.Index;
        float i2 = endCell.Index;

        TriangulateEdgeStrip(wrapper, begin, weights1, i1, e2, w2, i2, hasRoad);

        for (int i = 2; i < HexMetrics.terraceSteps; i++)
        {
            EdgeVertices e1 = e2;
            float4 w1 = w2;
            e2 = HexMetrics.TerraceLerp(begin, end, i);
            w2 = HexMetrics.TerraceLerp(weights1, weights2, i);
            TriangulateEdgeStrip(wrapper, e1, w1, i1, e2, w2, i2, hasRoad);
        }

        TriangulateEdgeStrip(wrapper, e2, w2, i1, end, weights2, i2, hasRoad);
    }

    private void TriangulateCorner(MeshWrapper wrapper, float3 bottom, HexChunkCellWrapper bottomCell, float3 left, HexChunkCellWrapper leftCell, float3 right, HexChunkCellWrapper rightCell)
    {
        HexEdgeType leftHexEdgeType = bottomCell.cellTerrain.GetEdgeType(leftCell.cellTerrain);
        HexEdgeType rightHexEdgeType = bottomCell.cellTerrain.GetEdgeType(rightCell.cellTerrain);

        switch (leftHexEdgeType)
        {
            case HexEdgeType.Slope:
                switch (rightHexEdgeType)
                {
                    case HexEdgeType.Slope:
                        TriangulateCornerTerraces(wrapper, bottomCell, bottom, leftCell, left, rightCell, right);
                        break;
                    case HexEdgeType.Flat:
                        TriangulateCornerTerraces(wrapper, leftCell, left, rightCell, right, bottomCell, bottom);
                        break;
                    default:
                        TriangulateCornerTerracesCliff(wrapper, bottomCell, bottom, leftCell, left, rightCell, right);
                        break;
                }
                break;
            default:
                if (rightHexEdgeType == HexEdgeType.Slope)
                {
                    switch (leftHexEdgeType)
                    {
                        case HexEdgeType.Flat:
                            TriangulateCornerTerraces(wrapper, rightCell, right, bottomCell, bottom, leftCell, left);
                            break;
                        default:
                            TriangulateCornerCliffTerraces(wrapper, bottomCell, bottom, leftCell, left, rightCell, right);
                            break;
                    }
                }
                else if (leftCell.cellTerrain.GetEdgeType(rightCell.cellTerrain) == HexEdgeType.Slope)
                {
                    if (leftCell.Elevation < rightCell.Elevation)
                    {
                        TriangulateCornerCliffTerraces(wrapper, rightCell, right, bottomCell, bottom, leftCell, left);
                    }
                    else
                    {
                        TriangulateCornerTerracesCliff(wrapper, leftCell, left, rightCell, right, bottomCell, bottom);
                    }
                }
                else
                {
                    wrapper.terrianMesh.AddTriangleInfo(bottom, left, right, new float3(bottomCell.Index, leftCell.Index, rightCell.Index), weights1, weights2, weights3);
                }

                break;
        }

        AddWall(wrapper, bottom, bottomCell, left, leftCell, right, rightCell);
    }

    private void TriangulateCornerTerraces(MeshWrapper wrapper, HexChunkCellWrapper beginCell, float3 begin, HexChunkCellWrapper leftCell,
        float3 left, HexChunkCellWrapper rightCell, float3 right)
    {
        float3 c2 = HexMetrics.TerraceLerp(begin, left, 1);
        float3 c3 = HexMetrics.TerraceLerp(begin, right, 1);
        float4 w2 = HexMetrics.TerraceLerp(weights1, weights2, 1);
        float4 w3 = HexMetrics.TerraceLerp(weights1, weights3, 1);
        float3 indices = new(beginCell.Index, leftCell.Index, rightCell.Index);

        wrapper.terrianMesh.AddTriangleInfo(begin, c2, c3, indices, weights1, w2, w3);

        for (int i = 2; i < HexMetrics.terraceSteps; i++)
        {
            float3 c0 = c2;
            float3 c1 = c3;
            float4 w0 = w2;
            float4 w1 = w3;
            c2 = HexMetrics.TerraceLerp(begin, left, i);
            c3 = HexMetrics.TerraceLerp(begin, right, i);
            w2 = HexMetrics.TerraceLerp(weights1, weights2, i);
            w3 = HexMetrics.TerraceLerp(weights1, weights3, i);
            wrapper.terrianMesh.AddQuadInfo(c0, c1, c2, c3, indices, w0, w1, w2, w3);
        }

        wrapper.terrianMesh.AddQuadInfo(c2, c3, left, right, indices, weights2, weights2, weights3, weights3);
    }

    private void TriangulateCornerTerracesCliff(MeshWrapper wrapper, HexChunkCellWrapper beginCell, float3 begin, HexChunkCellWrapper leftCell,
        float3 left, HexChunkCellWrapper rightCell, float3 right)
    {
        float b = math.abs(1f / (rightCell.Elevation - beginCell.Elevation));

        float3 boundary = math.lerp(Perturb(begin), Perturb(right), b);
        float4 boundaryWeights = HexExtensions.Lerp(weights1, weights3, b);
        float3 indices = new(beginCell.Index, leftCell.Index, rightCell.Index);
        TriangulateBoundaryTriangle(wrapper, begin, weights1, left, weights2, boundary, boundaryWeights, indices);

        switch (leftCell.cellTerrain.GetEdgeType(rightCell.cellTerrain))
        {
            case HexEdgeType.Slope:
                TriangulateBoundaryTriangle(wrapper, left, weights2, right, weights3, boundary, boundaryWeights, indices);
                break;
            default:
                wrapper.terrianMesh.AddTriangleInfoUnperturbed(Perturb(left), Perturb(right), boundary, indices, weights2, weights3, boundaryWeights);
                break;
        }
    }

    private void TriangulateCornerCliffTerraces(MeshWrapper wrapper, HexChunkCellWrapper beginCell, float3 begin, HexChunkCellWrapper leftCell,
        float3 left, HexChunkCellWrapper rightCell, float3 right)
    {
        float b = math.abs(1f / (leftCell.Elevation - beginCell.Elevation));
        b = b < 0 ? -b : b;
        float3 boundary = math.lerp(Perturb(begin), Perturb(left), b);
        float4 boundaryWeights = HexExtensions.Lerp(weights1, weights2, b);

        float3 indices = new(beginCell.Index, leftCell.Index, rightCell.Index);

        TriangulateBoundaryTriangle(wrapper, right, weights3, begin, weights1, boundary, boundaryWeights, indices);

        switch (leftCell.cellTerrain.GetEdgeType(rightCell.cellTerrain))
        {
            case HexEdgeType.Slope:
                TriangulateBoundaryTriangle(wrapper, left, weights1, right, weights3, boundary, boundaryWeights, indices);
                break;
            default:
                wrapper.terrianMesh.AddTriangleInfoUnperturbed(Perturb(left), Perturb(right), boundary, indices, weights2, weights3, boundaryWeights);
                break;
        }
    }

    private void TriangulateBoundaryTriangle(MeshWrapper wrapper, float3 begin, float4 beginWeights, float3 left, float4 leftWeights, float3 boundary, float4 boundaryWeights, float3 indices)
    {
        float3 v2 = Perturb(HexMetrics.TerraceLerp(begin, left, 1));
        float4 w2 = HexMetrics.TerraceLerp(beginWeights, leftWeights, 1);

        wrapper.terrianMesh.AddTriangleInfoUnperturbed(Perturb(begin), v2, boundary, indices, boundaryWeights, boundaryWeights, boundaryWeights);

        for (int i = 2; i < HexMetrics.terraceSteps; i++)
        {
            float3 v1 = v2;
            float4 w1 = w2;
            v2 = Perturb(HexMetrics.TerraceLerp(begin, left, i));
            w2 = HexMetrics.TerraceLerp(beginWeights, leftWeights, i);
            wrapper.terrianMesh.AddTriangleInfoUnperturbed(v1, v2, boundary, indices, w1, w2, boundaryWeights);
        }

        wrapper.terrianMesh.AddTriangleInfoUnperturbed(v2, Perturb(left), boundary, indices, w2, leftWeights, boundaryWeights);
    }

    private void TriangulateWithoutRiver(MeshWrapper wrapper, HexDirection direction, HexChunkCellWrapper cell, float3 center, EdgeVertices e)
    {
        TriangulateEdgeFan(wrapper, center, e, cell.Index);

        if (cell.cellTerrain.HasRoads)
        {
            float2 interpolators = GetRoadInterpolators(direction, cell);
            TriangulateRoad(wrapper, center, math.lerp(center, e.c0, interpolators.x), math.lerp(center, e.c4, interpolators.y), e, cell.cellTerrain.HasRoadThroughEdge(direction), cell.Index);
        }
    }

    private void TriangulateWithRiver(MeshWrapper wrapper, HexDirection direction, HexChunkCellWrapper cell, float3 center, EdgeVertices e)
    {
        float3 centerL;
        float3 centerR;
        if (cell.cellTerrain.HasRiverThroughEdge(direction.Opposite()))
        {
            centerL = center + HexMetrics.GetFirstSolidCorner(direction.Previous()) * 0.25f;
            centerR = center + HexMetrics.GetSecondSolidCorner(direction.Next()) * 0.25f;
        }
        else if (cell.cellTerrain.HasRiverThroughEdge(direction.Next()))
        {
            centerL = center;
            centerR = math.lerp(center, e.c4, 2f / 3f);
        }
        else if (cell.cellTerrain.HasRiverThroughEdge(direction.Previous()))
        {
            centerL = math.lerp(center, e.c0, 2f / 3f);
            centerR = center;
        }
        else if (cell.cellTerrain.HasRiverThroughEdge(direction.Next2()))
        {
            centerL = center;
            centerR = center + HexMetrics.GetSolidEdgeMiddle(direction.Next()) * (0.5f * HexMetrics.innerToOuter);
        }
        else
        {
            centerL = center + HexMetrics.GetSolidEdgeMiddle(direction.Previous()) * (0.5f * HexMetrics.innerToOuter);
            centerR = center;
        }
        center = math.lerp(centerL, centerR, 0.5f);

        EdgeVertices m = new(math.lerp(centerL, e.c0, 0.5f), math.lerp(centerR, e.c4, 0.5f), 1f / 6f);
        m.c2.y = center.y = e.c2.y;

        TriangulateEdgeStrip(wrapper, m, weights1, cell.Index, e, weights1, cell.Index);

        float3 indices = new(cell.Index);
        wrapper.terrianMesh.AddTriangleInfo(centerL, m.c0, m.c1, indices, weights1, weights1, weights1);
        wrapper.terrianMesh.AddQuadInfo(centerL, center, m.c1, m.c2, indices, weights1, weights1, weights1, weights1);
        wrapper.terrianMesh.AddQuadInfo(center, centerR, m.c2, m.c3, indices, weights1, weights1, weights1, weights1);
        wrapper.terrianMesh.AddTriangleInfo(centerR, m.c3, m.c4, indices, weights1, weights1, weights1);

        if (!cell.cellTerrain.IsUnderwater)
        {
            bool reversed = cell.cellTerrain.IncomingRiver == direction;
            TriangulateRiverQuad(wrapper, centerL, centerR, m.c1, m.c3, cell.cellTerrain.RiverSurfaceY, cell.cellTerrain.RiverSurfaceY, 0.4f, reversed, indices);
            TriangulateRiverQuad(wrapper, m.c1, m.c3, e.c1, e.c3, cell.cellTerrain.RiverSurfaceY, cell.cellTerrain.RiverSurfaceY, 0.6f, reversed, indices);
        }
    }

    private void TriangulateAdjacentToRiver(MeshWrapper wrapper, HexDirection direction, HexChunkCellWrapper cell, float3 center, EdgeVertices e)
    {
        if (cell.cellTerrain.HasRoads)
        {
            TriangulateRoadAdjacentToRiver(wrapper, direction, cell, center, e);
        }

        if (cell.cellTerrain.HasRiverThroughEdge(direction.Next()))
        {
            if (cell.cellTerrain.HasRiverThroughEdge(direction.Previous()))
            {
                center += HexMetrics.GetSolidEdgeMiddle(direction) * (HexMetrics.innerToOuter * 0.5f);
            }
            else if (cell.cellTerrain.HasRiverThroughEdge(direction.Previous2()))
            {
                center += HexMetrics.GetFirstSolidCorner(direction) * 0.25f;
            }
        }
        else if (cell.cellTerrain.HasRiverThroughEdge(direction.Previous()) && cell.cellTerrain.HasRiverThroughEdge(direction.Next2()))
        {
            center += HexMetrics.GetSecondSolidCorner(direction) * 0.25f;
        }

        EdgeVertices m = new(math.lerp(center, e.c0, 0.5f), math.lerp(center, e.c4, 0.5f));

        TriangulateEdgeStrip(wrapper, m, weights1, cell.Index, e, weights1, cell.Index);
        TriangulateEdgeFan(wrapper, center, m, cell.Index);

        if (!cell.cellTerrain.IsUnderwater && !cell.cellTerrain.HasRoadThroughEdge(direction))
        {
            AddFeature(wrapper, cell, (center + e.c0 + e.c4) * (1f / 3f));
        }
    }

    private void TriangulateRoadAdjacentToRiver(MeshWrapper wrapper, HexDirection direction, HexChunkCellWrapper cell, float3 center, EdgeVertices e)
    {
        bool hasRoadThroughEdge = cell.cellTerrain.HasRoadThroughEdge(direction);
        bool previousHasRiver = cell.cellTerrain.HasRiverThroughEdge(direction.Previous());
        bool nextHasRiver = cell.cellTerrain.HasRiverThroughEdge(direction.Next());
        float2 interpolators = GetRoadInterpolators(direction, cell);
        float3 roadCenter = center;

        if (cell.cellTerrain.HasRiverBeginOrEnd)
        {
            roadCenter += HexMetrics.GetSolidEdgeMiddle(cell.cellTerrain.RiverBeginOrEndDirection.Opposite()) * (1f / 3f);
        }
        else if (cell.cellTerrain.IncomingRiver == cell.cellTerrain.OutgoingRiver.Opposite())
        {
            float3 corner;
            if (previousHasRiver)
            {
                if (!hasRoadThroughEdge && !cell.cellTerrain.HasRoadThroughEdge(direction.Next()))
                {
                    return;
                }
                corner = HexMetrics.GetSecondSolidCorner(direction);
            }
            else
            {
                if (!hasRoadThroughEdge && !cell.cellTerrain.HasRoadThroughEdge(direction.Previous()))
                {
                    return;
                }
                corner = HexMetrics.GetFirstSolidCorner(direction);
            }
            roadCenter += corner * 0.5f;
            if (cell.cellTerrain.IncomingRiver == direction.Next() &&
                (cell.cellTerrain.HasRoadThroughEdge(direction.Next2()) ||
                cell.cellTerrain.HasRoadThroughEdge(direction.Opposite())))
            {
                AddBridge(wrapper, roadCenter, center - corner * 0.5f);
            }
            center += corner * 0.25f;
        }
        else if (cell.cellTerrain.IncomingRiver == cell.cellTerrain.OutgoingRiver.Previous())
        {
            roadCenter -= HexMetrics.GetSecondCorner(cell.cellTerrain.IncomingRiver) * 0.2f;
        }
        else if (cell.cellTerrain.IncomingRiver == cell.cellTerrain.OutgoingRiver.Next())
        {
            roadCenter -= HexMetrics.GetFirstCorner(cell.cellTerrain.IncomingRiver) * 0.2f;
        }
        else if (previousHasRiver && nextHasRiver)
        {
            if (!hasRoadThroughEdge)
            {
                return;
            }
            float3 offset = HexMetrics.GetSolidEdgeMiddle(direction) * HexMetrics.innerToOuter;
            roadCenter += offset * 0.7f;
            center += offset * 0.5f;
        }
        else
        {
            HexDirection middle;
            if (previousHasRiver)
            {
                middle = direction.Next();
            }
            else if (nextHasRiver)
            {
                middle = direction.Previous();
            }
            else
            {
                middle = direction;
            }
            if (!cell.cellTerrain.HasRoadThroughEdge(middle) &&
                !cell.cellTerrain.HasRoadThroughEdge(middle.Previous()) &&
                !cell.cellTerrain.HasRoadThroughEdge(middle.Next()))
            {
                return;
            }
            float3 offset = HexMetrics.GetSolidEdgeMiddle(middle);
            roadCenter += offset * 0.25f;
            if (direction == middle && cell.cellTerrain.HasRoadThroughEdge(direction.Opposite())
            )
            {
                AddBridge(wrapper, roadCenter, center - offset * (HexMetrics.innerToOuter * 0.7f));
            }
        }

        float3 mL = math.lerp(roadCenter, e.c0, interpolators.x);
        float3 mR = math.lerp(roadCenter, e.c4, interpolators.y);
        TriangulateRoad(wrapper, roadCenter, mL, mR, e, hasRoadThroughEdge, cell.Index);
        if (previousHasRiver)
        {
            TriangulateRoadEdge(wrapper, roadCenter, center, mL, cell.Index);
        }
        if (nextHasRiver)
        {
            TriangulateRoadEdge(wrapper, roadCenter, mR, center, cell.Index);
        }
    }

    private void TriangulateRiverQuad(MeshWrapper wrapper, float3 c0, float3 c1, float3 c2, float3 c3, float y0, float y1, float v, bool reversed, float3 indices)
    {
        c0.y = c1.y = y0;
        c2.y = c3.y = y1;
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
        wrapper.riverMesh.AddQuadInfoUV(c0, c1, c2, c3, riverUV.c0, riverUV.c1, riverUV.c2, riverUV.c3, indices, weights1, weights1, weights2, weights2);
    }

    private void TriangulateWaterfallInWater(MeshWrapper wrapper, float3 v1, float3 v2, float3 v3, float3 v4, float y1, float y2, float waterY, float3 indices)
    {
        v1.y = v2.y = y1;
        v3.y = v4.y = y2;
        v1 = Perturb(v1);
        v2 = Perturb(v2);
        v3 = Perturb(v3);
        v4 = Perturb(v4);
        float t = (waterY - y2) / (y1 - y2);
        v3 = math.lerp(v3, v1, t);
        v4 = math.lerp(v4, v2, t);

        wrapper.riverMesh.AddQuadInfoUVUnperturbed(v1, v2, v3, v4, new float2(0f, 0.8f), new float2(1f, 0.8f), new float2(0f, 1f), new float2(1f, 1f), indices, weights1, weights1, weights2, weights2);
    }

    private void TriangulateRoad(MeshWrapper wrapper, float3 center, float3 mL, float3 mR, EdgeVertices e, bool hasRoadThroughCellEdge, float index)
    {
        if (hasRoadThroughCellEdge)
        {
            float3 indices;
            indices.x = indices.y = indices.z = index;
            float3 mC = math.lerp(mL, mR, 0.5f);
            TriangulateRoadSegment(wrapper, mL, mC, mR, e.c1, e.c2, e.c3, weights1, weights1, indices);
            wrapper.roadMesh.AddTriangleInfoUV(center, mL, mC, new float2(1f, 0f), new float2(0f), new float2(1f, 0f), indices, weights1, weights1, weights1);
            wrapper.roadMesh.AddTriangleInfoUV(center, mC, mR, new float2(1f, 0f), new float2(1f, 0f), new float2(0f), indices, weights1, weights1, weights1);
        }
        else
        {
            TriangulateRoadEdge(wrapper, center, mL, mR, index);
        }
    }

    private void TriangulateRoadSegment(MeshWrapper wrapper, float3 c0, float3 c1, float3 c2, float3 c3, float3 c4, float3 c5, float4 w1, float4 w2, float3 indices)
    {
        float2x4 QuadUVs = new()
        {
            c0 = 0f,
            c1 = new float2(1f, 0f),
            c2 = 0f,
            c3 = new float2(1f, 0f),
        };
        wrapper.roadMesh.AddQuadInfoUV(c0, c1, c3, c4, QuadUVs.c0, QuadUVs.c1, QuadUVs.c2, QuadUVs.c3, indices, w1, w1, w2, w2);
        QuadUVs.c0 = new float2(1f, 0f);
        QuadUVs.c1 = 0f;
        QuadUVs.c2 = new float2(1f, 0f);
        QuadUVs.c3 = 0f;
        wrapper.roadMesh.AddQuadInfoUV(c1, c2, c4, c5, QuadUVs.c0, QuadUVs.c1, QuadUVs.c2, QuadUVs.c3, indices, w1, w1, w2, w2);
    }

    private float2 GetRoadInterpolators(HexDirection direction, HexChunkCellWrapper cell)
    {
        float2 interpolators;
        if (cell.cellTerrain.HasRoadThroughEdge(direction))
        {
            interpolators.x = interpolators.y = 0.5f;
        }
        else
        {
            interpolators.x = cell.cellTerrain.HasRoadThroughEdge(direction.Previous()) ? 0.5f : 0.25f;
            interpolators.y = cell.cellTerrain.HasRoadThroughEdge(direction.Next()) ? 0.5f : 0.25f;
        }
        return interpolators;
    }

    private void TriangulateWithRiverBeginOrEnd(MeshWrapper wrapper, HexChunkCellWrapper cell, float3 center, EdgeVertices e)
    {
        EdgeVertices m = new(math.lerp(center, e.c0, 0.5f), math.lerp(center, e.c4, 0.5f));
        m.c2.y = e.c2.y;

        TriangulateEdgeStrip(wrapper, m, weights1, cell.Index, e, weights1, cell.Index);
        TriangulateEdgeFan(wrapper, center, m, cell.Index);

        if (!cell.cellTerrain.IsUnderwater)
        {
            bool reversed = cell.cellTerrain.hasIncomingRiver;
            float3 indices = new(cell.Index);
            TriangulateRiverQuad(wrapper, m.c1, m.c3, e.c1, e.c3, cell.cellTerrain.RiverSurfaceY, cell.cellTerrain.RiverSurfaceY, 0.6f, reversed, indices);
            center.y = m.c1.y = m.c3.y = cell.cellTerrain.RiverSurfaceY;
            float2x3 riverUVs = reversed switch
            {
                true => new float2x3
                {
                    c0 = new float2(0.5f, 0.4f),
                    c1 = new float2(1f, 0.2f),
                    c2 = new float2(0f, 0.2f)
                },
                _ => new float2x3
                {
                    c0 = new float2(0.5f, 0.4f),
                    c1 = new float2(0f, 0.6f),
                    c2 = new float2(1f, 0.6f)
                }
            };
            wrapper.riverMesh.AddTriangleInfoUV(center, m.c1, m.c3, riverUVs.c0, riverUVs.c1, riverUVs.c2, indices, weights1, weights1, weights1);
        }
    }

    private void AddWall(MeshWrapper wrapper, EdgeVertices near, HexChunkCellWrapper nearCell, EdgeVertices far, HexChunkCellWrapper farCell, bool hasRiver, bool hasRoad)
    {
        if (nearCell.cellTerrain.walled != farCell.cellTerrain.walled &&
            !nearCell.cellTerrain.IsUnderwater && !farCell.cellTerrain.IsUnderwater &&
            nearCell.cellTerrain.GetEdgeType(farCell.cellTerrain) != HexEdgeType.Cliff)
        {
            AddWallSegment(wrapper, near.c0, far.c0, near.c1, far.c1);
            if (hasRiver || hasRoad)
            {
                AddWallCap(wrapper, near.c1, far.c1);
                AddWallCap(wrapper, far.c3, near.c3);
            }
            else
            {
                AddWallSegment(wrapper, near.c1, far.c1, near.c2, far.c2);
                AddWallSegment(wrapper, near.c2, far.c2, near.c3, far.c3);
            }
            AddWallSegment(wrapper, near.c3, far.c3, near.c4, far.c4);
        }
    }

    private void AddWall(MeshWrapper wrapper, float3 c0, HexChunkCellWrapper cell0, float3 c1, HexChunkCellWrapper cell1, float3 c2, HexChunkCellWrapper cell2)
    {
        if (cell0.cellTerrain.walled)
        {
            if (cell1.cellTerrain.walled)
            {
                if (!cell2.cellTerrain.walled)
                {
                    AddWallSegment(wrapper, c2, cell2, c0, cell0, c1, cell1);
                }
            }
            else if (cell2.cellTerrain.walled)
            {
                AddWallSegment(wrapper, c1, cell1, c2, cell2, c0, cell0);
            }
            else
            {
                AddWallSegment(wrapper, c0, cell0, c1, cell1, c2, cell2);
            }
        }
        else if (cell1.cellTerrain.walled)
        {
            if (cell2.cellTerrain.walled)
            {
                AddWallSegment(wrapper, c0, cell0, c1, cell1, c2, cell2);
            }
            else
            {
                AddWallSegment(wrapper, c1, cell1, c2, cell2, c0, cell0);
            }
        }
        else if (cell2.cellTerrain.walled)
        {
            AddWallSegment(wrapper, c2, cell2, c0, cell0, c1, cell1);
        }
    }

    private void AddWallSegment(MeshWrapper wrapper, float3 nearLeft, float3 farLeft, float3 nearRight, float3 farRight, bool addTower = false)
    {
        nearLeft = Perturb(nearLeft);
        farLeft = Perturb(farLeft);
        nearRight = Perturb(nearRight);
        farRight = Perturb(farRight);

        float3 left = HexMetrics.WallLerp(nearLeft, farLeft);
        float3 right = HexMetrics.WallLerp(nearRight, farRight);

        float3 leftThicknessOffset = HexMetrics.WallThicknessOffset(nearLeft, farLeft);
        float3 rightThicknessOffset = HexMetrics.WallThicknessOffset(nearRight, farRight);

        float leftTop = left.y + HexMetrics.wallHeight;
        float rightTop = right.y + HexMetrics.wallHeight;

        float3 v1, v2, v3, v4;
        v1 = v3 = left - leftThicknessOffset;
        v2 = v4 = right - rightThicknessOffset;
        v3.y = leftTop;
        v4.y = rightTop;
        wrapper.wallMesh.AddQuadInfoUnperturbed(v1, v2, v3, v4);

        float3 t1 = v3, t2 = v4;

        v1 = v3 = left + leftThicknessOffset;
        v2 = v4 = right + rightThicknessOffset;
        v3.y = leftTop;
        v4.y = rightTop;
        wrapper.wallMesh.AddQuadInfoUnperturbed(v2, v1, v4, v3);

        wrapper.wallMesh.AddQuadInfoUnperturbed(t1, t2, v3, v4);

        if (addTower)
        {
            float3 rightDirection = right - left;
            rightDirection.y = 0f;
            wrapper.featureRequests.Add(new HexFeatureRequest
            {
                type = HexFeatureType.Tower,
                localPosition = (left + right) * 0.5f,
                directionRight = rightDirection
            });
        }
    }

    private void AddWallSegment(MeshWrapper wrapper, float3 pivot, HexChunkCellWrapper pivotCell, float3 left, HexChunkCellWrapper leftCell, float3 right, HexChunkCellWrapper rightCell)
    {
        if (pivotCell.cellTerrain.IsUnderwater)
        {
            return;
        }

        bool hasLeftWall = !leftCell.cellTerrain.IsUnderwater &&
            pivotCell.cellTerrain.GetEdgeType(leftCell.cellTerrain) != HexEdgeType.Cliff;
        bool hasRighWall = !rightCell.cellTerrain.IsUnderwater &&
            pivotCell.cellTerrain.GetEdgeType(rightCell.cellTerrain) != HexEdgeType.Cliff;

        if (hasLeftWall)
        {
            if (hasRighWall)
            {
                bool hasTower = false;
                if (leftCell.Elevation == rightCell.Elevation)
                {
                    HexHash hash = HexMetrics.SampleHashGrid(hashGrid, (pivot + left + right) * (1f / 3f));
                    hasTower = hash.e < HexMetrics.wallTowerThreshold;
                }
                AddWallSegment(wrapper, pivot, left, pivot, right, hasTower);
            }
            else if (leftCell.Elevation < rightCell.Elevation)
            {
                AddWallWedge(wrapper, pivot, left, right);
            }
            else
            {
                AddWallCap(wrapper, pivot, left);
            }
        }
        else if (hasRighWall)
        {
            if (rightCell.Elevation < leftCell.Elevation)
            {
                AddWallWedge(wrapper, right, pivot, left);
            }
            else
            {
                AddWallCap(wrapper, right, pivot);
            }
        }
    }

    private void AddWallCap(MeshWrapper wrapper, float3 near, float3 far)
    {
        near = Perturb(near);
        far = Perturb(far);

        float3 center = HexMetrics.WallLerp(near, far);
        float3 thickness = HexMetrics.WallThicknessOffset(near, far);

        float3 v1, v2, v3, v4;

        v1 = v3 = center - thickness;
        v2 = v4 = center + thickness;
        v3.y = v4.y = center.y + HexMetrics.wallHeight;
        wrapper.wallMesh.AddQuadInfoUnperturbed(v1, v2, v3, v4);
    }

    private void AddWallWedge(MeshWrapper wrapper, float3 near, float3 far, float3 point)
    {
        near = Perturb(near);
        far = Perturb(far);
        point = Perturb(point);

        float3 center = HexMetrics.WallLerp(near, far);
        float3 thickness = HexMetrics.WallThicknessOffset(near, far);

        float3 v1, v2, v3, v4;
        float3 pointTop = point;
        point.y = center.y;

        v1 = v3 = center - thickness;
        v2 = v4 = center + thickness;
        v3.y = v4.y = pointTop.y = center.y + HexMetrics.wallHeight;

        wrapper.wallMesh.AddQuadInfoUnperturbed(v1, point, v3, pointTop);
        wrapper.wallMesh.AddQuadInfoUnperturbed(point, v2, pointTop, v4);
        wrapper.wallMesh.AddTriangleInfoUnperturbed(pointTop, v3, v4);
    }

    private void AddBridge(MeshWrapper wrapper, float3 roadCentre0, float3 roadCentre1)
    {
        roadCentre0 = Perturb(roadCentre0);
        roadCentre1 = Perturb(roadCentre1);
        wrapper.featureRequests.Add(new HexFeatureRequest
        {
            type = HexFeatureType.Bridge,
            localPosition = (roadCentre0 + roadCentre1) * 0.5f,
            directionForward = roadCentre1 - roadCentre0,
            localScale = new float3(1f, 1f, math.distance(roadCentre0, roadCentre1) * (1f / HexMetrics.bridgeDesignLength))
        });
    }

    private void AddFeature(MeshWrapper wrapper, HexChunkCellWrapper cell, float3 position)
    {
        if (cell.cellTerrain.IsSpeical)
        {
            return;
        }

        HexHash hash = HexMetrics.SampleHashGrid(hashGrid, position);
        Entity prefab = PickPrefab(featureCollections.urbanCollections, cell.cellTerrain.urbanlevel, hash.a, hash.d, out float urban);
        Entity otherPrefab = PickPrefab(featureCollections.farmCollections, cell.cellTerrain.farmLevel, hash.b, hash.d, out float farm);
        float usedHash = hash.a;
        float scaleY = urban;
        if (prefab != Entity.Null)
        {
            if (otherPrefab != Entity.Null && hash.b < hash.a)
            {
                prefab = otherPrefab;
                scaleY = farm;
                usedHash = hash.b;
            }
        }
        else if (otherPrefab != Entity.Null)
        {
            scaleY = farm;
            prefab = otherPrefab;
            usedHash = hash.b;
        }
        otherPrefab = PickPrefab(featureCollections.plantCollections, cell.cellTerrain.plantLevel, hash.c, hash.d, out float plant);
        if (prefab != Entity.Null)
        {
            if (otherPrefab != Entity.Null && hash.c < usedHash)
            {
                scaleY = plant;
                prefab = otherPrefab;
            }
        }
        else if (otherPrefab != Entity.Null)
        {
            scaleY = plant;
            prefab = otherPrefab;
        }
        else
        {
            return;
        }

        position.y += scaleY * 0.5f;

        wrapper.featureRequests.Add(new HexFeatureRequest
        {
            type = HexFeatureType.Generic,
            prefab = prefab,
            localPosition = position,
            directionForward = math.mul(quaternion.EulerXYZ(0f, 360f * hash.e, 0f), math.forward())
        });
    }

    private void AddSpecialFeature(MeshWrapper wrapper, HexChunkCellWrapper cell, float3 position)
    {
        HexHash hash = HexMetrics.SampleHashGrid(hashGrid, position);
        wrapper.featureRequests.Add(new HexFeatureRequest
        {
            type = HexFeatureType.Generic,
            prefab = specialPrefabs[cell.cellTerrain.specialIndex - 1],
            localPosition = Perturb(position),
            directionForward = math.mul(quaternion.EulerXYZ(0f, 360f * hash.e, 0f), math.forward())
        });
    }

    private Entity PickPrefab(HexFeatureCollection collection, int level, float hash, float choice, out float scaleY)
    {
        scaleY = 1;
        if (level > 0)
        {
            float3 thresholds = HexMetrics.GetFeatureThresholds(level - 1);
            for (int i = 0; i < 3; i++)
            {
                if (hash < thresholds[i])
                {
                    HexFeaturePrefab prefab = collection[i].Pick(choice);
                    scaleY = prefab.localYscale;
                    return prefab.prefab;
                }
            }
        }
        return Entity.Null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TriangulateRoadEdge(MeshWrapper wrapper, float3 center, float3 mL, float3 mR, float index)
        => wrapper.roadMesh.AddTriangleInfoUV(center, mL, mR, new float2(1f, 0f), new float2(0f, 0f), new float2(0f, 0f), index, weights1, weights1, weights1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float3 Perturb(float3 pos) => HexMetrics.Perturb(noiseColours, pos, wrapSize);
}
