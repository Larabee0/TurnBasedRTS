using Unity.Entities;
using Unity.Physics;
using UnityEngine;

/// <summary>
/// Provides authoring interaface for HexChunk Colliders, this allows the physics world index to be set.
/// it probably shouldn't be anything other than 0 though.
/// 
/// The baker also adds a HexChunkCollider tagging component.
/// </summary>
public class HexChunkColliderBaker : MonoBehaviour
{
    public uint physicsWorldIndex = 0;
}


public partial class ChunkColliderBaker : Baker<HexChunkColliderBaker>
{
    public override void Bake(HexChunkColliderBaker authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.Dynamic);
        AddSharedComponent(entity, new PhysicsWorldIndex { Value = authoring.physicsWorldIndex });
        AddComponent<HexChunkCollier>(entity);
    }
}