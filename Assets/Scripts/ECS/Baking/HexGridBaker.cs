using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// WIP
/// Currently grids can only be created by having a gameobject with this component in the hierarchy prior to runtime
/// Contains all the settings for a grid and is responsible for intitialising the HashGrid for the HexGrid.
/// This needs to be moved to a system at some point when multi-map support is added.
/// 
/// Additionally the material refrences here need to be moved to <see cref="StaticVariableHandler"/>
/// This is mostly because Domain Reloading is disabled per the documentation <see cref="https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/getting-started-installation.html#domain-reload-setting"/>
/// so static variables are not reset
/// when you enter playmode.
/// </summary>
public class HexGridBaker : MonoBehaviour
{
    public int cellCountX = 20;
    public int cellCountZ = 15;

    public bool wrapping;


    public Material Terrain;
    public Material Rivers;
    public Material Water;
    public Material WaterShore;
    public Material Estuaries;
    public Material Roads;
    public Material Walls;


    public int chunkCountX;
    public int chunkCountZ;

    [Header("Generation Settings")]
    public bool generate;
    public bool useFixedSeed;
    [Min(1)]
    public uint seed = 1;
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

    /// <summary>
    /// Sets static data in <see cref="HexMetrics"/> static class.
    /// this should be moved somewhere else when this is turned into a prefab.
    /// </summary>
    public void SetData()
    {
        HexMetrics.InitializeHashGrid(seed);
        HexMetrics.Terrain = Terrain;
        HexMetrics.Rivers = Rivers;
        HexMetrics.Water = Water;
        HexMetrics.WaterShore = WaterShore;
        HexMetrics.Estuaries = Estuaries;
        HexMetrics.Roads = Roads;
        HexMetrics.Walls = Walls;

    }

    public HexMapGenerationSettings CreateGenerationData(uint seed)
    {
        return new HexMapGenerationSettings
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

    /// <summary>
    /// CreateMap should me moved out of here when the object becomes a prefab.
    /// For now though this validates the dimentions and sets the wrapSize for a new map.
    /// </summary>
    /// <param name="x">cell count in x axis</param>
    /// <param name="z">cell count in z axis</param>
    /// <param name="wrapping">should this map wrap around left to right?</param>
    /// <returns></returns>
    public bool CreateMap(int x, int z, bool wrapping)
    {
        HexMetrics.wrapSize = wrapping ? cellCountX : 0;
        if (x <= 0 || x % HexMetrics.chunkSizeX != 0 || z <= 0 || z % HexMetrics.chunkSizeZ != 0)
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

/// <summary>
/// HexGrid baker, bakes the above monobehaviour into an none-prefab entity (aka can appear in entity queries)
/// As with all bakers, this just adds teh required components for the HexGrid.
/// 
/// In this case extra components are added so <see cref="HexGridCreatorSystem"/> actually produces a hex map
/// </summary>
public class HexGridBaking : Baker<HexGridBaker>
{
    public override void Bake(HexGridBaker authoring)
    {
        authoring.SetData();
        authoring.CreateMap(authoring.cellCountX, authoring.cellCountZ, authoring.wrapping);
        Entity entity = GetEntity(TransformUsageFlags.Dynamic);

        AddBuffer<HexGridChunkBuffer>(entity);
        AddBuffer<HexGridColumnBuffer>(entity);
        AddBuffer<HexCellReference>(entity);

        AddComponent<HexGridUnInitialised>(entity);

        AddComponent(entity, new HexGridCreateChunks
        {
            columns = authoring.chunkCountX,
            chunks = authoring.chunkCountX * authoring.chunkCountZ
        });

        AddComponent(entity, new HexGridBasic
        {
            cellCountX = authoring.cellCountX,
            cellCountZ = authoring.cellCountZ,
            chunkCountX = authoring.chunkCountX,
            chunkCountZ = authoring.chunkCountZ,
            seed = authoring.seed,
            wrapping = authoring.wrapping,
            wrapSize = HexMetrics.wrapSize
        });

        if (authoring.generate)
        {
            uint seed = authoring.seed;
            if (!authoring.useFixedSeed)
            {
                seed = (uint)UnityEngine.Random.Range(0, int.MaxValue);
                seed ^= (uint)DateTime.Now.Ticks;
                seed ^= (uint)Time.unscaledTime;
                seed &= uint.MaxValue;
            }
            AddComponent(entity, authoring.CreateGenerationData(seed));
            AddComponent(entity, new HexMapGenerate());
        }
    }
}