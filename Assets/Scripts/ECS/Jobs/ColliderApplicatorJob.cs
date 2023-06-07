using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

[BurstCompile]
public struct ColliderApplicatorJob : IJobParallelFor
{
    public Entity colliderPrefab;
    [ReadOnly]
    public NativeList<Entity> colliderEntities;
    [ReadOnly]
    public NativeList<BlobAssetReference<Collider>> physicsBlobs;

    public EntityCommandBuffer.ParallelWriter ecbEnd;

    public void Execute(int index)
    {
        Entity collider = ecbEnd.Instantiate(colliderEntities.Length, colliderPrefab);
        ecbEnd.AddComponent(colliderEntities.Length, collider, new PhysicsCollider() { Value = physicsBlobs[index] });
        ecbEnd.AddComponent(colliderEntities.Length, collider, new Parent { Value = colliderEntities[index] });
        ecbEnd.RemoveComponent<HexChunkColliderRebuild>(colliderEntities.Length, colliderEntities[index]);
        ecbEnd.AddComponent(colliderEntities.Length, colliderEntities[index], new HexChunkColliderReference { value = collider });
    }
}

