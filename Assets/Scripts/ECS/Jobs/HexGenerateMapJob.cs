using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using System.Runtime.CompilerServices;

[BurstCompile, WithAll(typeof(HexMapGenerate), typeof(HexChunkCellDataCompleted)), WithNone(typeof(HexGridUnInitialised))]
public partial struct HexGenerateMapJob : IJobEntity
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

    public EntityCommandBuffer.ParallelWriter ecbEnd;

    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main,
        ref DynamicBuffer<HexChunkCellWrapper> cellWrappers,
        in DynamicBuffer<HexCellReference> hexCells,
        in DynamicBuffer<HexGridChunkBuffer> chunkBuffer,
        in HexMapGenerationSettings generationSettings, in HexGridBasic grid)
    {
        NativeList<MapRegion> regions = new(4, Allocator.Temp);

        Random randomNumberGenerator = new(generationSettings.seed);
        CreateRegions(ref randomNumberGenerator, regions, grid, generationSettings);

        NativeArray<HexChunkCellWrapper> cells = cellWrappers.AsNativeArray();
        cells.Sort(new WrappedCellIndexSorter());
        NativeArray<HexCellQueueElement> searchCells = new(cells.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
        {
            HexChunkCellWrapper cell = cells[cellIndex];
            cell.cellTerrain.waterLevel = generationSettings.waterLevel;
            cells[cellIndex] = cell;
            searchCells[cellIndex] = new HexCellQueueElement
            {
                cellIndex = cellIndex,
                NextWithSamePriority = int.MinValue,
                SearchPhase = generationSettings.SearchPhase
            };
        }
        HexCellPriorityQueue searchFrontier = new(searchCells);

        int landCells = CreateLand(ref randomNumberGenerator, ref searchFrontier, cells, regions, grid, generationSettings);

        NativeList<int> erodibleCells = GetErodibleCells(cells);
        ErodeLand(ref randomNumberGenerator, cells, erodibleCells, generationSettings.erosionPercentage, grid.wrapSize);

        NativeArray<ClimateData> climate = new(cells.Length, Allocator.Temp);
        NativeArray<ClimateData> nextClimate = new(cells.Length, Allocator.Temp);
        climate = CreateClimate(cells, climate, nextClimate, generationSettings);

        CreateRivers(ref randomNumberGenerator, cells, climate, generationSettings, landCells, grid.wrapSize);

        SetTerrianType(ref randomNumberGenerator, cells, climate, grid, generationSettings);

        for (int i = 0; i < hexCells.Length; i++)
        {
            ecbEnd.SetComponent(jobChunkIndex, hexCells[i].Value, cells[i].cellBasic);
            ecbEnd.SetComponent(jobChunkIndex, hexCells[i].Value, cells[i].cellTerrain);
        }

        for (int chunkIndex = 0; chunkIndex < chunkBuffer.Length; chunkIndex++)
        {
            ecbEnd.AddComponent<HexChunkRefreshRequest>(jobChunkIndex, chunkBuffer[chunkIndex].Value);
        }
        ecbEnd.RemoveComponent<HexMapGenerate>(jobChunkIndex, main);
        ecbEnd.RemoveComponent<HexChunkCellWrapper>(jobChunkIndex, main);
        ecbEnd.RemoveComponent<HexChunkCellDataCompleted>(jobChunkIndex, main);
    }

    private NativeList<int> GetErodibleCells(NativeArray<HexChunkCellWrapper> cells)
    {
        NativeList<int> erodibleCells = new(cells.Length / 2, Allocator.Temp);
        for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
        {
            HexChunkCellWrapper cell = cells[cellIndex];
            switch (IsErodible(cells, cell))
            {
                case true:
                    erodibleCells.Add(cell.Index);
                    break;
            }
        }
        return erodibleCells;
    }

    private void CreateRegions(ref Random randomNumberGenerator, NativeList<MapRegion> regions, HexGridBasic grid, HexMapGenerationSettings settings)
    {
        int borderX = grid.wrapping ? settings.regionBorder : settings.mapBoarderX;
        MapRegion region = new();
        switch (settings.regionCount)
        {
            case 2:
                if (randomNumberGenerator.NextFloat(0, 1f) < 0.5f)
                {
                    region.XMin = borderX;
                    region.YMax = grid.cellCountX / 2 - settings.regionBorder;
                    region.ZMin = settings.mapBorderZ;
                    region.ZMax = grid.cellCountZ - settings.mapBorderZ;
                    regions.Add(region);
                    region.XMin = grid.cellCountX / 2 + settings.regionBorder;
                    region.YMax = grid.cellCountX - borderX;
                    regions.Add(region);
                }
                else
                {
                    if (grid.wrapping)
                    {
                        borderX = 0;
                    }
                    region.XMin = borderX;
                    region.YMax = grid.cellCountX - borderX;
                    region.ZMin = settings.mapBorderZ;
                    region.ZMax = grid.cellCountZ / 2 - settings.regionBorder;
                    regions.Add(region);
                    region.ZMin = grid.cellCountZ / 2 + settings.regionBorder;
                    region.ZMax = grid.cellCountZ - settings.mapBorderZ;
                    regions.Add(region);
                }
                break;
            case 3:
                region.XMin = borderX;
                region.YMax = grid.cellCountX / 3 - settings.regionBorder;
                region.ZMin = settings.mapBorderZ;
                region.ZMax = grid.cellCountZ - settings.mapBorderZ;
                regions.Add(region);
                region.XMin = grid.cellCountX / 3 + settings.regionBorder;
                region.YMax = grid.cellCountX * 2 / 3 - settings.regionBorder;
                regions.Add(region);
                region.XMin = grid.cellCountX * 2 / 3 + settings.regionBorder;
                region.YMax = grid.cellCountX - borderX;
                regions.Add(region);
                break;
            case 4:
                region.XMin = borderX;
                region.YMax = grid.cellCountX / 2 - settings.regionBorder;
                region.ZMin = settings.mapBorderZ;
                region.ZMax = grid.cellCountZ / 2 - settings.regionBorder;
                regions.Add(region);
                region.XMin = grid.cellCountX / 2 + settings.regionBorder;
                region.YMax = grid.cellCountX - borderX;
                regions.Add(region);
                region.ZMin = grid.cellCountZ / 2 + settings.regionBorder;
                region.ZMax = grid.cellCountZ - settings.mapBorderZ;
                regions.Add(region);
                region.XMin = borderX;
                region.YMax = grid.cellCountX / 2 - settings.regionBorder;
                regions.Add(region);
                break;

            default:
                if (grid.wrapping)
                {
                    borderX = 0;
                }
                region.XMin = borderX;
                region.YMax = grid.cellCountX - borderX;
                region.ZMin = settings.mapBorderZ;
                region.ZMax = grid.cellCountZ - settings.mapBorderZ;
                regions.Add(region);
                break;
        }
    }

    private NativeArray<ClimateData> CreateClimate(NativeArray<HexChunkCellWrapper> cells, NativeArray<ClimateData> climate, NativeArray<ClimateData> nextClimate, HexMapGenerationSettings settings)
    {
        ClimateData initialData = new()
        {
            Moisture = settings.startingMoisture
        };
        ClimateData clearData = new();

        for (int i = 0; i < cells.Length; i++)
        {
            climate[i] = initialData;
            nextClimate[i] = clearData;
        }

        for (int cycle = 0; cycle < 40; cycle++)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                HexChunkCellWrapper cell = cells[i];
                ClimateData cellClimate = climate[i];
                if (cell.cellTerrain.IsUnderwater)
                {
                    cellClimate.Moisture = 1f;
                    cellClimate.Clouds += settings.evaporationFactor;
                }
                else
                {
                    float evaporation = cellClimate.Moisture * settings.evaporationFactor;
                    cellClimate.Moisture -= evaporation;
                    cellClimate.Clouds += evaporation;
                }
                float precipitation = cellClimate.Clouds * settings.precipitationFactor;
                cellClimate.Clouds -= precipitation;
                cellClimate.Moisture += precipitation;
                float cloudMaximum = 1f - cell.cellTerrain.ViewElevation / (settings.elevationMaximum + 1f);
                if (cellClimate.Clouds > cloudMaximum)
                {
                    cellClimate.Moisture += cellClimate.Clouds - cloudMaximum;
                    cellClimate.Clouds = cloudMaximum;
                }
                HexDirection mainDispersalDirection = settings.windDirection.Opposite();
                float cloudDispersal = cellClimate.Clouds * (1f / (5f + settings.windStrength));
                float runoff = cellClimate.Moisture * settings.runoffFactor * (1f / 6f);
                float seepage = cellClimate.Moisture * settings.seepageFactor * (1f / 6f);
                for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                {
                    int neighbourIndex = cell.GetNeighbourIndex(d);

                    switch (neighbourIndex)
                    {
                        case >= 0:
                            HexChunkCellWrapper neighbour = cells[neighbourIndex];
                            ClimateData neighbourClimate = nextClimate[neighbour.Index];
                            if (d == mainDispersalDirection)
                            {
                                neighbourClimate.Clouds += cloudDispersal * settings.windStrength;
                            }
                            else
                            {
                                neighbourClimate.Clouds += cloudDispersal;
                            }

                            int elevationDelta = neighbour.cellTerrain.ViewElevation - cell.Elevation;
                            switch (elevationDelta)
                            {
                                case 0:
                                    cellClimate.Moisture -= seepage;
                                    neighbourClimate.Moisture += seepage;
                                    break;
                                case < 0:
                                    cellClimate.Moisture -= runoff;
                                    neighbourClimate.Moisture += runoff;
                                    break;
                            }

                            nextClimate[neighbour.Index] = neighbourClimate;
                            break;
                    }
                }

                ClimateData nextCellClimate = nextClimate[i];
                nextCellClimate.Moisture += cellClimate.Moisture;
                if (nextCellClimate.Moisture > 1f)
                {
                    nextCellClimate.Moisture = 1f;
                }
                nextClimate[i] = nextCellClimate;
                climate[i] = new ClimateData();
            }
            (nextClimate, climate) = (climate, nextClimate);
        }
        return climate;
    }

    #region CreateLand
    private int CreateLand(ref Random randomNumber, ref HexCellPriorityQueue searchFrontier, NativeArray<HexChunkCellWrapper> cells,
        NativeList<MapRegion> regions, HexGridBasic grid, HexMapGenerationSettings settings)
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
                    SinkTerrain(ref randomNumber, ref searchFrontier, ref landBudget, cells, chunkSize, region, settings, grid);
                }
                else
                {
                    RaiseTerrain(ref randomNumber, ref searchFrontier, ref landBudget, cells, chunkSize, region, settings, grid);
                    if (landBudget == 0)
                    {
                        settings.SearchPhase = searchFrontier.searchPhase;
                        return landCells;
                    }
                }
                chunkSize = randomNumber.NextInt(settings.chunkSizeMin, settings.chunkSizeMax - 1);
            }
        }
        switch (landBudget)
        {
            case > 0:
                landCells -= landBudget;
                break;
        }
        return landCells;
    }

    private int RaiseTerrain(ref Random randomNumber, ref HexCellPriorityQueue searchFrontier, ref int budget, NativeArray<HexChunkCellWrapper> cells,
        int chunkSize, MapRegion region, HexMapGenerationSettings settings, HexGridBasic grid)
    {
        searchFrontier.searchPhase += 1;
        int cellIndex = GetRandomCell(ref randomNumber, region, grid.cellCountX);
        HexChunkCellWrapper firstCell = cells[cellIndex];
        HexCellQueueElement firstCellElement = searchFrontier.elements[cellIndex];
        firstCellElement.SearchPhase = searchFrontier.searchPhase;
        firstCellElement.Distance = 0;
        firstCellElement.SearchHeuristic = 0;
        searchFrontier.elements[cellIndex] = firstCellElement;
        searchFrontier.Enqueue(firstCellElement);
        HexCoordinates centre = firstCell.cellBasic.Coorindate;

        int rise = randomNumber.NextFloat(0, 1) < settings.highRiseProbability ? 2 : 1;
        int size = 0;
        while (size < chunkSize && searchFrontier.Count > 0)
        {
            HexChunkCellWrapper current = cells[searchFrontier.DequeueIndex()];
            int originalElevation = current.Elevation;
            int newElevation = originalElevation + rise;

            if (newElevation > settings.elevationMaximum)
            {
                continue;
            }
            current.cellTerrain.elevation = newElevation;
            current.cellBasic.RefreshPosition(noiseColours, newElevation, grid.wrapSize);
            current.ValidateRivers(cells);
            if (originalElevation < settings.waterLevel && newElevation >= settings.waterLevel && --budget == 0)
            {
                break;
            }

            size += 1;

            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                int neighbourIndex = current.cellNeighbours.GetNeighbourIndex(d);
                switch (neighbourIndex)
                {
                    case >= 0:
                        HexCellQueueElement neighbourElement = searchFrontier.elements[neighbourIndex];
                        HexCoordinates neighbour = cells[neighbourIndex].cellBasic.Coorindate;
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
        return budget;
    }

    private void SinkTerrain(ref Random randomNumber, ref HexCellPriorityQueue searchFrontier, ref int budget,
        NativeArray<HexChunkCellWrapper> cells, int chunkSize, MapRegion region,
        HexMapGenerationSettings settings, HexGridBasic grid)
    {
        searchFrontier.searchPhase += 1;
        int cellIndex = GetRandomCell(ref randomNumber, region, grid.cellCountX);
        HexChunkCellWrapper firstCell = cells[cellIndex];
        HexCellQueueElement firstCellElement = searchFrontier.elements[cellIndex];
        firstCellElement.SearchPhase = searchFrontier.searchPhase;
        firstCellElement.Distance = 0;
        firstCellElement.SearchHeuristic = 0;
        searchFrontier.elements[cellIndex] = firstCellElement;
        searchFrontier.Enqueue(firstCellElement);
        HexCoordinates centre = firstCell.cellBasic.Coorindate;

        int sink = randomNumber.NextFloat(0, 1) < settings.highRiseProbability ? 2 : 1;
        int size = 0;
        while (size < chunkSize && searchFrontier.Count > 0)
        {
            HexChunkCellWrapper current = cells[searchFrontier.DequeueIndex()];
            int originalElevation = current.Elevation;
            int newElevation = originalElevation - sink;

            if (newElevation < settings.elevationMaximum)
            {
                continue;
            }

            current.cellTerrain.elevation = newElevation;
            current.cellBasic.RefreshPosition(noiseColours, newElevation, grid.wrapSize);
            current.ValidateRivers(cells);
            switch (originalElevation >= settings.waterLevel && newElevation < settings.waterLevel)
            {
                case true:
                    budget += 1;
                    break;
            }

            size += 1;

            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                int neighbourIndex = current.cellNeighbours.GetNeighbourIndex(d);
                switch (neighbourIndex)
                {
                    case >= 0:
                        HexCellQueueElement neighbourElement = searchFrontier.elements[neighbourIndex];
                        HexCoordinates neighbour = cells[neighbourIndex].cellBasic.Coorindate;
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
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetRandomCell(ref Random randomNumber, MapRegion region, int cellCountX)
        => randomNumber.NextInt(region.XMin, region.YMax) + randomNumber.NextInt(region.ZMin, region.ZMax) * cellCountX;
    #endregion

    #region ErodeLand
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsErodible(NativeArray<HexChunkCellWrapper> cells, HexChunkCellWrapper cell)
    {
        int erodibleElevation = cell.Elevation - 2;
        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
        {
            int neighbourIndex = cell.GetNeighbourIndex(d);

            if (neighbourIndex >= 0)
            {
                HexChunkCellWrapper neighbour = cells[neighbourIndex];
                if (neighbour.Elevation <= erodibleElevation)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void ErodeLand(ref Random randomNumber, NativeArray<HexChunkCellWrapper> cells, NativeList<int> erodibleCells, int erosionPercentage, int wrapSize)
    {
        NativeList<int> candidates = new(6, Allocator.Temp);

        int targetErodibleCount = (int)(erodibleCells.Length * (100 - erosionPercentage) * 0.01f);
        while (erodibleCells.Length > targetErodibleCount)
        {
            int index = randomNumber.NextInt(0, erodibleCells.Length);

            HexChunkCellWrapper cell = cells[erodibleCells[index]];
            int targetCellIndex = GetErosionTarget(ref randomNumber, cells, cell, candidates);
            if (targetCellIndex == int.MinValue)
            {
                if (!IsErodible(cells, cell))
                {
                    erodibleCells.RemoveAt(index);
                }
                continue;
            }
            cell.cellTerrain.elevation -= 1;
            cell.cellBasic.RefreshPosition(noiseColours, cell.cellTerrain.elevation, wrapSize);
            cell.ValidateRivers(cells);
            HexChunkCellWrapper targetCell = cells[targetCellIndex];
            targetCell.cellTerrain.elevation += 1;
            targetCell.cellBasic.RefreshPosition(noiseColours, targetCell.cellTerrain.elevation, wrapSize);
            targetCell.ValidateRivers(cells);
            cell = cells[cell.Index];
            if (!IsErodible(cells, cell))
            {
                erodibleCells[index] = erodibleCells[^1];
                erodibleCells.RemoveAt(erodibleCells.Length - 1);
            }

            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                int neighbourIndex = cell.GetNeighbourIndex(d);

                if (neighbourIndex >= 0)
                {
                    HexChunkCellWrapper neighbour = cells[neighbourIndex];
                    if (neighbour.Elevation == cell.Elevation + 2 && !erodibleCells.Contains(neighbour.Index))
                    {
                        erodibleCells.Add(neighbour.Index);
                    }
                }
            }

            if (IsErodible(cells, targetCell) && !erodibleCells.Contains(targetCellIndex))
            {
                erodibleCells.Add(targetCellIndex);
            }

            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                int neighbourIndex = cell.GetNeighbourIndex(d);
                if (neighbourIndex >= 0)
                {
                    HexChunkCellWrapper neighbour = cells[neighbourIndex];

                    if (neighbour.Index != cell.Index && neighbour.Elevation == targetCell.Elevation + 1 && !IsErodible(cells, neighbour))
                    {
                        for (int i = 0; i < erodibleCells.Length; i++)
                        {
                            if (erodibleCells[i] == neighbour.Index)
                            {
                                erodibleCells.RemoveAt(i);
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    private int GetErosionTarget(ref Random randomNumber, NativeArray<HexChunkCellWrapper> cells, HexChunkCellWrapper cell, NativeList<int> candidates)
    {
        candidates.Clear();
        int erodibleElevation = cell.Elevation - 2;
        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
        {
            int neighbourIndex = cell.GetNeighbourIndex(d);
            if (neighbourIndex >= 0)
            {
                HexChunkCellWrapper neighbour = cells[neighbourIndex];

                if (neighbour.Elevation <= erodibleElevation)
                {
                    candidates.Add(neighbour.Index);
                }
            }
        }
        if (candidates.Length == 0)
        {
            return int.MinValue;
        }
        return candidates[randomNumber.NextInt(0, candidates.Length)];
    }
    #endregion

    #region CreateRivers
    private void CreateRivers(ref Random randomNumber, NativeArray<HexChunkCellWrapper> cells, NativeArray<ClimateData> climate, HexMapGenerationSettings settings, int landCells, int wrapSize)
    {
        NativeList<int> riverOrigins = new(cells.Length * 2, Allocator.Temp);
        int riverBudget = (int)math.round(landCells * settings.riverPercentage * 0.01f);
        riverOrigins.Add(riverBudget);
        for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
        {
            HexChunkCellWrapper cell = cells[cellIndex];
            switch (cell.cellTerrain.IsUnderwater)
            {
                case true:
                    continue;
            }

            ClimateData data = climate[cellIndex];
            float weight = data.Moisture * (cell.Elevation - settings.waterLevel) / (settings.elevationMaximum - settings.waterLevel);

            if (weight > 0.75f)
            {
                riverOrigins.Add(cell.Index);
                riverOrigins.Add(cell.Index);
            }
            if (weight > 0.5f)
            {
                riverOrigins.Add(cell.Index);
            }
            if (weight > 0.25f)
            {
                riverOrigins.Add(cell.Index);
            }
        }
        riverOrigins.TrimExcess();
        NativeList<HexDirection> flowDirections = new(Allocator.Temp);
        while (riverBudget > 0 && riverOrigins.Length > 1)
        {
            int index = randomNumber.NextInt(1, riverOrigins.Length);
            int lastIndex = riverOrigins.Length - 1;
            HexChunkCellWrapper origin = cells[riverOrigins[index]];
            riverOrigins[index] = riverOrigins[lastIndex];
            riverOrigins.RemoveAt(lastIndex);
            if (!origin.cellTerrain.HasRiver)
            {
                bool isValidOrigin = true;
                for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                {
                    int neighbourIndex = origin.GetNeighbourIndex(d);
                    switch (neighbourIndex)
                    {
                        case > 0:
                            HexChunkCellWrapper neighbour = cells[neighbourIndex];
                            switch (neighbour.cellTerrain.HasRiver || neighbour.cellTerrain.IsUnderwater)
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
                    int toMinus = CreateRiver(ref randomNumber, cells, flowDirections, origin.Index, settings.extraLakeProbability, wrapSize);
                    riverBudget -= toMinus;
                }
            }
        }
        riverOrigins[0] = riverBudget;
    }
    private int CreateRiver(ref Random randomNumber, NativeArray<HexChunkCellWrapper> cells, NativeList<HexDirection> flowDirections, int originIndex, float extraLakeProbability, int wrapSize)
    {
        int length = 1;
        HexChunkCellWrapper cell = cells[originIndex];
        HexDirection direction = HexDirection.NE;
        while (!cell.cellTerrain.IsUnderwater)
        {
            int minNeighbourElevation = int.MaxValue;
            flowDirections.Clear();
            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                int neighbourIndex = cell.GetNeighbourIndex(d);
                switch (neighbourIndex)
                {
                    case > 0:
                        HexChunkCellWrapper neighbour = cells[neighbourIndex];
                        switch (neighbour.Elevation < minNeighbourElevation)
                        {
                            case true:
                                minNeighbourElevation = neighbour.Elevation;
                                break;
                        }
                        switch (neighbour.Index == originIndex || neighbour.cellTerrain.hasIncomingRiver)
                        {
                            case false:
                                int delta = neighbour.Elevation - cell.Elevation;
                                switch (delta > 0)
                                {
                                    case false:
                                        switch (neighbour.cellTerrain.hasOutgoingRiver)
                                        {
                                            case true:
                                                cell.SetOutgoingRiver(cells, direction);
                                                return length;
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
                switch (length)
                {
                    case 1:
                        cells[cell.Index] = cell;
                        return 0;
                }
                switch (minNeighbourElevation >= cell.Elevation)
                {
                    case true:
                        cell.cellTerrain.waterLevel = minNeighbourElevation;
                        cell.ValidateRivers(cells);
                        switch (minNeighbourElevation == cell.Elevation)
                        {
                            case true:
                                cell.cellTerrain.elevation = minNeighbourElevation - 1;
                                cell.cellBasic.RefreshPosition(noiseColours, minNeighbourElevation - 1, wrapSize);
                                cell.ValidateRivers(cells);
                                break;
                        }
                        break;
                }
                break;
            }
            direction = flowDirections[randomNumber.NextInt(0, flowDirections.Length)];
            cell.SetOutgoingRiver(cells, direction);
            length += 1;

            switch (minNeighbourElevation >= cell.Elevation && randomNumber.NextFloat(0, 1f) < extraLakeProbability)
            {
                case true:
                    cell.cellTerrain.waterLevel = cell.Elevation;
                    cell.ValidateRivers(cells);
                    cell.cellTerrain.elevation -= 1;
                    cell.cellBasic.RefreshPosition(noiseColours, cell.cellTerrain.elevation, wrapSize);
                    cell.ValidateRivers(cells);
                    break;
            }
            cells[cell.Index] = cell;
            cell = cell.GetNeighbour(cells, direction);
        }
        return length;
    }
    #endregion

    #region SetTerrian
    private void SetTerrianType(ref Random randomNumberGenerator, NativeArray<HexChunkCellWrapper> cells, NativeArray<ClimateData> climate, HexGridBasic grid, HexMapGenerationSettings settings)
    {
        int temperatureJitterChannel = randomNumberGenerator.NextInt(0, 4);
        int rockDesertElevation = settings.elevationMaximum - (settings.elevationMaximum - settings.waterLevel) / 2;
        for (int cellIndex = 0; cellIndex < cells.Length; cellIndex++)
        {
            HexChunkCellWrapper cell = cells[cellIndex];

            float temperature = DetermineTemperature(cell, grid, settings, temperatureJitterChannel);
            float moisture = climate[cellIndex].Moisture;
            if (!cell.cellTerrain.IsUnderwater)
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
                else if (cellBiome.plant < 3 && cell.cellTerrain.HasRiver)
                {
                    cellBiome.plant += 1;
                }
                cell.cellTerrain.terrainTypeIndex = cellBiome.terrian;
                cell.cellTerrain.plantLevel = cellBiome.plant;
            }
            else
            {
                int terrain;
                if (cell.Elevation == settings.waterLevel - 1)
                {
                    int cliffs = 0, slopes = 0;
                    for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                    {
                        int neighbourIndex = cell.GetNeighbourIndex(d);
                        switch (neighbourIndex)
                        {
                            case >= 0:
                                HexChunkCellWrapper neighbour = cells[neighbourIndex];
                                int delta = neighbour.Elevation - cell.cellTerrain.waterLevel;
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
                cell.cellTerrain.terrainTypeIndex = terrain;
            }
            cells[cellIndex] = cell;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float DetermineTemperature(HexChunkCellWrapper cell, HexGridBasic grid, HexMapGenerationSettings settings, int temperatureJitterChannel)
    {
        float latitude = (float)cell.cellBasic.Coorindate.Z / grid.cellCountZ;
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
        temperature *= 1f - (cell.cellTerrain.ViewElevation - settings.waterLevel) / (settings.elevationMaximum - settings.waterLevel + 1f);
        float jitter = HexMetrics.SampleNoise(noiseColours, cell.Position * 0.1f, grid.wrapSize)[temperatureJitterChannel];
        temperature += (jitter * 2f - 1f) * settings.temperatureJitter;
        return temperature;
    }
    #endregion
}