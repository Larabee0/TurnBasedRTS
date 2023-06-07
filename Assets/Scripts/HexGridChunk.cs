using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using Unity.Physics;
using System.Runtime.CompilerServices;
using UnityEngine.Rendering;
using Unity.Rendering;
using Unity.Transforms;
using Unity.Collections.LowLevel.Unsafe;

public class HexGridChunk : MonoBehaviour, IConvertGameObjectToEntity
{
   public static Color weights1 = new(1f, 0f, 0f);
   public static Color weights2 = new(0f, 1f, 0f);
   public static Color weights3 = new(0f, 0f, 1f);

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponent<HexGridChunkTag>(entity);
        dstManager.AddComponent<HexGridMeshEntities>(entity);
        //NativeArray<float3> colliderVerts = new(new float3[] { new float3(1), new float3(0), new float3(1, 0, 0) }, Allocator.Temp);
        //NativeArray<int3> colliderTris = new(1, Allocator.Temp);
        //colliderTris[0] = new int3(0, 1, 2);
        //PhysicsCollider collider = new()
        //{
        //    Value = Unity.Physics.MeshCollider.Create(colliderVerts, colliderTris)
        //};
        // dstManager.AddComponentData(entity, collider);
        dstManager.AddSharedComponentData(entity, new PhysicsWorldIndex { Value = 0u });
    }
}

public struct HexGridChunkTag: IComponentData
{
    public static implicit operator int(HexGridChunkTag v) { return v.Index; }
    public static implicit operator HexGridChunkTag(int v) { return new HexGridChunkTag { Index = v }; }
    public int Index;
}

public struct ChunkRefresh : IComponentData { }
public struct UnsortedChunkCellDataCompleted :IComponentData { }
public struct ChunkCellDataCompleted : IComponentData { }
public struct InitColumnIndex : IComponentData
{
    public static implicit operator int(InitColumnIndex v) { return v.Index; }
    public static implicit operator InitColumnIndex(int v) { return new InitColumnIndex { Index = v }; }
    public int Index;
}

public struct HexGridReference : IComponentData
{
    public static implicit operator Entity(HexGridReference v) { return v.Value; }
    public static implicit operator HexGridReference(Entity v) { return new HexGridReference { Value = v }; }
    public Entity Value;
}

public struct HexGridMeshUpdating : IComponentData { public double timeStamp; }

public struct HexGridMeshEntities : IComponentData
{
    public Entity Terrain;
    public Entity Rivers;
    public Entity Water;
    public Entity WaterShore;
    public Entity Estuaries;
    public Entity Roads;
    public Entity Walls;

    public Entity this[int index] => index switch
    {
        0 => Terrain,
        1 => Rivers,
        2 => Water,
        3 => WaterShore,
        4 => Estuaries,
        5 => Roads,
        6 => Walls,
        _ => Entity.Null,
    };
}

public partial class HexGridChunkSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem ecbEndSys;
    private EntityQuery TriangulatorInitQuery;
    private EntityQuery TriangulatorRunQuery;
    private EntityQuery TriangulatorCompleteQuery;

    private readonly List<MeshDataWrapper> meshDataWrappers = new();

    public BufferTypeHandle<CellWrapper> allChunkCellsTypeHandle;
    public BufferTypeHandle<HexCellReference> chunkCellsTypeHandle;

    protected override void OnCreate()
    {

        var entityQueries = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(ChunkCellDataCompleted),
                typeof(HexGridChunkTag),
                typeof(ChunkRefresh),
                typeof(CellWrapper),
                typeof(HexCellReference)
            },
            None = new ComponentType[]
            {
                typeof(HexGridMeshUpdating)
            }
        };
        TriangulatorInitQuery = GetEntityQuery(entityQueries);

        entityQueries = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(ChunkCellDataCompleted),
                typeof(HexGridChunkTag),
                typeof(ChunkRefresh),
                typeof(CellWrapper),
                typeof(HexCellReference),
                typeof(HexGridMeshUpdating)
            }
        };
        TriangulatorRunQuery = GetEntityQuery(entityQueries);


        entityQueries = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(HexGridChunkTag),
                typeof(ChunkRefresh),
                typeof(HexGridMeshUpdating),
                typeof(HexCellReference),
                typeof(HexGridMeshEntities)
            },
            None = new ComponentType[]
            {
                typeof(CellWrapper),
                typeof(ChunkCellDataCompleted)
            }
        };

        TriangulatorCompleteQuery = GetEntityQuery(entityQueries);

        allChunkCellsTypeHandle = GetBufferTypeHandle<CellWrapper>(true);
        chunkCellsTypeHandle = GetBufferTypeHandle<HexCellReference>(true);

        ecbEndSys = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer.ParallelWriter ecbEnd = ecbEndSys.CreateCommandBuffer().AsParallelWriter();
        // request for cells to provide data to chunk for terrain mesh calulation
        Dependency = Entities.WithAll<HexGridChunkTag, ChunkRefresh, HexCellReference>()
            .WithNone<CellWrapper,HexGridMeshUpdating>()
            .ForEach((ref Entity main, in DynamicBuffer<HexCellReference> chunkBuffer) =>
            {
                for (int i = 0; i < chunkBuffer.Length; i++)
                {
                    ecbEnd.AddComponent<HexCellChunkBuilder>(main.Index, chunkBuffer[i].Value);
                    ecbEnd.AddBuffer<HexCellChunkNeighbour>(main.Index, chunkBuffer[i].Value);
                }
                ecbEnd.AddBuffer<CellWrapper>(main.Index, main);
                
            }).ScheduleParallel(Dependency);

        // sort data for binary search, move to within hexMeshJob 
        // binarySearch example: cells.BinarySearch(new HexCellWrapper { cellBasic = new HexCellBasic { Index = 0 } });
        // returns -1 if the item is not present within the set and returns the index of the item in the set if present.
        Dependency = Entities.WithAll<HexGridChunkTag, ChunkRefresh, CellWrapper>().WithAll<UnsortedChunkCellDataCompleted>()
            .ForEach((ref Entity main, ref DynamicBuffer<CellWrapper> wrappedCells) =>
            {
                NativeArray<CellWrapper> cells = wrappedCells.AsNativeArray();
                cells.Sort(new WrappedCellIndexSorter());
                ecbEnd.RemoveComponent<UnsortedChunkCellDataCompleted>(main.Index, main);
                ecbEnd.AddComponent<ChunkCellDataCompleted>(main.Index, main);
            }).ScheduleParallel(Dependency);

        if (!TriangulatorInitQuery.IsEmpty)
        {
            double timeStamp = Time.ElapsedTime;
            int chunkCount = TriangulatorInitQuery.CalculateEntityCount();
            NativeParallelHashSet<int> includedChunks = new(chunkCount, Allocator.Persistent);
            NativeArray<HexGridChunkTag> chunkIndices = TriangulatorInitQuery.ToComponentDataArray<HexGridChunkTag>(Allocator.Temp);
            NativeArray<Entity> chunkWitnessComps = TriangulatorInitQuery.ToEntityArray(Allocator.Temp);
            HexGridMeshUpdating witness = new() { timeStamp = timeStamp };
            for (int i = 0; i < chunkIndices.Length; i++)
            {
                includedChunks.Add(chunkIndices[i]);
                ecbEnd.AddComponent(chunkWitnessComps[i].Index, chunkWitnessComps[i], witness);
            }
            MeshDataWrapper meshData = new(timeStamp, includedChunks, Mesh.AllocateWritableMeshData(chunkCount*7));
            meshDataWrappers.Add(meshData);

            allChunkCellsTypeHandle.Update(this);
            chunkCellsTypeHandle.Update(this);
            var triangulatorJob = new TrianglulateChunksJob
            {
                chunkEntityTypeHandle = GetEntityTypeHandle(),
                allChunkCellsTypeHandle = allChunkCellsTypeHandle,
                chunkCellsTypeHandle = chunkCellsTypeHandle,
                chunkColliderTypeHandle = GetComponentTypeHandle<PhysicsCollider>(),
                wrapSize = HexMetrics.wrapSize,
                noiseColours = HexMetrics.noiseColours,
                meshDataArray = meshData.meshDataArray,
                ecbEnd = ecbEnd
            };

            Dependency = triangulatorJob.ScheduleParallel(TriangulatorInitQuery,1, Dependency);
        }
        if (!TriangulatorCompleteQuery.IsEmpty)
        {
            int chunkEntities = TriangulatorCompleteQuery.CalculateEntityCount();
            NativeArray<Entity> chunks = TriangulatorCompleteQuery.ToEntityArray(Allocator.Temp);
            NativeArray<HexGridMeshEntities> chunkMeshEntities = TriangulatorCompleteQuery.ToComponentDataArray<HexGridMeshEntities>(Allocator.Temp);
            Mesh[] meshes = new Mesh[chunkEntities*7];
            double stamp = EntityManager.GetComponentData<HexGridMeshUpdating>(chunks[0]).timeStamp;
            for (int i = 0, m = 0; i < chunkMeshEntities.Length; i++, m += 7)
            {
                if(stamp != EntityManager.GetComponentData<HexGridMeshUpdating>(chunks[i]).timeStamp)
                {
                    Debug.LogError("Missaligned chunks! Mesh applicaiton will fail");
                }
                ecbEnd.RemoveComponent<HexGridMeshUpdating>(i, chunks[i]);
                ecbEnd.RemoveComponent<ChunkRefresh>(i, chunks[i]);
                HexGridMeshEntities meshEntities = chunkMeshEntities[i];

                for (int im = m, e = 0; im < m + 7; im++,e++)
                {
                    meshes[im] = EntityManager.GetComponentData<MeshRef>(meshEntities[e]).mesh;
                }
            }

            int wrapperIndex = meshDataWrappers.FindIndex(0, meshDataWrappers.Count, wrapper => wrapper.TimeStamp == stamp);
            if(wrapperIndex != -1)
            {
                Mesh.ApplyAndDisposeWritableMeshData(meshDataWrappers[wrapperIndex].meshDataArray, meshes);
                meshDataWrappers[wrapperIndex].chunksIncluded.Dispose();
                meshDataWrappers.RemoveAt(wrapperIndex);
                EntityCommandBuffer ecbEndSerial = ecbEndSys.CreateCommandBuffer();
                for (int i = 0, m = 0; i < chunkMeshEntities.Length; i++, m += 7)
                {
                    if (stamp != EntityManager.GetComponentData<HexGridMeshUpdating>(chunks[i]).timeStamp)
                    {
                        Debug.LogError("Missaligned chunks! Mesh applicaiton will fail");
                    }
                    ecbEnd.RemoveComponent<HexGridMeshUpdating>(i, chunks[i]);
                    ecbEnd.RemoveComponent<ChunkRefresh>(i, chunks[i]);
                    HexGridMeshEntities meshEntities = chunkMeshEntities[i];

                    meshEntities = chunkMeshEntities[i];
                    for (int im = m, e = 0; im < m+7; im++, e++)
                    {
                        Debug.LogFormat("Submesh Count: {0}, Vertex Couint: {1}", meshes[im].subMeshCount, meshes[im].vertexCount);
                        meshes[im].RecalculateNormals();
                        meshes[im].RecalculateBounds();
                        AABB bounds = meshes[im].bounds.ToAABB();
                        ecbEnd.SetComponent(wrapperIndex, meshEntities[e], new RenderBounds { Value = bounds });
                        ecbEnd.AddComponent(wrapperIndex, meshEntities[e], new Scale { Value = 1f });
                    }
                }
            }
        }

        ecbEndSys.AddJobHandleForProducer(Dependency);
    }

    protected override void OnDestroy()
    {
        for (int i = 0; i < meshDataWrappers.Count; i++)
        {
            meshDataWrappers[i].chunksIncluded.Dispose();
            meshDataWrappers[i].meshDataArray.Dispose();
        }
    }

    [BurstCompile]
    public struct TrianglulateChunksJob : IJobEntityBatchWithIndex
    {
        private static readonly float4 weights1 = new(1f, 0f, 0f, 0f);
        private static readonly float4 weights2 = new(0f, 1f, 0f, 0f);
        private static readonly float4 weights3 = new(0f, 0f, 1f, 0f);
        public int wrapSize;

        [ReadOnly]
        public EntityTypeHandle chunkEntityTypeHandle;

        [ReadOnly]
        public BufferTypeHandle<CellWrapper> allChunkCellsTypeHandle;
        [ReadOnly]
        public BufferTypeHandle<HexCellReference> chunkCellsTypeHandle;

        public ComponentTypeHandle<PhysicsCollider> chunkColliderTypeHandle;

        [ReadOnly]
        public NativeArray<float4> noiseColours;

        [NativeDisableParallelForRestriction]
        public Mesh.MeshDataArray meshDataArray;

        public EntityCommandBuffer.ParallelWriter ecbEnd;

        public void Execute(ArchetypeChunk batchInChunk, int batchIndex, int indexOfFirstEntityInQuery)
        {
            NativeArray<Entity> chunkEntities = batchInChunk.GetNativeArray(chunkEntityTypeHandle);
            NativeArray<PhysicsCollider> chunkColliders = batchInChunk.GetNativeArray(chunkColliderTypeHandle);
            BufferAccessor<CellWrapper> wrappedCells = batchInChunk.GetBufferAccessor(allChunkCellsTypeHandle);
            BufferAccessor<HexCellReference> chunkCellsAccessor = batchInChunk.GetBufferAccessor(chunkCellsTypeHandle);

            for (int i = 0; i < chunkEntities.Length; i++)
            {
                int internalChunkIndex = indexOfFirstEntityInQuery + i;

                int terrainIndex = internalChunkIndex * 7;

                Entity chunkEntity = chunkEntities[i];
                TriWrapper triangulatorWrapper = new(noiseColours, wrapSize, i, indexOfFirstEntityInQuery);
                DynamicBuffer<HexCellReference> chunkCells = chunkCellsAccessor[i];
                NativeArray<CellWrapper> allChunkCells = wrappedCells[i].ToNativeArray(Allocator.Temp);
                allChunkCells.Sort(new WrappedCellIndexSorter());

                for (int c = 0; c < chunkCells.Length; c++)
                {
                    int cellIndex = allChunkCells.BinarySearch(new CellWrapper { cellBasic = new HexCellBasic { Index = chunkCells[c].Index } });
                    if(cellIndex != -1)
                    {
                        CellWrapper cell = allChunkCells[cellIndex];
                        float3 center = cell.Position;
                        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                        {
                            float3x4 e = HexExtensions.BundleEdge(center + HexMetrics.GetFirstSolidCorner(d), center + HexMetrics.GetSecondSolidCorner(d));

                            TriangulateEdgeFan(triangulatorWrapper, cell, center, e);

                            if(d <= HexDirection.SE)
                            {
                                TriangulateConnection(triangulatorWrapper, allChunkCells, d, cell, e);
                            }
                        }
                    }
                }

                if (chunkColliders[i].IsValid)
                {
                    chunkColliders[i].Value.Dispose();
                }

                // chunkColliders[i] = new PhysicsCollider()
                // {
                //     Value = Unity.Physics.MeshCollider.CreateUnsafe(triangulatorWrapper.terrianMesh.vertices.AsArray(),
                //     triangulatorWrapper.terrianMesh.triangles.AsArray().Reinterpret<int3>(UnsafeUtility.SizeOf<uint>()))
                // };
                chunkColliders[i] = new PhysicsCollider()
                {
                    Value = Unity.Physics.MeshCollider.Create(triangulatorWrapper.terrianMesh.vertices.AsArray(),
                    triangulatorWrapper.terrianMesh.triangles.AsArray().Reinterpret<int3>(UnsafeUtility.SizeOf<uint>()))
                };
                Mesh.MeshData terrain = meshDataArray[terrainIndex];
                triangulatorWrapper.terrianMesh.ApplyMesh(terrain);
                ecbEnd.RemoveComponent<CellWrapper>(batchIndex, chunkEntity);
                ecbEnd.RemoveComponent<ChunkCellDataCompleted>(batchIndex, chunkEntity);
            }
        }

        private void TriangulateConnection(TriWrapper wrapper, NativeArray<CellWrapper> allChunkCells, HexDirection d, CellWrapper cell, float3x4 e1)
        {
            int neighbourIndex = allChunkCells.BinarySearch(new CellWrapper { cellBasic = new HexCellBasic { Index = cell.GetNeighbourIndex(d) } });
            if (neighbourIndex < 0)
            {
                return;
            }

            CellWrapper neighbour = allChunkCells[neighbourIndex];

            float3 bridge = HexMetrics.GetBridge(d);
            bridge.y = neighbour.Position.y - cell.Position.y;

            float3x4 e2 = HexExtensions.BundleEdge(e1.c0 + bridge, e1.c3 + bridge);

            switch (cell.cellTerrain.GetEdgeType(neighbour.cellTerrain))
            {
                case HexEdgeType.Slope:
                    TriangulateEdgeTerraces(wrapper, cell, e1, neighbour, e2);
                    break;
                default:
                    TriangulateEdgeStrip(wrapper, cell, e1, e2);
                    break;
            }

            int nextNeighbourIndex = allChunkCells.BinarySearch(new CellWrapper { cellBasic = new HexCellBasic { Index = cell.GetNeighbourIndex(d.Next()) } });
            if (d <= HexDirection.E && nextNeighbourIndex >= 0)
            {
                CellWrapper nextNeighbour = allChunkCells[nextNeighbourIndex];
                float3 v5 = e1.c3 + HexMetrics.GetBridge(d.Next());
                v5.y = nextNeighbour.Position.y;

                if (cell.Elevation <= neighbour.Elevation)
                {
                    if (cell.Elevation <= nextNeighbour.Elevation)
                    {
                        TriangulateCorner(wrapper, cell, e1.c3, neighbour, e2.c3, nextNeighbour, v5);
                    }
                    else
                    {
                        TriangulateCorner(wrapper, nextNeighbour, v5, cell, e1.c3, neighbour, e2.c3);
                    }
                }
                else if (neighbour.Elevation <= nextNeighbour.Elevation)
                {
                    TriangulateCorner(wrapper, neighbour, e2.c3, nextNeighbour, v5, cell, e1.c3);
                }
                else
                {
                    TriangulateCorner(wrapper, nextNeighbour, v5, cell, e1.c3, neighbour, e2.c3);
                }
            }
        }

        private void TriangulateEdgeFan(TriWrapper wrapper, CellWrapper cell, float3 center, float3x4 edge)
        {
            wrapper.terrianMesh.AddTriangleInfo(center, edge.c0, edge.c1,cell.Index,weights1,weights1,weights1);
            wrapper.terrianMesh.AddTriangleInfo(center, edge.c1, edge.c2, cell.Index, weights1, weights1, weights1);
            wrapper.terrianMesh.AddTriangleInfo(center, edge.c2, edge.c3, cell.Index, weights1, weights1, weights1);
        }

        private void TriangulateEdgeStrip(TriWrapper wrapper, CellWrapper cell,float3x4 e1, float3x4 e2)
        {
            wrapper.terrianMesh.AddQuadInfo(e1.c0, e1.c1, e2.c0, e2.c1, cell.Index, weights1, weights1, weights1, weights1);
            wrapper.terrianMesh.AddQuadInfo(e1.c1, e1.c2, e2.c1, e2.c2, cell.Index, weights1, weights1, weights1, weights1);
            wrapper.terrianMesh.AddQuadInfo(e1.c2, e1.c3, e2.c2, e2.c3, cell.Index, weights1, weights1, weights1, weights1);
        }

        private void TriangulateEdgeTerraces(TriWrapper wrapper, CellWrapper beginCell,float3x4 begin, CellWrapper endCell, float3x4 end)
        {
            float3x4 e2 = HexMetrics.TerraceLerp(begin, end, 1);

            TriangulateEdgeStrip(wrapper, beginCell, begin, e2);

            for (int i = 2; i < HexMetrics.terraceSteps; i++)
            {
                float3x4 e1 = e2;
                e2 = HexMetrics.TerraceLerp(begin, end, i);
                TriangulateEdgeStrip(wrapper, beginCell, e1, e2);
            }

            TriangulateEdgeStrip(wrapper, beginCell, e2, end);
        }

        private void TriangulateCorner(TriWrapper wrapper, CellWrapper bottomCell, float3 bottom,
            CellWrapper leftCell, float3 left, CellWrapper rightCell, float3 right)
        {
            HexEdgeType leftHexEdgeType = bottomCell.cellTerrain.GetEdgeType(leftCell.cellTerrain);
            HexEdgeType rightHexEdgeType = bottomCell.cellTerrain.GetEdgeType(rightCell.cellTerrain);

            if (leftHexEdgeType == HexEdgeType.Slope)
            {
                switch (rightHexEdgeType)
                {
                    case HexEdgeType.Slope:
                        TriangulateCornerTerraces(wrapper, bottomCell, bottom, leftCell, left, rightCell, right);
                        return;
                    case HexEdgeType.Flat:
                        TriangulateCornerTerraces(wrapper, leftCell, left, rightCell, right, bottomCell, bottom);
                        return;
                    default:
                        TriangulateCornerTerracesCliff(wrapper, bottomCell, bottom, leftCell, left, rightCell, right);
                        return;
                }
            }
            else if (rightHexEdgeType == HexEdgeType.Slope)
            {
                switch (leftHexEdgeType)
                {
                    case HexEdgeType.Flat:
                        TriangulateCornerTerraces(wrapper, rightCell, right, bottomCell, bottom, leftCell, left);
                        return;
                    default:
                        TriangulateCornerCliffTerraces(wrapper, bottomCell, bottom, leftCell, left, rightCell, right);
                        return;
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
                wrapper.terrianMesh.AddTriangleInfo(bottom, left, right, bottomCell.Index, weights1, weights1, weights1);
            }
        }

        private void TriangulateCornerTerraces(TriWrapper wrapper, CellWrapper beginCell, float3 begin, CellWrapper leftCell,
            float3 left, CellWrapper rightCell,float3 right)
        {
            float3 v3 = HexMetrics.TerraceLerp(begin, left, 1);
            float3 v4 = HexMetrics.TerraceLerp(begin, right, 1);
            wrapper.terrianMesh.AddTriangleInfo(begin, v3, v4,beginCell.Index,weights1, weights1, weights1);

            for (int i = 2; i < HexMetrics.terraceSteps; i++)
            {
                float3 v1 = v3;
                float3 v2 = v4;
                v3 = HexMetrics.TerraceLerp(begin, left, i);
                v4 = HexMetrics.TerraceLerp(begin, right, i);
                wrapper.terrianMesh.AddQuadInfo(v1, v2, v3, v4, beginCell.Index, weights1, weights1, weights1, weights1);
            }

            wrapper.terrianMesh.AddQuadInfo(v3, v4, left, right, beginCell.Index, weights1, weights1, weights1, weights1);
        }

        private void TriangulateCornerTerracesCliff(TriWrapper wrapper, CellWrapper beginCell, float3 begin, CellWrapper leftCell,
            float3 left, CellWrapper rightCell, float3 right)
        {
            float b = math.abs( 1f / (rightCell.Elevation - beginCell.Elevation));

            float3 boundary = math.lerp(Perturb(begin), Perturb(right), b);
            TriangulateBoundaryTriangle(wrapper, beginCell, begin, leftCell, left, boundary);

            switch (leftCell.cellTerrain.GetEdgeType(rightCell.cellTerrain))
            {
                case HexEdgeType.Slope:
                    TriangulateBoundaryTriangle(wrapper, leftCell, left, rightCell, right, boundary);
                    break;
                default:
                    wrapper.terrianMesh.AddTriangleInfoUnperturbed(Perturb(left), Perturb(right), boundary, beginCell.Index, weights1, weights1, weights1);
                    break;
            }
        }

        private void TriangulateCornerCliffTerraces(TriWrapper wrapper, CellWrapper beginCell, float3 begin, CellWrapper leftCell,
            float3 left, CellWrapper rightCell, float3 right)
        {
            float b = math.abs( 1f / (leftCell.Elevation - beginCell.Elevation));
            float3 boundary = math.lerp(Perturb(begin), Perturb(left), b);
            TriangulateBoundaryTriangle(wrapper, rightCell, right, beginCell, begin, boundary);

            switch (leftCell.cellTerrain.GetEdgeType(rightCell.cellTerrain))
            {
                case HexEdgeType.Slope:
                    TriangulateBoundaryTriangle(wrapper, leftCell, left, rightCell, right, boundary);
                    break;
                default:
                    wrapper.terrianMesh.AddTriangleInfoUnperturbed(Perturb(left), Perturb(right), boundary, beginCell.Index, weights1, weights1, weights1);
                    break;
            }
        }

        private void TriangulateBoundaryTriangle(TriWrapper wrapper, CellWrapper beginCell, float3 begin, CellWrapper leftCell,
            float3 left,float3 boundary)
        {

            float3 v2 = Perturb(HexMetrics.TerraceLerp(begin, left, 1));

            wrapper.terrianMesh.AddTriangleInfoUnperturbed(Perturb(begin), v2, boundary, beginCell.Index, weights1, weights1, weights1);

            for (int i = 2; i < HexMetrics.terraceSteps; i++)
            {
                float3 v1 = v2;
                v2 = Perturb(HexMetrics.TerraceLerp(begin, left, i));
                wrapper.terrianMesh.AddTriangleInfoUnperturbed(v1, v2, boundary, beginCell.Index, weights1, weights1, weights1);
            }

            wrapper.terrianMesh.AddTriangleInfoUnperturbed(v2, Perturb( left), boundary, beginCell.Index, weights1, weights1, weights1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 Perturb(float3 pos)
        {
            return HexMetrics.Perturb(noiseColours, pos, wrapSize);
        }

        private struct TriWrapper
        {
            public int chunkIndex;
            public int indexOfFirstEntityInQuery;

            public MeshData terrianMesh;
            public MeshUV riverMesh;
            public MeshData waterMesh;
            public MeshUV waterShoreMesh;
            public Mesh2UV estuaryMesh;
            public MeshUV roadMesh;
            public MeshBasic wallMesh;

            public TriWrapper(NativeArray<float4> noiseColours, int wrapSize,int chunkIndex, int indexOfFirstEntityInQuery)
            {
                this.chunkIndex = chunkIndex;
                this.indexOfFirstEntityInQuery = indexOfFirstEntityInQuery;

                terrianMesh = new MeshData(noiseColours, wrapSize, 0);
                riverMesh = new MeshUV(0);
                waterMesh = new MeshData(noiseColours, wrapSize, 0);
                waterShoreMesh = new MeshUV(0);
                estuaryMesh = new Mesh2UV(0);
                roadMesh = new MeshUV(0);
                wallMesh = new MeshBasic(0);
            }
        }
    }
}