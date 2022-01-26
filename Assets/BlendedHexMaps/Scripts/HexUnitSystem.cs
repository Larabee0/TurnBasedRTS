using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Rendering;
using Unity.Transforms;
using Unity.Mathematics;

namespace DOTSHexagonsV2 {
    public class HexUnitSystem : JobComponentSystem
    {
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            return inputDeps;
        }

        private struct GetPath : IJobEntityBatch
        {
            public EntityTypeHandle entityTypeHandle;
            public ComponentTypeHandle<HexUnitComp> hexUnitTypeHandle;
            public ComponentTypeHandle<HexUnitLocation> locationTypeHandle;
            public ComponentTypeHandle<HexUnitCurrentTravelLocation> currentTravelLocationTypeHandle;
            public ComponentTypeHandle<HexUnitPathTo> PathToTypeHandle;
            public BufferTypeHandle<HexPath> pathToTravelTypeHandle;

            [ReadOnly]
            public NativeArray<ArchetypeChunk> gridChunks;
            [ReadOnly]
            public BufferTypeHandle<HexCell> cellTypeHandle;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                NativeHashMap<Entity, GridToArcetype> GridEntities = new NativeHashMap<Entity, GridToArcetype>(gridChunks.Length,Allocator.Temp);
                for (int i = 0; i < gridChunks.Length; i++)
                {
                    NativeArray<Entity> entities = gridChunks[i].GetNativeArray(entityTypeHandle);
                    for (int e = 0; e < entities.Length; e++)
                    {
                        GridEntities.Add(entities[i], new GridToArcetype { ArchArrayIndex = i, SubArrayIndex = e });
                    }
                }

                NativeArray<Entity> unitEntities = batchInChunk.GetNativeArray(entityTypeHandle);
                NativeArray<HexUnitComp> unitComps = batchInChunk.GetNativeArray(hexUnitTypeHandle);
                NativeArray<HexUnitLocation> locationComps = batchInChunk.GetNativeArray(locationTypeHandle);
                NativeArray<HexUnitCurrentTravelLocation> cTravelLocationComps = batchInChunk.GetNativeArray(currentTravelLocationTypeHandle);
                NativeArray<HexUnitPathTo> pathToComps = batchInChunk.GetNativeArray(PathToTypeHandle);
                BufferAccessor<HexPath> pathToTravelAccessors = batchInChunk.GetBufferAccessor(pathToTravelTypeHandle);

                for (int i = 0; i < unitEntities.Length; i++)
                {
                    Entity unitEntity = unitEntities[i];
                    HexUnitComp unitComp = unitComps[i];
                    HexCell fromCell = locationComps[i].Cell;
                    HexCell toCell = pathToComps[i].Cell;
                    GridToArcetype gridChunk = GridEntities[unitComp.GridEntity];
                    DynamicBuffer<HexCell> cells = gridChunks[gridChunk.ArchArrayIndex].GetBufferAccessor(cellTypeHandle)[gridChunk.SubArrayIndex];

                    NativeArray<HexCellQueueElement> searchCells = new NativeArray<HexCellQueueElement>(cells.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
                    {
                        searchCells[cellIndex] = new HexCellQueueElement
                        {
                            cellIndex = cellIndex,
                            NextWithSamePriority = int.MinValue,
                            PathFrom = int.MinValue
                        };
                    }
                    HexCellPriorityQueue searchFrontier = new HexCellPriorityQueue(searchCells);
                    bool pathFound;
                    (pathFound, searchFrontier) = Search(searchFrontier, cells, searchFrontier.elements[fromCell.Index], searchFrontier.elements[toCell.Index], unitComp);
                    NativeList<HexPath> path = new NativeList<HexPath>(Allocator.Temp);
                    if (pathFound)
                    {
                        
                        HexCellQueueElement current = searchFrontier.elements[toCell.Index];
                        while(current != searchFrontier.elements[fromCell.Index])
                        {
                            path.Add(new HexPath{ Cell = cells[current.cellIndex]});
                            current = searchFrontier.elements[current.PathFrom];
                        }
                        pathToTravelAccessors[i].CopyFrom(path.AsArray());
                    }
                    else
                    {
                        pathToTravelAccessors[i].Clear();
                    }
                }
            }


            private (bool, HexCellPriorityQueue) Search(HexCellPriorityQueue searchFrontier, DynamicBuffer<HexCell> cells, HexCellQueueElement fromCellElement, HexCellQueueElement toCellElement, HexUnitComp unit)
            {
                int searchFrontierPhase = 0;
                int speed = unit.Speed;
                searchFrontierPhase += 2;
                
                fromCellElement.SearchPhase = searchFrontierPhase;
                fromCellElement.Distance = 0;
                searchFrontier.Enqueue(fromCellElement);
                while (searchFrontier.Count > 0)
                {
                    HexCellQueueElement currentElement = searchFrontier.elements[searchFrontier.DequeueIndex()];
                    currentElement.SearchPhase += 1;
                    searchFrontier.elements[currentElement.cellIndex] = currentElement;
                    if (currentElement==toCellElement)
                    {
                        return (true, searchFrontier);
                    }
                    int currentTurn = (currentElement.Distance - 1) / speed;
                    for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                    {
                        HexCellQueueElement neighbourElement = searchFrontier.elements[HexCell.GetNeighbourIndex(cells[currentElement.cellIndex], d)];
                        if (neighbourElement == null || neighbourElement.SearchPhase > searchFrontierPhase)
                        {
                            continue;
                        }
                        if (!IsValidDestination(cells[neighbourElement.cellIndex]))
                        {
                            continue;
                        }
                        int moveCost = GetMoveCost(cells[currentElement.cellIndex], cells[neighbourElement.cellIndex], d);
                        if (moveCost < 0)
                        {
                            continue;
                        }
                        int distance = currentElement.Distance + moveCost;
                        int turn = (distance - 1) / speed;
                        if (turn > currentTurn)
                        {
                            distance = turn * speed + moveCost;
                        }
                        if (neighbourElement.SearchPhase < searchFrontierPhase)
                        {
                            neighbourElement.SearchPhase = searchFrontierPhase;
                            neighbourElement.Distance = distance;
                            neighbourElement.PathFrom = currentElement.cellIndex;
                            neighbourElement.SearchHeuristic = cells[neighbourElement.cellIndex].coordinates.DistanceTo(cells[toCellElement.cellIndex].coordinates);
                            searchFrontier.Enqueue(neighbourElement);
                        }
                        else if (distance < neighbourElement.Distance)
                        {
                            int oldPriority = neighbourElement.SearchPriority;
                            neighbourElement.Distance = distance;
                            neighbourElement.PathFrom = currentElement.cellIndex;
                            searchFrontier.Change(neighbourElement, oldPriority);
                        }
                        searchFrontier.elements[neighbourElement.cellIndex] = neighbourElement;
                    }
                }
                return (false, searchFrontier);
            }

            public bool IsValidDestination(HexCell cell)
            {
                return cell.IsExplored && !cell.IsUnderwater;
            }

            public int GetMoveCost(HexCell fromCell, HexCell toCell, HexDirection direction)
            {
                HexEdgeType edgeType = HexCell.GetEdgeType(fromCell, toCell);
                if (edgeType == HexEdgeType.Cliff)
                {
                    return -1;
                }
                int moveCost;
                if (HexCell.HasRoadThroughEdge(fromCell,direction))
                {
                    moveCost = 1;
                }
                else if (fromCell.Walled != toCell.Walled)
                {
                    return -1;
                }
                else
                {
                    moveCost = edgeType == HexEdgeType.Flat ? 5 : 10;
                    moveCost += toCell.urbanLevel + toCell.farmLevel + toCell.plantLevel;
                }
                return moveCost;
            }


            private struct GridToArcetype
            {
                public int ArchArrayIndex;
                public int SubArrayIndex;
            }
        }

        private struct Travel : IJobEntityBatch
        {
            public float deltaTime;
            public EntityTypeHandle entityTypeHandle;
            public ComponentTypeHandle<HexUnitComp> hexUnitTypeHandle;
            public ComponentTypeHandle<HexUnitLocation> locationTypeHandle;
            public ComponentTypeHandle<HexUnitCurrentTravelLocation> currentTravelLocationTypeHandle;
            public ComponentTypeHandle<Translation> translationTypeHandle;
            public ComponentTypeHandle<Rotation> rotationTypeHandle;
            public BufferTypeHandle<HexPath> pathToTravelTypeHandle;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                NativeArray<Entity> unitEntities = batchInChunk.GetNativeArray(entityTypeHandle);
                NativeArray<HexUnitComp> unitComps = batchInChunk.GetNativeArray(hexUnitTypeHandle);
                NativeArray<HexUnitLocation> locationComps = batchInChunk.GetNativeArray(locationTypeHandle);
                NativeArray<HexUnitCurrentTravelLocation> cTravelLocationComps = batchInChunk.GetNativeArray(currentTravelLocationTypeHandle);
                NativeArray<Translation> translationComps = batchInChunk.GetNativeArray(translationTypeHandle);
                NativeArray<Rotation> rotationComps = batchInChunk.GetNativeArray(rotationTypeHandle);
                BufferAccessor<HexPath> pathToTravelAccessors = batchInChunk.GetBufferAccessor(pathToTravelTypeHandle);

                for (int i = 0; i < unitEntities.Length; i++)
                {
                    Entity unitEntity = unitEntities[i];
                    HexUnitComp unitComp = unitComps[i];
                    HexUnitLocation locationComp = locationComps[i];
                    HexUnitCurrentTravelLocation currentTravelLocationComp = cTravelLocationComps[i];
                    Translation translationComp = translationComps[i];
                    Rotation rotationComp = rotationComps[i];
                    DynamicBuffer<HexPath> pathToTravel = pathToTravelAccessors[i];

                    float3 a, b, c = pathToTravel[0].Cell.Position;
                    LookAt(pathToTravel[1].Cell.Position, ref translationComp, ref rotationComp, ref unitComp, pathToTravel[1].Cell);

                    if (!currentTravelLocationComp.Cell)
                    {
                        currentTravelLocationComp.Cell = pathToTravel[0].Cell;
                    }
                    // grid visability
                    int currentColumn = currentTravelLocationComp.Cell.ColumnIndex;
                    float speed = unitComp.travelSpeed * deltaTime;
                    
                }
            }

            private void LookAt(float3 point, ref Translation translationComp, ref Rotation rotationComp, ref HexUnitComp unitComp, HexCell cell)
            {
                if (cell.wrapSize > 0)
                {
                    float xDistance = point.x - translationComp.Value.x;
                    if (xDistance < -HexFunctions.innerRadius * cell.wrapSize)
                    {
                        point.x += HexFunctions.innerDiameter * cell.wrapSize;
                    }
                    else if (xDistance > HexFunctions.innerRadius * cell.wrapSize)
                    {
                        point.x -= HexFunctions.innerDiameter * cell.wrapSize;
                    }
                }
                point.y = translationComp.Value.y;
                quaternion fromRotation = rotationComp.Value;
                quaternion toRotation = quaternion.LookRotationSafe(point - translationComp.Value, new float3(0f,1f,0f));
                float angle = Angle(fromRotation, toRotation);

                float speed = unitComp.rotationSpeed / angle;
                rotationComp.Value = math.slerp(fromRotation, toRotation, deltaTime * speed);
                unitComp.orientation = ExtraTurretMathsFunctions.ToEuler(rotationComp.Value).y;
            }


            public static float Angle(quaternion a, quaternion b)
            {
                float num = math.dot(a, b);
                return IsEqualUsingDot(num) ? 0f : (math.acos(math.min(math.abs(num), 1f)) * 2f * 57.29578f);
            }

            private static bool IsEqualUsingDot(float dot)
            {
                return dot > 0.999999f;
            }
        }
    }

    [UpdateInGroup(typeof(HexGridV2SystemGroup))]
    [UpdateAfter(typeof(HexMapGeneratorSystem))]
    public class HexPathFinderSystem : JobComponentSystem
    {
        private EntityQuery GridQuery;
        private EntityQuery FindPath;
        private BeginSimulationEntityCommandBufferSystem commandBufferSystemBegin;
        protected override void OnCreate()
        {
            GridQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] { typeof(HexGridComponent), typeof(HexGridCreated) },
                None = new ComponentType[] { typeof(Generate) }
            });
            FindPath = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] { typeof(HexFromCell), typeof(HexToCell), typeof(FindPath) },
                None = new ComponentType[] { typeof(FoundPath), typeof(NotFoundPath) }
            });
            commandBufferSystemBegin = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            NativeArray<ArchetypeChunk> gridEntitiesChunks = GridQuery.CreateArchetypeChunkArray(Allocator.TempJob);
            NativeHashMap<Entity, GridEntitiesArchIndex> GridInfo = new NativeHashMap<Entity, GridEntitiesArchIndex>(gridEntitiesChunks.Length, Allocator.TempJob);
            for (int i = 0; i < gridEntitiesChunks.Length; i++)
            {
                NativeArray<Entity> entities = gridEntitiesChunks[i].GetNativeArray(GetEntityTypeHandle());
                GridEntitiesArchIndex ChunkInfo = new GridEntitiesArchIndex { ChunkArrayIndex = i };
                for (int e = 0; e < entities.Length; e++)
                {
                    ChunkInfo.ChunkIndex = e;
                    GridInfo.Add(entities[e], ChunkInfo);
                }
            }

            FindPathJob findPathsJob = new FindPathJob
            {
                entityTypeHandle = GetEntityTypeHandle(),
                fromCellTypeHandle = GetComponentTypeHandle<HexFromCell>(true),
                toCellTypeHandle = GetComponentTypeHandle<HexToCell>(true),
                pathOptionsTypeHandle = GetComponentTypeHandle<FindPath>(),
                gridInfo = GridInfo,
                gridEntitiesChunks = gridEntitiesChunks,
                cellBufferTypeHandle = GetBufferTypeHandle<HexCell>(true),
                ecbBegin = commandBufferSystemBegin.CreateCommandBuffer().AsParallelWriter()
            };
            JobHandle outputDeps = GridInfo.Dispose(findPathsJob.ScheduleParallel(FindPath, 32, inputDeps));
            commandBufferSystemBegin.AddJobHandleForProducer(outputDeps);
            return outputDeps;
        }

        [BurstCompile]
        private struct FindPathJob : IJobEntityBatch
        {
            [ReadOnly]
            public EntityTypeHandle entityTypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<HexFromCell> fromCellTypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<HexToCell> toCellTypeHandle;
            public ComponentTypeHandle<FindPath> pathOptionsTypeHandle;

            [ReadOnly]
            public NativeHashMap<Entity, GridEntitiesArchIndex> gridInfo;
            [ReadOnly] [DeallocateOnJobCompletion]
            public NativeArray<ArchetypeChunk> gridEntitiesChunks;
            [ReadOnly]
            public BufferTypeHandle<HexCell> cellBufferTypeHandle;

            public EntityCommandBuffer.ParallelWriter ecbBegin;
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                NativeArray<HexFromCell> fromCells = batchInChunk.GetNativeArray(fromCellTypeHandle);
                NativeArray<HexToCell> toCells = batchInChunk.GetNativeArray(toCellTypeHandle);
                NativeArray<FindPath> pathOptions = batchInChunk.GetNativeArray(pathOptionsTypeHandle);
                NativeArray<Entity> entities = batchInChunk.GetNativeArray(entityTypeHandle);

                for (int i = 0; i < fromCells.Length; i++)
                {
                    PathFindingOptions options = pathOptions[i];
                    HexCell fromCell = fromCells[i];
                    HexCell toCell = toCells[i];
                    (DynamicBuffer<HexCell> cells, HexCellPriorityQueue searchFrontier) = GetCellsAndSearcher(options);
                    bool found;
                    (searchFrontier, found) = Search(cells, searchFrontier, fromCell, toCell, ref options);
                    if (found)
                    {
                        HexCellQueueElement current = searchFrontier.elements[toCell.Index];
                        HexCellQueueElement currentFromElement = searchFrontier.elements[fromCell.Index];
                        NativeList<HexCell> path = new NativeList<HexCell>(Allocator.Temp);
                        while (current != currentFromElement)
                        {
                            path.Add(cells[currentFromElement.cellIndex]);
                            current = searchFrontier.elements[current.PathFrom];
                        }
                        ecbBegin.AddBuffer<HexCell>(batchIndex, entities[i]).CopyFrom(path);
                        ecbBegin.AddComponent<FoundPath>(batchIndex, entities[i]);
                    }
                    else
                    {
                        ecbBegin.AddComponent<NotFoundPath>(batchIndex, entities[i]);
                    }
                    ecbBegin.RemoveComponent<FindPath>(batchIndex, entities[i]);
                    pathOptions[i] = options;
                }
            }

            private (HexCellPriorityQueue, bool) Search(DynamicBuffer<HexCell> cells, HexCellPriorityQueue searchFrontier, HexCell fromCell, HexCell toCell, ref PathFindingOptions options)
            {
                int speed = options.Speed;
                options.searchFrontierPhase += 2;
                HexCellQueueElement fromElement = searchFrontier.elements[fromCell.Index];
                HexCellQueueElement toElement = searchFrontier.elements[toCell.Index];
                fromElement.SearchPhase = options.searchFrontierPhase;
                fromElement.Distance = 0;
                searchFrontier.Enqueue(fromElement);
                while (searchFrontier.Count > 0)
                {
                    HexCellQueueElement currentElement = searchFrontier.DequeueElement();
                    currentElement.SearchPhase += 1;
                    searchFrontier.elements[currentElement.cellIndex] = currentElement;
                    if (currentElement == toElement)
                    {
                        return (searchFrontier, true);
                    }
                    int currentTurn = (currentElement.Distance - 1) / speed;
                    for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                    {
                        HexCell neighbourCell = HexCell.GetNeighbour(currentElement.cellIndex, cells, d);

                        if (neighbourCell == HexCell.Null)
                        {
                            continue;
                        }
                        HexCellQueueElement neighbourElement = searchFrontier.elements[neighbourCell.Index];
                        if (neighbourElement.SearchPhase > options.searchFrontierPhase)
                        {
                            continue;
                        }
                        if (!HexCell.IsValidDestination(neighbourCell))
                        {
                            continue;
                        }
                        int moveCost = HexCell.GetMoveCost(cells[currentElement.cellIndex], neighbourCell, d);
                        if (moveCost < 0)
                        {
                            continue;
                        }
                        int distance = currentElement.Distance + moveCost;
                        int turn = (distance - 1) / speed;
                        if (turn > currentTurn)
                        {
                            distance = turn * speed + moveCost;
                        }
                        if (neighbourElement.SearchPhase < options.searchFrontierPhase)
                        {
                            neighbourElement.SearchPhase = options.searchFrontierPhase;
                            neighbourElement.Distance = distance;
                            neighbourElement.PathFrom = currentElement.cellIndex;
                            neighbourElement.SearchHeuristic = neighbourCell.coordinates.DistanceTo(toCell.coordinates);
                            searchFrontier.Enqueue(neighbourElement);
                        }
                        else if (distance < neighbourElement.Distance)
                        {
                            int oldPriority = neighbourElement.SearchPriority;
                            neighbourElement.Distance = distance;
                            neighbourElement.PathFrom = currentTurn;
                            searchFrontier.Change(neighbourElement, oldPriority);
                        }
                    }
                }
                return (searchFrontier, false);
            }

            private (DynamicBuffer<HexCell>, HexCellPriorityQueue) GetCellsAndSearcher(PathFindingOptions options)
            {
                GridEntitiesArchIndex gridData = gridInfo[options.GridEntity];
                DynamicBuffer<HexCell> cells = gridEntitiesChunks[gridData.ChunkIndex].GetBufferAccessor(cellBufferTypeHandle)[gridData.ChunkArrayIndex];
                NativeArray<HexCellQueueElement> elements = new NativeArray<HexCellQueueElement>(cells.Length, Allocator.Temp, NativeArrayOptions.ClearMemory);
                for (int i = 0; i < cells.Length; i++)
                {
                    elements[i] = new HexCellQueueElement
                    {
                        cellIndex = i,
                        NextWithSamePriority = int.MinValue,
                        SearchPhase = options.searchFrontierPhase
                    };
                }
                HexCellPriorityQueue searchFrontier = new HexCellPriorityQueue(elements);
                return (cells, searchFrontier);
            }
        }
    }
}