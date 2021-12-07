using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Rendering;
using Unity.Mathematics;
using Unity.Jobs;
using System.Runtime.CompilerServices;
using Unity.Burst;
namespace DOTSHexagonsV2
{
    public class HexGridMeshGenerator : JobComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem commandBufferSystemEnd;
        private BeginSimulationEntityCommandBufferSystem commandBufferSystemBegin;
        EntityQuery RefreshQuery;
        protected override void OnCreate()
        {
            commandBufferSystemEnd = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            commandBufferSystemBegin = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            RefreshQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] { typeof(HexGridChunkComponent), typeof(RefreshChunk) },
                None = new ComponentType[] { typeof(Generate) }
            });
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            TriangulatorEverything triangulateAll = new TriangulatorEverything
            {

                noiseColours = HexFunctions.noiseColours,
                chunkEntityType = this.GetEntityTypeHandle(),
                hexGridChunkComponentType = this.GetComponentTypeHandle<HexGridChunkComponent>(true),
                hexGridCellBufferType = this.GetBufferTypeHandle<HexGridCellBuffer>(true),
                hexCellBufferType = this.GetBufferFromEntity<HexCell>(true),
                childBufferType = this.GetBufferFromEntity<HexGridChild>(true),
                hexHashData = this.GetBufferFromEntity<HexHash>(true),
                featureDataComponentType = this.GetComponentDataFromEntity<FeatureDataContainer>(true),
                gridDataComponentType = this.GetComponentDataFromEntity<HexGridComponent>(true),
                ecbBegin = commandBufferSystemBegin.CreateCommandBuffer().AsParallelWriter(),
                ecbEnd = commandBufferSystemEnd.CreateCommandBuffer().AsParallelWriter(),
            };

            JobHandle outputDeps;
            if (BurstCompiler.IsEnabled)
            {
                outputDeps = triangulateAll.ScheduleParallel(RefreshQuery, 32, inputDeps);
            }
            else
            {
                outputDeps = triangulateAll.Schedule(RefreshQuery, inputDeps);
            }

            commandBufferSystemBegin.AddJobHandleForProducer(outputDeps);
            commandBufferSystemEnd.AddJobHandleForProducer(outputDeps);

            return outputDeps;
        }
    }
}