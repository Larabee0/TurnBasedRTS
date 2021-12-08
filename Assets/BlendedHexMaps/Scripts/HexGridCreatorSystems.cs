using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Jobs;
using Unity.Burst;

namespace DOTSHexagonsV2
{

    [UpdateAfter(typeof(HexGridParentSystem))]
    public class HexGridV2SystemGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(HexGridV2SystemGroup))]
    public class HexGridCreateColumnsSystem : JobComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem ecbEndSystem;
        private BeginSimulationEntityCommandBufferSystem ecbBeginSystem;
        private EntityArchetype column;
        private EntityQuery CreateGridColumnQuery;

        protected override void OnCreate()
        {
            ecbEndSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();

            column = EntityManager.CreateArchetype(typeof(HexGridChild), typeof(HexGridParent), typeof(HexColumn));
            CreateGridColumnQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(HexGridUnInitialised) } });
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            CreateGridColumns columnsJob = new CreateGridColumns
            {
                column = column,
                hGCTypeHandle = GetComponentTypeHandle<HexGridComponent>(true),
                ecbEnd = ecbEndSystem.CreateCommandBuffer().AsParallelWriter(),
                ecbBegin = ecbBeginSystem.CreateCommandBuffer().AsParallelWriter()
            };
            JobHandle outputDeps = columnsJob.ScheduleParallel(CreateGridColumnQuery, 64, inputDeps);
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
                        ecbBegin.SetComponent(batchIndex ^ col + i, newColumn, new HexGridParent { Value = comp.gridEntity });
                        ecbBegin.SetComponent(batchIndex ^ col + i, newColumn, new HexColumn { columnIndex = col });
                    }
                    ecbEnd.RemoveComponent<HexGridUnInitialised>(batchIndex ^ i, comp.gridEntity);
                    ecbBegin.AddComponent<GridWithColumns>(batchIndex ^ i, comp.gridEntity);
                }
            }
        }
    }

    [UpdateInGroup(typeof(HexGridV2SystemGroup))]
    [UpdateAfter(typeof(HexGridCreateColumnsSystem))]
    public class HexGridCreateChunksSystem : JobComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem ecbEndSystem;
        private BeginSimulationEntityCommandBufferSystem ecbBeginSystem;
        private EntityQuery CreateGridChunkQuery;

        protected override void OnCreate()
        {
            ecbEndSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            CreateGridChunkQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(GridWithColumns) } });
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            CreateGridChunksPrefab chunkJob = new CreateGridChunksPrefab
            {
                chunkPrefab = HexGridChunkSystem.HexGridChunkPrefab,
                hGCTypeHandle = GetComponentTypeHandle<HexGridComponent>(true),
                cTypeHandle = GetBufferTypeHandle<HexGridChild>(true),
                columnDataFromEntity = GetComponentDataFromEntity<HexColumn>(true),
                ecbEnd = ecbEndSystem.CreateCommandBuffer().AsParallelWriter(),
                ecbBegin = ecbBeginSystem.CreateCommandBuffer().AsParallelWriter()
            };

            JobHandle outputDeps = chunkJob.ScheduleParallel(CreateGridChunkQuery, 64, inputDeps);
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
            public BufferTypeHandle<HexGridChild> cTypeHandle;
            [ReadOnly]
            public ComponentDataFromEntity<HexColumn> columnDataFromEntity;
            public EntityCommandBuffer.ParallelWriter ecbEnd;
            public EntityCommandBuffer.ParallelWriter ecbBegin;
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                BufferAccessor<HexGridChild> childrenAccessor = batchInChunk.GetBufferAccessor(cTypeHandle);
                NativeArray<HexGridComponent> hexGridCompArray = batchInChunk.GetNativeArray(hGCTypeHandle);
                for (int index = 0; index < hexGridCompArray.Length; index++)
                {
                    HexGridComponent comp = hexGridCompArray[index];
                    int chunkCountX = comp.chunkCountX;
                    int chunkCountZ = comp.chunkCountZ;
                    NativeArray<HexGridChunkBuffer> HexGridChunkEntities = new NativeArray<HexGridChunkBuffer>(chunkCountX * chunkCountZ, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    DynamicBuffer<HexGridChild> gridChildren = childrenAccessor[index];
                    for (int z = 0, i = 0; z < chunkCountZ; z++)
                    {
                        for (int x = 0; x < chunkCountX; x++)
                        {
                            Entity newChunk = ecbBegin.Instantiate(batchIndex ^ index + i, chunkPrefab);
                            HexGridChunkEntities[i] = new HexGridChunkBuffer { ChunkEntity = newChunk, ChunkIndex = i };
                            ecbBegin.SetComponent(batchIndex ^ index + i, newChunk, new HexGridParent { Value = GetColumn(gridChildren, x) });
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
            private Entity GetColumn(DynamicBuffer<HexGridChild> columns, int x)
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

    [UpdateInGroup(typeof(HexGridV2SystemGroup))]
    [UpdateAfter(typeof(HexGridCreateChunksSystem))]
    public class HexGridCreateCellsSystem : JobComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem ecbEndSystem;
        private BeginSimulationEntityCommandBufferSystem ecbBeginSystem;
        private EntityQuery CreateGridCellsQuery;

        protected override void OnCreate()
        {
            ecbEndSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            CreateGridCellsQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(GridWithChunks) } });
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            CreateGridCells cellsJob = new CreateGridCells
            {
                noiseColours = HexFunctions.noiseColours,
                hGCTypeHandle = GetComponentTypeHandle<HexGridComponent>(true),
                hgCCFromEntity = GetComponentDataFromEntity<HexGridChunkInitialisationComponent>(true),
                childFromEntity = GetBufferFromEntity<HexGridChild>(true),
                gridChunkBufferTypeHandle = GetBufferTypeHandle<HexGridChunkBuffer>(true),
                ecbEnd = ecbEndSystem.CreateCommandBuffer().AsParallelWriter(),
                ecbBegin = ecbBeginSystem.CreateCommandBuffer().AsParallelWriter()
            };

            JobHandle outputDeps = cellsJob.ScheduleParallel(CreateGridCellsQuery, 64, inputDeps);
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
            public BufferFromEntity<HexGridChild> childFromEntity;
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
                    DynamicBuffer<HexGridChild> gridChildren = childFromEntity[comp.gridEntity];
                    for (int i = 0; i < gridChildren.Length; i++)
                    {
                        Entity column = gridChildren[i].Value;
                        DynamicBuffer<HexGridChild> columnChunks = childFromEntity[column];
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
                        cell.Position.x = ((x + z * 0.5f - z / 2) * HexFunctions.innerDiameter);
                        cell.Position.y = 0f;
                        cell.Position.z = (z * (HexFunctions.outerRadius * 1.5f));
                        cell.wrapSize = comp.wrapSize;
                        cell.ColumnIndex = x / HexFunctions.chunkSizeX;
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
                        int chunkX = x / HexFunctions.chunkSizeX;
                        int chunkZ = z / HexFunctions.chunkSizeZ;

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

                    int cellsPerChunk = HexFunctions.chunkSizeX * HexFunctions.chunkSizeZ;
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
                            list.Add(cell.Index);

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
                            int localX = x - (x / HexFunctions.chunkSizeX) * HexFunctions.chunkSizeX;
                            int localZ = z - (z / HexFunctions.chunkSizeZ) * HexFunctions.chunkSizeZ;
                            buffer[localX + localZ * HexFunctions.chunkSizeX] = new HexGridCellBuffer { cellIndex = list[b] };
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

    [UpdateInGroup(typeof(HexGridV2SystemGroup))]
    [UpdateAfter(typeof(HexGridCreateCellsSystem))]
    public class HexGridPreSystem : JobComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem ecbEndSystem;
        private BeginSimulationEntityCommandBufferSystem ecbBeginSystem;
        private EntityQuery CreateGridVisualsPreQuery  ;
        private EntityQuery CreateGridChunkVisualsPreQuery  ;

        protected override void OnCreate()
        {
            ecbEndSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            CreateGridVisualsPreQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(HexGridDataInitialised) } });
            CreateGridChunkVisualsPreQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridChunkComponent), typeof(HexGridDataInitialised) } });
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (CreateGridVisualsPreQuery.IsEmpty || CreateGridChunkVisualsPreQuery.IsEmpty)
            {
                return inputDeps;
            }
            SetFeautreContainers VisualsPreInitialise = new SetFeautreContainers
            {
                gridMarkForGeneration = GetComponentDataFromEntity<Generate>(true),
                linkedEntityGroups = GetBufferTypeHandle<LinkedEntityGroup>(true),
                cellBuffer = GetBufferTypeHandle<HexGridCellBuffer>(true),
                hGCCTypeHandle = GetComponentTypeHandle<HexGridChunkComponent>(true),
                hGCICTypeHandle = GetComponentTypeHandle<HexGridChunkInitialisationComponent>(true),
                ecbEnd = ecbEndSystem.CreateCommandBuffer().AsParallelWriter(),
                ecbBegin = ecbBeginSystem.CreateCommandBuffer().AsParallelWriter()
            };

            JobHandle outputDeps = VisualsPreInitialise.ScheduleParallel(CreateGridChunkVisualsPreQuery, 64, inputDeps);
            ecbEndSystem.AddJobHandleForProducer(outputDeps);
            ecbBeginSystem.AddJobHandleForProducer(outputDeps);
            return outputDeps;
        }
        
        [BurstCompile]
        private struct SetFeautreContainers : IJobEntityBatch
        {
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
                    ecbBegin.SetComponent(batchIndex, comp.entityTerrian, new HexRenderer { ChunkIndex = comp.chunkIndex, rendererID = RendererID.Terrian });
                    ecbBegin.SetComponent(batchIndex, comp.entityRiver, new HexRenderer { ChunkIndex = comp.chunkIndex, rendererID = RendererID.River });
                    ecbBegin.SetComponent(batchIndex, comp.entityWater, new HexRenderer { ChunkIndex = comp.chunkIndex, rendererID = RendererID.Water });
                    ecbBegin.SetComponent(batchIndex, comp.entityWaterShore, new HexRenderer { ChunkIndex = comp.chunkIndex, rendererID = RendererID.WaterShore });
                    ecbBegin.SetComponent(batchIndex, comp.entityEstuaries, new HexRenderer { ChunkIndex = comp.chunkIndex, rendererID = RendererID.Estuaries });
                    ecbBegin.SetComponent(batchIndex, comp.entityRoads, new HexRenderer { ChunkIndex = comp.chunkIndex, rendererID = RendererID.Roads });
                    ecbBegin.SetComponent(batchIndex, comp.entityWalls, new HexRenderer { ChunkIndex = comp.chunkIndex, rendererID = RendererID.Walls });
                    ecbBegin.SetComponent(batchIndex, comp.FeatureContainer, new FeatureContainer { GridEntity = comp.gridEntity });

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

    [UpdateInGroup(typeof(HexGridV2SystemGroup))]
    [UpdateAfter(typeof(HexGridPreSystem))]
    public class HexGridInvokeSystem : JobComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem ecbEndSystem;
        private BeginSimulationEntityCommandBufferSystem ecbBeginSystem;
        private EntityQuery CreateGridVisualsPostQuery;
    
        protected override void OnCreate()
        {
            ecbEndSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            CreateGridVisualsPostQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(HexGridVisualsInitialised) } });
        }
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            EntityCommandBuffer ecbEnd = ecbEndSystem.CreateCommandBuffer();
            EntityCommandBuffer ecbBegin = ecbBeginSystem.CreateCommandBuffer();
            NativeArray<Entity> grids = CreateGridVisualsPostQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < grids.Length; i++)
            {
                GridAPI.ActiveGridEntity = grids[i];
                GridAPI.Instance.enabled = true;
                ecbEnd.RemoveComponent<HexGridVisualsInitialised>(grids[i]);
                ecbBegin.AddComponent<HexGridCreated>(grids[i]);
            }
            DOTSHexEditorV2.GridEntities.AddRange(grids);
            if (DOTSHexEditorV2.Instance != null)
            {
                DOTSHexEditorV2.Instance.HandleNewGrids();
            }
            
            Debug.Log("Currently " + DOTSHexEditorV2.GridEntities.Count + " grids.");
            grids.Dispose();
    
            return inputDeps;
        }
    }
}