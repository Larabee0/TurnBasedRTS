using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using System.Runtime.CompilerServices;

namespace DOTSHexagonsV2
{
    public struct GenerationSettings : IComponentData
    {
        public float jitterProbability;
        public float highRiseProbability;
        public float sinkProbability;
        public float evaporationFactor;
        public float precipitationFactor;
        public float runoffFactor;
        public float seepageFactor;
        public float windStrength;
        public float startingMoisture;
        public float extraLakeProbability;
        public float lowTemperature;
        public float highTemperature;
        public float temperatureJitter;
        public int SearchPhase;
        public uint seed;
        public int chunkSizeMin;
        public int chunkSizeMax;
        public int landPercentage;
        public int waterLevel;
        public int elevationMinimum;
        public int elevationMaximum;
        public int mapBoarderX;
        public int mapBorderZ;
        public int regionBorder;
        public int regionCount;
        public int erosionPercentage;
        public int riverPercentage;
        public HexDirection windDirection;
        public HemiSphereMode hemiSphereMode;
    }
    public struct Generate : IComponentData { }

    public class HexMapGenerator : MonoBehaviour
    {
        private EntityManager entityManager;

        public GridAPI grid;
        public bool useFixedSeed;
        public int seed = 0;
        [Range(0, 0.5f)]
        public float jitterProbability = 0.25f;
        [Range(20, 200)]
        public int chunkSizeMin = 30;
        [Range(20, 200)]
        public int chunkSizeMax = 100;
        [Range(5, 95)]
        public int landPercentage = 50;
        [Range(1, 5)]
        public int waterLevel = 3;
        [Range(0, 1f)]
        public float highRiseProbability = 0.25f;
        [Range(0, 0.4f)]
        public float sinkProbability = 0.2f;
        [Range(-4, 0)]
        public int elevationMinimum = -2;
        [Range(6, 10)]
        public int elevationMaximum = 8;
        [Range(0, 10)]
        public int mapBoarderX = 5;
        [Range(0, 10)]
        public int mapBorderZ = 5;
        [Range(0, 10)]
        public int regionBorder = 5;
        [Range(1, 4)]
        public int regionCount = 1;
        [Range(0, 100)]
        public int erosionPercentage = 50;
        [Range(0f, 1f)]
        public float evaporationFactor = 0.5f;
        [Range(0f, 1f)]
        public float precipitationFactor = 0.25f;
        [Range(0f, 1f)]
        public float runoffFactor = 0.25f;
        [Range(0f, 1f)]
        public float seepageFactor = 0.125f;
        public HexDirection windDirection = HexDirection.NW;
        [Range(1f, 10f)]
        public float windStrength = 4f;
        [Range(0f, 1f)]
        public float startingMoisture = 0.1f;
        [Range(0, 20)]
        public int riverPercentage = 10;
        [Range(0f, 1f)]
        public float extraLakeProbability = 0.1f;
        [Range(0f, 1f)]
        public float lowTemperature = 0f;
        [Range(0f, 1f)]
        public float highTemperature = 1f;
        public HemiSphereMode hemiSphereMode = HemiSphereMode.Both;
        [Range(0f, 1f)]
        public float temperatureJitter = 0.1f;

        void Start()
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        }

        public GenerationSettings CreateGenerationData(uint seed)
        {
            return new GenerationSettings
            {
                SearchPhase = 0,
                seed = seed,
                jitterProbability = jitterProbability,
                chunkSizeMin = chunkSizeMin,
                chunkSizeMax = chunkSizeMax,
                landPercentage = landPercentage,
                waterLevel = waterLevel,
                highRiseProbability = highRiseProbability,
                sinkProbability = sinkProbability,
                elevationMinimum = elevationMinimum,
                elevationMaximum = elevationMaximum,
                mapBoarderX = mapBoarderX,
                mapBorderZ = mapBorderZ,
                regionBorder = regionBorder,
                regionCount = regionCount,
                erosionPercentage = erosionPercentage,
                evaporationFactor = evaporationFactor,
                precipitationFactor = precipitationFactor,
                runoffFactor = runoffFactor,
                seepageFactor = seepageFactor,
                windDirection = windDirection,
                windStrength = windStrength,
                startingMoisture = startingMoisture,
                riverPercentage = riverPercentage,
                extraLakeProbability = extraLakeProbability,
                lowTemperature = lowTemperature,
                highTemperature = highTemperature,
                hemiSphereMode = hemiSphereMode,
                temperatureJitter = temperatureJitter
            };
        }

        public void GenerateMap(int x, int z, bool wrapping)
        {
            uint seed = 0;
            if (!useFixedSeed)
            {
                seed = (uint)UnityEngine.Random.Range(0, int.MaxValue);
                seed ^= (uint)DateTime.Now.Ticks;
                seed ^= (uint)Time.unscaledTime;
                seed &= uint.MaxValue;
            }
            Entity gridEntity = entityManager.CreateEntity(typeof(HexGridComponent), typeof(HexGridChild), typeof(HexCell), typeof(HexGridChunkBuffer), typeof(Generate), typeof(HexGridUnInitialised), typeof(HexHash));
            GridAPI.ActiveGridEntity = gridEntity;
            bool WillGetGrid = grid.CreateMapDataFullJob(gridEntity, seed, x, z, wrapping);
            if (WillGetGrid)
            {
                entityManager.AddComponentData(gridEntity, CreateGenerationData(seed));
                Debug.Log("Map generator is expecting a grid soon. " + Time.realtimeSinceStartup);
            }
            else
            {
                Debug.LogWarning("Map generator will not get a grid. " + Time.realtimeSinceStartup);
            }
        }
    }

    public class HexMapGeneratorSystem : JobComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem ecbEndSystem;
        private EntityQuery GenerateMapQuery;

        protected override void OnCreate()
        {
            ecbEndSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            GenerateMapQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(GenerationSettings), typeof(HexGridCreated), typeof(Generate) } });
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            GenerateMap generateMapJob = new GenerateMap
            {
                noiseColours = HexFunctions.noiseColours,
                hGCTypeHandle = GetComponentTypeHandle<HexGridComponent>(true),
                generationsTypeHandle = GetComponentTypeHandle<GenerationSettings>(true),
                hexCellBufferTypeHandle = GetBufferTypeHandle<HexCell>(),
                hexGridChunkBufferTypeHandle = GetBufferTypeHandle<HexGridChunkBuffer>(true),
                ecbEnd = ecbEndSystem.CreateCommandBuffer().AsParallelWriter()
            };
            JobHandle outputDeps = generateMapJob.ScheduleParallel(GenerateMapQuery, 32, inputDeps);
            ecbEndSystem.AddJobHandleForProducer(outputDeps);
            return outputDeps;
        }

        [BurstCompile]
        public struct GenerateMap : IJobEntityBatch
        {
            private static readonly float[] temperatureBands = { 0.1f, 0.3f, 0.6f };
            private static readonly float[] moistureBands = { 0.12f, 0.28f, 0.85f };

            private static readonly Biome[] biomes =
            {
            new Biome(0,0), new Biome(4,0), new Biome(4,0), new Biome(4,0),
            new Biome(0,0), new Biome(2,0), new Biome(2,1), new Biome(2,2),
            new Biome(0,0), new Biome(1,0), new Biome(1,1), new Biome(1,2),
            new Biome(0,0), new Biome(1,1), new Biome(1,2), new Biome(1,3)
            };
            [ReadOnly]
            public NativeArray<float4> noiseColours;

            [ReadOnly]
            public ComponentTypeHandle<HexGridComponent> hGCTypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<GenerationSettings> generationsTypeHandle;
            public BufferTypeHandle<HexCell> hexCellBufferTypeHandle;
            [ReadOnly]
            public BufferTypeHandle<HexGridChunkBuffer> hexGridChunkBufferTypeHandle;

            public EntityCommandBuffer.ParallelWriter ecbEnd;
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                BufferAccessor<HexCell> hexCellBufferAccessors = batchInChunk.GetBufferAccessor(hexCellBufferTypeHandle);
                BufferAccessor<HexGridChunkBuffer> hexGridChunkBufferAccessors = batchInChunk.GetBufferAccessor(hexGridChunkBufferTypeHandle);
                NativeArray<GenerationSettings> genSettings = batchInChunk.GetNativeArray(generationsTypeHandle);
                NativeArray<HexGridComponent> hexGridCompArray = batchInChunk.GetNativeArray(hGCTypeHandle);
                for (int i = 0; i < genSettings.Length; i++)
                {
                    GenerationSettings settings = genSettings[i];

                    HexGridComponent grid = hexGridCompArray[i];
                    NativeList<MapRegion> regions = new NativeList<MapRegion>(4, Allocator.Temp);
                    Unity.Mathematics.Random randomNumberGenerator = CreateRegions(regions, grid, settings, new Unity.Mathematics.Random(settings.seed));

                    NativeArray<HexCell> cells = hexCellBufferAccessors[i].ToNativeArray(Allocator.Temp);
                    NativeArray<HexCellQueueElement> searchCells = new NativeArray<HexCellQueueElement>(cells.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
                    {
                        HexCell cell = cells[cellIndex];
                        cell.WaterLevel = settings.waterLevel;
                        cells[cellIndex] = cell;
                        searchCells[cellIndex] = new HexCellQueueElement
                        {
                            cellIndex = cellIndex,
                            NextWithSamePriority = int.MinValue,
                            SearchPhase = settings.SearchPhase
                        };
                    }
                    HexCellPriorityQueue searchFrontier = new HexCellPriorityQueue(searchCells);
                    int landCells;
                    (landCells, randomNumberGenerator) = CreateLand(cells, regions, grid, settings, randomNumberGenerator, searchFrontier);
                    searchFrontier.Dispose();
                    regions.Dispose();

                    NativeList<int> erodibleCells = new NativeList<int>(cells.Length / 2, Allocator.Temp);
                    for (int cellIndex = 0; cellIndex < erodibleCells.Length; cellIndex++)
                    {
                        HexCell cell = cells[cellIndex];
                        switch (IsErodible(cells, cell))
                        {
                            case true:
                                erodibleCells.Add(cell.Index);
                                break;
                        }
                    }
                    randomNumberGenerator = ErodeLand(cells, erodibleCells, randomNumberGenerator, settings.erosionPercentage);

                    NativeArray<ClimateData> climate = new NativeArray<ClimateData>(cells.Length, Allocator.Temp);
                    NativeArray<ClimateData> nextClimate = new NativeArray<ClimateData>(cells.Length, Allocator.Temp);
                    climate = CreateClimate(cells, climate, nextClimate, settings);

                    randomNumberGenerator = CreateRivers(cells, climate, randomNumberGenerator, settings, landCells);

                    SetTerrianType(cells, climate, grid, settings, randomNumberGenerator);
                    climate.Dispose();
                    hexCellBufferAccessors[i].CopyFrom(cells);
                    cells.Dispose();
                    DynamicBuffer<HexGridChunkBuffer> chunkBuffer = hexGridChunkBufferAccessors[i];
                    for (int chunkIndex = 0; chunkIndex < chunkBuffer.Length; chunkIndex++)
                    {
                        ecbEnd.AddComponent<RefreshChunk>(batchIndex, chunkBuffer[chunkIndex].ChunkEntity);
                    }
                    ecbEnd.RemoveComponent<Generate>(batchIndex, grid.gridEntity);
                }
            }

            private Unity.Mathematics.Random CreateRegions(NativeList<MapRegion> regions, HexGridComponent grid, GenerationSettings settings, Unity.Mathematics.Random randomNumberGenerator)
            {
                int borderX = grid.wrapping ? settings.regionBorder : settings.mapBoarderX;
                MapRegion region;
                switch (settings.regionCount)
                {
                    default:
                        if (grid.wrapping)
                        {
                            borderX = 0;
                        }
                        region.xMin = borderX;
                        region.xMax = grid.cellCountX - borderX;
                        region.zMin = settings.mapBorderZ;
                        region.zMax = grid.cellCountZ - settings.mapBorderZ;
                        regions.Add(region);
                        break;
                    case 2:
                        if (randomNumberGenerator.NextFloat(0, 1f) < 0.5f)
                        {
                            region.xMin = borderX;
                            region.xMax = grid.cellCountX / 2 - settings.regionBorder;
                            region.zMin = settings.mapBorderZ;
                            region.zMax = grid.cellCountZ - settings.mapBorderZ;
                            regions.Add(region);
                            region.xMin = grid.cellCountX / 2 + settings.regionBorder;
                            region.xMax = grid.cellCountX - borderX;
                            regions.Add(region);
                        }
                        else
                        {
                            if (grid.wrapping)
                            {
                                borderX = 0;
                            }
                            region.xMin = borderX;
                            region.xMax = grid.cellCountX - borderX;
                            region.zMin = settings.mapBorderZ;
                            region.zMax = grid.cellCountZ / 2 - settings.regionBorder;
                            regions.Add(region);
                            region.zMin = grid.cellCountZ / 2 + settings.regionBorder;
                            region.zMax = grid.cellCountZ - settings.mapBorderZ;
                            regions.Add(region);
                        }
                        break;
                    case 3:
                        region.xMin = borderX;
                        region.xMax = grid.cellCountX / 3 - settings.regionBorder;
                        region.zMin = settings.mapBorderZ;
                        region.zMax = grid.cellCountZ - settings.mapBorderZ;
                        regions.Add(region);
                        region.xMin = grid.cellCountX / 3 + settings.regionBorder;
                        region.xMax = grid.cellCountX * 2 / 3 - settings.regionBorder;
                        regions.Add(region);
                        region.xMin = grid.cellCountX * 2 / 3 + settings.regionBorder;
                        region.xMax = grid.cellCountX - borderX;
                        regions.Add(region);
                        break;
                    case 4:
                        region.xMin = borderX;
                        region.xMax = grid.cellCountX / 2 - settings.regionBorder;
                        region.zMin = settings.mapBorderZ;
                        region.zMax = grid.cellCountZ / 2 - settings.regionBorder;
                        regions.Add(region);
                        region.xMin = grid.cellCountX / 2 + settings.regionBorder;
                        region.xMax = grid.cellCountX - borderX;
                        regions.Add(region);
                        region.zMin = grid.cellCountZ / 2 + settings.regionBorder;
                        region.zMax = grid.cellCountZ - settings.mapBorderZ;
                        regions.Add(region);
                        region.xMin = borderX;
                        region.xMax = grid.cellCountX / 2 - settings.regionBorder;
                        regions.Add(region);
                        break;
                }
                return randomNumberGenerator;
            }
            #region CreateLand
            private (int, Unity.Mathematics.Random) CreateLand(NativeArray<HexCell> cells, NativeList<MapRegion> regions, HexGridComponent grid, GenerationSettings settings, Unity.Mathematics.Random randomNumber, HexCellPriorityQueue searchFrontier)
            {
                int landBudget = (int)math.round(cells.Length * settings.landPercentage * 0.01f);
                int landCells = landBudget;
                searchFrontier.searchPhase = settings.SearchPhase;
                for (int guard = 0; guard < 10000; guard++)
                {
                    bool sink = randomNumber.NextFloat(0, 1) < settings.sinkProbability;
                    int chunkSize = randomNumber.NextInt(settings.chunkSizeMin, settings.chunkSizeMax - 1);
                    for (int i = 0; i < regions.Length; i++)
                    {
                        MapRegion region = regions[i];
                        if (sink)
                        {
                            (landBudget, searchFrontier, randomNumber) = SinkTerrain(randomNumber, searchFrontier, cells, chunkSize, landBudget, region, settings, grid);
                        }
                        else
                        {
                            (landBudget, searchFrontier, randomNumber) = RaiseTerrain(randomNumber, searchFrontier, cells, chunkSize, landBudget, region, settings, grid);
                            if (landBudget == 0)
                            {
                                settings.SearchPhase = searchFrontier.searchPhase;
                                return (landCells, randomNumber);
                            }
                        }
                        chunkSize = randomNumber.NextInt(settings.chunkSizeMin, settings.chunkSizeMax - 1);
                    }
                }
                switch (landBudget > 0)
                {
                    case true:
                        landCells -= landBudget;
                        break;
                }
                return (landCells, randomNumber);
            }

            private (int, HexCellPriorityQueue, Unity.Mathematics.Random randomNumber) RaiseTerrain(Unity.Mathematics.Random randomNumber, HexCellPriorityQueue searchFrontier, NativeArray<HexCell> cells, int chunkSize, int budget, MapRegion region, GenerationSettings settings, HexGridComponent grid)
            {
                searchFrontier.searchPhase += 1;
                int cellIndex = GetRandomCell(randomNumber, region, grid.cellCountX);
                HexCell firstCell = cells[cellIndex];
                HexCellQueueElement firstCellElement = searchFrontier.elements[cellIndex];
                firstCellElement.SearchPhase = searchFrontier.searchPhase;
                firstCellElement.Distance = 0;
                firstCellElement.SearchHeuristic = 0;
                searchFrontier.elements[cellIndex] = firstCellElement;
                searchFrontier.Enqueue(firstCellElement);
                HexCoordinates centre = firstCell.coordinates;

                int rise = randomNumber.NextFloat(0, 1) < settings.highRiseProbability ? 2 : 1;
                int size = 0;
                while (size < chunkSize && searchFrontier.Count > 0)
                {
                    HexCell current = cells[searchFrontier.Dequeue()];
                    int originalElevation = current.Elevation;
                    int newElevation = originalElevation + rise;

                    if (newElevation > settings.elevationMaximum)
                    {
                        continue;
                    }
                    current.elevation = newElevation;
                    current.RefreshPosition(noiseColours);
                    current = HexCell.ValidateRivers(cells, current);
                    if (originalElevation < settings.waterLevel && newElevation >= settings.waterLevel && --budget == 0)
                    {
                        break;
                    }

                    size += 1;

                    for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                    {
                        int neighbourIndex = HexCell.GetNeighbourIndex(current, d);
                        switch (neighbourIndex != int.MinValue)
                        {
                            case true:
                                HexCellQueueElement neighbourElement = searchFrontier.elements[neighbourIndex];
                                HexCoordinates neighbour = cells[neighbourIndex].coordinates;
                                switch (neighbourElement.SearchPhase < searchFrontier.searchPhase)
                                {
                                    case true:
                                        neighbourElement.SearchPhase = searchFrontier.searchPhase;
                                        neighbourElement.Distance = neighbour.DistanceTo(centre, grid.wrapSize);
                                        neighbourElement.SearchHeuristic = randomNumber.NextFloat(0, 1) < settings.jitterProbability ? 1 : 0;
                                        searchFrontier.elements[neighbourIndex] = neighbourElement;
                                        searchFrontier.Enqueue(neighbourElement);
                                        break;
                                }
                                break;
                        }

                    }
                    cells[current.Index] = current;
                }
                searchFrontier.Clear();
                return (budget, searchFrontier, randomNumber);
            }

            private (int, HexCellPriorityQueue, Unity.Mathematics.Random randomNumber) SinkTerrain(Unity.Mathematics.Random randomNumber, HexCellPriorityQueue searchFrontier, NativeArray<HexCell> cells, int chunkSize, int budget, MapRegion region, GenerationSettings settings, HexGridComponent grid)
            {
                searchFrontier.searchPhase += 1;
                int cellIndex = GetRandomCell(randomNumber, region, grid.cellCountX);
                HexCell firstCell = cells[cellIndex];
                HexCellQueueElement firstCellElement = searchFrontier.elements[cellIndex];
                firstCellElement.SearchPhase = searchFrontier.searchPhase;
                firstCellElement.Distance = 0;
                firstCellElement.SearchHeuristic = 0;
                searchFrontier.elements[cellIndex] = firstCellElement;
                searchFrontier.Enqueue(firstCellElement);
                HexCoordinates centre = firstCell.coordinates;

                int sink = randomNumber.NextFloat(0, 1) < settings.highRiseProbability ? 2 : 1;
                int size = 0;
                while (size < chunkSize && searchFrontier.Count > 0)
                {
                    HexCell current = cells[searchFrontier.Dequeue()];
                    int originalElevation = current.Elevation;
                    int newElevation = originalElevation - sink;

                    if (newElevation < settings.elevationMaximum)
                    {
                        continue;
                    }

                    current.elevation = newElevation;
                    current.RefreshPosition(noiseColours);
                    current = HexCell.ValidateRivers(cells, current);
                    switch (originalElevation >= settings.waterLevel && newElevation < settings.waterLevel)
                    {
                        case true:
                            budget += 1;
                            break;
                    }

                    size += 1;

                    for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                    {
                        int neighbourIndex = HexCell.GetNeighbourIndex(current, d);
                        switch (neighbourIndex != int.MinValue)
                        {
                            case true:
                                HexCellQueueElement neighbourElement = searchFrontier.elements[neighbourIndex];
                                HexCoordinates neighbour = cells[neighbourIndex].coordinates;
                                switch (neighbourElement.SearchPhase < searchFrontier.searchPhase)
                                {
                                    case true:
                                        neighbourElement.SearchPhase = searchFrontier.searchPhase;
                                        neighbourElement.Distance = neighbour.DistanceTo(centre, grid.wrapSize);
                                        neighbourElement.SearchHeuristic = randomNumber.NextFloat(0, 1) < settings.jitterProbability ? 1 : 0;
                                        searchFrontier.elements[neighbourIndex] = neighbourElement;
                                        searchFrontier.Enqueue(neighbourElement);
                                        break;
                                }
                                break;
                        }
                    }
                    cells[current.Index] = current;
                }
                searchFrontier.Clear();
                return (budget, searchFrontier, randomNumber);
            }

            private int GetRandomCell(Unity.Mathematics.Random randomNumber, MapRegion region, int cellCountX)
            {
                int xOffset = randomNumber.NextInt(region.xMin, region.xMax);
                int zOffset = randomNumber.NextInt(region.zMin, region.zMax);

                return xOffset + zOffset * cellCountX;
            }
            #endregion
            #region ErodeLand
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool IsErodible(NativeArray<HexCell> cells, HexCell cell)
            {
                int erodibleElevation = cell.Elevation - 2;
                for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                {
                    int neighbourIndex = HexCell.GetNeighbourIndex(cell, d);
                    if (neighbourIndex != int.MinValue)
                    {
                        HexCell neighbour = cells[neighbourIndex];
                        if (neighbour.Elevation <= erodibleElevation)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            private Unity.Mathematics.Random ErodeLand(NativeArray<HexCell> cells, NativeList<int> erodibleCells, Unity.Mathematics.Random randomNumber, int erosionPercentage)
            {
                NativeList<int> candidates = new NativeList<int>(6, Allocator.Temp);

                int targetErodibleCount = (int)(erodibleCells.Length * (100 - erosionPercentage) * 0.01f);
                while (erodibleCells.Length > targetErodibleCount)
                {
                    int index = randomNumber.NextInt(0, erodibleCells.Length);

                    HexCell cell = cells[erodibleCells[index]];
                    int targetCellIndex;
                    (targetCellIndex, randomNumber) = GetErosionTarget(randomNumber, cells, cell, candidates);
                    if (targetCellIndex == int.MinValue)
                    {

                        if (!IsErodible(cells, cell))
                        {
                            erodibleCells.RemoveAt(index);
                        }
                        continue;
                    }
                    cell.elevation -= 1;
                    cell.RefreshPosition(noiseColours);
                    cell = HexCell.ValidateRivers(cells, cell);
                    HexCell targetCell = cells[targetCellIndex];
                    targetCell.elevation += 1;
                    targetCell.RefreshPosition(noiseColours);
                    targetCell = HexCell.ValidateRivers(cells, targetCell);
                    cell = cells[cell.Index];
                    if (!IsErodible(cells, cell))
                    {
                        erodibleCells[index] = erodibleCells[erodibleCells.Length - 1];
                        erodibleCells.RemoveAt(erodibleCells.Length - 1);
                    }

                    for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                    {
                        int neighbourIndex = HexCell.GetNeighbourIndex(cell, d);
                        if (neighbourIndex != int.MinValue)
                        {
                            HexCell neighbour = cells[neighbourIndex];
                            if (neighbour.Elevation == cell.Elevation + 2 && !erodibleCells.Contains(neighbourIndex))
                            {
                                erodibleCells.Add(neighbourIndex);
                            }
                        }
                    }

                    if (IsErodible(cells, targetCell) && !erodibleCells.Contains(targetCellIndex))
                    {
                        erodibleCells.Add(targetCellIndex);
                    }

                    for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                    {
                        int neighbourIndex = HexCell.GetNeighbourIndex(targetCell, d);
                        if (neighbourIndex != int.MinValue)
                        {
                            HexCell neighbour = cells[neighbourIndex];
                            if (!neighbour.Equals(cell) && neighbour.Elevation == targetCell.Elevation + 1 && !IsErodible(cells, neighbour))
                            {
                                for (int i = 0; i < erodibleCells.Length; i++)
                                {
                                    if (erodibleCells[i] == neighbourIndex)
                                    {
                                        erodibleCells.RemoveAt(i);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                candidates.Dispose();
                return randomNumber;
            }

            private (int, Unity.Mathematics.Random) GetErosionTarget(Unity.Mathematics.Random randomNumber, NativeArray<HexCell> cells, HexCell cell, NativeList<int> candidates)
            {
                candidates.Clear();
                int erodibleElevation = cell.Elevation - 2;
                for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                {
                    int neighbourIndex = HexCell.GetNeighbourIndex(cell, d);
                    if (neighbourIndex != int.MinValue)
                    {
                        HexCell neighbour = cells[neighbourIndex];
                        if (neighbour.Elevation <= erodibleElevation)
                        {
                            candidates.Add(neighbour.Index);
                        }
                    }
                }
                if (candidates.Length == 0)
                {
                    return (int.MinValue, randomNumber);
                }
                return (candidates[randomNumber.NextInt(0, candidates.Length)], randomNumber);

            }
            #endregion
            private NativeArray<ClimateData> CreateClimate(NativeArray<HexCell> cells, NativeArray<ClimateData> climate, NativeArray<ClimateData> nextClimate,GenerationSettings settings)
            {
                ClimateData initialData = new ClimateData
                {
                    moisture = settings.startingMoisture
                };
                ClimateData clearData = new ClimateData();

                for (int i = 0; i < cells.Length; i++)
                {
                    climate[i]=initialData;
                    nextClimate[i] = clearData;
                }

                for (int cycle = 0; cycle < 40; cycle++)
                {
                    for (int i = 0; i < cells.Length; i++)
                    {
                        HexCell cell = cells[i];
                        ClimateData cellClimate = climate[i];
                        if (cell.IsUnderwater)
                        {
                            cellClimate.moisture = 1f;
                            cellClimate.clouds += settings.evaporationFactor;
                        }
                        else
                        {
                            float evaporation = cellClimate.moisture * settings.evaporationFactor;
                            cellClimate.moisture -= evaporation;
                            cellClimate.clouds += evaporation;
                        }
                        float precipitation = cellClimate.clouds * settings.precipitationFactor;
                        cellClimate.clouds -= precipitation;
                        cellClimate.moisture += precipitation;
                        float cloudMaximum = 1f - cell.ViewElevation / (settings.elevationMaximum + 1f);
                        if (cellClimate.clouds > cloudMaximum)
                        {
                            cellClimate.moisture += cellClimate.clouds - cloudMaximum;
                            cellClimate.clouds = cloudMaximum;
                        }
                        HexDirection mainDispersalDirection = settings.windDirection.Opposite();
                        float cloudDispersal = cellClimate.clouds * (1f / (5f + settings.windStrength));
                        float runoff = cellClimate.moisture * settings.runoffFactor * (1f / 6f);
                        float seepage = cellClimate.moisture * settings.seepageFactor * (1f / 6f);
                        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                        {
                            int neighbourIndex = HexCell.GetNeighbourIndex(cell, d);
                            switch (neighbourIndex != int.MinValue)
                            {
                                case true:
                                    HexCell neighbour = cells[neighbourIndex];

                                    ClimateData neighbourClimate = nextClimate[neighbour.Index];
                                    if (d == mainDispersalDirection)
                                    {
                                        neighbourClimate.clouds += cloudDispersal * settings.windStrength;
                                    }
                                    else
                                    {
                                        neighbourClimate.clouds += cloudDispersal;
                                    }

                                    int elevationDelta = neighbour.ViewElevation - cell.Elevation;
                                    if (elevationDelta < 0)
                                    {
                                        cellClimate.moisture -= runoff;
                                        neighbourClimate.moisture += runoff;
                                    }
                                    else if (elevationDelta == 0)
                                    {
                                        cellClimate.moisture -= seepage;
                                        neighbourClimate.moisture += seepage;
                                    }

                                    nextClimate[neighbour.Index] = neighbourClimate;
                                    break;
                            }

                        }

                        ClimateData nextCellClimate = nextClimate[i];
                        nextCellClimate.moisture += cellClimate.moisture;
                        if (nextCellClimate.moisture > 1f)
                        {
                            nextCellClimate.moisture = 1f;
                        }
                        nextClimate[i] = nextCellClimate;
                        climate[i] = new ClimateData();
                    }
                    NativeArray<ClimateData> swap = new NativeArray<ClimateData>(climate, Allocator.Temp);
                    climate.Dispose();
                    climate = new NativeArray<ClimateData>(nextClimate, Allocator.Temp);
                    nextClimate.Dispose();
                    nextClimate = new NativeArray<ClimateData>(swap, Allocator.Temp);
                    swap.Dispose();
                }

                nextClimate.Dispose();
                return climate;
            }
            #region CreateRivers
            private Unity.Mathematics.Random CreateRivers(NativeArray<HexCell> cells, NativeArray<ClimateData> climate, Unity.Mathematics.Random randomNumber, GenerationSettings settings, int landCells)
            {
                NativeList<int> riverOrigins = new NativeList<int>(cells.Length * 2, Allocator.Temp);
                int riverBudget = (int)math.round(landCells * settings.riverPercentage * 0.01f);
                riverOrigins.Add(riverBudget);
                for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
                {
                    HexCell cell = cells[cellIndex];
                    switch (cell.IsUnderwater)
                    {
                        case true:
                            continue;
                    }

                    ClimateData data = climate[cellIndex];
                    float weight = data.moisture * (cell.Elevation - settings.waterLevel) / (settings.elevationMaximum - settings.waterLevel);

                    switch (weight > 0.75f)
                    {
                        case true:

                            riverOrigins.AddNoResize(cellIndex);
                            riverOrigins.AddNoResize(cellIndex);
                            break;
                        case false:
                            switch (weight > 0.5f)
                            {
                                case true:
                                    riverOrigins.AddNoResize(cellIndex);
                                    break;
                                case false:
                                    switch (weight > 0.5f)
                                    {
                                        case true:
                                            riverOrigins.AddNoResize(cellIndex);
                                            break;
                                        case false:

                                            switch (weight > 0.25f)
                                            {
                                                case true:
                                                    riverOrigins.AddNoResize(cellIndex);
                                                    break;
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                    }
                }
                riverOrigins.Capacity = riverOrigins.Length;
                NativeList<HexDirection> flowDirections = new NativeList<HexDirection>(Allocator.Temp);
                while (riverBudget > 0 && riverOrigins.Length > 1)
                {
                    int index = randomNumber.NextInt(1, riverOrigins.Length);
                    int lastIndex = riverOrigins.Length - 1;
                    HexCell origin = cells[riverOrigins[index]];
                    riverOrigins[index] = riverOrigins[lastIndex];
                    riverOrigins.RemoveAt(lastIndex);
                    if (!origin.HasRiver)
                    {
                        bool isValidOrigin = true;
                        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                        {
                            int neighbourIndex = HexCell.GetNeighbourIndex(origin, d);
                            switch (neighbourIndex != int.MinValue)
                            {
                                case true:
                                    HexCell neighbour = cells[neighbourIndex];
                                    switch (neighbour.HasRiver || neighbour.IsUnderwater)
                                    {
                                        case true:
                                            isValidOrigin = false;
                                            break;
                                    }
                                    break;
                            }
                        }
                        if (isValidOrigin)
                        {
                            int toMinus;
                            (toMinus, randomNumber) = CreateRiver(cells, flowDirections, randomNumber, origin.Index, settings.extraLakeProbability);
                            riverBudget -= toMinus;
                        }
                    }
                }
                flowDirections.Dispose();
                riverOrigins[0] = riverBudget;

                return randomNumber;
            }
            private (int, Unity.Mathematics.Random) CreateRiver(NativeArray<HexCell> cells, NativeList<HexDirection> flowDirections, Unity.Mathematics.Random randomNumber, int originIndex, float extraLakeProbability)
            {
                int length = 1;
                HexCell origin = cells[originIndex];
                HexCell cell = cells[originIndex];
                HexDirection direction = HexDirection.NE;
                while (!cell.IsUnderwater)
                {
                    int minNeighbourElevation = int.MaxValue;
                    flowDirections.Clear();
                    for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                    {
                        int neighbourIndex = HexCell.GetNeighbourIndex(cell, d);
                        switch (neighbourIndex != int.MinValue)
                        {
                            case true:
                                HexCell neighbour = cells[neighbourIndex];
                                switch (neighbour.Elevation < minNeighbourElevation)
                                {
                                    case true:
                                        minNeighbourElevation = neighbour.Elevation;
                                        break;
                                }
                                switch (neighbour.Equals(origin) || neighbour.HasIncomingRiver)
                                {
                                    case false:
                                        int delta = neighbour.Elevation - cell.Elevation;
                                        switch (delta > 0)
                                        {
                                            case false:
                                                switch (neighbour.HasOutgoingRiver)
                                                {
                                                    case true:
                                                        HexCell.SetOutgoingRiver(cells, cell, direction);
                                                        return (length, randomNumber);
                                                }
                                                switch (delta < 0)
                                                {
                                                    case true:
                                                        flowDirections.Add(d);
                                                        flowDirections.Add(d);
                                                        flowDirections.Add(d);
                                                        break;
                                                }
                                                switch (length == 1 || (d != direction.Next2() && d != direction.Previous2()))
                                                {
                                                    case true:
                                                        flowDirections.Add(d);
                                                        break;
                                                }
                                                flowDirections.Add(d);
                                                break;
                                        }
                                        break;
                                }
                                break;
                        }
                    }
                    if (flowDirections.Length == 0)
                    {
                        switch (length == 1)
                        {
                            case true:
                                cells[cell.Index] = cell;
                                return (0, randomNumber);
                        }
                        switch (minNeighbourElevation >= cell.Elevation)
                        {
                            case true:
                                cell.WaterLevel = minNeighbourElevation;
                                cell = HexCell.ValidateRivers(cells, cell);
                                switch (minNeighbourElevation == cell.Elevation)
                                {
                                    case true:
                                        cell.elevation = minNeighbourElevation - 1;
                                        cell.RefreshPosition(noiseColours);
                                        cell = HexCell.ValidateRivers(cells, cell);
                                        break;
                                }
                                break;
                        }
                        break;
                    }
                    direction = flowDirections[randomNumber.NextInt(0, flowDirections.Length)];
                    cell = HexCell.SetOutgoingRiver(cells, cell, direction);
                    length += 1;

                    switch (minNeighbourElevation >= cell.Elevation && randomNumber.NextFloat(0, 1f) < extraLakeProbability)
                    {
                        case true:
                            cell.WaterLevel = cell.Elevation;
                            cell = HexCell.ValidateRivers(cells, cell);
                            cell.elevation -= 1;
                            cell.RefreshPosition(noiseColours);
                            cell = HexCell.ValidateRivers(cells, cell);
                            break;
                    }
                    cells[cell.Index] = cell;
                    origin = cells[originIndex];
                    cell = cells[HexCell.GetNeighbourIndex(cell, direction)];

                }
                return (length, randomNumber);
            }
            #endregion
            #region SetTerrian
            private void SetTerrianType(NativeArray<HexCell> cells, NativeArray<ClimateData> climate, HexGridComponent grid, GenerationSettings settings, Unity.Mathematics.Random randomNumberGenerator)
            {

                int temperatureJitterChannel = randomNumberGenerator.NextInt(0, 4);
                int rockDesertElevation = settings.elevationMaximum - (settings.elevationMaximum - settings.waterLevel) / 2;
                for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
                {
                    HexCell cell = cells[cellIndex];

                    float temperature = DetermineTemperature(cell, grid, settings, temperatureJitterChannel);
                    float moisture = climate[cellIndex].moisture;
                    if (!cell.IsUnderwater)
                    {
                        int t = 0;
                        for (; t < temperatureBands.Length; t++)
                        {
                            if (temperature < temperatureBands[t])
                            {
                                break;
                            }
                        }
                        int m = 0;
                        for (; m < temperatureBands.Length; m++)
                        {
                            if (moisture < moistureBands[m])
                            {
                                break;
                            }
                        }

                        Biome cellBiome = biomes[t * 4 + m];
                        if (cellBiome.terrian == 0)
                        {
                            if (cell.Elevation >= rockDesertElevation)
                            {
                                cellBiome.terrian = 3;
                            }
                        }
                        else if (cell.Elevation == settings.elevationMaximum)
                        {
                            cellBiome.terrian = 4;
                        }

                        if (cellBiome.terrian == 4)
                        {
                            cellBiome.plant = 0;
                        }
                        else if (cellBiome.plant < 3 && cell.HasRiver)
                        {
                            cellBiome.plant += 1;
                        }
                        cell.terrianTypeIndex = cellBiome.terrian;
                        cell.plantLevel = cellBiome.plant;
                    }
                    else
                    {
                        int terrain;
                        if (cell.Elevation == settings.waterLevel - 1)
                        {
                            int cliffs = 0, slopes = 0;
                            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                            {
                                int neighbourIndex = HexCell.GetNeighbourIndex(cell, d);
                                switch (neighbourIndex != int.MinValue)
                                {
                                    case true:
                                        HexCell neighbour = cells[neighbourIndex];
                                        int delta = neighbour.Elevation - cell.WaterLevel;
                                        if (delta == 0)
                                        {
                                            slopes += 1;
                                        }
                                        else if (delta > 0)
                                        {
                                            cliffs += 1;
                                        }
                                        break;
                                }
                            }
                            if (cliffs + slopes > 3)
                            {
                                terrain = 1;
                            }
                            else if (cliffs > 0)
                            {
                                terrain = 3;
                            }
                            else if (slopes > 0)
                            {
                                terrain = 0;
                            }
                            else
                            {
                                terrain = 1;
                            }
                        }
                        else if (cell.Elevation >= settings.waterLevel)
                        {
                            terrain = 1;
                        }
                        else if (cell.Elevation < 0)
                        {
                            terrain = 3;
                        }
                        else
                        {
                            terrain = 2;
                        }
                        if (terrain == 1 && temperature < temperatureBands[0])
                        {
                            terrain = 2;
                        }
                        cell.terrianTypeIndex = terrain;
                    }
                    cells[cellIndex] = cell;
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private float DetermineTemperature(HexCell cell, HexGridComponent grid, GenerationSettings settings, int temperatureJitterChannel)
            {
                float latitude = (float)cell.coordinates.Z / grid.cellCountZ;
                if (settings.hemiSphereMode == HemiSphereMode.Both)
                {
                    latitude *= 2f;
                    if (latitude > 1f)
                    {
                        latitude = 2f - latitude;
                    }
                }
                else if (settings.hemiSphereMode == HemiSphereMode.North)
                {
                    latitude = 1f - latitude;
                }
                float temperature = math.lerp(settings.lowTemperature, settings.highTemperature, latitude);
                temperature *= 1f - (cell.ViewElevation - settings.waterLevel) / (settings.elevationMaximum - settings.waterLevel + 1f);
                float jitter = HexFunctions.SampleNoise(noiseColours, cell.Position * 0.1f, grid.wrapSize)[temperatureJitterChannel];
                temperature += (jitter * 2f - 1f) * settings.temperatureJitter;
                return temperature;
            }
            #endregion
            private struct Biome
            {
                public int terrian, plant;

                public Biome(int terrian, int plant)
                {
                    this.terrian = terrian;
                    this.plant = plant;
                }
            }
            private struct MapRegion
            {
                public int xMin, xMax, zMin, zMax;

                public MapRegion(int xMin, int xMax, int zMin, int zMax)
                {
                    this.xMax = xMax;
                    this.xMin = xMin;
                    this.zMin = zMin;
                    this.zMax = zMax;
                }
            }
            private struct ClimateData
            {
                public float clouds, moisture;
            }
        }
    }
}