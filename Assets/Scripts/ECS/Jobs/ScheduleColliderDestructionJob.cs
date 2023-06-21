using Unity.Physics;
using Unity.Entities;
using Unity.Burst;

/// <summary>
/// The rendered mesh stores the collider reference for a chunk's collider, so when destruction is required
/// <see cref="HexChunkColliderRebuild"/> is added to the mesh entity to run this job, to destroy the current collider.
/// 
/// This removes the PhysicsWorldIndex from the collider entity, to begin the process of removing it from the PhysicsWorld
/// It then adds <see cref="HexChunkColliderRebuild"/> to the collider, which triggers <see cref="RemoveExistingColliderQueryJob"/> to run
/// on the now unreferenced old collider.
/// By this point that collider is guarateed to be removed from the physics world and disposed of 
/// without any errors. And that means by this point the mesh is ready for its new collider.
/// </summary>
[BurstCompile, WithAll(typeof(HexMeshData),typeof(HexChunkColliderRebuild))]
public partial struct ScheduleColliderDestructionJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecbEnd;

    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main, in HexChunkColliderReference collider)
    {
        ecbEnd.RemoveComponent<HexChunkColliderReference>(jobChunkIndex, main);
        ecbEnd.AddComponent<HexChunkColliderRebuild>(jobChunkIndex, collider.value);
        ecbEnd.RemoveComponent<PhysicsWorldIndex>(jobChunkIndex, collider.value);
    }
}
