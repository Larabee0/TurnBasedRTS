using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Jobs;
using Hexagons;
using Unity.Physics.Systems;
using UnityEngine;
using Unity.Rendering;

[UpdateBefore(typeof(TransformSystemGroup))]
public class HexPreInitialiserSystem : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationCommandBuffer;
    private EntityQueryDesc UninitialisedGridQuery;

    protected override void OnCreate()
    {
        UninitialisedGridQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(GridBasicInfo), typeof(GridUninitialised) }
        };
        endSimulationCommandBuffer = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override  JobHandle OnUpdate(JobHandle inputDeps)
    {
        float width = 2 * Hex.size;
        float threeQuaterWidth = width * 0.75f;
        float root3 = math.sqrt(3);
        float height = root3 * Hex.size;
        float halfHeight = height / 2;
        PreInitialiseHexGrid spawnHexGridJob = new PreInitialiseHexGrid
        {
            GridTypeHandle = this.GetEntityTypeHandle(),
            GridBasicInfoTypeHandle = this.GetComponentTypeHandle<GridBasicInfo>(true),
            prefabEntity = Hex.TilePrefab,
            width = width,
            threeQuaterWidth = threeQuaterWidth,
            root3 = root3,
            height = height,
            halfHeight = halfHeight,
            HexTileBufferTypeHandle = this.GetBufferTypeHandle<HexTileBufferElement>(),
            entiyCommandBuffer = endSimulationCommandBuffer.CreateCommandBuffer().AsParallelWriter(),
        };
        JobHandle jobHandle = spawnHexGridJob.ScheduleParallel(GetEntityQuery(this.UninitialisedGridQuery),1, inputDeps);
        endSimulationCommandBuffer.AddJobHandleForProducer(jobHandle);
        return jobHandle;
    }

    [BurstCompile]
    public struct PreInitialiseHexGrid : IJobEntityBatch
    {
        [ReadOnly]
        public EntityTypeHandle GridTypeHandle;
        [ReadOnly]
        public ComponentTypeHandle<GridBasicInfo> GridBasicInfoTypeHandle;
        [ReadOnly]
        public Entity prefabEntity;
        [ReadOnly]
        public float width;
        [ReadOnly]
        public float threeQuaterWidth;
        [ReadOnly]
        public float root3;
        [ReadOnly]
        public float height;
        [ReadOnly]
        public float halfHeight;

        public BufferTypeHandle<HexTileBufferElement> HexTileBufferTypeHandle;
        public EntityCommandBuffer.ParallelWriter entiyCommandBuffer;

        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {
            NativeArray<GridBasicInfo> GridBasicInfos = batchInChunk.GetNativeArray(GridBasicInfoTypeHandle);
            NativeArray<Entity> gridEntitys = batchInChunk.GetNativeArray(GridTypeHandle);
            BufferAccessor<HexTileBufferElement> HexTileBufferAccessors = batchInChunk.GetBufferAccessor(HexTileBufferTypeHandle);

            for (int entity = 0; entity < batchInChunk.Count; entity++)
            {
                GridBasicInfo gridInfo = GridBasicInfos[entity];
                NativeHashMap<int2, HexTileBufferElement> MapStorage = new NativeHashMap<int2, HexTileBufferElement>(Hex.TileCount(gridInfo.RingCount), Allocator.Temp);
                int CentreColumn = (gridInfo.RingCount * 2) - 1;
                int2 centre = new int2(CentreColumn / 2, CentreColumn / 2);
                HexTileBufferElement Origin = new HexTileBufferElement(centre, gridInfo.Centre, NodeTerrian.Planet);
                MapStorage.Add(centre, Origin);

                for (int i = 1; i < gridInfo.RingCount; i++)
                {
                    NativeArray<HexTileBufferElement> Ring;
                    if(i >= Hex.SystemIntermediateIndex && i < Hex.SystemSpaceIndex)
                    {
                        Ring = Hex.CubeRing(Origin, i,NodeTerrian.Intermediate);
                    }
                    else if(i > Hex.SystemSpaceIndex)
                    {
                        Ring = Hex.CubeRing(Origin, i, NodeTerrian.Space);
                    }
                    else
                    {
                        Ring = Hex.CubeRing(Origin, i, NodeTerrian.Planet);
                    }
                    float startpos = (height + Hex.CellGap) * i;
                    MapStorage = Hex.SpawnRing(MapStorage, Ring, new float3(gridInfo.Centre.x, gridInfo.Centre.y, gridInfo.Centre.z - startpos), i, height, halfHeight, threeQuaterWidth, Hex.CellGap);
                }
                MapStorage = Hex.CalculateNeighbours(MapStorage);
                NativeArray<HexTileBufferElement> MapArray = MapStorage.GetValueArray(Allocator.Temp);
                MapStorage.Dispose();
                for (int i = 0; i < MapArray.Length; i++)
                {
                    HexTileBufferElement hexTile = MapArray[i];
                    Entity e = entiyCommandBuffer.Instantiate(batchIndex, prefabEntity);
                    if (hexTile.Terrian == NodeTerrian.Intermediate)
                    {
                        entiyCommandBuffer.AddComponent<IntermediateTerrian>(batchIndex, e);
                    }
                    else if (hexTile.Terrian == NodeTerrian.Space)
                    {
                        entiyCommandBuffer.AddComponent<SpaceTerrian>(batchIndex, e);
                    }
                    else
                    {
                        entiyCommandBuffer.AddComponent<PlanetTerrian>(batchIndex, e);
                    }
                    entiyCommandBuffer.AddComponent(batchIndex, e, new HexTileComponent { ID = hexTile.ID, Grid = gridEntitys[entity] });
                    entiyCommandBuffer.AddComponent(batchIndex, e, new Translation { Value = hexTile.position });
                    entiyCommandBuffer.AddComponent<GridPreInitialised>(batchIndex, e);
                }

                DynamicBuffer<HexTileBufferElement> hexTileBuffer = HexTileBufferAccessors[entity];
                hexTileBuffer.CopyFrom(MapArray);
                MapArray.Dispose();

                entiyCommandBuffer.RemoveComponent<GridUninitialised>(batchIndex, gridEntitys[entity]);
                entiyCommandBuffer.AddComponent<GridPreInitialised>(batchIndex, gridEntitys[entity]);
            }
        }
    }
}


[UpdateAfter(typeof(TransformSystemGroup))]
public class HexInitialiserSystem : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationCommandBuffer;
    private EntityQueryDesc PreinitialisedGridQuery;
    private EntityQueryDesc PreinitialisedGridElementQuery;
    protected override void OnCreate()
    {
        PreinitialisedGridQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(GridBasicInfo), typeof(GridPreInitialised) }
        };
        PreinitialisedGridElementQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(HexTileComponent), typeof(GridPreInitialised) }
        };
        endSimulationCommandBuffer = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        EntityQuery PreinitialisedGridQuery = GetEntityQuery(this.PreinitialisedGridQuery);
        if (PreinitialisedGridQuery.IsEmpty)
        {
            return inputDeps;
        }
        EntityQuery elementEntities = GetEntityQuery(PreinitialisedGridElementQuery);
        
        NativeArray<HexTileComponent> hexTileComponents = elementEntities.ToComponentDataArray<HexTileComponent>(Allocator.Temp);
        NativeArray<Entity> HexGrids = elementEntities.ToEntityArray(Allocator.Temp);
        NativeHashMap<HexTileComponent, Entity> TilesWithChild = new NativeHashMap<HexTileComponent, Entity>(hexTileComponents.Length, Allocator.TempJob);
        for (int i = 0; i < hexTileComponents.Length; i++)
        {
            TilesWithChild.Add(hexTileComponents[i], HexGrids[i]);
        }
        hexTileComponents.Dispose();
        HexGrids.Dispose();
        InitialiseHexGrid spawnHexGridJob = new InitialiseHexGrid
        {
            GridTypeHandle = this.GetEntityTypeHandle(),
            HexTiles = TilesWithChild,
            prefabEntity = Hex.GridPrefab,
            HexTileBufferTypeHandle = this.GetBufferTypeHandle<HexTileBufferElement>(),
            ChildTypeHandle = this.GetBufferTypeHandle<Child>(),
            entityCommandBuffer = endSimulationCommandBuffer.CreateCommandBuffer().AsParallelWriter(),
        };
        JobHandle jobHandle = spawnHexGridJob.ScheduleParallel(PreinitialisedGridQuery, 1,inputDeps);
        endSimulationCommandBuffer.AddJobHandleForProducer(jobHandle);
        
        return TilesWithChild.Dispose(jobHandle);
    }

    [BurstCompile]
    public struct InitialiseHexGrid : IJobEntityBatch
    {
        [ReadOnly]
        public EntityTypeHandle GridTypeHandle;
        /// HexTileComponent (key) contains the tile ID and the Grid it belongs to.
        /// value = the the entity the compopnent is attached to, and that needs adding as a child to the Grid.
        [ReadOnly]
        public NativeHashMap<HexTileComponent, Entity> HexTiles;// = new NativeHashMap<HexTileComponent, Entity>();
        [ReadOnly]
        public Entity prefabEntity;

        public BufferTypeHandle<HexTileBufferElement> HexTileBufferTypeHandle;
        public BufferTypeHandle<Child> ChildTypeHandle;
        public EntityCommandBuffer.ParallelWriter entityCommandBuffer;
        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
        {
            NativeArray<Entity> gridEntitys = batchInChunk.GetNativeArray(GridTypeHandle);
            BufferAccessor<HexTileBufferElement> HexTileBufferAccessors = batchInChunk.GetBufferAccessor(HexTileBufferTypeHandle);
            BufferAccessor<Child> ChildBufferAccessors = batchInChunk.GetBufferAccessor(ChildTypeHandle);

            for (int entity = 0; entity < batchInChunk.Count; entity++)
            {

                Entity Grid = gridEntitys[entity];
                NativeArray<HexTileBufferElement> hexTileArray = HexTileBufferAccessors[entity].ToNativeArray(Allocator.Temp);
                NativeArray<Child> childArray = new NativeArray<Child>(hexTileArray.Length, Allocator.Temp);
                for (int i = 0; i < hexTileArray.Length; i++)
                {
                    HexTileBufferElement hexTile = hexTileArray[i];
                    HexTileComponent hexTileComponent = new HexTileComponent { Grid = Grid, ID = hexTile.ID };
                    Entity newChild = HexTiles[hexTileComponent];
                    entityCommandBuffer.AddComponent<LocalToParent>(batchIndex, newChild);
                    entityCommandBuffer.AddComponent(batchIndex, newChild, new Parent { Value = Grid });
                    entityCommandBuffer.RemoveComponent<GridPreInitialised>(batchIndex, newChild);
                    Entity Visual = entityCommandBuffer.Instantiate(batchIndex, prefabEntity);
                    entityCommandBuffer.AddComponent(batchIndex, Visual, new Translation { Value = hexTile.position });
                    entityCommandBuffer.AddComponent(batchIndex, Visual, new GridVisualComponent { Parent = newChild });
                    hexTile.entity = newChild;
                    hexTileArray[i] = hexTile;
                    childArray[i] = new Child { Value = newChild };
                }

                DynamicBuffer<HexTileBufferElement> hexTileBuffer = HexTileBufferAccessors[entity];
                hexTileBuffer.CopyFrom(hexTileArray);
                hexTileArray.Dispose();
                DynamicBuffer<Child> childBuffer = ChildBufferAccessors[entity];
                childBuffer.CopyFrom(childArray);
                childArray.Dispose();

                entityCommandBuffer.RemoveComponent<GridPreInitialised>(batchIndex, gridEntitys[entity]);
                entityCommandBuffer.AddComponent<GridInitialised>(batchIndex, gridEntitys[entity]);
            }
        }
    }
}

/// <summary>
/// Delinking the visual hexagon map from the logical map;
/// - these should be seperate entities.
/// - keep the physics colliders on the tiles for path finding and interaction.
/// - anything permant for this tile should become a child of the tile.
/// - none permant entities should just occupy the same position.
/// 
/// Displaying path finding;
/// - Instead of chaning the colour of the grid, a seperate visual entity should be spawned.
/// - this would mean we can use the ECB instead of the entity manager, avoiding syncpoints.
/// </summary>

public class HitDetectionSystem : ComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationCommandBuffer;
    private BuildPhysicsWorld physicsWorld;
    
    private NativeList<Entity> ActualPathThisFrame;
    private NativeList<Entity> ActualPathLastFrame;
    private NativeList<Entity> VisualPath;
    private NativeList<StartEndNodeWithPosition> StartEndNodes;
    //private Entity PathEntity;
    private int SubPaths = -1;
    private bool Debugged = false;
    private Entity pathFinderController;
    public struct StartEndNodeWithPosition
    {
        public HexTileComponent StartNode;
        public float3 StartPos;
        public HexTileComponent EndNode;
        public float3 EndPos;
        public static StartEndNodeWithPosition Null { get; }
        public StartEndNodeWithPosition(HexTileComponent startEntity, float3 startPos, HexTileComponent endEntity, float3 endPos)
        {
            StartNode = startEntity;
            StartPos = startPos;
            EndNode = endEntity;
            EndPos = endPos;
        }
        public StartEndNodeWithPosition(HexTileComponent startEntity, float3 startPos)
        {
            StartNode = startEntity;
            StartPos = startPos;
            EndNode = HexTileComponent.Null;
            EndPos = float3.zero;
        }

        public void EndToStart()
        {
            StartNode = EndNode;
            StartPos = EndPos;
        }
        public void SetEnd(HexTileComponent endEntity,float3 endPos)
        {
            EndNode = endEntity;
            EndPos = endPos;
        }

        public bool AreNodesEqual()
        {
            return StartNode.Equals(EndNode);
        }
    }

    protected override void OnCreate()
    {
        endSimulationCommandBuffer = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        physicsWorld = World.DefaultGameObjectInjectionWorld.GetExistingSystem<BuildPhysicsWorld>();
        VisualPath = new NativeList<Entity>(2, Allocator.Persistent);
        ActualPathThisFrame = new NativeList<Entity>(2, Allocator.Persistent);
        ActualPathLastFrame = new NativeList<Entity>(2, Allocator.Persistent);
        StartEndNodes = new NativeList<StartEndNodeWithPosition>(2, Allocator.Persistent);
        pathFinderController = EntityManager.CreateEntity();

        EntityManager.AddBuffer<PathStorageBufferElement>(pathFinderController);
    }

    protected override void OnUpdate()
    {

        
        if (Input.GetKeyUp(KeyCode.B) && !Debugged)
        {
            DebugNameEntities();
        }
        if (Input.GetMouseButtonDown(1))
        {
            OnRightMouseDown();
        }
        if (Input.GetMouseButton(1))
        {
            OnRightMouse();
        }
        if (Input.GetMouseButtonUp(1))
        {
            OnRightMosueUp();
        }
        if(ActualPathThisFrame.Length > 0)
        {
            if (ActualPathLastFrame.Length > 0)
            {
                ActualPathLastFrame.Clear();
            }
            ActualPathLastFrame.AddRange(ActualPathThisFrame);
        }

    }

    private void DebugNameEntities()
    {
        EntityQuery GridQuery = EntityManager.CreateEntityQuery(new ComponentType[] { typeof(GridBasicInfo), typeof(GridInitialised) });
        Entity Grid = GridQuery.GetSingletonEntity();
        NativeArray<HexTileBufferElement> elements  =EntityManager.GetBuffer<HexTileBufferElement>(Grid).AsNativeArray();
        //for (int i = 0; i < elements.Length; i++)
        //{
        //    EntityManager.SetName(elements[i].entity, elements[i].ID.ToString());
        //}
        
        elements.Dispose();
        Debugged = true;
    }
    private void OnRightMouseDown()
    {
        Entity NodeEntity = DoGridRayCastForEntity();
        if (NodeEntity == Entity.Null)
        {
            return;
        }

        HexTileComponent Node = EntityManager.GetComponentData<HexTileComponent>(NodeEntity);
        SubPaths++;
        StartEndNodes.Add(new StartEndNodeWithPosition(Node, EntityManager.GetComponentData<LocalToWorld>(NodeEntity).Position));

    }
    private void OnRightMouse()
    {
        if (SubPaths < 0)
        {
            return;
        }
        StartEndNodeWithPosition Current = StartEndNodes[SubPaths];
        if (StartEndNodes[SubPaths].StartNode.Equals(HexTileComponent.Null))
        {
            return;
        }
        Entity entity = DoGridRayCastForEntity();
        if (entity == Entity.Null)
        {
            return;
        }
        HexTileComponent Node = EntityManager.GetComponentData<HexTileComponent>(entity);
        if (Current.StartNode.Equals(entity))
        {
            return;
        }
        Current.SetEnd(Node, EntityManager.GetComponentData<LocalToWorld>(entity).Position);
        StartEndNodes[SubPaths] = Current;
        DynamicBuffer<HexTileBufferElement> mainSet = EntityManager.GetBuffer<HexTileBufferElement>(Current.EndNode.Grid);
        NativeArray<Entity> path = PathFinder.FindPath(mainSet.AsNativeArray(), Current.StartNode.ID, Current.EndNode.ID);
        if(path.Length > 0)
        {
            NativeArray<Entity> pathlastframe = new NativeArray<Entity>(ActualPathLastFrame, Allocator.Temp);
            if (path != pathlastframe)
            {
                
                DestroyPath();
                if (ActualPathThisFrame.Length > 0)
                {
                    ActualPathThisFrame.Clear();
                }
                
                ActualPathThisFrame.AddRange(path);
                PlotPath();
            }
            pathlastframe.Dispose();
        }        
        path.Dispose();
    }

    private void OnRightMosueUp()
    {
        ClearPath();
    }

    private void PlotPath()
    {
        if(VisualPath.Capacity < ActualPathThisFrame.Length)
        {
            VisualPath.Capacity = ActualPathThisFrame.Length;
        }
        for (int i = 0; i < ActualPathThisFrame.Length; i++)
        {
            Entity PathPart =  EntityManager.Instantiate(Hex.PathPrefab);
            EntityManager.SetComponentData(PathPart, new Translation { Value = EntityManager.GetComponentData<LocalToWorld>(ActualPathThisFrame[i]).Position });
            VisualPath.Add(PathPart);
        }
    }
    private void DestroyPath()
    {
        EntityCommandBuffer commandBuffer = endSimulationCommandBuffer.CreateCommandBuffer();
        for (int i = 0; i < VisualPath.Length; i++)
        {
            commandBuffer.DestroyEntity(VisualPath[i]);
        }
        VisualPath.Clear();
    }

    private void ClearPath()
    {
        DestroyPath();
        StartEndNodes.Clear();
        ActualPathThisFrame.Clear();
        ActualPathLastFrame.Clear();
        SubPaths = -1;
    }
    private Entity DoGridRayCastForEntity()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        return PhysicsFunctions.Raycast(physicsWorld, ray.origin, ray.direction * Hex.GridRayCastRange, Hex.GridRayCastRange);
    }

    protected override void OnDestroy()
    {
        if (ActualPathThisFrame.IsCreated)
        {
            ActualPathThisFrame.Dispose();
        }
        if (ActualPathLastFrame.IsCreated)
        {
            ActualPathLastFrame.Dispose();
        }
        if (StartEndNodes.IsCreated)
        {
            StartEndNodes.Dispose();
        }
        if (VisualPath.IsCreated)
        {
            VisualPath.Dispose();
        }
    }
}