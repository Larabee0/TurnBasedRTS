using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using static DOTSHexagons.UpdateColliderSystem;

namespace DOTSHexagons
{
    public class HexGridChunkSystem : JobComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem commandBufferSystemEnd;
        private BeginSimulationEntityCommandBufferSystem commandBufferSystemBegin;

        public static Entity HexGridChunkPrefab;
        // linked entity group index 1 is the chunkFeatureContainer entity.
        // linked entity group index 9+ are containerBuffer Entities.
        protected override void OnCreate()
        {
            commandBufferSystemEnd = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            commandBufferSystemBegin = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();

            HexGridChunkPrefab = EntityManager.CreateEntity(typeof(Translation), typeof(LocalToWorld), typeof(LocalToParent), typeof(Child), typeof(LinkedEntityGroup), typeof(Parent), typeof(HexGridChunkComponent), typeof(HexGridCellBuffer),typeof(Prefab));
            //EntityManager.SetName(HexGridChunkPrefab, "HexGridChunk");
            EntityManager.GetBuffer<LinkedEntityGroup>(HexGridChunkPrefab).Add(HexGridChunkPrefab);

            EntityArchetype chunkRendererArch = EntityManager.CreateArchetype(typeof(Translation), typeof(LocalToWorld), typeof(LocalToParent), typeof(Parent), typeof(Prefab));
            EntityArchetype containerBufferArch = EntityManager.CreateArchetype(typeof(Feature), typeof(FeatureDataContainer), typeof(PossibleFeaturePosition), typeof(Parent), typeof(Child), typeof(Translation), typeof(LocalToWorld), typeof(LocalToParent), typeof(Prefab));

            Entity chunkFeatureContainer = EntityManager.CreateEntity(typeof(CellFeature), typeof(CellContainer), typeof(Translation), typeof(LocalToWorld), typeof(LocalToParent), typeof(Parent), typeof(Child), typeof(FeatureContainer), typeof(Prefab));
            //EntityManager.SetName(chunkFeatureContainer, "chunkFeatureContainer");
            EntityManager.SetComponentData(chunkFeatureContainer, new Parent { Value = HexGridChunkPrefab });
            HexGridChunkComponent data = EntityManager.GetComponentData<HexGridChunkComponent>(HexGridChunkPrefab);
            data.FeatureContainer = chunkFeatureContainer;
            EntityManager.GetBuffer<LinkedEntityGroup>(HexGridChunkPrefab).Add(chunkFeatureContainer);


            Entity mesh = EntityManager.CreateEntity(chunkRendererArch);
            //EntityManager.SetName(mesh, "TerrianMesh");
            EntityManager.AddBuffer<Float3ForCollider>(mesh);
            EntityManager.AddBuffer<UintForCollider>(mesh);
            NativeArray<float3> colliderVerts = new NativeArray<float3>(new float3[] { new float3(1), new float3(0), new float3(1, 0, 0) }, Allocator.Temp);
            NativeArray<int3> colliderTris = new NativeArray<int3>(1, Allocator.Temp);
            colliderTris[0] = new int3(0, 1, 2);
            PhysicsCollider collider = new PhysicsCollider
            {
                Value = MeshCollider.Create(colliderVerts, colliderTris)
            };
            colliderVerts.Dispose();
            colliderTris.Dispose();
            EntityManager.AddComponentData(mesh, collider);
            EntityManager.SetComponentData(mesh, new Parent { Value = HexGridChunkPrefab });
            data.entityTerrian = mesh;
            EntityManager.GetBuffer<LinkedEntityGroup>(HexGridChunkPrefab).Add(mesh);

            mesh = EntityManager.CreateEntity(chunkRendererArch);
            //EntityManager.SetName(mesh, "RiverMesh");
            EntityManager.SetComponentData(mesh, new Parent { Value = HexGridChunkPrefab });
            data.entityRiver = mesh;
            EntityManager.GetBuffer<LinkedEntityGroup>(HexGridChunkPrefab).Add(mesh);

            mesh = EntityManager.CreateEntity(chunkRendererArch);
            //EntityManager.SetName(mesh, "WaterMesh");
            EntityManager.SetComponentData(mesh, new Parent { Value = HexGridChunkPrefab });
            data.entityWater = mesh;
            EntityManager.GetBuffer<LinkedEntityGroup>(HexGridChunkPrefab).Add(mesh);

            mesh = EntityManager.CreateEntity(chunkRendererArch);
            //EntityManager.SetName(mesh, "ShoreMesh");
            EntityManager.SetComponentData(mesh, new Parent { Value = HexGridChunkPrefab });
            data.entityWaterShore = mesh;
            EntityManager.GetBuffer<LinkedEntityGroup>(HexGridChunkPrefab).Add(mesh);

            mesh = EntityManager.CreateEntity(chunkRendererArch);
            //EntityManager.SetName(mesh, "EstuaryMesh");
            EntityManager.SetComponentData(mesh, new Parent { Value = HexGridChunkPrefab });
            data.entityEstuaries = mesh;
            EntityManager.GetBuffer<LinkedEntityGroup>(HexGridChunkPrefab).Add(mesh);

            mesh = EntityManager.CreateEntity(chunkRendererArch);
            //EntityManager.SetName(mesh, "RoadMesh");
            EntityManager.SetComponentData(mesh, new Parent { Value = HexGridChunkPrefab });
            data.entityRoads = mesh;
            EntityManager.GetBuffer<LinkedEntityGroup>(HexGridChunkPrefab).Add(mesh);

            mesh = EntityManager.CreateEntity(chunkRendererArch);
            //EntityManager.SetName(mesh, "WallMesh");
            EntityManager.SetComponentData(mesh, new Parent { Value = HexGridChunkPrefab });
            data.entityWalls = mesh;
            EntityManager.GetBuffer<LinkedEntityGroup>(HexGridChunkPrefab).Add(mesh);

            EntityManager.SetComponentData(HexGridChunkPrefab, data);

            int cellsPerChunk = HexMetrics.chunkSizeX * HexMetrics.chunkSizeZ;
            for (int cPCi = 0; cPCi < cellsPerChunk; cPCi++)
            {
                Entity feautreBuffer = EntityManager.CreateEntity(containerBufferArch);
                //EntityManager.SetName(feautreBuffer, "containerBuffer" + cPCi );
                EntityManager.SetComponentData(feautreBuffer, new Parent { Value = data.FeatureContainer });
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

    public struct HexGridChunkInitialisationComponent : IComponentData
    {
        public int chunkIndex;
        public Entity gridEntity;
    }
    public struct HexGridChunkComponent : IComponentData
    {
        public int chunkIndex;

        public Entity entityTerrian;
        public Entity entityRiver;
        public Entity entityWater;
        public Entity entityWaterShore;
        public Entity entityEstuaries;
        public Entity entityRoads;
        public Entity entityWalls;

        public float3 Position;
        public Entity gridEntity;
        public Entity FeatureContainer;
    }

    public struct HexGridChunkBuffer : IBufferElementData
    {
        public Entity ChunkEntity;
    }

    public struct HexGridCellBuffer : IBufferElementData
    {
        public int cellIndex;
        public int featureCount;
        public int towerCount;
        public bool hasSpecialFeature;
        public bool HasFeature { get { return featureCount > 0; } }
        public bool HasAnyFeature { get { return featureCount > 0 || towerCount > 0 || hasSpecialFeature; } }
        public bool HasSpecialFeature { get { return hasSpecialFeature; } }
        public bool HasTowers { get {return towerCount > 0; } }
    }

    public struct RefreshChunk : IComponentData { }
}