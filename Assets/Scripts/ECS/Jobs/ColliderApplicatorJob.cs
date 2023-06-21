using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// This job turns the colliders created in <see cref="HexChunkColliderSystem"/> into physics collider entities
/// attached to the correct chunks it runs in parallel for each colliderChunkTargetEntities & physicsBlobs pair
/// </summary>
[BurstCompile]
public struct ColliderApplicatorJob : IJobParallelFor
{
    public Entity colliderPrefab;
    [ReadOnly]
    public NativeList<Entity> colliderChunkTargetEntities;
    [ReadOnly]
    public NativeList<BlobAssetReference<Collider>> physicsBlobs;

    public EntityCommandBuffer.ParallelWriter ecbEnd;

    public void Execute(int index)
    {
        Entity collider = ecbEnd.Instantiate(colliderChunkTargetEntities.Length, colliderPrefab);
        ecbEnd.AddComponent(colliderChunkTargetEntities.Length, collider, new PhysicsCollider() { Value = physicsBlobs[index] });
        ecbEnd.AddComponent(colliderChunkTargetEntities.Length, collider, new Parent { Value = colliderChunkTargetEntities[index] });

        ecbEnd.AddComponent(colliderChunkTargetEntities.Length, colliderChunkTargetEntities[index], new HexChunkColliderReference { value = collider });
        ecbEnd.RemoveComponent<HexChunkColliderRebuild>(colliderChunkTargetEntities.Length, colliderChunkTargetEntities[index]);
    }
}

