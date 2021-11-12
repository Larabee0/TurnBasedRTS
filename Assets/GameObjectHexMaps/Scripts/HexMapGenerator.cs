using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameObjectHexagons
{
    public class HexMapGenerator : MonoBehaviour
    {
        private List<MapRegion> regions;
        private List<ClimateData> climate = new List<ClimateData>();
        private List<ClimateData> nextClimate = new List<ClimateData>();
        private List<HexDirection> flowDirections = new List<HexDirection>();
        private int cellCount, landCells;
        private int searchFrontierPhase;
        private int temperatureJitterChannel;
        private HexCellPriorityQueue searchFrontier;

        static float[] temperatureBands = { 0.1f, 0.3f, 0.6f };
        static float[] moistureBands = { 0.12f, 0.28f, 0.85f };

        static Biome[] biomes =
        {
            new Biome(0,0), new Biome(4,0), new Biome(4,0), new Biome(4,0),
            new Biome(0,0), new Biome(2,0), new Biome(2,1), new Biome(2,2),
            new Biome(0,0), new Biome(1,0), new Biome(1,1), new Biome(1,2),
            new Biome(0,0), new Biome(1,1), new Biome(1,2), new Biome(1,3)
        };

        public HexGrid grid;
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
        [Range(0,0.4f)]
        public float sinkProbability = 0.2f;
        [Range(-4,0)]
        public int elevationMinimum = -2;
        [Range(6,10)]
        public int elevationMaximum = 8;
        [Range(0,10)]
        public int mapBoarderX = 5;
        [Range(0, 10)]
        public int mapBorderZ = 5;
        [Range(0, 10)]
        public int regionBorder = 5;
        [Range(1, 4)]
        public int regionCount = 1;
        [Range(0, 100)]
        public int erosionPercentage = 50;
        [Range(0f,1f)]
        public float evaporationFactor = 0.5f;
        [Range(0f, 1f)]
        public float precipitationFactor = 0.25f;
        [Range(0f, 1f)]
        public float runoffFactor = 0.25f;
        [Range(0f, 1f)]
        public float seepageFactor = 0.125f;
        public HexDirection windDirectipn = HexDirection.NW;
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


        public void GenerateMap(int x, int z, bool wrapping)
        {
            float totalTotal = Time.realtimeSinceStartup;

            Random.State originalRandomState = Random.state;
            if (!useFixedSeed)
            {
                seed = Random.Range(0, int.MaxValue);
                seed ^= (int)System.DateTime.Now.Ticks;
                seed ^= (int)Time.unscaledTime;
                seed &= int.MaxValue;
            }
            Random.InitState(seed);

            cellCount = x * z;
            grid.CreateMap(x, z, wrapping);
            float totalTime = Time.realtimeSinceStartup;
            if (searchFrontier == null)
            {
                searchFrontier = new HexCellPriorityQueue();
            }
            for (int i = 0; i < cellCount; i++)
            {
                grid.GetCell(i).WaterLevel = waterLevel;
            }

            float start = Time.realtimeSinceStartup;
            CreateRegions();
            Debug.Log("CreateRegions Time " + (Time.realtimeSinceStartup - start) * 1000f + "ms");
            start = Time.realtimeSinceStartup;
            CreateLand();
            Debug.Log("CreateLand Time " + (Time.realtimeSinceStartup - start) * 1000f + "ms");
            start = Time.realtimeSinceStartup;
            ErodeLand();
            Debug.Log("ErodeLand Time " + (Time.realtimeSinceStartup - start) * 1000f + "ms");
            start = Time.realtimeSinceStartup;
            CreateClimate();
            Debug.Log("CreateClimate Time " + (Time.realtimeSinceStartup - start) * 1000f + "ms");
            start = Time.realtimeSinceStartup;
            CreateRivers();
            Debug.Log("CreateRivers Time " + (Time.realtimeSinceStartup - start) * 1000f + "ms");
            start = Time.realtimeSinceStartup;
            SetTerrianType();
            Debug.Log("SetTerrianType Time " + (Time.realtimeSinceStartup - start) * 1000f + "ms");

            for (int i = 0; i < cellCount; i++)
            {
                grid.GetCell(i).SearchPhase = 0;
            }

            Random.state = originalRandomState;
            Debug.Log("Internal Generation Time " + (Time.realtimeSinceStartup - totalTime) * 1000f + "ms");
            Debug.Log("Total Generation Time " + (Time.realtimeSinceStartup - totalTotal) * 1000f + "ms");
        }
        
        private void CreateRivers()
        {
            List<HexCell> riverOrigins = ListPool<HexCell>.Get();
            for (int i = 0; i < cellCount; i++)
            {
                HexCell cell = grid.GetCell(i);
                if (cell.IsUnderwater)
                {
                    continue;
                }
                ClimateData data = climate[i];
                float weight = data.moisture * (cell.Elevation - waterLevel) / (elevationMaximum - waterLevel);

                if (weight > 0.75f)
                {
                    riverOrigins.Add(cell);
                    riverOrigins.Add(cell);
                }
                else if (weight > 0.5f)
                {
                    riverOrigins.Add(cell);
                }
                else if (weight > 0.25f)
                {
                    riverOrigins.Add(cell);
                }
            }

            int riverBudget = Mathf.RoundToInt(landCells * riverPercentage * 0.01f);
            while (riverBudget > 0 && riverOrigins.Count > 0)
            {
                
                int index = Random.Range(0, riverOrigins.Count);
                int lastIndex = riverOrigins.Count - 1;
                HexCell origin = riverOrigins[index];
                riverOrigins[index] = riverOrigins[lastIndex];
                riverOrigins.RemoveAt(lastIndex);

                if (!origin.HasRiver)
                {
                    bool isValidOrigin = true;
                    for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                    {
                        HexCell neighbour = origin.GetNeighbour(d);
                        if (neighbour && (neighbour.HasRiver || neighbour.IsUnderwater))
                        {
                            isValidOrigin = false;
                            break;
                        }
                    }
                    if (isValidOrigin)
                    {
                        riverBudget -= CreateRiver(origin);
                    }
                }
            }
            if (riverBudget > 0)
            {
                Debug.LogWarning("Failed to use up river budget");
            }
            ListPool<HexCell>.Add(riverOrigins);
        }

        private void CreateClimate()
        {
            climate.Clear();
            nextClimate.Clear();
            ClimateData initialData = new ClimateData();
            initialData.moisture = startingMoisture;
            ClimateData clearData = new ClimateData();
            for (int i = 0; i < cellCount; i++)
            {
                climate.Add(initialData);
                nextClimate.Add(clearData);
            }

            for (int cycle = 0; cycle < 40; cycle++)
            {
                for (int i = 0; i < cellCount; i++)
                {
                    EvolveClimate(i);
                }
                List<ClimateData> swap = climate;
                climate = nextClimate;
                nextClimate = swap;
            }
        }

        private void ErodeLand()
        {
            List<HexCell> erodibleCells = ListPool<HexCell>.Get();
            for (int i = 0; i < cellCount; i++)
            {
                HexCell cell = grid.GetCell(i);
                if (IsErodible(cell))
                {
                    erodibleCells.Add(cell);
                }
            }

            int targetErodibleCount = (int)(erodibleCells.Count * (100 - erosionPercentage) * 0.01f);
            while (erodibleCells.Count > targetErodibleCount)
            {
                int index = Random.Range(0, erodibleCells.Count);

                HexCell cell = erodibleCells[index];
                HexCell targetCell = GetErosionTarget(cell);
                cell.Elevation -= 1;
                targetCell.Elevation += 1;

                if (!IsErodible(cell))
                {
                    erodibleCells[index] = erodibleCells[erodibleCells.Count - 1];
                    erodibleCells.RemoveAt(erodibleCells.Count - 1);
                }

                for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                {
                    HexCell neighbour = cell.GetNeighbour(d);
                    if (neighbour && neighbour.Elevation == cell.Elevation + 2 && !erodibleCells.Contains(neighbour))
                    {
                        erodibleCells.Add(neighbour);
                    }
                }

                if(IsErodible(targetCell) && !erodibleCells.Contains(targetCell))
                {
                    erodibleCells.Add(targetCell);
                }

                for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                {
                    HexCell neighbour = targetCell.GetNeighbour(d);
                    if (neighbour && neighbour != cell && neighbour.Elevation == targetCell.Elevation + 1 && !IsErodible(neighbour))
                    {
                        erodibleCells.Remove(neighbour);
                    }
                }
            }
            ListPool<HexCell>.Add(erodibleCells);
        }

        private void CreateRegions()
        {
            if (regions == null)
            {
                regions = new List<MapRegion>();
            }
            else
            {
                regions.Clear();
            }

            int borderX = grid.wrapping ? regionBorder : mapBoarderX;
            MapRegion region;
            switch (regionCount)
            {
                default:
                    if (grid.wrapping)
                    {
                        borderX = 0;
                    }
                    region.xMin = borderX;
                    region.xMax = grid.cellCountX - borderX;
                    region.zMin = mapBorderZ;
                    region.zMax = grid.cellCountZ - mapBorderZ;
                    regions.Add(region);
                    break;
                case 2:
                    if (Random.value < 0.5f)
                    {
                        region.xMin = borderX;
                        region.xMax = grid.cellCountX / 2 - regionBorder;
                        region.zMin = mapBorderZ;
                        region.zMax = grid.cellCountZ - mapBorderZ;
                        regions.Add(region);
                        region.xMin = grid.cellCountX / 2 + regionBorder;
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
                        region.zMin = mapBorderZ;
                        region.zMax = grid.cellCountZ / 2 - regionBorder;
                        regions.Add(region);
                        region.zMin = grid.cellCountZ / 2 + regionBorder;
                        region.zMax = grid.cellCountZ - mapBorderZ;
                        regions.Add(region);
                    }
                    break;
                case 3:
                    region.xMin = borderX;
                    region.xMax = grid.cellCountX / 3 - regionBorder;
                    region.zMin = mapBorderZ;
                    region.zMax = grid.cellCountZ - mapBorderZ;
                    regions.Add(region);
                    region.xMin = grid.cellCountX / 3 + regionBorder;
                    region.xMax = grid.cellCountX * 2 / 3 - regionBorder;
                    regions.Add(region);
                    region.xMin = grid.cellCountX * 2 / 3 + regionBorder;
                    region.xMax = grid.cellCountX - borderX;
                    regions.Add(region);
                    break;
                case 4:
                    region.xMin = borderX;
                    region.xMax = grid.cellCountX / 2 - regionBorder;
                    region.zMin = mapBorderZ;
                    region.zMax = grid.cellCountZ / 2 - regionBorder;
                    regions.Add(region);
                    region.xMin = grid.cellCountX / 2 + regionBorder;
                    region.xMax = grid.cellCountX - borderX;
                    regions.Add(region);
                    region.zMin = grid.cellCountZ / 2 + regionBorder;
                    region.zMax = grid.cellCountZ - mapBorderZ;
                    regions.Add(region);
                    region.xMin = borderX;
                    region.xMax = grid.cellCountX / 2 - regionBorder;
                    regions.Add(region);
                    break;
            }
        }

        private void CreateLand()
        {
            int landBudget = Mathf.RoundToInt(cellCount * landPercentage * 0.01f);
            landCells = landBudget;
            for (int guard = 0; guard < 10000; guard++)
            {
                bool sink = Random.value < sinkProbability;
                int chunkSize = Random.Range(chunkSizeMin, chunkSizeMax - 1);
                for (int i = 0; i < regions.Count; i++)
                {
                    MapRegion region = regions[i];
                    if (sink)
                    {
                        landBudget = SinkTerrain(chunkSize, landBudget, region);
                    }
                    else
                    {
                        landBudget = RaiseTerrain(chunkSize, landBudget, region);
                        if (landBudget == 0)
                        {
                            return;
                        }
                    }
                    chunkSize = Random.Range(chunkSizeMin, chunkSizeMax - 1);
                }
            }

            if (landBudget > 0)
            {
                Debug.LogWarning("Failed to use up " + landBudget + " land budget.");
                landCells -= landBudget;
            }
        }

        private int RaiseTerrain(int chunkSize, int budget, MapRegion region)
        {
            searchFrontierPhase += 1;
            HexCell firstCell = GetRandomCell(region);
            firstCell.SearchPhase = searchFrontierPhase;
            firstCell.Distance = 0;
            firstCell.SearchHeuristic = 0;
            searchFrontier.Enqueue(firstCell);
            HexCoordinates centre = firstCell.coordinates;

            int rise = Random.value < highRiseProbability ? 2 : 1;
            int size = 0;
            while (size < chunkSize && searchFrontier.Count > 0)
            {
                HexCell current = searchFrontier.Dequeue();
                int originalElevation = current.Elevation;
                int newElevation = originalElevation + rise;

                if (newElevation > elevationMaximum)
                {
                    continue;
                }

                current.Elevation = newElevation;

                if (originalElevation < waterLevel && newElevation >= waterLevel && --budget == 0)
                {
                    break;
                }

                size += 1;

                for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                {
                    HexCell neighbour = current.GetNeighbour(d);
                    if (neighbour && neighbour.SearchPhase < searchFrontierPhase)
                    {
                        neighbour.SearchPhase = searchFrontierPhase;
                        neighbour.Distance = neighbour.coordinates.DistanceTo(centre);
                        neighbour.SearchHeuristic = Random.value < jitterProbability ? 1 : 0;
                        searchFrontier.Enqueue(neighbour);
                    }
                }
            }
            searchFrontier.Clear();
            return budget;
        }

        private int SinkTerrain(int chunkSize, int budget, MapRegion region)
        {
            searchFrontierPhase += 1;
            HexCell firstCell = GetRandomCell(region);
            firstCell.SearchPhase = searchFrontierPhase;
            firstCell.Distance = 0;
            firstCell.SearchHeuristic = 0;
            searchFrontier.Enqueue(firstCell);
            HexCoordinates centre = firstCell.coordinates;

            int sink = Random.value < highRiseProbability ? 2 : 1;
            int size = 0;
            while (size < chunkSize && searchFrontier.Count > 0)
            {
                HexCell current = searchFrontier.Dequeue();
                int originalElevation = current.Elevation;
                int newElevation = originalElevation - sink;

                if (newElevation < elevationMaximum)
                {
                    continue;
                }

                current.Elevation = newElevation;

                if (originalElevation >= waterLevel && newElevation < waterLevel)
                {
                    budget += 1;
                }

                size += 1;

                for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                {
                    HexCell neighbour = current.GetNeighbour(d);
                    if (neighbour && neighbour.SearchPhase < searchFrontierPhase)
                    {
                        neighbour.SearchPhase = searchFrontierPhase;
                        neighbour.Distance = neighbour.coordinates.DistanceTo(centre);
                        neighbour.SearchHeuristic = Random.value < jitterProbability ? 1 : 0;
                        searchFrontier.Enqueue(neighbour);
                    }
                }
            }
            searchFrontier.Clear();
            return budget;
        }

        private int CreateRiver(HexCell origin)
        {
            int length = 1;
            HexCell cell = origin;
            HexDirection direction = HexDirection.NE;
            while (!cell.IsUnderwater)
            {
                int minNeighbourElevation = int.MaxValue;
                flowDirections.Clear();
                for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                {
                    HexCell neighbour = cell.GetNeighbour(d);
                    if (!neighbour)
                    {
                        continue;
                    }

                    if (neighbour.Elevation < minNeighbourElevation)
                    {
                        minNeighbourElevation = neighbour.Elevation;
                    }

                    if (neighbour == origin || neighbour.HasIncomingRiver)
                    {
                        continue;
                    }

                    int delta = neighbour.Elevation - cell.Elevation;
                    if (delta > 0)
                    {
                        continue;
                    }

                    if (neighbour.HasOutgoingRiver)
                    {
                        cell.SetOutgoingRiver(d);
                        return length;
                    }

                    if (delta < 0)
                    {
                        flowDirections.Add(d);
                        flowDirections.Add(d);
                        flowDirections.Add(d);
                    }

                    if (length == 1 || (d != direction.Next2() && d != direction.Previous2()))
                    {
                        flowDirections.Add(d);
                    }
                    flowDirections.Add(d);
                }

                if (flowDirections.Count == 0)
                {
                    if (length == 1)
                    {
                        return 0;
                    }

                    if (minNeighbourElevation >= cell.Elevation)
                    {
                        cell.WaterLevel = minNeighbourElevation;
                        if (minNeighbourElevation == cell.Elevation)
                        {
                            cell.Elevation = minNeighbourElevation - 1;
                        }
                    }
                    break;
                }

                direction = flowDirections[Random.Range(0, flowDirections.Count)];
                cell.SetOutgoingRiver(direction);
                length += 1;

                if (minNeighbourElevation >= cell.Elevation && Random.value < extraLakeProbability)
                {
                    cell.WaterLevel = cell.Elevation;
                    cell.Elevation -= 1;
                }

                cell = cell.GetNeighbour(direction);
            }
            return length;
        }

        private float DetermineTemperature(HexCell cell)
        {
            float latitude = (float)cell.coordinates.Z / grid.cellCountZ;
            if (hemiSphereMode == HemiSphereMode.Both)
            {
                latitude *= 2f;
                if (latitude > 1f)
                {
                    latitude = 2f - latitude;
                }
            }
            else if(hemiSphereMode == HemiSphereMode.North)
            {
                latitude = 1f - latitude;
            }
            float temperature = Mathf.LerpUnclamped(lowTemperature, highTemperature, latitude);
            temperature *= 1f - (cell.ViewElevation - waterLevel) / (elevationMaximum - waterLevel + 1f);
            float jitter = HexMetrics.SampleNoise(cell.Position * 0.1f)[temperatureJitterChannel];
            temperature += (jitter * 2f - 1f) * temperatureJitter;
            return temperature;
        }

        private void SetTerrianType()
        {
            temperatureJitterChannel = Random.Range(0, 4);
            int rockDesertElevation = elevationMaximum - (elevationMaximum - waterLevel) / 2;
            for (int i = 0; i < cellCount; i++)
            {
                HexCell cell = grid.GetCell(i);
                float temperature = DetermineTemperature(cell);

                float moisture = climate[i].moisture;
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
                    else if (cell.Elevation == elevationMaximum)
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
                    cell.TerrianTypeIndex = cellBiome.terrian;
                    cell.PlantLevel = cellBiome.plant;
                }
                else
                {
                    int terrain;
                    if (cell.Elevation == waterLevel - 1)
                    {
                        int cliffs = 0, slopes = 0;
                        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
                        {
                            HexCell neighbour = cell.GetNeighbour(d);
                            if (!neighbour)
                            {
                                continue;
                            }
                            int delta = neighbour.Elevation - cell.WaterLevel;
                            if (delta == 0)
                            {
                                slopes += 1;
                            }
                            else if (delta > 0)
                            {
                                cliffs += 1;
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
                    else if (cell.Elevation >= waterLevel)
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
                    cell.TerrianTypeIndex = terrain;
                }
            }
        }

        private void EvolveClimate(int cellIndex)
        {
            HexCell cell = grid.GetCell(cellIndex);
            ClimateData cellClimate = climate[cellIndex];
            if (cell.IsUnderwater)
            {
                cellClimate.moisture = 1f;
                cellClimate.clouds += evaporationFactor;
            }
            else
            {
                float evaporation = cellClimate.moisture * evaporationFactor;
                cellClimate.moisture -= evaporation;
                cellClimate.clouds += evaporation;
            }
            float precipitation = cellClimate.clouds * precipitationFactor;
            cellClimate.clouds -= precipitation;
            cellClimate.moisture += precipitation;
            float cloudMaximum = 1f - cell.ViewElevation / (elevationMaximum + 1f);
            if (cellClimate.clouds > cloudMaximum)
            {
                cellClimate.moisture += cellClimate.clouds - cloudMaximum;
                cellClimate.clouds = cloudMaximum;
            }
            HexDirection mainDispersalDirection = windDirectipn.Opposite();
            float cloudDispersal = cellClimate.clouds * (1f / (5f + windStrength));
            float runoff = cellClimate.moisture * runoffFactor * (1f / 6f);
            float seepage = cellClimate.moisture * seepageFactor * (1f / 6f);
            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                HexCell neighbour = cell.GetNeighbour(d);
                if (!neighbour)
                {
                    continue;
                }
                ClimateData neighbourClimate = nextClimate[neighbour.Index];
                if (d == mainDispersalDirection)
                {
                    neighbourClimate.clouds += cloudDispersal * windStrength;
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
            }

            ClimateData nextCellClimate = nextClimate[cellIndex];
            nextCellClimate.moisture += cellClimate.moisture;
            if (nextCellClimate.moisture > 1f)
            {
                nextCellClimate.moisture = 1f;
            }
            nextClimate[cellIndex] = nextCellClimate;
            climate[cellIndex] = new ClimateData();
        }


        private HexCell GetRandomCell(MapRegion region)
        {
            return grid.GetCell(Random.Range(region.xMin, region.xMax), Random.Range(region.zMin, region.zMax));
        }

        private HexCell GetErosionTarget(HexCell cell)
        {
            List<HexCell> candidates = ListPool<HexCell>.Get();
            int erodibleElevation = cell.Elevation - 2;
            for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
            {
                HexCell neighbour = cell.GetNeighbour(d);
                if (neighbour && neighbour.Elevation <= erodibleElevation)
                {
                    candidates.Add(neighbour);
                }
            }
            HexCell target = candidates[Random.Range(0, candidates.Count)];
            ListPool<HexCell>.Add(candidates);
            return target;
        }

        private bool IsErodible(HexCell cell)
        {
            int erodibleElevation = cell.Elevation - 2;
            for (HexDirection d = 0; d <= HexDirection.NW; d++)
            {
                HexCell neighbour = cell.GetNeighbour(d);
                if (neighbour && neighbour.Elevation <= erodibleElevation)
                {
                    return true;
                }
            }
            return false;
        }

        private struct Biome
        {
            public int terrian,plant;
            
            public Biome (int terrian,int plant)
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