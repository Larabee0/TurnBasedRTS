using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics.Systems;

[BurstCompile, UpdateInGroup(typeof(PhysicsSystemGroup)), UpdateBefore(typeof(SyncCustomPhysicsProxySystem))]
public partial struct HexChunkColliderDisposalSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        EntityQueryBuilder builder = new(Allocator.Temp);
        state.RequireForUpdate(builder.WithAllRW<HexChunkColliderForDisposal>().Build(ref state));
        builder.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        new ColliderDisposalJob
        {
            ecb = GetFixedEntityCommandBuffer(ref state)
        }.ScheduleParallel();
    }

    [BurstCompile]
    private EntityCommandBuffer.ParallelWriter GetFixedEntityCommandBuffer(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<BeginFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        return ecb.AsParallelWriter();
    }
}
