using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Rendering;
using Unity.Physics;

namespace DOTSHexagonsV2
{
    public class HexGridActivationSystem : JobComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem ecbEndSystem;
        private BeginSimulationEntityCommandBufferSystem ecbBeginSystem;

        private EntityQuery MakeActiveQuery;
        private EntityQuery AllCreatedGridsQuery;

        protected override void OnCreate()
        {
            ecbEndSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();

            MakeActiveQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(HexGridCreated), typeof(MakeActiveGridEntity) } });
            AllCreatedGridsQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(HexGridCreated) } });
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            JobHandle outputDeps;
            if (!MakeActiveQuery.IsEmpty)
            {
                GridActivation gridActivation = new GridActivation
                {
                    shaderEntity = HexGridShaderSystem.HexCellShaderData,
                    makeActive = GetComponentDataFromEntity<MakeActiveGridEntity>(true),
                    currentActive = GetComponentDataFromEntity<ActiveGridEntity>(true),
                    gridCompTypes = GetComponentTypeHandle<HexGridComponent>(true),
                    ecbBegin = ecbBeginSystem.CreateCommandBuffer(),
                    ecbEnd = ecbEndSystem.CreateCommandBuffer()
                };

                outputDeps = gridActivation.Schedule(AllCreatedGridsQuery, inputDeps);
                ecbBeginSystem.AddJobHandleForProducer(outputDeps);
                ecbEndSystem.AddJobHandleForProducer(outputDeps);
            }
            else
            {
                outputDeps = inputDeps;
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

            public EntityCommandBuffer ecbEnd;
            public EntityCommandBuffer ecbBegin;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                NativeArray<HexGridComponent> grids = batchInChunk.GetNativeArray(gridCompTypes);
                for (int i = 0; i < grids.Length; i++)
                {
                    Entity grid = grids[i].gridEntity;
                    if (makeActive.HasComponent(grid))
                    {
                        ecbEnd.RemoveComponent<MakeActiveGridEntity>(grid);
                        ecbBegin.AddComponent<ActiveGridEntity>(grid);
                        ecbBegin.AddComponent(shaderEntity, new InitialiseHexCellShader { grid = grid, x = grids[i].cellCountX, z = grids[i].cellCountZ });
                    }

                    if (currentActive.HasComponent(grid))
                    {
                        ecbEnd.RemoveComponent<ActiveGridEntity>(grid);
                    }
                }
            }
        }

    }
}