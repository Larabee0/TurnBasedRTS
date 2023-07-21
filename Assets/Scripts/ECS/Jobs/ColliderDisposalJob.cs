using Unity.Burst;
using Unity.Entities;

/// <summary>
/// To ensure a collider has had the chance to be removed from the PhysicsWorld this job is run in the fixed simulation group
/// by <see cref="HexChunkColliderDisposalSystem"/> this contains now only a reference to teh collider blob on a new entity
/// This ensure the PhysicsCollider and its entity have been removed from the PhysicsWorld, ensuring no raycast errors in
/// <see cref="HexMapEditorSystem"/>
/// 
/// This simply then cleans up the blob and the entity it is attached to this is
/// done in the <see cref="BeginFixedStepSimulationEntityCommandBufferSystem"/> (the start of the next fixed update, rather than the end 
/// of this one, again just to ensure the collider *really* has been removed from the PhysicsWorld)
/// </summary>
[BurstCompile]
public partial struct ColliderDisposalJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;

    /// <summary>
    /// This job disposes of colliders no longer needed in the game.
    /// </summary>
    /// <param name="jobChunkIndex">ParallelWriter ecbs need a sort key,
    /// IJobEntity can have the ChunkIndexInQuery attribute added to an int in its execute parameters, this is a good value
    /// to use for the sort key.</param>
    /// <param name="main">the entity the components in the parameters are attached to</param>
    /// <param name="collider">the collider data to dispose (this parsed as ref to indicate we want read/write access)</param>
    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main, ref HexChunkColliderForDisposal collider)
    {
        // queue the entity for destruction in the command buffer
        ecb.DestroyEntity(jobChunkIndex, main);
        // dispose the collider data to prevent a memory leak
        collider.Dispose();
    }
}