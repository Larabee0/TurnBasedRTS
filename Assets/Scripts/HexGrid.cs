using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Physics;

public class HexGrid : MonoBehaviour, IConvertGameObjectToEntity
{
    public int cellCountX = 20;
    public int cellCountZ = 15;

    public bool wrapping;

    public Texture2D noiseSource;
    [Min(1)]
    public uint seed = 1;

    private int chunkCountX;
    private int chunkCountZ;
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        CreateMap(cellCountX, cellCountZ, wrapping);

        dstManager.AddComponent<HexGridBasic>(entity);
        dstManager.AddBuffer<HexGridChunkBuffer>(entity);
        dstManager.AddBuffer<HexGridColumnBuffer>(entity);
        dstManager.AddBuffer<HexCellReference>(entity);
        dstManager.AddComponentData(entity, new HexGridCreateChunks
        {
            columns = chunkCountX,
            chunks = chunkCountX * chunkCountZ
        });
        dstManager.SetComponentData(entity, new HexGridBasic
        {
            cellCountX = cellCountX,
            cellCountZ = cellCountZ,
            chunkCountX = chunkCountX,
            chunkCountZ = chunkCountZ,
            seed = seed,
            wrapping = wrapping,
            wrapSize = HexMetrics.wrapSize
        });
    }

    private void Awake()
    {
        HexMetrics.InitialiseHashGrid(seed);
        HexMetrics.SetNoiseColours(noiseSource);
    }

    public bool CreateMap(int x, int z, bool wrapping)
    {
        HexMetrics.wrapSize = wrapping ? cellCountX : 0;
        if(x<=0||x % HexMetrics.chunkSizeX != 0 || z <= 0 || z % HexMetrics.chunkSizeZ != 0)
        {
            Debug.LogError("Unsupported map size.");
            return false;
        }

        // clear paths and units?
        // destroy columns.

        chunkCountX = cellCountX / HexMetrics.chunkSizeX;
        chunkCountZ = cellCountZ / HexMetrics.chunkSizeZ;

        // create chunks
        // create cells

        return true;
    }
}

public struct HexGridBasic : IComponentData
{
    public uint seed;

    public int cellCountX;
    public int cellCountZ;
    public int chunkCountX;
    public int chunkCountZ;

    public bool wrapping;
    public int wrapSize;
}

public struct HexGridCreateChunks : IComponentData
{
    public int columns;
    public int chunks;
}

public struct HexGridInitCells : IComponentData { }
public struct HexGridSortCells : IComponentData { }
public struct HexGridNeighbourEntitySet : IComponentData { }

public struct HexGridChunkBuffer : IBufferElementData
{
    public Entity Value;
    public int Index;
}

public struct HexGridColumnBuffer : IBufferElementData
{
    public Entity Value;
    public int Index;
}

public struct HexGridColumn : IComponentData
{
    public static implicit operator int(HexGridColumn v) { return v.Index; }
    public static implicit operator HexGridColumn(int v) { return new HexGridColumn { Index = v }; }
    public int Index;
}

public partial class HexGridSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem ecbEndSys;
    private EntityQuery HexGridChunkPrefab;
    private EntityQuery HexCellPrefab;
    private EntityQuery HexGridSetNeighbourEntitySet;
    private EntityQuery HexGridSetNeighbourEntitySetGrid;

    private bool latch = false;

    protected override void OnCreate()
    {
        var entityQueries = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(Prefab),
                typeof(HexGridChunkTag)
            },
            None = new ComponentType[] { typeof(HexCellReference) }
        };
        HexGridChunkPrefab = GetEntityQuery(entityQueries);

        entityQueries = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(Prefab),
                typeof(HexCellBasic),
                typeof(HexCellTerrain),
                typeof(HexCellNeighbours)
            }
        };

        HexCellPrefab = GetEntityQuery(entityQueries);

        entityQueries = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(HexGridNeighbourEntitySet),
                typeof(HexGridBasic),
                typeof(HexCellReference)
            }
        };

        HexGridSetNeighbourEntitySetGrid = GetEntityQuery(entityQueries);

        entityQueries = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(HexGridNeighbourEntitySet),
                typeof(HexCellNeighbours)
            }
        };

        HexGridSetNeighbourEntitySet = GetEntityQuery(entityQueries);

        ecbEndSys = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer.ParallelWriter ecbEnd = ecbEndSys.CreateCommandBuffer().AsParallelWriter();

        Entity HexGridChunkPrefab = this.HexGridChunkPrefab.GetSingletonEntity();
        Dependency = Entities.WithAll<HexGridBasic, HexGridCreateChunks, HexGridChunkBuffer>()
            .ForEach((ref Entity main, in HexGridCreateChunks createChunks, in HexGridBasic basic) =>
            InitiliseChunksAndColumns(HexGridChunkPrefab, ecbEnd, ref main, in createChunks, in basic))
            .ScheduleParallel(Dependency);

        Entity HexCellPrefab = this.HexCellPrefab.GetSingletonEntity();
        Dependency = Entities.WithAll<HexGridChunkTag, HexGridReference>().WithNone<HexCellReference>().ForEach((ref Entity main, in HexGridChunkTag chunkTag, in HexGridReference gridReference) =>
        {
            NativeArray<HexCellReference> chunkCells = new(HexMetrics.chunkSizeX * HexMetrics.chunkSizeZ, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < chunkCells.Length; i++)
            {
                Entity temp = ecbEnd.Instantiate(main.Index, HexCellPrefab);
                ecbEnd.AddComponent(main.Index, temp, new Parent { Value = main });
                chunkCells[i] = new() { Value = temp, ChunkIndex = chunkTag.Index };
                ecbEnd.AppendToBuffer(main.Index, gridReference.Value, chunkCells[i]);
            }
            ecbEnd.AddBuffer<HexCellReference>(main.Index, main).CopyFrom(chunkCells);
            ecbEnd.AddComponent<HexGridSortCells>(main.Index, gridReference.Value);
        }).ScheduleParallel(Dependency);

        Dependency = Entities.WithAll<HexGridBasic, HexCellReference, HexGridSortCells>().ForEach((ref Entity main, ref DynamicBuffer<HexCellReference> hexCellBuffer) =>
        {
            NativeArray<HexCellReference> cells = hexCellBuffer.ToNativeArray(Allocator.Temp);
            cells.Sort(new HexCellChunkSorter());
            hexCellBuffer.CopyFrom(cells);
            ecbEnd.RemoveComponent<HexGridSortCells>(main.Index, main);
            ecbEnd.AddComponent<HexGridInitCells>(main.Index, main);
        }).ScheduleParallel(Dependency);

        Dependency = Entities.WithAll<HexGridBasic, HexGridInitCells, HexCellReference>()
            .WithNone<HexGridSortCells, HexGridCreateChunks>()
            .ForEach((ref Entity main, ref DynamicBuffer<HexCellReference> cellBuffer, in HexGridBasic basic, in DynamicBuffer<HexGridChunkBuffer> chunkBuffer) =>
            InitiliseCells(ecbEnd, ref main, ref cellBuffer, in basic, in chunkBuffer)).ScheduleParallel(Dependency);

        Dependency = Entities.WithAll<HexCellBasic, HexCellNeighbours, FindNeighbours>()
            .ForEach((ref Entity main, ref HexCellNeighbours neighbours, in HexCellBasic basic, in FindNeighbours findNeighbours) =>
            FindCellNeighbours(ecbEnd,ref main,ref neighbours,in basic, in findNeighbours)
        ).ScheduleParallel(Dependency);

        if (!HexGridSetNeighbourEntitySetGrid.IsEmpty && !HexGridSetNeighbourEntitySet.IsEmpty)
        {
            Entity grid = HexGridSetNeighbourEntitySetGrid.GetSingletonEntity();
            NativeArray<HexCellReference> hexCells = EntityManager.GetBuffer<HexCellReference>(grid, true).ToNativeArray(Allocator.TempJob);
            hexCells.Sort(new HexCellIndexSorter());
            var neighbourJob = new NeighbourEntityJob
            {
                hexCells = hexCells,
                entityTypeHandle = GetEntityTypeHandle(),
                neigbourTypeHandle = GetComponentTypeHandle<HexCellNeighbours>(),
                ecbEnd = ecbEnd,
            };
            Dependency = neighbourJob.Schedule(HexGridSetNeighbourEntitySet, Dependency);
            latch = true;
        }
        else if(!HexGridSetNeighbourEntitySetGrid.IsEmpty && latch)
        {
            Entity grid = HexGridSetNeighbourEntitySetGrid.GetSingletonEntity();
            ecbEnd.RemoveComponent<HexGridNeighbourEntitySet>(grid.Index, grid);
            latch = false;

            HexMapUIInterface.Instance.SetMap(grid);
        }
        ecbEndSys.AddJobHandleForProducer(Dependency);
    }

    private static void InitiliseChunksAndColumns(Entity HexGridChunkPrefab, EntityCommandBuffer.ParallelWriter ecbEnd,ref Entity main, in HexGridCreateChunks createChunks, in HexGridBasic basic)
    {
        NativeArray<Entity> cols = new(createChunks.columns, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < createChunks.columns; i++)
        {
            Entity temp = cols[i] = ecbEnd.CreateEntity(main.Index);
            ecbEnd.AddComponent(main.Index, temp, new Parent { Value = main });
            ecbEnd.AddComponent(main.Index, temp, new HexGridColumn { Index = i });
            
            ecbEnd.AppendToBuffer(main.Index, main, new HexGridColumnBuffer { Index = i, Value = temp });
        }

        for (int z = 0, i = 0; z < basic.chunkCountZ; z++)
        {
            for (int x = 0; x < basic.chunkCountX; x++, i++)
            {
                Entity temp = ecbEnd.Instantiate(main.Index, HexGridChunkPrefab);
                ecbEnd.AddComponent(main.Index, temp, new InitColumnIndex { Index = x });
                ecbEnd.AddComponent(main.Index, temp, new Parent { Value = cols[x] });
                ecbEnd.AddComponent(main.Index, temp, new HexGridReference { Value = main });
                ecbEnd.SetComponent(main.Index, temp, new HexGridChunkTag { Index = i });
                ecbEnd.AppendToBuffer(main.Index, main, new HexGridChunkBuffer { Index = i, Value = temp });
                NativeArray<float3> colliderVerts = new(3, Allocator.Temp,NativeArrayOptions.UninitializedMemory);
                colliderVerts[0] = new float3(1);
                colliderVerts[1] = new float3(0);
                colliderVerts[2] = new float3(1, 0, 0);
                NativeArray<int3> colliderTris = new(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                colliderTris[0] = new int3(0, 1, 2);
                PhysicsCollider collider = new()
                {
                    Value = Unity.Physics.MeshCollider.Create(colliderVerts, colliderTris)
                };
                ecbEnd.SetComponent(main.Index, temp, collider);
            }
        }
        ecbEnd.AddBuffer<HexCellReference>(main.Index, main);
        ecbEnd.RemoveComponent<HexGridCreateChunks>(main.Index, main);
    }

    private static void InitiliseCells(EntityCommandBuffer.ParallelWriter ecbEnd, ref Entity main, ref DynamicBuffer<HexCellReference> cellBuffer, in HexGridBasic basic, in DynamicBuffer<HexGridChunkBuffer> chunkBuffer)
    {
        int cellCountX = basic.cellCountX;
        int cellCountZ = basic.cellCountZ;
        int chunkSize = HexMetrics.chunkSizeX * HexMetrics.chunkSizeZ;
        for (int z = 0, i = 0; z < cellCountZ; z++)
        {
            for (int x = 0; x < cellCountX; x++, i++)
            {
                int chunkX = x / HexMetrics.chunkSizeX;
                int chunkZ = z / HexMetrics.chunkSizeZ;

                // cells are stored sorted by chunk Index at this part of initilisation.
                int chunkIndex = (chunkX + chunkZ * basic.chunkCountX);

                int localX = x - chunkX * HexMetrics.chunkSizeX;
                int localZ = z - chunkZ * HexMetrics.chunkSizeZ;
                int cellChunkIndex = localX + localZ * HexMetrics.chunkSizeX + (chunkIndex * chunkSize);

                HexCellReference cell = cellBuffer[cellChunkIndex];
                cell.Index = i;
                cellBuffer[cellChunkIndex] = cell;

                HexCellBasic cellBasic = new()
                {
                    Index = i,
                    ColumnIndex = x / HexMetrics.chunkSizeX,
                    rawX = x,
                    rawZ =z,
                    wrapping = basic.wrapping,
                    Position = new float3()
                    {
                        x = (x + z * 0.5f - z / 2) * (HexMetrics.innerRadius * 2f),
                        y = 0f,
                        z = z * (HexMetrics.outerRadius * 1.5f)
            },
                    Coorindate = HexCoordinates.FromOffsetCoordinates(x, z, basic.wrapSize)
                };
                ecbEnd.SetComponent(main.Index, cell.Value, new HexGridReference { Value = main });
                ecbEnd.SetComponent(main.Index, cell.Value, cellBasic);
                ecbEnd.AddComponent(main.Index, cell.Value, new FindNeighbours { cellCountX = cellCountX, cellCountZ = cellCountZ,chunkCountX = basic.chunkCountX });
                ecbEnd.AddComponent(main.Index, cell.Value, new HexCellChunkReference { Value = chunkBuffer[chunkIndex].Value });
            }
        }
        NativeArray<HexCellReference> chunkCells = new(chunkSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

        for (int i = 0, cellToChunkIndex = 0; i < cellBuffer.Length; i++, cellToChunkIndex = (cellToChunkIndex + 1) % chunkSize)
        {
            chunkCells[cellToChunkIndex] = cellBuffer[i];
            if (cellToChunkIndex == chunkSize - 1)
            {
                ecbEnd.SetBuffer<HexCellReference>(main.Index, chunkBuffer[cellBuffer[i].ChunkIndex].Value).CopyFrom(chunkCells);
            }
        }

        NativeArray<HexCellReference> cells = cellBuffer.ToNativeArray(Allocator.Temp);
        cells.Sort(new HexCellChunkSorter());
        cellBuffer.CopyFrom(cells);
        ecbEnd.RemoveComponent<HexGridInitCells>(main.Index, main);
        for (int i = 0; i < chunkBuffer.Length; i++)
        {
            ecbEnd.AddComponent<ChunkRefresh>(main.Index, chunkBuffer[i].Value);
        }
        ecbEnd.AddComponent<HexGridNeighbourEntitySet>(main.Index, main);
    }

    private static void FindCellNeighbours(EntityCommandBuffer.ParallelWriter ecbEnd, ref Entity main, ref HexCellNeighbours neighbours, in HexCellBasic basic, in FindNeighbours findNeighbours)
    {
        if (basic.rawX < findNeighbours.cellCountX - 1)
        {
            HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.E, basic.Index + 1);
            if (basic.wrapping && basic.rawX == 0)
            {
                HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.W, basic.Index + findNeighbours.cellCountX - 1);
            }
        }
        if (basic.rawX > 0)
        {
            HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.W, basic.Index - 1);
            if (basic.wrapping && basic.rawX == findNeighbours.cellCountX - 1)
            {
                HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.E, basic.Index - basic.rawX);
            }
        }
        if (basic.rawZ < findNeighbours.cellCountZ - 1)
        {
            switch (basic.rawZ & 1)
            {
                case 0:
                    HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.NE, basic.Index + findNeighbours.cellCountX);
                    if (basic.rawX > 0)
                    {
                        HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.NW, basic.Index + findNeighbours.cellCountX - 1);
                    }
                    else if (basic.wrapping)
                    {
                        HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.NW, basic.Index + findNeighbours.cellCountX * 2 - 1);
                    }
                    break;
                default:
                    HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.NW, basic.Index + findNeighbours.cellCountX);
                    if (basic.rawX < findNeighbours.cellCountX - 1)
                    {
                        HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.NE, basic.Index + findNeighbours.cellCountX + 1);
                    }
                    else if (basic.wrapping)
                    {
                        HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.NE, basic.Index + 1);
                    }
                    break;
            }
        }
        if (basic.rawZ > 0)
        {
            switch (basic.rawZ & 1)
            {
                case 0:
                    HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.SE, basic.Index - findNeighbours.cellCountX);
                    if (basic.rawX > 0)
                    {
                        HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.SW, basic.Index - findNeighbours.cellCountX - 1);
                    }
                    else if (basic.wrapping)
                    {
                        HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.SW, basic.Index - 1);
                    }
                    break;
                default:
                    HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.SW, basic.Index - findNeighbours.cellCountX);
                    if (basic.rawX < findNeighbours.cellCountX - 1)
                    {
                        HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.SE, basic.Index - findNeighbours.cellCountX + 1);
                    }
                    else if (basic.wrapping)
                    {
                        HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.SE, basic.Index - findNeighbours.cellCountX * 2 + 1);
                    }
                    break;
            }
        }

        ecbEnd.RemoveComponent<FindNeighbours>(main.Index, main);
        ecbEnd.AddComponent<HexGridNeighbourEntitySet>(main.Index, main);
    }

    public struct NeighbourEntityJob : IJobEntityBatch
    {
        [ReadOnly][DeallocateOnJobCompletion]
        public NativeArray<HexCellReference> hexCells;

        public ComponentTypeHandle<HexCellNeighbours> neigbourTypeHandle;
        [ReadOnly]
        public EntityTypeHandle entityTypeHandle;

        public EntityCommandBuffer.ParallelWriter ecbEnd;

        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {
            NativeArray<Entity> cellEntities = batchInChunk.GetNativeArray(entityTypeHandle);
            NativeArray<HexCellNeighbours> neighbourComponents = batchInChunk.GetNativeArray(neigbourTypeHandle);
            for (int i = 0; i < neighbourComponents.Length; i++)
            {
                HexCellNeighbours neighbours = neighbourComponents[i];
                for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                {
                    int index = neighbours.GetNeighbourIndex(d);
                    switch (index)
                    {
                        case -1:
                            continue;
                        default:
                            HexCellNeighbours.SetNeighbourEntity(ref neighbours, hexCells[index].Value, d);
                            neighbourComponents[i] = neighbours;
                            break;
                    }
                }
                ecbEnd.RemoveComponent<HexGridNeighbourEntitySet>(batchIndex, cellEntities[i]);
            }
        }
    }
}