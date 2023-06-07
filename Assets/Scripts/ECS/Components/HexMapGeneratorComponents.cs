using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;



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

public struct HexMapGenerate : IComponentData { }

public struct Biome
{
    public int terrian, plant;

    public Biome(int terrian, int plant)
    {
        this.terrian = terrian;
        this.plant = plant;
    }
}

public struct MapRegion
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

public struct ClimateData
{
    public float clouds, moisture;
}