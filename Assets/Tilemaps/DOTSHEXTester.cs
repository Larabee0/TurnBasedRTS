using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Hexagons;
using Unity.Mathematics;
using Unity.Collections;

public class DOTSHEXTester : MonoBehaviour
{
    private EntityManager entityManager;
    public uint ToSpawn = 1;
    public bool RandomPosition = false;
    EntityArchetype GridArch;
    public float3 SpawnPosition = float3.zero;
    public Material DefaultHexagonMaterial;
    public Material SelectedHexagonMaterial;

    // Start is called before the first frame update
    private void Start()
    {
        Hex.DefaultHexagonMaterial = DefaultHexagonMaterial;
        Hex.SelectedHexagonMaterial = SelectedHexagonMaterial;
        float3[] originPositions = new float3[ToSpawn];
        for (int i = 0; i < ToSpawn; i++)
        {
            if (RandomPosition)
            {
                originPositions[i] = new float3(UnityEngine.Random.Range(-50f, 50f), UnityEngine.Random.Range(-50f, 50f), UnityEngine.Random.Range(-50f, 50f));
            }
            else
            {
                originPositions[i] = SpawnPosition;
            }
        }
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        GridArch = entityManager.CreateArchetype(typeof(GridBasicInfo), typeof(GridUninitialised), typeof(Translation), typeof(LocalToWorld), typeof(Child));
        NativeArray<Entity> grids = entityManager.CreateEntity(GridArch, (int)ToSpawn, Allocator.Temp);
        
        for (int i = 0; i < ToSpawn; i++)
        {
            GridBasicInfo info = new GridBasicInfo { ID = i, RingCount = Hex.SystemRingCount, Centre = originPositions[i], entity = grids[i] };
            entityManager.SetComponentData(grids[i], info);
            entityManager.AddBuffer<HexTileBufferElement>(grids[i]);
        }        
        grids.Dispose();

    }
}
