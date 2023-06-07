using UnityEngine;
using Unity.Entities;

/// <summary>
/// HexChunkBaker MonoBehaviour with fields for each Mesh GameObject in the prefab
/// </summary>
public class HexChunkBaker : MonoBehaviour
{
    public GameObject Terrain;
    public GameObject Rivers;
    public GameObject Water;
    public GameObject WaterShore;
    public GameObject Estuaries;
    public GameObject Roads;
    public GameObject Walls;
}

/// <summary>
/// HexChunk baker class adds all the required IComponentData componets to the entity world entity prefab.
/// </summary>
public class HexChunkBaking : Baker<HexChunkBaker>
{
    public override void Bake(HexChunkBaker authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.Dynamic);

        DependsOn(authoring.Terrain);
        DependsOn(authoring.Rivers);
        DependsOn(authoring.Water);
        DependsOn(authoring.WaterShore);
        DependsOn(authoring.Estuaries);
        DependsOn(authoring.Roads);
        DependsOn(authoring.Walls);

        HexChunkMeshEntities meshEntities = new()
        {
            /// because the mesh GameObjects have their own baker, we won't interfer with the TransformUsageFlags
            Terrain = GetEntity(authoring.Terrain, TransformUsageFlags.None),
            Rivers = GetEntity(authoring.Rivers, TransformUsageFlags.None),
            Water = GetEntity(authoring.Water, TransformUsageFlags.None),
            WaterShore = GetEntity(authoring.WaterShore, TransformUsageFlags.None),
            Estuaries = GetEntity(authoring.Estuaries, TransformUsageFlags.None),
            Roads = GetEntity(authoring.Roads, TransformUsageFlags.None),
            Walls = GetEntity(authoring.Walls, TransformUsageFlags.None)
        };
        AddComponent<HexChunkTag>(entity);
        AddComponent(entity, meshEntities);
        AddBuffer<HexCellChunkNeighbour>(entity);
        AddBuffer<HexFeatureRequest>(entity);
        AddBuffer<HexFeatureSpawnedFeature>(entity);
    }
}