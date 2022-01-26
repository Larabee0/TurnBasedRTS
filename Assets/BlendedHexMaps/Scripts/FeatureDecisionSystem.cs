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
        private NativeHashMap<int, CollectionInfo> featureCollectionInformation;

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
                    NativeArray<CellFeature> newFeatures = new NativeArray<CellFeature>(possibleFeaturePositions.Length, Allocator.Temp);
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

            private CellFeature AddFeature(DynamicBuffer<HexHash> Hash, HexCell cell, PossibleFeaturePosition possibleFeature)
            {
                CellFeature feature = new CellFeature { cellIndex = cell.Index };
                if (cell.IsSpeical)
                {
                    switch (possibleFeature.ReservedFor)
                    {
                        case FeatureType.Special:

                            HexHash hash = HexFunctions.SampleHashGrid(Hash, possibleFeature.position);
                            feature.featureLevelIndex = cell.SpecialIndex - 1;
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
                InternalFeatureStore prefabAlt = PickPrefab(FeatureType.Farm, cell.farmLevel, hash.b, hash.d);

                float useHash = hash.a;
                if (prefab.featureType != FeatureType.None)
                {
                    if (prefabAlt.featureType != FeatureType.None && hash.b < hash.a)
                    {
                        prefab = prefabAlt;
                        useHash = hash.b;
                    }
                }
                else if (prefabAlt.featureType != FeatureType.None)
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
                if (level > 0)
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
}