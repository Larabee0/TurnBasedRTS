using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Rendering;
using Unity.Collections;

public class ECSMeshBounds : MonoBehaviour
{
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private bool hideOnGet = false;
    [SerializeField] private bool hideOnConvert = false;
    private bool getRun = false;
    private World world;
    private EntityManager EntityManager => world.EntityManager;

    private EntityQuery meshEntityQuery;
    private void Start()
    {
        var entityQueries = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(HexGridChunkTag),
                typeof(HexCellReference),
                typeof(HexGridMeshEntities)
            },
            None = new ComponentType[]
            {
                typeof(CellWrapper),
                typeof(HexGridMeshUpdating),
                typeof(ChunkRefresh),
                typeof(ChunkCellDataCompleted)
            }
        };
        world = World.DefaultGameObjectInjectionWorld;
        meshEntityQuery = EntityManager.CreateEntityQuery(entityQueries);
        getRun = false;
    }

    public void GatherMeshFromECSWorld()
    {
        NativeArray<HexGridMeshEntities> chunkMeshEntities = meshEntityQuery.ToComponentDataArray<HexGridMeshEntities>(Allocator.Temp);
        Entity lastTerrainEntity = chunkMeshEntities[^1].Terrain;
        if (hideOnGet)
        {
            for (int i = 0; i < chunkMeshEntities.Length; i++)
            {
                HexGridMeshEntities chunkMeshEntity = chunkMeshEntities[i];
                for (int e = 0; e < 7; e++)
                {
                    EntityManager.AddComponent<DisableRendering>(chunkMeshEntity[e]);
                }
            }
        }
        meshFilter.mesh = EntityManager.GetComponentData<MeshRef>(lastTerrainEntity).mesh;
    }

    public void ConverToEntityEditor()
    {
        NativeArray<HexGridMeshEntities> chunkMeshEntities = meshEntityQuery.ToComponentDataArray<HexGridMeshEntities>(Allocator.Temp);
        if (!getRun)
        {
            bool cache = hideOnGet;
            hideOnGet = false;
            GatherMeshFromECSWorld();
            hideOnGet = cache;
        }
        if (hideOnConvert)
        {
            
            for (int i = 0; i < chunkMeshEntities.Length; i++)
            {
                HexGridMeshEntities chunkMeshEntity = chunkMeshEntities[i];
                for (int e = 0; e < 7; e++)
                {
                    EntityManager.AddComponent<DisableRendering>(chunkMeshEntity[e]);
                }
            }
        }

        // convert

        Entity lastTerrainEntity = chunkMeshEntities[^1].Terrain;
        EntityManager.RemoveComponent<DisableRendering>(lastTerrainEntity);
        ConvertToEntity cte = transform.root.gameObject.AddComponent<ConvertToEntity>();
        cte.ConversionMode = ConvertToEntity.Mode.ConvertAndDestroy;
    }
}
