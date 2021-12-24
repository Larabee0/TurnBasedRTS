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
            public BufferTypeHandle<HexUnitPathToTravel> pathToTravelTypeHandle;

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
                BufferAccessor<HexUnitPathToTravel> pathToTravelAccessors = batchInChunk.GetBufferAccessor(pathToTravelTypeHandle);

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
                    NativeList<HexUnitPathToTravel> path = new NativeList<HexUnitPathToTravel>(Allocator.Temp);
                    if (pathFound)
                    {
                        
                        HexCellQueueElement current = searchFrontier.elements[toCell.Index];
                        while(current != searchFrontier.elements[fromCell.Index])
                        {
                            path.Add(new HexUnitPathToTravel{ Cell = cells[current.cellIndex]});
                            current = searchFrontier.elements[current.PathFrom];
                        }
                        pathToTravelAccessors[i].CopyFrom(path.AsArray());
                        path.Dispose();
                    }
                    else
                    {
                        pathToTravelAccessors[i].Clear();
                    }
                    searchFrontier.Dispose();
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
                    HexCellQueueElement currentElement = searchFrontier.elements[searchFrontier.Dequeue()];
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
            public BufferTypeHandle<HexUnitPathToTravel> pathToTravelTypeHandle;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                NativeArray<Entity> unitEntities = batchInChunk.GetNativeArray(entityTypeHandle);
                NativeArray<HexUnitComp> unitComps = batchInChunk.GetNativeArray(hexUnitTypeHandle);
                NativeArray<HexUnitLocation> locationComps = batchInChunk.GetNativeArray(locationTypeHandle);
                NativeArray<HexUnitCurrentTravelLocation> cTravelLocationComps = batchInChunk.GetNativeArray(currentTravelLocationTypeHandle);
                NativeArray<Translation> translationComps = batchInChunk.GetNativeArray(translationTypeHandle);
                NativeArray<Rotation> rotationComps = batchInChunk.GetNativeArray(rotationTypeHandle);
                BufferAccessor<HexUnitPathToTravel> pathToTravelAccessors = batchInChunk.GetBufferAccessor(pathToTravelTypeHandle);

                for (int i = 0; i < unitEntities.Length; i++)
                {
                    Entity unitEntity = unitEntities[i];
                    HexUnitComp unitComp = unitComps[i];
                    HexUnitLocation locationComp = locationComps[i];
                    HexUnitCurrentTravelLocation currentTravelLocationComp = cTravelLocationComps[i];
                    Translation translationComp = translationComps[i];
                    Rotation rotationComp = rotationComps[i];
                    DynamicBuffer<HexUnitPathToTravel> pathToTravel = pathToTravelAccessors[i];

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
}