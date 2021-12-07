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
    public class InternalPrefabContainers : MonoBehaviour
    {
        [SerializeField] private HexGridChunk gridChunkPrefab;

        [SerializeField] private Texture2D noiseSource;
        public HexGridChunk GridChunkPrefab { get { return gridChunkPrefab; } }

        private void Awake()
        {
            HexFunctions.noiseSource = noiseSource;
            HexFunctions.SetNoiseColours();
        }

        private void OnDestroy()
        {
            HexFunctions.CleanUpNoiseColours();
        }
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
            EntityArchetype containerBufferArch = EntityManager.CreateArchetype(typeof(Feature), typeof(FeatureDataContainer), typeof(PossibleFeaturePosition), typeof(HexGridChild), typeof(HexGridParent), typeof(Prefab));

            Entity chunkFeatureContainer = EntityManager.CreateEntity(typeof(CellFeature), typeof(CellContainer), typeof(HexGridChild), typeof(HexGridParent), typeof(FeatureContainer), typeof(Prefab));
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

            int cellsPerChunk = HexFunctions.chunkSizeX * HexFunctions.chunkSizeZ;
            for (int cPCi = 0; cPCi < cellsPerChunk; cPCi++)
            {
                Entity feautreBuffer = EntityManager.CreateEntity(containerBufferArch);
                //EntityManager.SetName(feautreBuffer, "containerBuffer" + cPCi );
                EntityManager.SetComponentData(feautreBuffer, new HexGridParent { Value = data.FeatureContainer });
                EntityManager.SetComponentData(feautreBuffer, new FeatureDataContainer { containerEntity = data.FeatureContainer });
                EntityManager.GetBuffer<LinkedEntityGroup>(HexGridChunkPrefab).Add(feautreBuffer);
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            //EntityManager.Instantiate(HexGridChunkPrefab);
            this.Enabled = false;
            return inputDeps;
        }
    }

}