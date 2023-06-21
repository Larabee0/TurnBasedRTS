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
    public bool meshDebugging = false;
}

/// <summary>
/// HexMesh baker, adds the rquired components for the HexMesh in the entity world.
/// If mesh debugging is set to true the HexMeshDebugger component is also added which provides
/// mesh diagnoistic information (triangle count, vertex count, submesh index) in the inspector.
/// </summary>
public class HexMeshBaking : Baker<HexMeshBaker>
{
    public override void Bake(HexMeshBaker authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, authoring.hexMeshData);
        AddComponent<HexMeshUninitilised>(entity);

        if (authoring.meshDebugging)
        {
            AddComponent<HexMeshDebugger>(entity);
        }
    }
}