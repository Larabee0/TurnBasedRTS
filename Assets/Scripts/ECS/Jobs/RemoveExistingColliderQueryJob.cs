using Unity.Physics;
using Unity.Entities;
using Unity.Burst;

/// <summary>
/// During the <see cref="HexChunkColliderSystem"/> update loop, this job runs on collider entities marked for removal.
/// This simply destroys the entity they are attached to and creates a new entity with a <see cref="HexChunkColliderForDisposal"/>
/// component containing the same BlobAssetReference as the <see cref="PhysicsCollider"/> This will remove the collider from the PhysicsWorld
/// next fixed time step without causing a null reference excpetion in <see cref="HexMapEditorSystem"/> because hte collider was disposed.
/// 
/// At some point during a fixed time step, after the collider has been removed from the physics world, the <see cref="ColliderDisposalJob"/>
/// will run to safely dispose of the BlobAsset.
/// </summary>
[BurstCompile, WithAll(typeof(HexChunkColliderRebuild)),WithNone(typeof(PhysicsWorldIndex))]
public partial struct RemoveExistingColliderQueryJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecbEnd;

    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main, in PhysicsCollider collider)
    {
        Entity colliderDisposer = ecbEnd.CreateEntity(jobChunkIndex);
        ecbEnd.AddComponent(jobChunkIndex, colliderDisposer, new HexChunkColliderForDisposal { colliderBlob = collider.Value });
        ecbEnd.DestroyEntity(jobChunkIndex, main);
    }
}
