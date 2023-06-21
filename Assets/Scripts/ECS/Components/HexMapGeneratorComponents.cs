using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// 110 bytes min
/// All the terrain generation settings used by the <see cref="HexMapGeneratorSystem"/>
/// </summary>
public struct HexMapGenerationSettings : IComponentData
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

/// <summary>
/// Tag to trigger <see cref="HexMapGeneratorSystem"/> to generate the map
/// </summary>
public struct HexMapGenerate : IComponentData { }

/// <summary>
/// Struct used during terrain generation to store various biomes
/// </summary>
public struct Biome
{
    public int terrian, plant;

    public Biome(int terrian, int plant)
    {
        this.terrian = terrian;
        this.plant = plant;
    }
}

/// <summary>
/// Struct used during terrain generation to denote map regions corners
/// </summary>
public struct MapRegion
{
    private int4 data;
    public int XMin { get => data.x; set => data.x = value; }
    public int YMax { get => data.y; set => data.y = value; }
    public int ZMin { get => data.z; set => data.z = value; }
    public int ZMax { get => data.w; set => data.w = value; }

    public MapRegion(int xMin, int xMax, int zMin, int zMax)
    {
        data = new int4(xMin, xMax, zMin, zMax);
    }
}

/// <summary>
/// Struct used during terrain generation to keep track water data during errions (rainfall not sea)
/// </summary>
public struct ClimateData
{
    private float2 data;
    public float Clouds { get => data.x; set => data.x = value; }
    public float Moisture { get => data.y; set => data.y = value; }
}