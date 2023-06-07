using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

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