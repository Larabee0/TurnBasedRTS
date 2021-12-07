using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

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
}