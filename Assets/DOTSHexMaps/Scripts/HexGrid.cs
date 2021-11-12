using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Jobs;
using Unity.Burst;
using Unity.Rendering;
using Unity.Physics;
using static DOTSHexagons.UpdateColliderSystem;

namespace DOTSHexagons
{
    public struct HexGridComponent : IComponentData
    {
        public int currentCentreColumnIndex;
        public int cellCountX;
        public int cellCountZ;
        public int cellCount;
        public int chunkCountX;
        public int chunkCountZ;
        public int chunkCount;
        public uint seed;
        public bool wrapping;
        public int wrapSize;
        public Entity gridEntity;
    }

    public struct HexGridUnInitialised : IComponentData{}

    public struct HexGridDataInitialised : IComponentData 
    {
        public int chunkIndex;
        public Entity gridEntity;
    }
    public struct HexGridInvokeEvent : IComponentData { }
    public struct HexGridCreated : IComponentData { }
    public struct GridWithColumns : IComponentData { }
    public struct GridWithChunks : IComponentData { }
    public struct HexGridVisualsPreInitialised : IComponentData { }
    public struct HexGridVisualsInitialised : IComponentData { }
    public struct MeshIndex : IComponentData { public int index; }

    [UpdateAfter(typeof(TransformSystemGroup))]
    public class HexGridSystemGroup: ComponentSystemGroup { }

    public struct HexColumn : IComponentData 
    {
        public int columnIndex;
    }

    [UpdateInGroup(typeof(HexGridSystemGroup))]
    public class HexGridCreateColumnsSystem : JobComponentSystem
    {
        EndSimulationEntityCommandBufferSystem ecbEndSystem;
        BeginSimulationEntityCommandBufferSystem ecbBeginSystem;
        EntityArchetype column;
        private readonly EntityQueryDesc CreateGridColumnQuery = new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(HexGridUnInitialised) } };

        protected override void OnCreate()
        {
            ecbEndSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();

            column = EntityManager.CreateArchetype(typeof(Translation), typeof(LocalToWorld), typeof(LocalToParent), typeof(Child), typeof(Parent), typeof(HexColumn));
        }
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            EntityQuery CreateGridColumns = GetEntityQuery(CreateGridColumnQuery);
            CreateGridColumns columnsJob = new CreateGridColumns
            {
                column = column,
                hGCTypeHandle = GetComponentTypeHandle<HexGridComponent>(true),
                ecbEnd = ecbEndSystem.CreateCommandBuffer().AsParallelWriter(),
                ecbBegin = ecbBeginSystem.CreateCommandBuffer().AsParallelWriter()
            };
            JobHandle outputDeps = columnsJob.ScheduleParallel(CreateGridColumns, 64,inputDeps);
            ecbEndSystem.AddJobHandleForProducer(outputDeps);
            ecbBeginSystem.AddJobHandleForProducer(outputDeps);
            return outputDeps;
        }
        [BurstCompile]
        private struct CreateGridColumns : IJobEntityBatch
        {
            public EntityArchetype column;
            [ReadOnly]
            public ComponentTypeHandle<HexGridComponent> hGCTypeHandle;

            public EntityCommandBuffer.ParallelWriter ecbEnd;
            public EntityCommandBuffer.ParallelWriter ecbBegin;
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {

                NativeArray<HexGridComponent> hexGridCompArray = batchInChunk.GetNativeArray(hGCTypeHandle);
                for (int i = 0; i < hexGridCompArray.Length; i++)
                {
                    HexGridComponent comp = hexGridCompArray[i];
                    int chunkCountX = comp.chunkCountX;
                    for (int col = 0; col < chunkCountX; col++)
                    {
                        Entity newColumn = ecbBegin.CreateEntity(batchIndex ^ col + i, column);
                        ecbBegin.SetComponent(batchIndex ^ col + i, newColumn, new Parent { Value = comp.gridEntity });
                        ecbBegin.SetComponent(batchIndex ^ col + i, newColumn, new HexColumn { columnIndex = col });
                    }
                    ecbEnd.RemoveComponent<HexGridUnInitialised>(batchIndex ^ i, comp.gridEntity);
                    ecbBegin.AddComponent<GridWithColumns>(batchIndex ^ i, comp.gridEntity);
                }
            }
        }
    }

    [UpdateInGroup(typeof(HexGridSystemGroup))]
    [UpdateAfter(typeof(HexGridCreateColumnsSystem))]
    public class HexGridCreateChunksSystem : JobComponentSystem
    {
        EndSimulationEntityCommandBufferSystem ecbEndSystem;
        BeginSimulationEntityCommandBufferSystem ecbBeginSystem;
        EntityArchetype chunk;
        private readonly EntityQueryDesc CreateGridChunkQuery = new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(GridWithColumns) } };

        protected override void OnCreate()
        {
            ecbEndSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();

            chunk = EntityManager.CreateArchetype(typeof(Translation), typeof(LocalToWorld), typeof(LocalToParent), typeof(Child), typeof(Parent), typeof(HexGridChunkComponent), typeof(HexGridCellBuffer));
        }
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            EntityQuery CreateGridChuks = GetEntityQuery(CreateGridChunkQuery);

            CreateGridChunksPrefab chunkJob = new CreateGridChunksPrefab
            {
                chunkPrefab = HexGridChunkSystem.HexGridChunkPrefab,
                hGCTypeHandle = GetComponentTypeHandle<HexGridComponent>(true),
                cTypeHandle = GetBufferTypeHandle<Child>(true),
                columnDataFromEntity = GetComponentDataFromEntity<HexColumn>(true),
                ecbEnd = ecbEndSystem.CreateCommandBuffer().AsParallelWriter(),
                ecbBegin = ecbBeginSystem.CreateCommandBuffer().AsParallelWriter()
            };

            JobHandle outputDeps = chunkJob.ScheduleParallel(CreateGridChuks, 64, inputDeps);
            ecbEndSystem.AddJobHandleForProducer(outputDeps);
            ecbBeginSystem.AddJobHandleForProducer(outputDeps);
            return outputDeps;
        }

        [BurstCompile]
        private struct CreateGridChunksPrefab : IJobEntityBatch
        {
            public Entity chunkPrefab;
            [ReadOnly]
            public ComponentTypeHandle<HexGridComponent> hGCTypeHandle;

            [ReadOnly]
            public BufferTypeHandle<Child> cTypeHandle;
            [ReadOnly]
            public ComponentDataFromEntity<HexColumn> columnDataFromEntity;
            public EntityCommandBuffer.ParallelWriter ecbEnd;
            public EntityCommandBuffer.ParallelWriter ecbBegin;
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                BufferAccessor<Child> childrenAccessor = batchInChunk.GetBufferAccessor(cTypeHandle);
                NativeArray<HexGridComponent> hexGridCompArray = batchInChunk.GetNativeArray(hGCTypeHandle);
                for (int index = 0; index < hexGridCompArray.Length; index++)
                {
                    HexGridComponent comp = hexGridCompArray[index];
                    int chunkCountX = comp.chunkCountX;
                    int chunkCountZ = comp.chunkCountZ;
                    NativeArray<HexGridChunkBuffer> HexGridChunkEntities = new NativeArray<HexGridChunkBuffer>(chunkCountX * chunkCountZ, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    DynamicBuffer<Child> gridChildren = childrenAccessor[index];
                    for (int z = 0, i = 0; z < chunkCountZ; z++)
                    {
                        for (int x = 0; x < chunkCountX; x++)
                        {
                            Entity newChunk = ecbBegin.Instantiate(batchIndex ^ index + i, chunkPrefab);
                            HexGridChunkEntities[i] = new HexGridChunkBuffer { ChunkEntity = newChunk };
                            ecbBegin.SetComponent(batchIndex ^ index + i, newChunk, new Parent { Value = GetColumn(gridChildren, x) });
                            ecbBegin.AddComponent(batchIndex ^ index + i, newChunk, new HexGridChunkInitialisationComponent { chunkIndex = i, gridEntity = comp.gridEntity });
                            i++;
                        }
                    }
                    ecbBegin.SetBuffer<HexGridChunkBuffer>(batchIndex ^ index, comp.gridEntity).CopyFrom(HexGridChunkEntities);
                    HexGridChunkEntities.Dispose();
                    ecbEnd.RemoveComponent<GridWithColumns>(batchIndex ^ index, comp.gridEntity);
                    ecbBegin.AddComponent<GridWithChunks>(batchIndex ^ index, comp.gridEntity);
                }
            }
            private Entity GetColumn(DynamicBuffer<Child> columns, int x)
            {
                for (int i = 0; i < columns.Length; i++)
                {
                    Entity col = columns[i].Value;
                    if (columnDataFromEntity[col].columnIndex == x)
                    {
                        return col;
                    }
                }
                return Entity.Null;
            }
        }
    }

    [UpdateInGroup(typeof(HexGridSystemGroup))]
    [UpdateAfter(typeof(HexGridCreateChunksSystem))]
    public class HexGridCreateCellsSystem : JobComponentSystem
    {
        EndSimulationEntityCommandBufferSystem ecbEndSystem;
        BeginSimulationEntityCommandBufferSystem ecbBeginSystem;
        private readonly EntityQueryDesc CreateGridCellsQuery = new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(GridWithChunks) } };

        protected override void OnCreate()
        {
            ecbEndSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            EntityQuery CreateGridCells = GetEntityQuery(CreateGridCellsQuery);
            CreateGridCells cellsJob = new CreateGridCells
            {
                noiseColours = HexMetrics.noiseColours,
                hGCTypeHandle = GetComponentTypeHandle<HexGridComponent>(true),
                hgCCFromEntity = GetComponentDataFromEntity<HexGridChunkInitialisationComponent>(true),
                childFromEntity = GetBufferFromEntity<Child>(true),
                gridChunkBufferTypeHandle = GetBufferTypeHandle<HexGridChunkBuffer>(true),
                ecbEnd = ecbEndSystem.CreateCommandBuffer().AsParallelWriter(),
                ecbBegin = ecbBeginSystem.CreateCommandBuffer().AsParallelWriter()
            };

            JobHandle outputDeps = cellsJob.ScheduleParallel(CreateGridCells, 64, inputDeps);
            ecbEndSystem.AddJobHandleForProducer(outputDeps);
            ecbBeginSystem.AddJobHandleForProducer(outputDeps);
            return outputDeps;
        }

        [BurstCompile]
        private struct CreateGridCells : IJobEntityBatch
        {
            [ReadOnly]
            public NativeArray<float4> noiseColours;
            [ReadOnly]
            public ComponentTypeHandle<HexGridComponent> hGCTypeHandle;
            [ReadOnly]
            public ComponentDataFromEntity<HexGridChunkInitialisationComponent> hgCCFromEntity;
            [ReadOnly]
            public BufferFromEntity<Child> childFromEntity;
            [ReadOnly]
            public BufferTypeHandle<HexGridChunkBuffer> gridChunkBufferTypeHandle;

            public EntityCommandBuffer.ParallelWriter ecbEnd;
            public EntityCommandBuffer.ParallelWriter ecbBegin;
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                BufferAccessor<HexGridChunkBuffer> gridChunkBufferAccessors = batchInChunk.GetBufferAccessor(gridChunkBufferTypeHandle);
                NativeArray<HexGridComponent> hexGridCompArray = batchInChunk.GetNativeArray(hGCTypeHandle);
                for (int index = 0; index < hexGridCompArray.Length; index++)
                {
                    HexGridComponent comp = hexGridCompArray[index];
                    int chunkCountX = comp.chunkCountX;
                    int cellCountX = comp.cellCountX;
                    int cellCountZ = comp.cellCountZ;
                    int wrapSize = comp.wrapSize;
                    bool wrapping = comp.wrapping;
                    //Get chunks
                    NativeArray<HexGridChunkBuffer> gridChunkBuffer = gridChunkBufferAccessors[index].ToNativeArray(Allocator.Temp);
                    DynamicBuffer<Child> gridChildren = childFromEntity[comp.gridEntity];
                    for (int i = 0; i < gridChildren.Length; i++)
                    {
                        Entity column = gridChildren[i].Value;
                        DynamicBuffer<Child> columnChunks = childFromEntity[column];
                        for (int c = 0; c < columnChunks.Length; c++)
                        {
                            int chunkIndex = hgCCFromEntity[columnChunks[c].Value].chunkIndex;
                            HexGridChunkBuffer chunkOnGrid = gridChunkBuffer[chunkIndex];
                            chunkOnGrid.ChunkEntity = columnChunks[c].Value;
                            gridChunkBuffer[chunkIndex] = chunkOnGrid;
                        }
                    }

                    NativeArray<HexCell> cells = new NativeArray<HexCell>(cellCountZ * cellCountX, Allocator.Temp);



                    for (int i = 0, z = 0; z < cellCountZ; z++)
                    {
                        for (int x = 0; x < cellCountX; x++)
                        {
                            cells[i] = HexCell.CreateWithNoNeighbours(i++, x, z, wrapSize);
                        }
                    }

                    for (int i = 0; i < cells.Length; i++)
                    {
                        HexCell cell = cells[i];
                        int x = cell.x;
                        int z = cell.z;
                        cell.grid = comp.gridEntity;
                        cell.Position.x = ((x + z * 0.5f - z / 2) * HexMetrics.innerDiameter);
                        cell.Position.y = 0f;
                        cell.Position.z = (z * (HexMetrics.outerRadius * 1.5f));
                        cell.wrapSize = comp.wrapSize;
                        cell.ColumnIndex = x / HexMetrics.chunkSizeX;
                        cell.Explorable = wrapping switch
                        {
                            true => z > 0 && z < cellCountZ - 1,
                            false => x > 0 && z > 0 && x < cellCountX - 1 && z < cellCountZ - 1
                        };
                        switch (x > 0)
                        {
                            case true:
                                cell = HexCell.SetNeighbour(cell, HexDirection.W, i - 1);
                                switch (wrapping && x == cellCountX - 1)
                                {
                                    case true:
                                        cell = HexCell.SetNeighbour(cell, HexDirection.E, i - x);
                                        break;
                                }
                                break;
                        }
                        switch (z > 0)
                        {
                            case true:
                                switch ((z & 1) == 0)
                                {
                                    case true:
                                        cell = HexCell.SetNeighbour(cell, HexDirection.SE, i - cellCountX);
                                        switch (x > 0)
                                        {
                                            case true:
                                                cell = HexCell.SetNeighbour(cell, HexDirection.SW, i - cellCountX - 1);
                                                break;
                                            case false:
                                                switch (wrapping)
                                                {
                                                    case true:
                                                        cell = HexCell.SetNeighbour(cell, HexDirection.SW, i - 1);
                                                        break;
                                                }
                                                break;
                                        }
                                        break;
                                    case false:
                                        cell = HexCell.SetNeighbour(cell, HexDirection.SW, i - cellCountX);
                                        switch (x < cellCountX - 1)
                                        {
                                            case true:
                                                cell = HexCell.SetNeighbour(cell, HexDirection.SE, i - cellCountX + 1);
                                                break;
                                            case false:
                                                switch (wrapping)
                                                {
                                                    case true:
                                                        cell = HexCell.SetNeighbour(cell, HexDirection.SE, i - cellCountX * 2 + 1);
                                                        break;
                                                }
                                                break;
                                        }
                                        break;
                                }
                                break;
                        }
                        cell.elevation = 0;
                        cell.RefreshPosition(noiseColours);
                        int chunkX = x / HexMetrics.chunkSizeX;
                        int chunkZ = z / HexMetrics.chunkSizeZ;

                        cell.ChunkIndex = chunkX + chunkZ * chunkCountX;
                        cells[i] = cell;
                    }

                    for (int i = 0; i < cells.Length; i++)
                    {
                        HexCell cell = cells[i];
                        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                        {
                            int neighbourIndex = HexCell.GetNeighbourIndex(cell, d);
                            switch (neighbourIndex)
                            {
                                case int.MinValue:
                                    break;
                                default:
                                    HexCell neighbour = cells[neighbourIndex];
                                    neighbour = HexCell.SetNeighbour(neighbour, d.Opposite(), i);
                                    cells[neighbourIndex] = neighbour;
                                    break;
                            }
                        }
                        cells[i] = cell;
                    }

                    int cellsPerChunk = HexMetrics.chunkSizeX * HexMetrics.chunkSizeZ;
                    for (int i = 0; i < gridChunkBuffer.Length; i++)
                    {
                        NativeList<int> list = new NativeList<int>(cellsPerChunk, Allocator.Temp);
                        for (int c = 0; c < cells.Length; c++)
                        {
                            HexCell cell = cells[c];
                            if (cell.ChunkIndex != i)
                            {
                                continue;
                            }
                            list.Add( cell.Index);

                            if (list.Length >= cellsPerChunk)
                            {
                                break;
                            }

                        }
                        NativeArray<HexGridCellBuffer> buffer = new NativeArray<HexGridCellBuffer>(cellsPerChunk, Allocator.Temp);
                        for (int b = 0; b < cellsPerChunk; b++)
                        {

                            HexCell cell = cells[list[b]];
                            int x = cell.x;
                            int z = cell.z;
                            int localX = x - (x / HexMetrics.chunkSizeX) * HexMetrics.chunkSizeX;
                            int localZ = z - (z / HexMetrics.chunkSizeZ) * HexMetrics.chunkSizeZ;
                            buffer[localX + localZ * HexMetrics.chunkSizeX] = new HexGridCellBuffer { cellIndex = list[b] };
                        }
                        ecbBegin.AddComponent<HexGridDataInitialised>(batchIndex ^ i, gridChunkBuffer[i].ChunkEntity);
                        ecbBegin.SetBuffer<HexGridCellBuffer>(batchIndex ^ i, gridChunkBuffer[i].ChunkEntity).CopyFrom(buffer);
                        buffer.Dispose();
                        list.Dispose();
                    }
                    ecbBegin.SetBuffer<HexCell>(batchIndex ^ index, comp.gridEntity).CopyFrom(cells);
                    cells.Dispose();
                    ecbBegin.SetBuffer<HexGridChunkBuffer>(batchIndex ^ index, comp.gridEntity).CopyFrom(gridChunkBuffer);
                    gridChunkBuffer.Dispose();
                    ecbEnd.RemoveComponent<GridWithChunks>(batchIndex ^ index, comp.gridEntity);
                    ecbBegin.AddComponent<HexGridDataInitialised>(batchIndex ^ index, comp.gridEntity);
                }
            }
        }
    }

    [UpdateInGroup(typeof(HexGridSystemGroup))]
    [UpdateAfter(typeof(HexGridCreateCellsSystem))]
    public class HexGridPreSystem : JobComponentSystem
    {
        EndSimulationEntityCommandBufferSystem ecbEndSystem;
        BeginSimulationEntityCommandBufferSystem ecbBeginSystem;
        private readonly EntityQueryDesc CreateGridVisualsPreQuery = new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(HexGridDataInitialised) } };
        private readonly EntityQueryDesc CreateGridChunkVisualsPreQuery = new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridChunkComponent), typeof(HexGridDataInitialised) } };

        protected override void OnCreate()
        {
            ecbEndSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            EntityQuery CreateGridVisualsPre = GetEntityQuery(CreateGridVisualsPreQuery);
            EntityQuery CreateGridChunkVisualsPre = GetEntityQuery(CreateGridChunkVisualsPreQuery);

            if (CreateGridVisualsPre.IsEmpty || CreateGridChunkVisualsPre.IsEmpty)
            {
                return inputDeps;
            }
            NativeArray<float3> colliderVerts = new NativeArray<float3>(new float3[] { new float3(1), new float3(0), new float3(1, 0, 0) }, Allocator.TempJob);
            NativeArray<int3> colliderTris = new NativeArray<int3>(1, Allocator.TempJob);
            colliderTris[0] = new int3(0, 1, 2);
            SetFeautreContainers VisualsPreInitialise = new SetFeautreContainers
            {
                colliderVerts = colliderVerts,
                colliderTris = colliderTris,
                gridMarkForGeneration = GetComponentDataFromEntity<Generate>(true),
                linkedEntityGroups = GetBufferTypeHandle<LinkedEntityGroup>(true),
                cellBuffer = GetBufferTypeHandle<HexGridCellBuffer>(true),
                hGCCTypeHandle = GetComponentTypeHandle<HexGridChunkComponent>(true),
                hGCICTypeHandle = GetComponentTypeHandle<HexGridChunkInitialisationComponent>(true),
                ecbEnd = ecbEndSystem.CreateCommandBuffer().AsParallelWriter(),
                ecbBegin = ecbBeginSystem.CreateCommandBuffer().AsParallelWriter()
            };

            JobHandle outputDeps = VisualsPreInitialise.ScheduleParallel(CreateGridChunkVisualsPre, 64, inputDeps);
            ecbEndSystem.AddJobHandleForProducer(outputDeps);
            ecbBeginSystem.AddJobHandleForProducer(outputDeps);
            return outputDeps;
        }
        [BurstCompile]
        private struct CreateGridVisuals : IJobEntityBatch
        {
            public EntityArchetype featureContainer;
            public EntityArchetype chunkRenderer;

            [ReadOnly]
            [DeallocateOnJobCompletion]
            public NativeArray<float3> colliderVerts;
            [ReadOnly]
            [DeallocateOnJobCompletion]
            public NativeArray<int3> colliderTris;
            [ReadOnly]
            public ComponentTypeHandle<HexGridComponent> hGCTypeHandle;
            [ReadOnly]
            public BufferTypeHandle<HexGridChunkBuffer> gridChunkBufferTypeHandle;
            public EntityCommandBuffer.ParallelWriter ecbEnd;
            public EntityCommandBuffer.ParallelWriter ecbBegin;
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                BufferAccessor<HexGridChunkBuffer> gridChunkBufferAccessors = batchInChunk.GetBufferAccessor(gridChunkBufferTypeHandle);
                NativeArray<HexGridComponent> hexGridCompArray = batchInChunk.GetNativeArray(hGCTypeHandle);
                for (int index = 0; index < hexGridCompArray.Length; index++)
                {
                    HexGridComponent comp = hexGridCompArray[index];
                    DynamicBuffer<HexGridChunkBuffer> gridChunkBuffer = gridChunkBufferAccessors[index];
                    for (int i = 0; i < gridChunkBuffer.Length; i++)
                    {
                        // we need to add a component to each new child which tells the next job, what that child needs assigning to
                        // ie, terrian mesh, feature mesh, etc
                        // children order is not garauteed and therefore leads to the terrian collider never being on the terrian entity
                        Entity ChunkEntity = gridChunkBuffer[i].ChunkEntity;
                        PhysicsCollider collider = new PhysicsCollider
                        {
                            Value = Unity.Physics.MeshCollider.Create(colliderVerts, colliderTris)
                        };

                        Entity featureEntity = ecbBegin.CreateEntity(batchIndex, featureContainer);
                        ecbBegin.SetComponent(batchIndex, featureEntity, new FeatureContainer { GridEntity = comp.gridEntity });
                        ecbBegin.SetComponent(batchIndex, featureEntity, new Parent { Value = ChunkEntity });
                        ecbBegin.AddComponent(batchIndex, featureEntity, new MeshIndex { index = 0 });

                        Entity terrianMesh = ecbBegin.CreateEntity(batchIndex, chunkRenderer);
                        ecbBegin.AddBuffer<Float3ForCollider>(batchIndex, terrianMesh);
                        ecbBegin.AddBuffer<UintForCollider>(batchIndex, terrianMesh);
                        ecbBegin.AddComponent(batchIndex, terrianMesh, collider);
                        ecbBegin.AddComponent(batchIndex, terrianMesh, new MeshIndex { index = 1 });
                        ecbBegin.SetComponent(batchIndex, terrianMesh, new Parent { Value = ChunkEntity });

                        for (int m = 0, offset = 2; m < 6; m++)
                        {
                            Entity mesh = ecbBegin.CreateEntity(batchIndex, chunkRenderer);
                            ecbBegin.SetComponent(batchIndex, mesh, new Parent { Value = ChunkEntity });
                            ecbBegin.AddComponent(batchIndex, mesh, new MeshIndex { index = offset + m });
                        }
                    }
                    ecbEnd.RemoveComponent<HexGridDataInitialised>(batchIndex, comp.gridEntity);
                    ecbBegin.AddComponent<HexGridVisualsPreInitialised>(batchIndex, comp.gridEntity);
                }
            }
        }
        [BurstCompile]
        private struct SetFeautreContainers : IJobEntityBatch
        {
            [ReadOnly]
            [DeallocateOnJobCompletion]
            public NativeArray<float3> colliderVerts;
            [ReadOnly]
            [DeallocateOnJobCompletion]
            public NativeArray<int3> colliderTris;
            [ReadOnly]
            public ComponentDataFromEntity<Generate> gridMarkForGeneration;
            [ReadOnly]
            public BufferTypeHandle<LinkedEntityGroup> linkedEntityGroups;
            [ReadOnly]
            public BufferTypeHandle<HexGridCellBuffer> cellBuffer;
            [ReadOnly]
            public ComponentTypeHandle<HexGridChunkComponent> hGCCTypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<HexGridChunkInitialisationComponent> hGCICTypeHandle;
            public EntityCommandBuffer.ParallelWriter ecbEnd;
            public EntityCommandBuffer.ParallelWriter ecbBegin;
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                NativeArray<HexGridChunkComponent> hexGridCompArray = batchInChunk.GetNativeArray(hGCCTypeHandle);
                NativeArray<HexGridChunkInitialisationComponent> hexGridInitCompArray = batchInChunk.GetNativeArray(hGCICTypeHandle);
                BufferAccessor<LinkedEntityGroup> linkedEntityGroupAccessors = batchInChunk.GetBufferAccessor(linkedEntityGroups);
                BufferAccessor<HexGridCellBuffer> cellBufferAccessors = batchInChunk.GetBufferAccessor(cellBuffer);
                for (int index = 0; index < hexGridCompArray.Length; index++)
                {
                    DynamicBuffer<LinkedEntityGroup> linkedEntities = linkedEntityGroupAccessors[index];
                    
                    HexGridChunkComponent comp = hexGridCompArray[index];
                    comp.gridEntity = hexGridInitCompArray[index].gridEntity;
                    comp.chunkIndex = hexGridInitCompArray[index].chunkIndex;
                    ecbBegin.SetComponent(batchIndex, comp.FeatureContainer, new FeatureContainer { GridEntity = comp.gridEntity });
                    PhysicsCollider collider = new PhysicsCollider
                    {
                        Value = Unity.Physics.MeshCollider.Create(colliderVerts, colliderTris)
                    };

                    ecbBegin.SetComponent(batchIndex, comp.entityTerrian, collider);

                    DynamicBuffer<HexGridCellBuffer> cellBuffer = cellBufferAccessors[index];
                    NativeArray<CellContainer> cellFeatures = new NativeArray<CellContainer>(cellBuffer.Length, Allocator.Temp);
                    for (int i = 9, cellBufferIndex = 0; i < linkedEntities.Length; i++, cellBufferIndex++)
                    {
                        cellFeatures[cellBufferIndex] = new CellContainer { cellIndex = cellBuffer[cellBufferIndex].cellIndex, container = linkedEntities[i].Value };
                        FeatureDataContainer featureData = new FeatureDataContainer
                        {
                            containerEntity = linkedEntities[1].Value,
                            cellIndex = cellBuffer[cellBufferIndex].cellIndex,
                            GridEntity = comp.gridEntity
                        };
                        ecbBegin.SetComponent(batchIndex, linkedEntities[i].Value, featureData);
                    }
                    ecbEnd.AddBuffer<CellContainer>(batchIndex, comp.FeatureContainer).CopyFrom(cellFeatures);
                    cellFeatures.Dispose();
                    ecbEnd.RemoveComponent<HexGridDataInitialised>(batchIndex, linkedEntities[0].Value);
                    ecbEnd.RemoveComponent<HexGridChunkInitialisationComponent>(batchIndex, linkedEntities[0].Value);
                    if (!gridMarkForGeneration.HasComponent(comp.gridEntity))
                    {
                        ecbBegin.AddComponent<RefreshChunk>(batchIndex, linkedEntities[0].Value);
                    }
                    ecbBegin.SetComponent(batchIndex, linkedEntities[0].Value, comp);
                    ecbEnd.RemoveComponent<HexGridDataInitialised>(batchIndex, comp.gridEntity);
                    ecbBegin.AddComponent<HexGridVisualsInitialised>(batchIndex, comp.gridEntity);
                }
            }
        }
    }

    [UpdateInGroup(typeof(HexGridSystemGroup))]
    [UpdateAfter(typeof(HexGridPreSystem))]
    public class HexGridInvokeSystem : JobComponentSystem
    {
        EndSimulationEntityCommandBufferSystem ecbEndSystem;
        BeginSimulationEntityCommandBufferSystem ecbBeginSystem;
        private readonly  EntityQueryDesc CreateGridVisualsPostQuery = new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(HexGridVisualsInitialised) } };

        protected override void OnCreate()
        {
            ecbEndSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
        }
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            EntityQuery CreateGridVisualsPost = GetEntityQuery(CreateGridVisualsPostQuery);
            EntityCommandBuffer ecbEnd = ecbEndSystem.CreateCommandBuffer();
            EntityCommandBuffer ecbBegin = ecbBeginSystem.CreateCommandBuffer();
            NativeArray<Entity> grids = CreateGridVisualsPost.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < grids.Length; i++)
            {

                ecbEnd.RemoveComponent<HexGridVisualsInitialised>(grids[i]);
                ecbBegin.AddComponent<HexGridCreated>(grids[i]);
            }
            DOTSHexEditor.GridEntities.AddRange(grids);
            if (DOTSHexEditor.Instance != null)
            {
                DOTSHexEditor.Instance.HandleNewGrids();
            }
            
            Debug.Log("Currently " + DOTSHexEditor.GridEntities.Count + " grids.");
            grids.Dispose();

            return inputDeps;
        }
    }

    public class HexGrid : MonoBehaviour
    {
        public int cellCountX = 20;
        public int cellCountZ = 15;

        public bool wrapping;

        public bool MapCreated { private set; get; }

        public Texture2D cellTexture;
        public Texture2D noiseSource;
        public HexMapGenerator mapGenerator;
        public GameObject HexMeshDebugPrefab;
        public UnityEngine.Material HexMeshDebugMat;

        public UnityEngine.Material terrianMat;
        public UnityEngine.Material riverMat;
        public UnityEngine.Material roadMat;
        public UnityEngine.Material waterMat;
        public UnityEngine.Material WaterShoreMat;
        public UnityEngine.Material EstuariesMat;
        public UnityEngine.Material WallsMat;

        private int chunkCountX;
        private int chunkCountZ;

        private World mainWorld;
        public DOTSHexEditor editor;
        private EntityManager entityManager;
        private void Awake()
        {
            HexMetrics.terrianMat = terrianMat;
            HexMetrics.riverMat = riverMat;
            HexMetrics.roadMat = roadMat;
            HexMetrics.waterMat = waterMat;
            HexMetrics.WaterShoreMat = WaterShoreMat;
            HexMetrics.EstuariesMat = EstuariesMat;
            HexMetrics.WallsMat = WallsMat;
            HexMetrics.noiseSource = noiseSource;
            HexMetrics.cellTexture = cellTexture;
            HexMetrics.SetNoiseColours();
        }

        public bool CreateMapDataFullJob(Entity grid,uint seed, int x, int z, bool wrapping)
        {
            mainWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = mainWorld.EntityManager;
            cellCountX = x;
            cellCountZ = z;
            chunkCountX = cellCountX / HexMetrics.chunkSizeX;
            chunkCountZ = cellCountZ / HexMetrics.chunkSizeZ;
            this.wrapping = wrapping;
            int wrapSize = wrapping ? cellCountX : 0;
            if (x <= 0 || x % HexMetrics.chunkSizeX != 0 || z <= 0 || z % HexMetrics.chunkSizeZ != 0)
            {
                Debug.LogError("Unsupported map size.");
                return false;
            }
            entityManager.SetComponentData(grid, new HexGridComponent
            {
                cellCountX = cellCountX,
                cellCountZ = cellCountZ,
                cellCount = cellCountZ * cellCountX,
                chunkCountX = chunkCountX,
                chunkCountZ = chunkCountZ,
                chunkCount = chunkCountX * chunkCountZ,
                seed = seed,
                wrapping = wrapping,
                wrapSize = wrapSize,
                gridEntity = grid,
                currentCentreColumnIndex = -1,
            });
            NativeArray<HexHash> hasGrid = HexMetrics.InitializeHashGrid(seed);
            entityManager.GetBuffer<HexHash>(grid).CopyFrom(hasGrid);
            hasGrid.Dispose();
            return true;
        }

        private void OnDestroy()
        {
            HexMetrics.CleanUpNoiseColours();
        }
    }
}