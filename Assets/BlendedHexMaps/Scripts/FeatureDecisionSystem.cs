using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;

namespace DOTSHexagonsV2
{
    [UpdateInGroup(typeof(HexGridV2SystemGroup))]
    [UpdateBefore(typeof(HexGridMeshGenerator))]
    public class FeatureDecisionSystem : JobComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem commandBufferSystemEnd;
        private BeginSimulationEntityCommandBufferSystem commandBufferSystemBegin;

        public InternalPrefabContainers internalPrefabs;

        public bool EntityWorldInfoCreated = false;

        private EntityQuery RefreshCellFeaturesQuery;
        private EntityQuery GridRootQuery;
        private NativeHashMap<int,CollectionInfo> featureCollectionInformation;

        protected override void OnCreate()
        {
            commandBufferSystemEnd = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            commandBufferSystemBegin = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();

            RefreshCellFeaturesQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(RefreshCellFeatures), typeof(CellFeature), typeof(PossibleFeaturePosition) } });
            GridRootQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(HexGridCreated) } });
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            // GetGrids
            if (!RefreshCellFeaturesQuery.IsEmpty)
            {
                NativeArray<ArchetypeChunk> gridEntitiesChunks = GridRootQuery.CreateArchetypeChunkArray(Allocator.TempJob);
                NativeHashMap<Entity, GridEntitiesArchIndex> GridInfo = new NativeHashMap<Entity, GridEntitiesArchIndex>(gridEntitiesChunks.Length, Allocator.TempJob);
                for (int i = 0; i < gridEntitiesChunks.Length; i++)
                {
                    NativeArray<Entity> entities = gridEntitiesChunks[i].GetNativeArray(GetEntityTypeHandle());
                    GridEntitiesArchIndex ChunkInfo = new GridEntitiesArchIndex { ChunkArrayIndex = i };
                    for (int e = 0; e < entities.Length; e++)
                    {
                        ChunkInfo.ChunkIndex = e;
                        GridInfo.Add(entities[e], ChunkInfo);
                    }
                }

                RefreshCellFeatureJobV3 mainFeatureJob = new RefreshCellFeatureJobV3
                {
                    noiseColours = HexFunctions.noiseColours,
                    featureCollectionInformation = featureCollectionInformation,
                    entityTypeHandle = GetEntityTypeHandle(),
                    featureContTypeHandle = GetComponentTypeHandle<FeatureContainer>(true),
                    featurePositionBufferType = this.GetBufferTypeHandle<PossibleFeaturePosition>(true),
                    cellFeatureBufferType = GetBufferTypeHandle<CellFeature>(),
                    gridInfo = GridInfo,
                    gridEntitiesChunks = gridEntitiesChunks,
                    cellBufferTypeHandle = GetBufferTypeHandle<HexCell>(true),
                    hashBufferTypeHandle = GetBufferTypeHandle<HexHash>(true),
                    ecbBegin = commandBufferSystemBegin.CreateCommandBuffer().AsParallelWriter(),
                    ecbEnd = commandBufferSystemEnd.CreateCommandBuffer().AsParallelWriter()
                };

                JobHandle job1 = GridInfo.Dispose(mainFeatureJob.ScheduleParallel(RefreshCellFeaturesQuery, 64, inputDeps));
                commandBufferSystemEnd.AddJobHandleForProducer(job1);
                commandBufferSystemBegin.AddJobHandleForProducer(job1);
                return job1;
            }
            return inputDeps;
        }

        // Grid Agnoistic Job.
        [BurstCompile]
        private struct RefreshCellFeatureJobV3 : IJobEntityBatch
        {
            [ReadOnly]
            public NativeArray<float4> noiseColours;
            [ReadOnly]
            public NativeHashMap<int, CollectionInfo> featureCollectionInformation;

            [ReadOnly]
            public EntityTypeHandle entityTypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<FeatureContainer> featureContTypeHandle;
            [ReadOnly]
            public BufferTypeHandle<PossibleFeaturePosition> featurePositionBufferType;
            
            public BufferTypeHandle<CellFeature> cellFeatureBufferType;

            [ReadOnly]
            public NativeHashMap<Entity, GridEntitiesArchIndex> gridInfo;
            [ReadOnly][DeallocateOnJobCompletion]
            public NativeArray<ArchetypeChunk> gridEntitiesChunks;
            [ReadOnly]
            public BufferTypeHandle<HexCell> cellBufferTypeHandle;
            [ReadOnly]
            public BufferTypeHandle<HexHash> hashBufferTypeHandle;


            public EntityCommandBuffer.ParallelWriter ecbBegin;
            public EntityCommandBuffer.ParallelWriter ecbEnd;
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                NativeArray<Entity> featureContEntities = batchInChunk.GetNativeArray(entityTypeHandle);
                NativeArray<FeatureContainer> featureContainers = batchInChunk.GetNativeArray(featureContTypeHandle);
                BufferAccessor<PossibleFeaturePosition> possibleFeatureBufferAccessors = batchInChunk.GetBufferAccessor(featurePositionBufferType);
                BufferAccessor<CellFeature> exitingFeatureAccessors = batchInChunk.GetBufferAccessor(cellFeatureBufferType);
                for (int fCont = 0; fCont < featureContainers.Length; fCont++)
                {
                    (DynamicBuffer<HexHash> Hash, DynamicBuffer<HexCell> cells) = GetCellsAndHash(gridInfo[featureContainers[fCont].GridEntity]);
                    DynamicBuffer<PossibleFeaturePosition> possibleFeaturePositions = possibleFeatureBufferAccessors[fCont];
                    NativeArray<CellFeature> newFeatures = new NativeArray<CellFeature>(possibleFeaturePositions.Length,Allocator.Temp);
                    for (int i = 0; i < possibleFeaturePositions.Length; i++)
                    {
                        newFeatures[i] = AddFeature(Hash, cells[possibleFeaturePositions[i].cellIndex], possibleFeaturePositions[i]);
                    }
                    exitingFeatureAccessors[fCont].CopyFrom(newFeatures);

                    Entity featureContainer = featureContEntities[fCont];
                    ecbEnd.RemoveComponent<RefreshCellFeatures>(batchIndex, featureContainer);
                    ecbBegin.AddComponent<ProcessFeatures>(batchIndex, featureContainer);
                }
            }

            private (DynamicBuffer<HexHash> Hash, DynamicBuffer<HexCell> cells) GetCellsAndHash(GridEntitiesArchIndex chunkInfo)
            {
                int ChunkArchIndex = chunkInfo.ChunkIndex;
                ArchetypeChunk GridsChunk = gridEntitiesChunks[chunkInfo.ChunkArrayIndex];
                return (GridsChunk.GetBufferAccessor(hashBufferTypeHandle)[ChunkArchIndex], GridsChunk.GetBufferAccessor(cellBufferTypeHandle)[ChunkArchIndex]);
            }

            private CellFeature AddFeature(DynamicBuffer<HexHash> Hash, HexCell cell,PossibleFeaturePosition possibleFeature)
            {
                CellFeature feature = new CellFeature { cellIndex = cell.Index };
                if (cell.IsSpeical)
                {
                    switch (possibleFeature.ReservedFor)
                    {
                        case FeatureType.Special:

                            HexHash hash = HexFunctions.SampleHashGrid(Hash, possibleFeature.position);
                            feature.featureLevelIndex = cell.SpecialIndex-1;
                            feature.featureType = FeatureType.Special;
                            feature.position = HexFunctions.Perturb(noiseColours, possibleFeature.position, cell.wrapSize);
                            feature.direction = new float3(0f, 360f * hash.e, 0f);
                            break;
                        case FeatureType.Bridge:
                            feature.featureType = FeatureType.Bridge;
                            feature.position = HexFunctions.Perturb(noiseColours, possibleFeature.position, cell.wrapSize);
                            feature.direction = HexFunctions.Perturb(noiseColours, possibleFeature.direction, cell.wrapSize);
                            break;
                        case FeatureType.WallTower:
                            feature.featureType = FeatureType.WallTower;
                            feature.direction = possibleFeature.direction;
                            feature.position = possibleFeature.position;
                            break;
                        default:
                            // return null feature at cell index.
                            feature.featureLevelIndex = int.MinValue;
                            feature.featureSubIndex = int.MinValue;
                            feature.featureType = FeatureType.None;
                            break;
                    }
                }
                else
                {
                    switch (possibleFeature.ReservedFor)
                    {
                        case FeatureType.Special:
                            // return null feature at cell index, cell not marked as special.
                            feature.featureLevelIndex = int.MinValue;
                            feature.featureSubIndex = int.MinValue;
                            feature.featureType = FeatureType.None;
                            break;
                        case FeatureType.Bridge:
                            feature.featureType = FeatureType.Bridge;
                            feature.position = HexFunctions.Perturb(noiseColours, possibleFeature.position, cell.wrapSize);
                            feature.direction = HexFunctions.Perturb(noiseColours, possibleFeature.direction, cell.wrapSize);
                            break;
                        case FeatureType.WallTower:
                            feature.featureType = FeatureType.WallTower;
                            feature.direction = possibleFeature.direction;
                            feature.position = possibleFeature.position;
                            break;
                        default:
                            feature = AddFeature(feature, Hash, cell, possibleFeature);
                            break;
                    }
                }
                return feature;
            }


            private CellFeature AddFeature(CellFeature feature, DynamicBuffer<HexHash> HashGrid, HexCell cell, PossibleFeaturePosition possibleFeature)
            {

                HexHash hash = HexFunctions.SampleHashGrid(HashGrid, possibleFeature.position);
                InternalFeatureStore prefab = PickPrefab(FeatureType.Urban, cell.urbanLevel, hash.a, hash.d);
                InternalFeatureStore prefabAlt = PickPrefab(FeatureType.Urban, cell.farmLevel, hash.b, hash.d);

                float useHash = hash.a;
                if(prefab.featureType != FeatureType.None)
                {
                    if (prefabAlt.featureType != FeatureType.None && hash.b < hash.a)
                    {
                        prefab = prefabAlt;
                        useHash = hash.b;
                    }
                }
                else if(prefabAlt.featureType != FeatureType.None)
                {
                    prefab = prefabAlt;
                    useHash = hash.b;
                }
                prefabAlt = PickPrefab(FeatureType.Plant, cell.plantLevel, hash.c, hash.d);

                if (prefab.featureType != FeatureType.None)
                {
                    if (prefabAlt.featureType != FeatureType.None && hash.c < useHash)
                    {
                        prefab = prefabAlt;
                    }
                }
                else if (prefabAlt.featureType != FeatureType.None)
                {
                    prefab = prefabAlt;
                }

                feature.featureType = prefab.featureType;
                feature.featureSubIndex = prefab.subIndex;
                feature.featureLevelIndex = prefab.levelIndex;
                feature.position = HexFunctions.Perturb(noiseColours, possibleFeature.position, cell.wrapSize);
                feature.direction = new float3(0f, 360f * hash.e, 0f);

                return feature;
            }

            private InternalFeatureStore PickPrefab(FeatureType type, int level, float hash, float choice)
            {
                if(level> 0)
                {
                    FeatureThresholdContainer threshold = HexFunctions.GetFeatureThresholds(level - 1);
                    for (int i = 0; i < 3; i++)
                    {
                        if (hash < threshold.GetLevel(i))
                        {
                            return new InternalFeatureStore
                            {
                                levelIndex = i,
                                subIndex = (int)(choice * featureCollectionInformation[(int)type].InternalCount),
                                featureType = type
                            };
                        }
                    }
                }
                return new InternalFeatureStore
                {
                    levelIndex = int.MinValue,
                    subIndex = int.MinValue,
                    featureType = FeatureType.None
                };
            }

            private struct InternalFeatureStore
            {
                public int levelIndex;
                public int subIndex;
                public FeatureType featureType;
            }

        }

        public struct CollectionInfo
        {
            public int CollectionCount;
            public int InternalCount;
            public FeatureType CollectionType;
        }

        public void CreateEntityWorldInfo()
        {
            try
            {
                featureCollectionInformation = new NativeHashMap<int, CollectionInfo>(7, Allocator.Persistent);
                featureCollectionInformation.Add((int)FeatureType.None, new CollectionInfo { CollectionCount = 0, InternalCount = 0, CollectionType = FeatureType.None });
                featureCollectionInformation.Add((int)FeatureType.WallTower, new CollectionInfo { CollectionCount = 1, InternalCount = 1, CollectionType = FeatureType.WallTower });
                featureCollectionInformation.Add((int)FeatureType.Bridge, new CollectionInfo { CollectionCount = 1, InternalCount = 1, CollectionType = FeatureType.Bridge });
                featureCollectionInformation.Add((int)FeatureType.Special, new CollectionInfo { CollectionCount = internalPrefabs.SpecialFeatures.Length, InternalCount = 1, CollectionType = FeatureType.Special });
                featureCollectionInformation.Add((int)FeatureType.Urban, new CollectionInfo { CollectionCount = internalPrefabs.UrbanCollections.Length, InternalCount = internalPrefabs.GetUrbanCollection(0).prefabs.Length, CollectionType = FeatureType.Urban });
                featureCollectionInformation.Add((int)FeatureType.Farm, new CollectionInfo { CollectionCount = internalPrefabs.FarmCollections.Length, InternalCount = internalPrefabs.GetFarmCollection(0).prefabs.Length, CollectionType = FeatureType.Farm });
                featureCollectionInformation.Add((int)FeatureType.Plant, new CollectionInfo { CollectionCount = internalPrefabs.PlantCollections.Length, InternalCount = internalPrefabs.GetPlantCollection(0).prefabs.Length, CollectionType = FeatureType.Plant });
                EntityWorldInfoCreated = true;
            }
            catch
            {
                EntityWorldInfoCreated = false;
                Debug.LogError("Fail to Create Entity World Feature Infomation");
                this.Enabled = false;
            }

        }

        protected override void OnDestroy()
        {
            try
            {
                featureCollectionInformation.Dispose();
            }
            catch { }
        }

    }

    public struct GridEntitiesArchIndex
    {
        public int ChunkArrayIndex;
        public int ChunkIndex;
    }

    //[DisableAutoCreation]
    //[UpdateAfter(typeof(UpdateFeatureDataSystem))]
    //public class SpawnFeatureSystem : JobComponentSystem
    //{
    //    private EndSimulationEntityCommandBufferSystem commandBufferSystemEnd;
    //    private BeginSimulationEntityCommandBufferSystem commandBufferSystemBegin;
    //
    //    private EntityQueryDesc RefreshCellFeaturesQuery = new EntityQueryDesc { All = new ComponentType[] { typeof(CheckAndSpawnFeatures) } 
    //    , None = new ComponentType[] { typeof(RefreshContainer), typeof(RefreshCellFeatures) }
    //    };
    //    protected override void OnCreate()
    //    {
    //        commandBufferSystemEnd = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    //        commandBufferSystemBegin = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
    //    }
    //
    //    protected override JobHandle OnUpdate(JobHandle inputDeps)
    //    {
    //        if (FeatureDecisionSystem.urbanCollections == null && FeatureDecisionSystem.farmCollections == null && FeatureDecisionSystem.plantCollections == null)
    //        {
    //            return inputDeps;
    //        }
    //        if (FeatureDecisionSystem.WallTower == Entity.Null && FeatureDecisionSystem.Bridge == Entity.Null)
    //        {
    //            return inputDeps;
    //        }
    //        if (HexFunctions.ActiveGridEntity == Entity.Null)
    //        {
    //            return inputDeps;
    //        }
    //
    //        EntityQuery refreshBufferQuery = GetEntityQuery(RefreshCellFeaturesQuery);
    //        SpawnCellFeatureJob spawnCellFeature = new SpawnCellFeatureJob
    //        {
    //            noiseColours = HexFunctions.noiseColours,
    //            HexCellShaderData = HexCellShaderSystem.HexCellShaderData,
    //
    //            urbanPrefabs = new NativeArray<HexFeaturePrefabContainer>(FeatureDecisionSystem.urbanCollections, Allocator.TempJob),
    //            farmPrefabs = new NativeArray<HexFeaturePrefabContainer>(FeatureDecisionSystem.farmCollections, Allocator.TempJob),
    //            plantPrefabs = new NativeArray<HexFeaturePrefabContainer>(FeatureDecisionSystem.plantCollections, Allocator.TempJob),
    //            WallTower = FeatureDecisionSystem.WallTower,
    //            Bridge = FeatureDecisionSystem.Bridge,
    //            featureDataType = this.GetComponentTypeHandle<FeatureDataContainer>(true),
    //            entityType = this.GetEntityTypeHandle(),
    //            scaleFromEntity = this.GetComponentDataFromEntity<NonUniformScale>(true),
    //            featureBufferType = this.GetBufferTypeHandle<Feature>(true),
    //            gridDataFromEntity = GetComponentDataFromEntity<HexGridComponent>(true),
    //            ecbBegin = commandBufferSystemBegin.CreateCommandBuffer().AsParallelWriter(),
    //            ecbEnd = commandBufferSystemEnd.CreateCommandBuffer().AsParallelWriter()
    //        };
    //        JobHandle outputDeps = spawnCellFeature.ScheduleParallel(refreshBufferQuery, 64, inputDeps);
    //        //JobHandle outputDeps = spawnCellFeature.Schedule(refreshBufferQuery, inputDeps);
    //        commandBufferSystemEnd.AddJobHandleForProducer(outputDeps);
    //        commandBufferSystemBegin.AddJobHandleForProducer(outputDeps);
    //
    //        return outputDeps;
    //    }
    //
    //    [BurstCompile]
    //    private struct SpawnCellFeatureJob : IJobEntityBatch
    //    {
    //        [ReadOnly]
    //        public NativeArray<float4> noiseColours;
    //
    //        [ReadOnly]
    //        public Entity HexCellShaderData;
    //
    //        [ReadOnly]
    //        [DeallocateOnJobCompletion]
    //        public NativeArray<HexFeaturePrefabContainer> urbanPrefabs;
    //        [ReadOnly]
    //        [DeallocateOnJobCompletion]
    //        public NativeArray<HexFeaturePrefabContainer> farmPrefabs;
    //        [ReadOnly]
    //        [DeallocateOnJobCompletion]
    //        public NativeArray<HexFeaturePrefabContainer> plantPrefabs;
    //        [ReadOnly]
    //        public Entity WallTower;
    //        [ReadOnly]
    //        public Entity Bridge;
    //
    //        [ReadOnly]
    //        public ComponentTypeHandle<FeatureDataContainer> featureDataType;
    //        [ReadOnly]
    //        public EntityTypeHandle entityType;
    //        [ReadOnly]
    //        public ComponentDataFromEntity<HexGridComponent> gridDataFromEntity;
    //        [ReadOnly]
    //        public ComponentDataFromEntity<NonUniformScale> scaleFromEntity;
    //        [ReadOnly]
    //        public BufferTypeHandle<Feature> featureBufferType;
    //
    //        public EntityCommandBuffer.ParallelWriter ecbBegin;
    //        public EntityCommandBuffer.ParallelWriter ecbEnd;
    //        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
    //        {
    //            BufferAccessor<Feature> chunkFeatures = batchInChunk.GetBufferAccessor(featureBufferType);
    //            NativeArray<FeatureDataContainer>.ReadOnly cellFeatureData = batchInChunk.GetNativeArray(featureDataType).AsReadOnly();
    //            NativeArray<Entity>.ReadOnly cellFeatureEntities = batchInChunk.GetNativeArray(entityType).AsReadOnly();
    //            for (int chunkFeatureIndex = 0; chunkFeatureIndex < chunkFeatures.Length; chunkFeatureIndex++)
    //            {
    //                DynamicBuffer<Feature> chunkFeature = chunkFeatures[chunkFeatureIndex];
    //                Entity cellFeatureEntity = cellFeatureEntities[chunkFeatureIndex];
    //                if (chunkFeature.Length > 0)
    //                {
    //                    FeatureDataContainer featureDataContainer = cellFeatureData[chunkFeatureIndex];
    //                    int wrapSize = gridDataFromEntity[featureDataContainer.GridEntity].wrapSize;
    //                    for (int i = 0; i < chunkFeature.Length; i++)
    //                    {
    //                        InstanceFeature(batchIndex, wrapSize, chunkFeature[i], i, featureDataContainer.GridEntity, cellFeatureEntity);
    //                    }
    //                    ecbBegin.AddComponent<RefreshContainer>(batchIndex, cellFeatureEntity);
    //                }
    //                
    //                ecbEnd.RemoveComponent<CheckAndSpawnFeatures>(batchIndex, cellFeatureEntity);
    //            }
    //            ecbBegin.AddComponent<SetFeatureVisability>(HexCellShaderData.Index, HexCellShaderData);
    //        }
    //
    //        private void InstanceFeature(int batchIndex, int wrapSize, Feature feature, int featureIndexInMainBuffer, Entity grid, Entity BufferContainerEntity)
    //        {
    //            bool spawn = feature.feature == Entity.Null;
    //            switch (spawn)
    //            {
    //                case false:
    //                    switch (feature.featureType)
    //                    {
    //                        case FeatureCollection.WallTower:
    //                            ecbBegin.SetComponent(batchIndex, feature.feature, new Translation { Value = feature.position });
    //                            ecbBegin.SetComponent(batchIndex, feature.feature, new Rotation { Value = HexFunctions.FromToRotation(new float3(1, 0, 0), feature.direction) });
    //                            break;
    //                        case FeatureCollection.Bridge:
    //                            feature.position = HexFunctions.Perturb(noiseColours, feature.position, wrapSize);
    //                            feature.direction = HexFunctions.Perturb(noiseColours, feature.direction, wrapSize);
    //                            float length = math.distance(feature.position, feature.direction);
    //                            ecbBegin.SetComponent(batchIndex, feature.feature, new Translation { Value = (feature.position + feature.direction) * 0.5f });
    //                            ecbBegin.SetComponent(batchIndex, feature.feature, new Rotation { Value = HexFunctions.FromToRotation(new float3(0, 0, 1), feature.direction - feature.position) });
    //                            ecbBegin.SetComponent(batchIndex, feature.feature, new NonUniformScale { Value = new float3(1f, 1f, length * (1f / HexFunctions.bridgeDesignLength)) });
    //                            break;
    //                        default:
    //                            feature.position.y += scaleFromEntity[feature.feature].Value.y * 0.5f;
    //                            ecbBegin.SetComponent(batchIndex, feature.feature, new Translation { Value = HexFunctions.Perturb(noiseColours, feature.position, wrapSize) });
    //                            ecbBegin.SetComponent(batchIndex, feature.feature, new Rotation { Value = quaternion.EulerXYZ(feature.direction) });
    //                            break;
    //                    }
    //                    return;
    //            }
    //            Entity toSpawn = feature.featureType switch
    //            {
    //                FeatureCollection.Urban => urbanPrefabs[feature.featureLevelIndex][feature.featureSubIndex],
    //                FeatureCollection.Farm => farmPrefabs[feature.featureLevelIndex][feature.featureSubIndex],
    //                FeatureCollection.Plant => plantPrefabs[feature.featureLevelIndex][feature.featureSubIndex],
    //                FeatureCollection.WallTower => WallTower,
    //                FeatureCollection.Bridge => Bridge,
    //                _ => Entity.Null,
    //            };
    //            switch (toSpawn == Entity.Null)
    //            {
    //                case false:
    //                    Entity newSpawn = ecbBegin.Instantiate(batchIndex, toSpawn);
    //                    ecbBegin.AddComponent(batchIndex, newSpawn, new NewFeatureSpawn { BufferContainer = BufferContainerEntity, Index = featureIndexInMainBuffer });
    //                    ecbBegin.AddComponent(batchIndex, newSpawn, new Parent { Value = BufferContainerEntity });
    //                    ecbBegin.AddComponent(batchIndex, newSpawn, new FeatureGridInfo { GridEntity = grid });
    //                    switch (feature.featureType)
    //                    {
    //                        case FeatureCollection.WallTower:
    //                            ecbBegin.SetComponent(batchIndex, newSpawn, new Translation { Value = feature.position });
    //                            ecbBegin.SetComponent(batchIndex, newSpawn, new Rotation { Value = HexFunctions.FromToRotation(new float3(1, 0, 0), feature.direction) });
    //                            break;
    //                        case FeatureCollection.Bridge:
    //                            feature.position = HexFunctions.Perturb(noiseColours, feature.position, wrapSize);
    //                            feature.direction = HexFunctions.Perturb(noiseColours, feature.direction, wrapSize);
    //                            float length = math.distance(feature.position, feature.direction);
    //                            ecbBegin.SetComponent(batchIndex, feature.feature, new Translation { Value = (feature.position + feature.direction) * 0.5f });
    //                            ecbBegin.SetComponent(batchIndex, feature.feature, new Rotation { Value = HexFunctions.FromToRotation(new float3(0, 0, 1), feature.direction - feature.position) });
    //                            ecbBegin.SetComponent(batchIndex, feature.feature, new NonUniformScale { Value = new float3(1f, 1f, length * (1f / HexFunctions.bridgeDesignLength)) }); ;
    //                            break;
    //                        default:
    //                            feature.position.y += scaleFromEntity[toSpawn].Value.y * 0.5f;
    //                            ecbBegin.SetComponent(batchIndex, newSpawn, new Translation { Value = HexFunctions.Perturb(noiseColours, feature.position, wrapSize) });
    //                            ecbBegin.SetComponent(batchIndex, newSpawn, new Rotation { Value = quaternion.EulerXYZ(feature.direction) });
    //                            break;
    //                    }
    //                    break;
    //            }
    //        }
    //
    //    }
    //}
    //
    //[DisableAutoCreation]
    //[UpdateBefore(typeof(FeatureDecisionSystem))]
    //[UpdateAfter(typeof(TransformSystemGroup))]
    //public class UpdateFeatureDataSystem : JobComponentSystem
    //{
    //    private EndSimulationEntityCommandBufferSystem commandBufferSystemEnd;
    //    private BeginSimulationEntityCommandBufferSystem commandBufferSystemBegin;
    //
    //    private EntityQueryDesc RefreshCellFeaturesQuery = new EntityQueryDesc { All = new ComponentType[] { typeof(RefreshContainer) },
    //        None = new ComponentType[] { typeof(CheckAndSpawnFeatures) } };
    //
    //    protected override void OnCreate()
    //    {
    //        commandBufferSystemEnd = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    //        commandBufferSystemBegin = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
    //    }
    //    protected override JobHandle OnUpdate(JobHandle inputDeps)
    //    {
    //        EntityQuery refreshBufferQuery = GetEntityQuery(RefreshCellFeaturesQuery);
    //        UpdateFeatureDataJob updateFeatureBuffers = new UpdateFeatureDataJob
    //        {
    //            newFeatureInfoFromEntity = GetComponentDataFromEntity<NewFeatureSpawn>(true),
    //            featureBufferTypeHandle = GetBufferTypeHandle<Feature>(),
    //            childrenFromBufferTypeHandle = GetBufferTypeHandle<Child>(true),
    //            entityTypeHandle = GetEntityTypeHandle(),
    //            ecbEnd = commandBufferSystemEnd.CreateCommandBuffer().AsParallelWriter()
    //        };
    //        JobHandle outputDeps = updateFeatureBuffers.ScheduleParallel(refreshBufferQuery, 1, inputDeps);
    //        //JobHandle outputDeps = updateFeatureBuffers.Schedule(refreshBufferQuery, inputDeps);
    //        commandBufferSystemEnd.AddJobHandleForProducer(outputDeps);
    //        return outputDeps;
    //
    //    }
    //
    //    [BurstCompile]
    //    private struct UpdateFeatureDataJob : IJobEntityBatch
    //    {
    //        [ReadOnly]
    //        public ComponentDataFromEntity<NewFeatureSpawn> newFeatureInfoFromEntity;
    //        public BufferTypeHandle<Feature> featureBufferTypeHandle;
    //        [ReadOnly]
    //        public BufferTypeHandle<Child> childrenFromBufferTypeHandle;
    //        [ReadOnly]
    //        public EntityTypeHandle entityTypeHandle;
    //        public EntityCommandBuffer.ParallelWriter ecbEnd;
    //        public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
    //        {
    //            BufferAccessor<Child> childrenAccessors = batchInChunk.GetBufferAccessor(childrenFromBufferTypeHandle);
    //            BufferAccessor<Feature> featureAccessors = batchInChunk.GetBufferAccessor(featureBufferTypeHandle);
    //            NativeArray<Entity> bufferContainers = batchInChunk.GetNativeArray(entityTypeHandle);
    //            for (int i = 0; i < childrenAccessors.Length; i++)
    //            {
    //                Entity bufferContainer = bufferContainers[i];
    //                DynamicBuffer<Child> children = childrenAccessors[i];
    //                DynamicBuffer<Feature> FeatureBuffer = featureAccessors[i];
    //                for (int c = 0; c < children.Length; c++)
    //                {
    //                    if (newFeatureInfoFromEntity.HasComponent(children[c].Value))
    //                    {
    //                        NewFeatureSpawn newFeatureEntity = newFeatureInfoFromEntity[children[c].Value];
    //                        Feature feature = FeatureBuffer[newFeatureEntity.Index];
    //                        feature.feature = children[c].Value;
    //                        FeatureBuffer[newFeatureEntity.Index] = feature;
    //                        bufferContainer = newFeatureEntity.BufferContainer;
    //                        ecbEnd.RemoveComponent<NewFeatureSpawn>(batchIndex, feature.feature);
    //                    }
    //                    else
    //                    {
    //                        bool cull = true;
    //                        for (int f = 0; f < FeatureBuffer.Length; f++)
    //                        {
    //                            if(FeatureBuffer[f].feature == children[c].Value)
    //                            {
    //                                cull = false;
    //                                break;
    //                            }
    //                        }
    //                        if (cull)
    //                        {
    //                            ecbEnd.DestroyEntity(batchIndex, children[c].Value);
    //                        }
    //                    }
    //                }
    //                ecbEnd.RemoveComponent<RefreshContainer>(batchIndex, bufferContainer);
    //            }
    //        }
    //    }
    //}
}