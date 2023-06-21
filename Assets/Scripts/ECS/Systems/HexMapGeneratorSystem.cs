using Unity.Burst;
using Unity.Entities;

[BurstCompile, UpdateInGroup(typeof(HexSystemGroup))]
public partial struct HexMapGeneratorSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {

    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer.ParallelWriter ecb = GetEndSimEntityCommandBuffer(ref state);
        new HexMapGeneratorRequestCellDataJob
        {
            ecbEnd = ecb
        }.ScheduleParallel();
        new HexGenerateMapJob
        {
            noiseColours = SystemAPI.GetSingleton<HexChunkTriangulatorArray>().noiseColours,
            ecbEnd = ecb
        }.ScheduleParallel();
    }

    [BurstCompile]
    private EntityCommandBuffer.ParallelWriter GetEndSimEntityCommandBuffer(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        return ecb.AsParallelWriter();
    }
}
