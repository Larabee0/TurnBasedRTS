using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics.Authoring;
using Unity.Physics.Systems;
using UnityEngine;

[BurstCompile, UpdateInGroup(typeof(PhysicsSystemGroup)), UpdateBefore(typeof(SyncCustomPhysicsProxySystem))]
public partial struct HexChunkColliderDisposal : ISystem
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

[BurstCompile]
public partial struct ColliderDisposalJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main, ref HexChunkColliderForDisposal collider)
    {
        ecb.DestroyEntity(jobChunkIndex, main);
        collider.Dispose();
    }
}