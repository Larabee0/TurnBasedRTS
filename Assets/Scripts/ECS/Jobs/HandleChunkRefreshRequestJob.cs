using Unity.Burst;
using Unity.Entities;

/// <summary>
/// This job schedules a when a chunk refresh occurs, and will prevent the same chunk refreshing simulatnously, but keep
/// in mind that it has been requested for refresh. It will schedule it to be refreshed once the current refresh cycle has completed.
/// </summary>
[BurstCompile, WithAll(typeof(HexChunkRefreshRequest)), WithNone(typeof(HexChunkRefresh))]
public partial struct HandleChunkRefreshRequestJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main)
    {
        ecb.RemoveComponent<HexChunkRefreshRequest>(jobChunkIndex, main);
        ecb.AddComponent<HexChunkRefresh>(jobChunkIndex, main);
    }
}
