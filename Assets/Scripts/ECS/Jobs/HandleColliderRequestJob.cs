using Unity.Entities;
using Unity.Burst;

/// <summary>
/// Job to prevent the game trying to run two simulatenous collider rebuilds at once. The game will request a collider rebuild,
/// this job will wait until the chunk has finished having its collider rebuilt before starting another rebuild.
/// </summary>
[BurstCompile, WithAll(typeof(HexChunkColliderRebuildRequest)), WithNone(typeof(HexChunkColliderRebuild))]
public partial struct HandleColliderRequestJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecbEnd;

    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main, in HexChunkMeshEntities meshEntities)
    {
        ecbEnd.AddComponent<HexChunkColliderRebuild>(jobChunkIndex, meshEntities.Terrain);
        ecbEnd.AddComponent<HexChunkColliderRebuild>(jobChunkIndex, main);
        ecbEnd.RemoveComponent<HexChunkColliderRebuildRequest>(jobChunkIndex, main);
    }
}
