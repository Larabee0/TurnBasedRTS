using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace DOTSHexagonsV2
{
    [System.Serializable]
    public struct FeatureCollection
    {
        public Transform[] prefabs;
    }

    public class InternalPrefabContainers : MonoBehaviour
    {
        [SerializeField] private HexGridColumn gridColumnPrefab;
        [SerializeField] private HexGridChunk gridChunkPrefab;


        [SerializeField] private Transform wallTower;
        [SerializeField] private Transform bridge;
        [SerializeField] private Transform[] specialFeatures;
        [SerializeField] private FeatureCollection[] urbanCollections;
        [SerializeField] private FeatureCollection[] farmCollections;
        [SerializeField] private FeatureCollection[] plantCollections;


        [SerializeField] private Texture2D noiseSource;

        public HexGridChunk GridChunkPrefab { get { return gridChunkPrefab; } }
        public HexGridColumn GridColumnPrefab { get { return gridColumnPrefab; } }

        public Transform WallTower { get { return wallTower; } }
        public Transform Bridge { get { return bridge; } }
        public Transform[] SpecialFeatures { get { return specialFeatures; } }
        public FeatureCollection[] UrbanCollections { get { return urbanCollections; } }
        public FeatureCollection[] FarmCollections { get { return farmCollections; } }
        public FeatureCollection[] PlantCollections { get { return plantCollections; } }

        private FeatureDecisionSystem FDS;

        private void Awake()
        {
            HexFunctions.noiseSource = noiseSource;
            HexFunctions.SetNoiseColours();
            FDS = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<FeatureDecisionSystem>();
            FDS.internalPrefabs = this;
            FDS.CreateEntityWorldInfo();
        }

        private void OnDestroy()
        {
            HexFunctions.CleanUpNoiseColours();
        }

        public HexGridFeatureInfo InstinateFeature(CellFeature feature)
        {
            return feature.featureType switch
            {
                FeatureType.WallTower => Instantiate(WallTower).GetComponent<HexGridFeatureInfo>(),
                FeatureType.Bridge => Instantiate(Bridge).GetComponent<HexGridFeatureInfo>(),
                FeatureType.Special => Instantiate(GetSpecialFeature(feature.featureLevelIndex)).GetComponent<HexGridFeatureInfo>(),
                FeatureType.Urban => Instantiate(GetUrbanFeature(feature.featureLevelIndex, feature.featureSubIndex)).GetComponent<HexGridFeatureInfo>(),
                FeatureType.Farm => Instantiate(GetFarmFeature(feature.featureLevelIndex, feature.featureSubIndex)).GetComponent<HexGridFeatureInfo>(),
                FeatureType.Plant => Instantiate(GetPlantFeature(feature.featureLevelIndex, feature.featureSubIndex)).GetComponent<HexGridFeatureInfo>(),
                _ => null,
            };
        }

        public void DestroyFeature(HexGridFeatureInfo feature)
        {
            if (feature != null)
            {
                Destroy(feature.gameObject);
            }
        }

        public Transform GetSpecialFeature(int index) { return SpecialFeatures[index]; }
        public FeatureCollection GetUrbanCollection(int index) { return UrbanCollections[index]; }
        public FeatureCollection GetFarmCollection(int index) { return FarmCollections[index]; }
        public FeatureCollection GetPlantCollection(int index) { return PlantCollections[index]; }

        public Transform GetUrbanFeature(int collectionIndex,int index) { return GetUrbanCollection(collectionIndex).prefabs[index]; }
        public Transform GetFarmFeature(int collectionIndex, int index) { return GetFarmCollection(collectionIndex).prefabs[index]; }
        public Transform GetPlantFeature(int collectionIndex, int index) { return GetPlantCollection(collectionIndex).prefabs[index]; }
    }

    public class HexGridChunkSystem : JobComponentSystem
    {
        public static Entity HexGridChunkPrefab;
        // linked entity group index 1 is the chunkFeatureContainer entity.
        // linked entity group index 9+ are containerBuffer Entities.
        protected override void OnCreate()
        {
            HexGridChunkPrefab = EntityManager.CreateEntity(typeof(HexGridChild), typeof(LinkedEntityGroup), typeof(HexGridParent), typeof(HexGridChunkComponent), typeof(HexGridCellBuffer), typeof(Prefab));
            //EntityManager.SetName(HexGridChunkPrefab, "HexGridChunk");
            EntityManager.GetBuffer<LinkedEntityGroup>(HexGridChunkPrefab).Add(HexGridChunkPrefab);

            EntityArchetype chunkRendererArch = EntityManager.CreateArchetype(typeof(HexRenderer),typeof(HexGridParent), typeof(Prefab), typeof(HexGridVertex), typeof(HexGridTriangles));

            Entity chunkFeatureContainer = EntityManager.CreateEntity(typeof(CellFeature), typeof(PossibleFeaturePosition), typeof(HexGridParent), typeof(FeatureContainer), typeof(Prefab));
            //EntityManager.SetName(chunkFeatureContainer, "chunkFeatureContainer");
            EntityManager.SetComponentData(chunkFeatureContainer, new HexGridParent { Value = HexGridChunkPrefab });
            HexGridChunkComponent data = EntityManager.GetComponentData<HexGridChunkComponent>(HexGridChunkPrefab);
            data.FeatureContainer = chunkFeatureContainer;
            EntityManager.GetBuffer<LinkedEntityGroup>(HexGridChunkPrefab).Add(chunkFeatureContainer);


            Entity mesh = EntityManager.CreateEntity(chunkRendererArch);
            //EntityManager.SetName(mesh, "TerrianMesh");
            EntityManager.AddBuffer<HexGridIndices>(mesh);
            EntityManager.AddBuffer<HexGridWeights>(mesh);
            EntityManager.SetComponentData(mesh, new HexGridParent { Value = HexGridChunkPrefab });
            EntityManager.SetComponentData(mesh, new HexRenderer { ChunkIndex = int.MinValue, rendererID = RendererID.Terrian });
            data.entityTerrian = mesh;
            EntityManager.GetBuffer<LinkedEntityGroup>(HexGridChunkPrefab).Add(mesh);

            mesh = EntityManager.CreateEntity(chunkRendererArch);
            //EntityManager.SetName(mesh, "RiverMesh");
            EntityManager.AddBuffer<HexGridIndices>(mesh);
            EntityManager.AddBuffer<HexGridWeights>(mesh);
            EntityManager.AddBuffer<HexGridUV2>(mesh);
            EntityManager.SetComponentData(mesh, new HexGridParent { Value = HexGridChunkPrefab });
            EntityManager.SetComponentData(mesh, new HexRenderer { ChunkIndex = int.MinValue, rendererID = RendererID.River });
            data.entityRiver = mesh;
            EntityManager.GetBuffer<LinkedEntityGroup>(HexGridChunkPrefab).Add(mesh);

            mesh = EntityManager.CreateEntity(chunkRendererArch);
            //EntityManager.SetName(mesh, "WaterMesh");
            EntityManager.AddBuffer<HexGridIndices>(mesh);
            EntityManager.AddBuffer<HexGridWeights>(mesh);
            EntityManager.SetComponentData(mesh, new HexGridParent { Value = HexGridChunkPrefab });
            EntityManager.SetComponentData(mesh, new HexRenderer { ChunkIndex = int.MinValue, rendererID = RendererID.Water });
            data.entityWater = mesh;
            EntityManager.GetBuffer<LinkedEntityGroup>(HexGridChunkPrefab).Add(mesh);

            mesh = EntityManager.CreateEntity(chunkRendererArch);
            //EntityManager.SetName(mesh, "ShoreMesh");
            EntityManager.AddBuffer<HexGridIndices>(mesh);
            EntityManager.AddBuffer<HexGridWeights>(mesh);
            EntityManager.AddBuffer<HexGridUV2>(mesh);
            EntityManager.SetComponentData(mesh, new HexGridParent { Value = HexGridChunkPrefab });
            EntityManager.SetComponentData(mesh, new HexRenderer { ChunkIndex = int.MinValue, rendererID = RendererID.WaterShore });
            data.entityWaterShore = mesh;
            EntityManager.GetBuffer<LinkedEntityGroup>(HexGridChunkPrefab).Add(mesh);

            mesh = EntityManager.CreateEntity(chunkRendererArch);
            //EntityManager.SetName(mesh, "EstuaryMesh");
            EntityManager.AddBuffer<HexGridIndices>(mesh);
            EntityManager.AddBuffer<HexGridWeights>(mesh);
            EntityManager.AddBuffer<HexGridUV4>(mesh);
            EntityManager.SetComponentData(mesh, new HexGridParent { Value = HexGridChunkPrefab });
            EntityManager.SetComponentData(mesh, new HexRenderer { ChunkIndex = int.MinValue, rendererID = RendererID.Estuaries });
            data.entityEstuaries = mesh;
            EntityManager.GetBuffer<LinkedEntityGroup>(HexGridChunkPrefab).Add(mesh);

            mesh = EntityManager.CreateEntity(chunkRendererArch);
            //EntityManager.SetName(mesh, "RoadMesh");
            EntityManager.AddBuffer<HexGridIndices>(mesh);
            EntityManager.AddBuffer<HexGridWeights>(mesh);
            EntityManager.AddBuffer<HexGridUV2>(mesh);
            EntityManager.SetComponentData(mesh, new HexGridParent { Value = HexGridChunkPrefab });
            EntityManager.SetComponentData(mesh, new HexRenderer { ChunkIndex = int.MinValue, rendererID = RendererID.Roads });
            data.entityRoads = mesh;
            EntityManager.GetBuffer<LinkedEntityGroup>(HexGridChunkPrefab).Add(mesh);

            mesh = EntityManager.CreateEntity(chunkRendererArch);
            //EntityManager.SetName(mesh, "WallMesh");
            EntityManager.SetComponentData(mesh, new HexGridParent { Value = HexGridChunkPrefab });
            EntityManager.SetComponentData(mesh, new HexRenderer { ChunkIndex = int.MinValue, rendererID = RendererID.Walls });
            data.entityWalls = mesh;
            EntityManager.GetBuffer<LinkedEntityGroup>(HexGridChunkPrefab).Add(mesh);

            EntityManager.SetComponentData(HexGridChunkPrefab, data);
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            this.Enabled = false;
            return inputDeps;
        }
    }

}