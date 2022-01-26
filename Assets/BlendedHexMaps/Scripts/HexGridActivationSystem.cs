using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Rendering;

namespace DOTSHexagonsV2
{
    [UpdateInGroup(typeof(HexGridV2SystemGroup))]
    public class HexGridActivationSystem : JobComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem ecbEndSystem;
        private BeginSimulationEntityCommandBufferSystem ecbBeginSystem;

        private EntityQuery MakeActiveQuery;
        private EntityQuery AllCreatedGridsQuery;
        private EntityQuery RepaintChunks;

        protected override void OnCreate()
        {
            ecbEndSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();

            MakeActiveQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(HexGridCreated), typeof(MakeActiveGridEntity) } });
            AllCreatedGridsQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(HexGridCreated) } });
            RepaintChunks = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(RepaintScheduled), typeof(HexGridChunkComponent) } });
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            JobHandle outputDeps;
            // If we need to make a new grid the active grid, do only this operation, repaint next time.
            if (!MakeActiveQuery.IsEmpty)
            {
                GridActivation gridActivation = new GridActivation
                {
                    shaderEntity = HexGridShaderSystem.HexCellShaderData,
                    makeActive = GetComponentDataFromEntity<MakeActiveGridEntity>(true),
                    currentActive = GetComponentDataFromEntity<ActiveGridEntity>(true),
                    gridCompTypes = GetComponentTypeHandle<HexGridComponent>(true),
                    ecbBegin = ecbBeginSystem.CreateCommandBuffer().AsParallelWriter(),
                    ecbEnd = ecbEndSystem.CreateCommandBuffer().AsParallelWriter()
                };

                outputDeps = gridActivation.ScheduleParallel(AllCreatedGridsQuery, 64, inputDeps);
                ecbBeginSystem.AddJobHandleForProducer(outputDeps);
                ecbEndSystem.AddJobHandleForProducer(outputDeps);
            }
            else // don't repaint until the grid has become active.
            {
                RepaintChunkRenderers repaintChunksJob = new RepaintChunkRenderers
                {
                    entityTypeHandle = GetEntityTypeHandle(),
                    hGCCTypeHandle = GetComponentTypeHandle<HexGridChunkComponent>(true),
                    ecbBegin = ecbBeginSystem.CreateCommandBuffer().AsParallelWriter(),
                    ecbEnd = ecbEndSystem.CreateCommandBuffer().AsParallelWriter()
                };
                outputDeps = repaintChunksJob.ScheduleParallel(RepaintChunks, 64, inputDeps);
                ecbBeginSystem.AddJobHandleForProducer(outputDeps);
                ecbEndSystem.AddJobHandleForProducer(outputDeps);
            }

            return outputDeps;
        }

        [BurstCompile]
        private struct GridActivation : IJobEntityBatch
        {
            [ReadOnly]
            public Entity shaderEntity;
            [ReadOnly]
            public ComponentDataFromEntity<MakeActiveGridEntity> makeActive;
            [ReadOnly]
            public ComponentDataFromEntity<ActiveGridEntity> currentActive;
            [ReadOnly]
            public ComponentTypeHandle<HexGridComponent> gridCompTypes;


            public EntityCommandBuffer.ParallelWriter ecbEnd;
            public EntityCommandBuffer.ParallelWriter ecbBegin;

            // For each grid go through see if it has the "MakeActiveGridEntity" component:
            // If yes then we add the "ActiveGridEntity" tagging component, and remove the "MakeActiveGridEntity" from it, then schedule the shader to reinitialise.

            // Next check to see if this grid entity is the currently active grid, which it will be if it has the "ActiveGridEntity" tagging component.
            // If it does, then remove it, that grid is no longer active.
            // We can do this safely because the command buffers won't execute the changes until after the job has fully completed.
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                NativeArray<HexGridComponent> grids = batchInChunk.GetNativeArray(gridCompTypes);
                for (int i = 0; i < grids.Length; i++)
                {
                    Entity grid = grids[i].gridEntity;
                    if (makeActive.HasComponent(grid))
                    {
                        ecbEnd.RemoveComponent<MakeActiveGridEntity>(batchIndex, grid);
                        ecbBegin.AddComponent<ActiveGridEntity>(batchIndex, grid);
                        ecbBegin.AddComponent(batchIndex, shaderEntity, new InitialiseHexCellShader { grid = grid, x = grids[i].cellCountX, z = grids[i].cellCountZ });
                    }

                    if (currentActive.HasComponent(grid))
                    {
                        ecbEnd.RemoveComponent<ActiveGridEntity>(batchIndex, grid);
                    }
                }
            }
        }

        [BurstCompile]
        private struct RepaintChunkRenderers : IJobEntityBatch
        {
            [ReadOnly]
            public EntityTypeHandle entityTypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<HexGridChunkComponent> hGCCTypeHandle;


            public EntityCommandBuffer.ParallelWriter ecbEnd;
            public EntityCommandBuffer.ParallelWriter ecbBegin;

            // For each Grid Chunk, from the currently active grid:
            // Add the RepaintScheduled tag component, to each "HexRenderer" entity.
            // Additionally, schedule the features to be reprocessed, and remove the RepaintSchedule tag from the Grid Chunk Entity.

            // the entity query system looks for grid chunks with "RepaintSchedule" comps, to run this system on, the GridAPIMeshSystem
            // looks for "HexRenderer"'s with "RepaintSchedule" comps, then creates a mesh class based off the data in each "HexRenderer" Entity
            // and applies it to some GameObject determined by the GridAPI system group.
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                NativeArray<Entity> chunks = batchInChunk.GetNativeArray(entityTypeHandle);
                NativeArray<HexGridChunkComponent> chunkComps = batchInChunk.GetNativeArray(hGCCTypeHandle);
                for (int i = 0; i < chunkComps.Length; i++)
                {
                    HexGridChunkComponent comp = chunkComps[i];
                    ecbEnd.RemoveComponent<RepaintScheduled>(batchIndex, chunks[i]);
                    ecbBegin.AddComponent<RepaintScheduled>(batchIndex, comp.entityTerrian);
                    ecbBegin.AddComponent<RepaintScheduled>(batchIndex, comp.entityRiver);
                    ecbBegin.AddComponent<RepaintScheduled>(batchIndex, comp.entityWater);
                    ecbBegin.AddComponent<RepaintScheduled>(batchIndex, comp.entityWaterShore);
                    ecbBegin.AddComponent<RepaintScheduled>(batchIndex, comp.entityEstuaries);
                    ecbBegin.AddComponent<RepaintScheduled>(batchIndex, comp.entityRoads);
                    ecbBegin.AddComponent<RepaintScheduled>(batchIndex, comp.entityWalls);
                    ecbBegin.AddComponent<ProcessFeatures>(batchIndex, comp.FeatureContainer);
                }
            }
        }
    }
}