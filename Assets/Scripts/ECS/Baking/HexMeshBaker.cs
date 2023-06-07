using Unity.Entities;
using Unity.Physics;
using UnityEngine;

/// <summary>
/// <see cref="HexMeshData"/> for more detials on the information for each HexMesh
/// This is just the authoring component.
/// </summary>
public class HexMeshBaker : MonoBehaviour
{
    public HexMeshData hexMeshData;
}

/// <summary>
/// HexMesh baker, adds the rquired components for the HexMesh in the entity world.
/// If this is the Terrain Mesh additionally add the PhysicsWorldIndex for the collider.
/// </summary>
public class HexMeshBaking : Baker<HexMeshBaker>
{
    public override void Bake(HexMeshBaker authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, authoring.hexMeshData);
        AddComponent<HexMeshUninitilised>(entity);
        // AddComponent<HexMeshDebugger>(entity);
        // if (authoring.hexMeshData.type == MeshType.Terrain)
        // {
        //     AddSharedComponentManaged(entity, new PhysicsWorldIndex { Value = 0 });
        // }
    }
}