using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using static Unity.Properties.PropertyPath;
using Unity.Collections;

public class PrefabDeclarer : MonoBehaviour, IDeclareReferencedPrefabs
{
    public GameObject[] Prefabs;
    public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs) => referencedPrefabs.AddRange(Prefabs);
}


public class PrefabSetter : ComponentSystem
{
    EntityQuery chunkPrefabQuery;
    EntityQuery hexMeshPrefabQuery;
    protected override void OnCreate()
    {
        var query = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(Prefab),
                typeof(HexGridChunkTag),
                typeof(HexGridMeshEntities)
            }
        };
        chunkPrefabQuery = GetEntityQuery(query);

        query = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(Prefab),
                typeof(HexMeshData),
            }
        };
        hexMeshPrefabQuery = GetEntityQuery(query);
    }

    protected override void OnUpdate()
    {
        if (!chunkPrefabQuery.IsEmpty && !hexMeshPrefabQuery.IsEmpty)
        {
            HexGridMeshEntities meshEntities = chunkPrefabQuery.GetSingleton<HexGridMeshEntities>();
            NativeArray<Entity> entities = hexMeshPrefabQuery.ToEntityArray(Allocator.Temp);
            NativeArray<HexMeshData> data = hexMeshPrefabQuery.ToComponentDataArray<HexMeshData>(Allocator.Temp);
            for (int i = 0; i < data.Length; i++)
            {
                switch (data[i].type)
                {
                    case MeshType.Terrain:
                        meshEntities.Terrain = entities[i];
                        break;
                    case MeshType.Rivers:
                        meshEntities.Rivers = entities[i];
                        break;
                    case MeshType.Water:
                        meshEntities.Water = entities[i];
                        break;
                    case MeshType.WaterShore:
                        meshEntities.WaterShore = entities[i];
                        break;
                    case MeshType.Estuaries:
                        meshEntities.Estuaries = entities[i];
                        break;
                    case MeshType.Roads:
                        meshEntities.Roads = entities[i];
                        break;
                    case MeshType.Walls:
                        meshEntities.Walls = entities[i];
                        break;
                }
            }
            chunkPrefabQuery.SetSingleton(meshEntities);
            Enabled = false;
        }
    }
}
