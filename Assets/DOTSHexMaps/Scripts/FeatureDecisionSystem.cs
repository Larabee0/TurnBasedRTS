using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Rendering;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Jobs;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace DOTSHexagons
{
    //[DisableAutoCreation]
    [UpdateBefore(typeof(UpdateMeshSystem))]
    public class FeatureDecisionSystem : JobComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem commandBufferSystemEnd;
        private BeginSimulationEntityCommandBufferSystem commandBufferSystemBegin;

        public static Entity WallTower;
        public static Entity Bridge;

        public static Entity[] special;
        public static HexFeaturePrefabContainer[] urbanCollections;
        public static HexFeaturePrefabContainer[] farmCollections;
        public static HexFeaturePrefabContainer[] plantCollections;


        private EntityQueryDesc RefreshCellFeaturesQuery = new EntityQueryDesc { All = new ComponentType[] { typeof(RefreshCellFeatures) }, 
            None = new ComponentType[] { typeof(RefreshContainer), typeof(CheckAndSpawnFeatures) } };

        protected override void OnCreate()
        {
            commandBufferSystemEnd = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            commandBufferSystemBegin = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();            
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (special == null)
            {
                return inputDeps;
            }
            if (urbanCollections == null && farmCollections == null && plantCollections == null)
            {
                return inputDeps;
            }
            if (WallTower == Entity.Null && Bridge == Entity.Null)
            {
                return inputDeps;
            }
            if(HexMetrics.ActiveGridEntity == Entity.Null)
            {
                return inputDeps;
            }
            float start = UnityEngine.Time.realtimeSinceStartup;
            EntityQuery RefreshQuery = GetEntityQuery(RefreshCellFeaturesQuery);
            RefreshCellFeatureJobV2 mainFeatureJob = new RefreshCellFeatureJobV2
            {
                HexCellShaderData = HexCellShaderSystem.HexCellShaderData,
                urbanPrefabs = new NativeArray<HexFeaturePrefabContainer>(urbanCollections, Allocator.TempJob),
                farmPrefabs = new NativeArray<HexFeaturePrefabContainer>(farmCollections, Allocator.TempJob),
                plantPrefabs = new NativeArray<HexFeaturePrefabContainer>(plantCollections, Allocator.TempJob),
                specialPrefabs = new NativeArray<Entity>(special, Allocator.TempJob),
                WallTower = WallTower,
                Bridge = Bridge,
                noiseColours = HexMetrics.noiseColours,
                hexHashBufferFromEntity = GetBufferFromEntity<HexHash>(true),
                featureDataType = this.GetComponentTypeHandle<FeatureDataContainer>(true),
                entityType = this.GetEntityTypeHandle(),
                scaleFromEntity = this.GetComponentDataFromEntity<NonUniformScale>(true),
                hexCellBufferFromEntity = this.GetBufferFromEntity<HexCell>(true),
                featureBufferType = this.GetBufferTypeHandle<Feature>(true),
                featurePositionBufferType = this.GetBufferTypeHandle<PossibleFeaturePosition>(true),
                ecbBegin = commandBufferSystemBegin.CreateCommandBuffer().AsParallelWriter(),
                ecbEnd = commandBufferSystemEnd.CreateCommandBuffer().AsParallelWriter()
            };

            JobHandle job1 = mainFeatureJob.ScheduleParallel(RefreshQuery, 64, inputDeps);
            //JobHandle job1 = mainFeatureJob.Schedule(RefreshQuery, inputDeps);
            commandBufferSystemEnd.AddJobHandleForProducer(job1);
            commandBufferSystemBegin.AddJobHandleForProducer(job1);
            Debug.Log("main Feature Job " + (UnityEngine.Time.realtimeSinceStartup - start) + "ms Entity Count " + RefreshQuery.CalculateEntityCount());
            return job1;
        }

        // Grid Agnoistic Job.
        [BurstCompile]
        private struct RefreshCellFeatureJobV2 : IJobEntityBatch
        {
            [ReadOnly]
            public NativeArray<float4> noiseColours;

            [ReadOnly]
            public Entity HexCellShaderData;

            [ReadOnly]
            [DeallocateOnJobCompletion]
            public NativeArray<HexFeaturePrefabContainer> urbanPrefabs;
            [ReadOnly]
            [DeallocateOnJobCompletion]
            public NativeArray<HexFeaturePrefabContainer> farmPrefabs;
            [ReadOnly]
            [DeallocateOnJobCompletion]
            public NativeArray<HexFeaturePrefabContainer> plantPrefabs;
            [ReadOnly]
            [DeallocateOnJobCompletion]
            public NativeArray<Entity> specialPrefabs;
            [ReadOnly]
            public Entity WallTower;
            [ReadOnly]
            public Entity Bridge;

            [ReadOnly]
            public ComponentTypeHandle<FeatureDataContainer> featureDataType;
            [ReadOnly]
            public EntityTypeHandle entityType;
            [ReadOnly]
            public ComponentDataFromEntity<NonUniformScale> scaleFromEntity;
            [ReadOnly]
            public BufferFromEntity<HexCell> hexCellBufferFromEntity;
            [ReadOnly]
            public BufferFromEntity<HexHash> hexHashBufferFromEntity;
            [ReadOnly]
            public BufferTypeHandle<Feature> featureBufferType;
            [ReadOnly]
            public BufferTypeHandle<PossibleFeaturePosition> featurePositionBufferType;

            public EntityCommandBuffer.ParallelWriter ecbBegin;
            public EntityCommandBuffer.ParallelWriter ecbEnd;
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                BufferAccessor<Feature> chunkFeatures = batchInChunk.GetBufferAccessor(featureBufferType);
                BufferAccessor<PossibleFeaturePosition> cellPossiblePositions = batchInChunk.GetBufferAccessor(featurePositionBufferType);
                NativeArray<FeatureDataContainer>.ReadOnly cellFeatureData = batchInChunk.GetNativeArray(featureDataType).AsReadOnly();
                NativeArray<Entity>.ReadOnly cellFeatureEntity = batchInChunk.GetNativeArray(entityType).AsReadOnly();
                for (int chunkFeatureIndex = 0; chunkFeatureIndex < chunkFeatures.Length; chunkFeatureIndex++)
                {
                    FeatureDataContainer featureData = cellFeatureData[chunkFeatureIndex];
                    HexCell cell = hexCellBufferFromEntity[featureData.GridEntity][cellFeatureData[chunkFeatureIndex].cellIndex];
                    Entity BufferContainerEntity = cellFeatureEntity[chunkFeatureIndex];
                    DynamicBuffer<Feature> cellFeatures = chunkFeatures[chunkFeatureIndex];
                    NativeList<int> clearedFeatures = new NativeList<int>(Allocator.Temp);
                    bool specialNeedsRefresh = true;
                    int wrapSize = cell.wrapSize;
                    if (cellFeatures.Length > 0)
                    {
                        for (int i = 0; i < cellFeatures.Length; i++)
                        {
                            Feature ExistingFeature = cellFeatures[i];
                            if (cell.IsSpeical)
                            {
                                if (ExistingFeature.featureType != FeatureCollection.Special || ExistingFeature.featureType != FeatureCollection.WallTower)
                                {
                                    ecbEnd.DestroyEntity(batchIndex, ExistingFeature.feature);
                                    clearedFeatures.Add(i);
                                }
                                else if (ExistingFeature.featureType == FeatureCollection.Bridge)
                                {
                                    bool bridgeValid = false;
                                    for (HexDirection direction = HexDirection.NE; direction <= HexDirection.NW; direction++)
                                    {
                                        bool previousHasRiver = HexCell.HasRiverThroughEdge(cell, direction.Previous());
                                        bool nextHasRiver = HexCell.HasRiverThroughEdge(cell, direction.Next());
                                        if (!cell.HasRiverBeginOrEnd)
                                        {
                                            if (cell.incomingRiver == cell.OutgoingRiver.Opposite())
                                            {
                                                if (cell.IncomingRiver == direction.Next() && (HexCell.HasRoadThroughEdge(cell, direction.Next2()) || HexCell.HasRoadThroughEdge(cell, direction.Opposite())))
                                                {
                                                    bridgeValid = true;
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                HexDirection middle;
                                                switch (previousHasRiver)
                                                {
                                                    case true:
                                                        middle = direction.Next();
                                                        break;
                                                    case false:
                                                        switch (nextHasRiver)
                                                        {
                                                            case true:
                                                                middle = direction.Previous();
                                                                break;
                                                            case false:
                                                                middle = direction;
                                                                break;
                                                        }
                                                        break;
                                                }
                                                if (cell.IncomingRiver != cell.OutgoingRiver.Previous() && !(previousHasRiver && nextHasRiver))
                                                {
                                                    if (direction == middle && HexCell.HasRoadThroughEdge(cell, direction.Opposite()))
                                                    {
                                                        bridgeValid = true;
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (!bridgeValid)
                                    {
                                        ecbEnd.DestroyEntity(batchIndex, ExistingFeature.feature);
                                        clearedFeatures.Add(i);
                                    }
                                }
                                else if (ExistingFeature.featureType == FeatureCollection.Special && (!ExistingFeature.position.Equals(cell.Position) && ExistingFeature.featureSubIndex == cell.SpecialIndex - 1))
                                {
                                    specialNeedsRefresh = false;
                                }
                            }
                            else
                            {
                                specialNeedsRefresh = false;
                                if (ExistingFeature.featureType == FeatureCollection.Bridge)
                                {
                                    bool bridgeValid = false;
                                    for (HexDirection direction = HexDirection.NE; direction <= HexDirection.NW; direction++)
                                    {
                                        bool previousHasRiver = HexCell.HasRiverThroughEdge(cell, direction.Previous());
                                        bool nextHasRiver = HexCell.HasRiverThroughEdge(cell, direction.Next());
                                        if (!cell.HasRiverBeginOrEnd)
                                        {
                                            if (cell.incomingRiver == cell.OutgoingRiver.Opposite())
                                            {
                                                if (cell.IncomingRiver == direction.Next() && (HexCell.HasRoadThroughEdge(cell, direction.Next2()) || HexCell.HasRoadThroughEdge(cell, direction.Opposite())))
                                                {
                                                    bridgeValid = true;
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                HexDirection middle;
                                                switch (previousHasRiver)
                                                {
                                                    case true:
                                                        middle = direction.Next();
                                                        break;
                                                    case false:
                                                        switch (nextHasRiver)
                                                        {
                                                            case true:
                                                                middle = direction.Previous();
                                                                break;
                                                            case false:
                                                                middle = direction;
                                                                break;
                                                        }
                                                        break;
                                                }
                                                if (cell.IncomingRiver != cell.OutgoingRiver.Previous() && !(previousHasRiver && nextHasRiver))
                                                {
                                                    if (direction == middle && HexCell.HasRoadThroughEdge(cell, direction.Opposite()))
                                                    {
                                                        bridgeValid = true;
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (!bridgeValid)
                                    {
                                        ecbEnd.DestroyEntity(batchIndex, ExistingFeature.feature);
                                        clearedFeatures.Add(i);
                                    }
                                }
                                if (ExistingFeature.featureType == FeatureCollection.Urban)
                                {
                                    if (cell.urbanLevel - 1 != ExistingFeature.featureLevelIndex)
                                    {
                                        ecbEnd.DestroyEntity(batchIndex, ExistingFeature.feature);
                                        clearedFeatures.Add(i);
                                    }
                                }
                                if (ExistingFeature.featureType == FeatureCollection.Farm)
                                {
                                    if (cell.farmLevel - 1 != ExistingFeature.featureLevelIndex)
                                    {
                                        ecbEnd.DestroyEntity(batchIndex, ExistingFeature.feature);
                                        clearedFeatures.Add(i);
                                    }
                                }
                                if (cell.plantLevel == 0 && ExistingFeature.featureType == FeatureCollection.Plant)
                                {
                                    if (cell.plantLevel - 1 != ExistingFeature.featureLevelIndex)
                                    {
                                        ecbEnd.DestroyEntity(batchIndex, ExistingFeature.feature);
                                        clearedFeatures.Add(i);
                                    }
                                }
                                if (cell.SpecialIndex == 0 && ExistingFeature.featureType == FeatureCollection.Special)
                                {
                                    ecbEnd.DestroyEntity(batchIndex, ExistingFeature.feature);
                                    clearedFeatures.Add(i);
                                }
                            }
                        }
                    }

                    if (cell.SpecialIndex > 0 && specialNeedsRefresh)
                    {
                        DynamicBuffer<PossibleFeaturePosition> possiblePositionsInternal = cellPossiblePositions[chunkFeatureIndex];
                        NativeList<Feature> writeBackFeatures = new NativeList<Feature>(Allocator.Temp);
                        NativeHashSet<float3> reserveredPositions = new NativeHashSet<float3>(possiblePositionsInternal.Length, Allocator.Temp);
                        NativeList<int> TowersForReprocessing = new NativeList<int>(Allocator.Temp);
                        for (int i = 0; i < cellFeatures.Length; i++)
                        {
                            Feature feature = cellFeatures[i];
                            for (int cf = 0; cf < clearedFeatures.Length; cf++)
                            {
                                if (clearedFeatures[cf] == i)
                                {
                                    feature.featureType = FeatureCollection.None;
                                    continue;
                                }
                            }
                            if (feature.featureType == FeatureCollection.WallTower)
                            {
                                bool feautreForResprocessing = true;
                                for (int pfp = 0; pfp < possiblePositionsInternal.Length; pfp++)
                                {
                                    if (feature.position.Equals(possiblePositionsInternal[pfp].position) && possiblePositionsInternal[pfp].ReservedFor == FeatureCollection.WallTower)
                                    {
                                        reserveredPositions.Add(possiblePositionsInternal[pfp].position);
                                        feautreForResprocessing = false;
                                        break;
                                    }
                                }
                                if (feautreForResprocessing)
                                {
                                    TowersForReprocessing.Add(i);
                                }
                            }
                        }
                        for (int i = 0; i < TowersForReprocessing.Length; i++)
                        {
                            Feature tower = cellFeatures[TowersForReprocessing[i]];
                            for (int pfp = 0; pfp < possiblePositionsInternal.Length; pfp++)
                            {
                                if (possiblePositionsInternal[pfp].ReservedFor == FeatureCollection.WallTower && !reserveredPositions.Contains(possiblePositionsInternal[pfp].position))
                                {
                                    reserveredPositions.Add(possiblePositionsInternal[pfp].position);
                                    InstanceFeature(writeBackFeatures, cell, tower, possiblePositionsInternal[pfp]);
                                    break;
                                }
                            }
                        }
                        TowersForReprocessing.Dispose();
                        for (int i = 0; i < possiblePositionsInternal.Length; i++)
                        {
                            if (!reserveredPositions.Contains(possiblePositionsInternal[i].position))
                            {
                                continue;
                            }
                            if (possiblePositionsInternal[i].ReservedFor == FeatureCollection.WallTower)
                            {
                                Feature Tower = new Feature
                                {
                                    cellIndex = cell.Index,
                                    direction = possiblePositionsInternal[i].direction,
                                    position = possiblePositionsInternal[i].position,
                                    featureType = FeatureCollection.WallTower,

                                };
                                InstanceFeature(writeBackFeatures, cell, Tower, possiblePositionsInternal[i]);
                            }
                        }
                        reserveredPositions.Dispose();
                        ecbEnd.SetBuffer<PossibleFeaturePosition>(batchIndex, BufferContainerEntity).Clear();
                        ecbBegin.SetBuffer<Feature>(batchIndex, BufferContainerEntity).CopyFrom(writeBackFeatures);
                        Entity newSpawn = ecbBegin.Instantiate(batchIndex, specialPrefabs[cell.SpecialIndex - 1]);
                        Feature NewFeature = new Feature
                        {
                            cellIndex = cell.Index,
                            direction = 0f,
                            position = cell.Position,
                            featureSubIndex = cell.SpecialIndex - 1,
                            featureType = FeatureCollection.Special,
                            featureLevelIndex = cell.SpecialIndex - 1
                        };
                        // cannot set this to 0 anymore, must be writebacks.length
                        ecbBegin.AddComponent(batchIndex, newSpawn, new NewFeatureSpawn { BufferContainer = BufferContainerEntity, Index = writeBackFeatures.Length });
                        ecbBegin.SetComponent(batchIndex, newSpawn, new Translation { Value = HexMetrics.Perturb(noiseColours, NewFeature.position, wrapSize) });
                        ecbBegin.SetComponent(batchIndex, newSpawn, new Rotation { Value = quaternion.EulerXYZ(NewFeature.direction) });
                        ecbBegin.AddComponent(batchIndex, newSpawn, new Parent { Value = BufferContainerEntity });
                        ecbBegin.AddComponent(batchIndex, newSpawn, new FeatureGridInfo { GridEntity = featureData.GridEntity });
                        ecbBegin.SetBuffer<Feature>(batchIndex, BufferContainerEntity).Add(NewFeature);
                        ecbEnd.RemoveComponent<RefreshCellFeatures>(batchIndex, BufferContainerEntity);
                        writeBackFeatures.Dispose();
                        continue;
                    }
                    DynamicBuffer<PossibleFeaturePosition> possiblePositions = cellPossiblePositions[chunkFeatureIndex];
                    NativeList<Feature> WriteBackFeatures = new NativeList<Feature>(Allocator.Temp);

                    NativeHashSet<float3> positions = new NativeHashSet<float3>(possiblePositions.Length, Allocator.Temp);
                    NativeHashSet<float3> directions = new NativeHashSet<float3>(possiblePositions.Length, Allocator.Temp);
                    for (int i = 0; i < possiblePositions.Length; i++)
                    {
                        positions.Add(possiblePositions[i].position);
                        directions.Add(possiblePositions[i].direction);
                    }
                    NativeList<int> featureNeedsPositionChange = new NativeList<int>(Allocator.Temp);
                    NativeHashSet<float3> reservedPosition = new NativeHashSet<float3>(possiblePositions.Length, Allocator.Temp);
                    for (int i = 0; i < cellFeatures.Length; i++)
                    {
                        Feature stilloccupiedFeature = cellFeatures[i];
                        if (clearedFeatures.Contains(i))
                        {
                            continue;
                        }
                        if (positions.Contains(stilloccupiedFeature.position))
                        {
                            WriteBackFeatures.Add(stilloccupiedFeature);
                            reservedPosition.Add(stilloccupiedFeature.position);
                            // position refresh not needed, no need to refresh feature
                        }
                        else if (!directions.Contains(stilloccupiedFeature.direction) && stilloccupiedFeature.featureType == FeatureCollection.WallTower)
                        {
                            // direction has changed for tower, feature needs direction updating.
                            ecbBegin.SetComponent(batchIndex, stilloccupiedFeature.feature, new Rotation { Value = HexMetrics.FromToRotation(new float3(1f, 0, 0), possiblePositions[i].direction) });//quaternion.EulerXYZ(possiblePositions[i].direction) });
                            WriteBackFeatures.Add(stilloccupiedFeature);
                        }
                        else if ((!directions.Contains(stilloccupiedFeature.direction) || !positions.Contains(stilloccupiedFeature.position)) && stilloccupiedFeature.featureType == FeatureCollection.Bridge)
                        {
                            // bridge postion/direction changed, need to reapply this. don't know how yet

                            stilloccupiedFeature.position = HexMetrics.Perturb(noiseColours, stilloccupiedFeature.position, cell.wrapSize);
                            stilloccupiedFeature.direction = HexMetrics.Perturb(noiseColours, stilloccupiedFeature.direction, cell.wrapSize);
                            float length = math.distance(stilloccupiedFeature.position, stilloccupiedFeature.direction);
                            ecbBegin.SetComponent(batchIndex, stilloccupiedFeature.feature, new Translation { Value = (stilloccupiedFeature.position + stilloccupiedFeature.direction) * 0.5f });
                            ecbBegin.SetComponent(batchIndex, stilloccupiedFeature.feature, new Rotation { Value = HexMetrics.FromToRotation(new float3(0, 0, 1), stilloccupiedFeature.direction - stilloccupiedFeature.position) });
                            ecbBegin.SetComponent(batchIndex, stilloccupiedFeature.feature, new NonUniformScale { Value = new float3(1f, 1f, length * (1f / HexMetrics.bridgeDesignLength)) });
                            WriteBackFeatures.Add(stilloccupiedFeature);
                        }
                        else
                        {
                            featureNeedsPositionChange.Add(i);
                        }
                    }
                    positions.Dispose();
                    directions.Dispose();
                    for (int i = 0; i < featureNeedsPositionChange.Length; i++)
                    {
                        for (int p = 0; p < possiblePositions.Length; p++)
                        {
                            if (!reservedPosition.Contains(possiblePositions[p].position))
                            {
                                reservedPosition.Add(possiblePositions[p].position);
                                InstanceFeature(WriteBackFeatures, cell, cellFeatures[featureNeedsPositionChange[i]], possiblePositions[p]);
                                break;
                            }
                        }
                    }
                    featureNeedsPositionChange.Dispose();
                    for (int i = 0; i < clearedFeatures.Length; i++)
                    {
                        for (int p = 0; p < possiblePositions.Length; p++)
                        {
                            if (!reservedPosition.Contains(possiblePositions[p].position))
                            {
                                reservedPosition.Add(possiblePositions[p].position);
                                InstanceFeature(WriteBackFeatures, cell, cellFeatures[clearedFeatures[i]], possiblePositions[p]);
                                break;
                            }
                        }
                    }
                    clearedFeatures.Dispose();
                    for (int i = 0; i < possiblePositions.Length; i++)
                    {
                        if (reservedPosition.Contains(possiblePositions[i].position))
                        {
                            continue;
                        }
                        Feature newFeature = new Feature()
                        {
                            cellIndex = cell.Index
                        };
                        InstanceFeature(WriteBackFeatures, cell, newFeature, possiblePositions[i]);
                    }
                    reservedPosition.Dispose();
                    ecbEnd.SetBuffer<PossibleFeaturePosition>(batchIndex, BufferContainerEntity).Clear();
                    ecbEnd.RemoveComponent<RefreshCellFeatures>(batchIndex, BufferContainerEntity);
                    if (WriteBackFeatures.Length > 0)
                    {
                        ecbEnd.SetBuffer<Feature>(batchIndex, BufferContainerEntity).CopyFrom(WriteBackFeatures);
                    }
                    else
                    {
                        ecbBegin.SetBuffer<Feature>(batchIndex, BufferContainerEntity).Clear();
                    }
                    ecbBegin.AddComponent<CheckAndSpawnFeatures>(batchIndex, BufferContainerEntity);
                    WriteBackFeatures.Dispose();
                }
                //ecbBegin.AddComponent<SetFeatureVisability>(HexCellShaderData.Index, HexCellShaderData);
            }


            private void InstanceFeature(NativeList<Feature> WriteBackFeatures, HexCell cell, Feature feature, PossibleFeaturePosition possibleFeature)
            {
                bool spawn;
                (spawn, feature) = SetFeature(cell, possibleFeature, feature);
                switch (spawn)
                {
                    case true:
                        feature.feature = Entity.Null;
                        break;
                    case false:
                        WriteBackFeatures.Add(feature);
                        return;
                }

                bool toSpawn = feature.featureType switch
                {
                    FeatureCollection.Urban => feature.featureSubIndex >= 0 && feature.featureLevelIndex >= 0,
                    FeatureCollection.Farm => feature.featureSubIndex >= 0 && feature.featureLevelIndex >= 0,
                    FeatureCollection.Plant => feature.featureSubIndex >= 0 && feature.featureLevelIndex >= 0,
                    FeatureCollection.WallTower => true,
                    FeatureCollection.Bridge => true,
                    _ => false,
                };

                switch (toSpawn)
                {
                    case true:
                        WriteBackFeatures.Add(feature);
                        break;
                }
            }

            private (bool, Feature) SetFeature(HexCell cell, PossibleFeaturePosition possibleFeature, Feature newFeature)
            {
                newFeature.position = possibleFeature.position;
                newFeature.direction = possibleFeature.direction;
                if (newFeature.featureType == FeatureCollection.WallTower && newFeature.feature != Entity.Null)
                {
                    return (false, newFeature);
                }

                if ((newFeature.featureType == FeatureCollection.WallTower || possibleFeature.ReservedFor == FeatureCollection.WallTower) && newFeature.feature == Entity.Null)
                {
                    newFeature.featureType = FeatureCollection.WallTower;
                    return (true, newFeature);
                }
                if (possibleFeature.ReservedFor == FeatureCollection.WallTower && newFeature.feature != Entity.Null)
                {
                    ecbEnd.DestroyEntity(newFeature.feature.Index, newFeature.feature);
                    newFeature.feature = Entity.Null;
                    return (true, newFeature);
                }

                if ((newFeature.featureType == FeatureCollection.Bridge || possibleFeature.ReservedFor == FeatureCollection.Bridge) && newFeature.feature == Entity.Null)
                {
                    newFeature.featureType = FeatureCollection.Bridge;
                    return (true, newFeature);
                }
                if (possibleFeature.ReservedFor == FeatureCollection.Bridge && newFeature.feature != Entity.Null)
                {
                    ecbEnd.DestroyEntity(newFeature.feature.Index, newFeature.feature);
                    newFeature.feature = Entity.Null;
                    return (true, newFeature);
                }

                HexHash hash = HexMetrics.SampleHashGrid(hexHashBufferFromEntity[cell.grid], newFeature.position);

                InternalFeatureStore prefab = new InternalFeatureStore
                {
                    levelIndex = cell.urbanLevel,
                    subIndex = PickPrefab(urbanPrefabs, cell.urbanLevel, hash.a, hash.d),
                    featureType = FeatureCollection.Urban
                };
                InternalFeatureStore otherPrefab = new InternalFeatureStore
                {
                    levelIndex = cell.farmLevel,
                    subIndex = PickPrefab(farmPrefabs, cell.farmLevel, hash.b, hash.d),
                    featureType = FeatureCollection.Farm
                };
                float useHash = hash.a;

                if (prefab.subIndex != int.MinValue)
                {
                    if (otherPrefab.subIndex != int.MinValue && hash.b < hash.a)
                    {
                        prefab = otherPrefab;
                        useHash = hash.b;
                    }
                }
                else if (otherPrefab.subIndex != int.MinValue)
                {
                    prefab = otherPrefab;
                    useHash = hash.b;
                }
                otherPrefab.levelIndex = cell.plantLevel;
                otherPrefab.subIndex = PickPrefab(plantPrefabs, cell.plantLevel, hash.c, hash.d);
                otherPrefab.featureType = FeatureCollection.Plant;


                if (prefab.subIndex != int.MinValue)
                {
                    if (otherPrefab.subIndex != int.MinValue && hash.c < useHash)
                    {
                        prefab = otherPrefab;
                    }
                }
                else if (otherPrefab.subIndex != int.MinValue)
                {
                    prefab = otherPrefab;
                }
                else
                {
                    newFeature.featureLevelIndex = int.MinValue;
                    newFeature.featureSubIndex = int.MinValue;
                    newFeature.featureType = FeatureCollection.None;
                    if (newFeature.feature != Entity.Null)
                    {
                        // if we still have entity, get rid of it.
                        newFeature.featureType = FeatureCollection.None;
                        ecbEnd.DestroyEntity(newFeature.feature.Index, newFeature.feature);
                        newFeature.feature = Entity.Null;
                    }
                    return (true, newFeature);
                }

                if (newFeature.feature != Entity.Null && newFeature.featureType == prefab.featureType && newFeature.featureLevelIndex == prefab.levelIndex - 1 && newFeature.featureSubIndex == prefab.subIndex)
                {
                    // choosen prefab is same as current, update positon only
                    // need a way to tell the instance feature method not to spawn a new feature just update it
                    newFeature.direction = new float3(0f, 360f * hash.e, 0f);
                    return (false, newFeature);
                }
                if (newFeature.feature != Entity.Null)
                {
                    // existing entity is different, needs deleting.
                    newFeature.featureType = FeatureCollection.None;
                    ecbEnd.DestroyEntity(newFeature.feature.Index, newFeature.feature);
                    newFeature.feature = Entity.Null;
                }
                newFeature.direction = new float3(0f, 360f * hash.e, 0f);
                newFeature.featureLevelIndex = prefab.levelIndex - 1;
                newFeature.featureSubIndex = prefab.subIndex;
                newFeature.featureType = prefab.featureType;
                return (true, newFeature);
            }

            private int PickPrefab(NativeArray<HexFeaturePrefabContainer> collection, int level, float hash, float choice)
            {
                switch (level > 0)
                {
                    case true:
                        FeatureThresholdContainer thresholds = HexMetrics.GetFeatureThresholds(level - 1);
                        for (int i = 0; i < collection.Length; i++)
                        {
                            switch (hash < thresholds.GetLevel(i))
                            {
                                case true:
                                    return collection[i].PickIndex(choice);
                            }
                        }
                        break;
                }
                return int.MinValue;
            }

            private struct InternalFeatureStore
            {
                public int levelIndex;
                public int subIndex;
                public FeatureCollection featureType;
            }
        }
    }

    //[DisableAutoCreation]
    [UpdateAfter(typeof(UpdateFeatureDataSystem))]
    public class SpawnFeatureSystem : JobComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem commandBufferSystemEnd;
        private BeginSimulationEntityCommandBufferSystem commandBufferSystemBegin;

        private EntityQueryDesc RefreshCellFeaturesQuery = new EntityQueryDesc { All = new ComponentType[] { typeof(CheckAndSpawnFeatures) } 
        , None = new ComponentType[] { typeof(RefreshContainer), typeof(RefreshCellFeatures) }
        };
        protected override void OnCreate()
        {
            commandBufferSystemEnd = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            commandBufferSystemBegin = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (FeatureDecisionSystem.urbanCollections == null && FeatureDecisionSystem.farmCollections == null && FeatureDecisionSystem.plantCollections == null)
            {
                return inputDeps;
            }
            if (FeatureDecisionSystem.WallTower == Entity.Null && FeatureDecisionSystem.Bridge == Entity.Null)
            {
                return inputDeps;
            }
            if (HexMetrics.ActiveGridEntity == Entity.Null)
            {
                return inputDeps;
            }

            EntityQuery refreshBufferQuery = GetEntityQuery(RefreshCellFeaturesQuery);
            SpawnCellFeatureJob spawnCellFeature = new SpawnCellFeatureJob
            {
                noiseColours = HexMetrics.noiseColours,
                HexCellShaderData = HexCellShaderSystem.HexCellShaderData,

                urbanPrefabs = new NativeArray<HexFeaturePrefabContainer>(FeatureDecisionSystem.urbanCollections, Allocator.TempJob),
                farmPrefabs = new NativeArray<HexFeaturePrefabContainer>(FeatureDecisionSystem.farmCollections, Allocator.TempJob),
                plantPrefabs = new NativeArray<HexFeaturePrefabContainer>(FeatureDecisionSystem.plantCollections, Allocator.TempJob),
                WallTower = FeatureDecisionSystem.WallTower,
                Bridge = FeatureDecisionSystem.Bridge,
                featureDataType = this.GetComponentTypeHandle<FeatureDataContainer>(true),
                entityType = this.GetEntityTypeHandle(),
                scaleFromEntity = this.GetComponentDataFromEntity<NonUniformScale>(true),
                featureBufferType = this.GetBufferTypeHandle<Feature>(true),
                gridDataFromEntity = GetComponentDataFromEntity<HexGridComponent>(true),
                ecbBegin = commandBufferSystemBegin.CreateCommandBuffer().AsParallelWriter(),
                ecbEnd = commandBufferSystemEnd.CreateCommandBuffer().AsParallelWriter()
            };
            JobHandle outputDeps = spawnCellFeature.ScheduleParallel(refreshBufferQuery, 64, inputDeps);
            //JobHandle outputDeps = spawnCellFeature.Schedule(refreshBufferQuery, inputDeps);
            commandBufferSystemEnd.AddJobHandleForProducer(outputDeps);
            commandBufferSystemBegin.AddJobHandleForProducer(outputDeps);

            return outputDeps;
        }

        [BurstCompile]
        private struct SpawnCellFeatureJob : IJobEntityBatch
        {
            [ReadOnly]
            public NativeArray<float4> noiseColours;

            [ReadOnly]
            public Entity HexCellShaderData;

            [ReadOnly]
            [DeallocateOnJobCompletion]
            public NativeArray<HexFeaturePrefabContainer> urbanPrefabs;
            [ReadOnly]
            [DeallocateOnJobCompletion]
            public NativeArray<HexFeaturePrefabContainer> farmPrefabs;
            [ReadOnly]
            [DeallocateOnJobCompletion]
            public NativeArray<HexFeaturePrefabContainer> plantPrefabs;
            [ReadOnly]
            public Entity WallTower;
            [ReadOnly]
            public Entity Bridge;

            [ReadOnly]
            public ComponentTypeHandle<FeatureDataContainer> featureDataType;
            [ReadOnly]
            public EntityTypeHandle entityType;
            [ReadOnly]
            public ComponentDataFromEntity<HexGridComponent> gridDataFromEntity;
            [ReadOnly]
            public ComponentDataFromEntity<NonUniformScale> scaleFromEntity;
            [ReadOnly]
            public BufferTypeHandle<Feature> featureBufferType;

            public EntityCommandBuffer.ParallelWriter ecbBegin;
            public EntityCommandBuffer.ParallelWriter ecbEnd;
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                BufferAccessor<Feature> chunkFeatures = batchInChunk.GetBufferAccessor(featureBufferType);
                NativeArray<FeatureDataContainer>.ReadOnly cellFeatureData = batchInChunk.GetNativeArray(featureDataType).AsReadOnly();
                NativeArray<Entity>.ReadOnly cellFeatureEntities = batchInChunk.GetNativeArray(entityType).AsReadOnly();
                for (int chunkFeatureIndex = 0; chunkFeatureIndex < chunkFeatures.Length; chunkFeatureIndex++)
                {
                    DynamicBuffer<Feature> chunkFeature = chunkFeatures[chunkFeatureIndex];
                    Entity cellFeatureEntity = cellFeatureEntities[chunkFeatureIndex];
                    if (chunkFeature.Length > 0)
                    {
                        FeatureDataContainer featureDataContainer = cellFeatureData[chunkFeatureIndex];
                        int wrapSize = gridDataFromEntity[featureDataContainer.GridEntity].wrapSize;
                        for (int i = 0; i < chunkFeature.Length; i++)
                        {
                            InstanceFeature(batchIndex, wrapSize, chunkFeature[i], i, featureDataContainer.GridEntity, cellFeatureEntity);
                        }
                        ecbBegin.AddComponent<RefreshContainer>(batchIndex, cellFeatureEntity);
                    }
                    
                    ecbEnd.RemoveComponent<CheckAndSpawnFeatures>(batchIndex, cellFeatureEntity);
                }
                ecbBegin.AddComponent<SetFeatureVisability>(HexCellShaderData.Index, HexCellShaderData);
            }

            private void InstanceFeature(int batchIndex, int wrapSize, Feature feature, int featureIndexInMainBuffer, Entity grid, Entity BufferContainerEntity)
            {
                bool spawn = feature.feature == Entity.Null;
                switch (spawn)
                {
                    case false:
                        switch (feature.featureType)
                        {
                            case FeatureCollection.WallTower:
                                ecbBegin.SetComponent(batchIndex, feature.feature, new Translation { Value = feature.position });
                                ecbBegin.SetComponent(batchIndex, feature.feature, new Rotation { Value = HexMetrics.FromToRotation(new float3(1, 0, 0), feature.direction) });
                                break;
                            case FeatureCollection.Bridge:
                                feature.position = HexMetrics.Perturb(noiseColours, feature.position, wrapSize);
                                feature.direction = HexMetrics.Perturb(noiseColours, feature.direction, wrapSize);
                                float length = math.distance(feature.position, feature.direction);
                                ecbBegin.SetComponent(batchIndex, feature.feature, new Translation { Value = (feature.position + feature.direction) * 0.5f });
                                ecbBegin.SetComponent(batchIndex, feature.feature, new Rotation { Value = HexMetrics.FromToRotation(new float3(0, 0, 1), feature.direction - feature.position) });
                                ecbBegin.SetComponent(batchIndex, feature.feature, new NonUniformScale { Value = new float3(1f, 1f, length * (1f / HexMetrics.bridgeDesignLength)) });
                                break;
                            default:
                                feature.position.y += scaleFromEntity[feature.feature].Value.y * 0.5f;
                                ecbBegin.SetComponent(batchIndex, feature.feature, new Translation { Value = HexMetrics.Perturb(noiseColours, feature.position, wrapSize) });
                                ecbBegin.SetComponent(batchIndex, feature.feature, new Rotation { Value = quaternion.EulerXYZ(feature.direction) });
                                break;
                        }
                        return;
                }
                Entity toSpawn = feature.featureType switch
                {
                    FeatureCollection.Urban => urbanPrefabs[feature.featureLevelIndex][feature.featureSubIndex],
                    FeatureCollection.Farm => farmPrefabs[feature.featureLevelIndex][feature.featureSubIndex],
                    FeatureCollection.Plant => plantPrefabs[feature.featureLevelIndex][feature.featureSubIndex],
                    FeatureCollection.WallTower => WallTower,
                    FeatureCollection.Bridge => Bridge,
                    _ => Entity.Null,
                };
                switch (toSpawn == Entity.Null)
                {
                    case false:
                        Entity newSpawn = ecbBegin.Instantiate(batchIndex, toSpawn);
                        ecbBegin.AddComponent(batchIndex, newSpawn, new NewFeatureSpawn { BufferContainer = BufferContainerEntity, Index = featureIndexInMainBuffer });
                        ecbBegin.AddComponent(batchIndex, newSpawn, new Parent { Value = BufferContainerEntity });
                        ecbBegin.AddComponent(batchIndex, newSpawn, new FeatureGridInfo { GridEntity = grid });
                        switch (feature.featureType)
                        {
                            case FeatureCollection.WallTower:
                                ecbBegin.SetComponent(batchIndex, newSpawn, new Translation { Value = feature.position });
                                ecbBegin.SetComponent(batchIndex, newSpawn, new Rotation { Value = HexMetrics.FromToRotation(new float3(1, 0, 0), feature.direction) });
                                break;
                            case FeatureCollection.Bridge:
                                feature.position = HexMetrics.Perturb(noiseColours, feature.position, wrapSize);
                                feature.direction = HexMetrics.Perturb(noiseColours, feature.direction, wrapSize);
                                float length = math.distance(feature.position, feature.direction);
                                ecbBegin.SetComponent(batchIndex, feature.feature, new Translation { Value = (feature.position + feature.direction) * 0.5f });
                                ecbBegin.SetComponent(batchIndex, feature.feature, new Rotation { Value = HexMetrics.FromToRotation(new float3(0, 0, 1), feature.direction - feature.position) });
                                ecbBegin.SetComponent(batchIndex, feature.feature, new NonUniformScale { Value = new float3(1f, 1f, length * (1f / HexMetrics.bridgeDesignLength)) }); ;
                                break;
                            default:
                                feature.position.y += scaleFromEntity[toSpawn].Value.y * 0.5f;
                                ecbBegin.SetComponent(batchIndex, newSpawn, new Translation { Value = HexMetrics.Perturb(noiseColours, feature.position, wrapSize) });
                                ecbBegin.SetComponent(batchIndex, newSpawn, new Rotation { Value = quaternion.EulerXYZ(feature.direction) });
                                break;
                        }
                        break;
                }
            }

        }
    }
    
    //[DisableAutoCreation]
    [UpdateBefore(typeof(FeatureDecisionSystem))]
    [UpdateAfter(typeof(TransformSystemGroup))]
    public class UpdateFeatureDataSystem : JobComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem commandBufferSystemEnd;
        private BeginSimulationEntityCommandBufferSystem commandBufferSystemBegin;

        private EntityQueryDesc RefreshCellFeaturesQuery = new EntityQueryDesc { All = new ComponentType[] { typeof(RefreshContainer) },
            None = new ComponentType[] { typeof(CheckAndSpawnFeatures) } };

        protected override void OnCreate()
        {
            commandBufferSystemEnd = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            commandBufferSystemBegin = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
        }
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            EntityQuery refreshBufferQuery = GetEntityQuery(RefreshCellFeaturesQuery);
            UpdateFeatureDataJob updateFeatureBuffers = new UpdateFeatureDataJob
            {
                newFeatureInfoFromEntity = GetComponentDataFromEntity<NewFeatureSpawn>(true),
                featureBufferTypeHandle = GetBufferTypeHandle<Feature>(),
                childrenFromBufferTypeHandle = GetBufferTypeHandle<Child>(true),
                entityTypeHandle = GetEntityTypeHandle(),
                ecbEnd = commandBufferSystemEnd.CreateCommandBuffer().AsParallelWriter()
            };
            JobHandle outputDeps = updateFeatureBuffers.ScheduleParallel(refreshBufferQuery, 1, inputDeps);
            //JobHandle outputDeps = updateFeatureBuffers.Schedule(refreshBufferQuery, inputDeps);
            commandBufferSystemEnd.AddJobHandleForProducer(outputDeps);
            return outputDeps;

        }

        [BurstCompile]
        private struct UpdateFeatureDataJob : IJobEntityBatch
        {
            [ReadOnly]
            public ComponentDataFromEntity<NewFeatureSpawn> newFeatureInfoFromEntity;
            public BufferTypeHandle<Feature> featureBufferTypeHandle;
            [ReadOnly]
            public BufferTypeHandle<Child> childrenFromBufferTypeHandle;
            [ReadOnly]
            public EntityTypeHandle entityTypeHandle;
            public EntityCommandBuffer.ParallelWriter ecbEnd;
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                BufferAccessor<Child> childrenAccessors = batchInChunk.GetBufferAccessor(childrenFromBufferTypeHandle);
                BufferAccessor<Feature> featureAccessors = batchInChunk.GetBufferAccessor(featureBufferTypeHandle);
                NativeArray<Entity> bufferContainers = batchInChunk.GetNativeArray(entityTypeHandle);
                for (int i = 0; i < childrenAccessors.Length; i++)
                {
                    Entity bufferContainer = bufferContainers[i];
                    DynamicBuffer<Child> children = childrenAccessors[i];
                    DynamicBuffer<Feature> FeatureBuffer = featureAccessors[i];
                    for (int c = 0; c < children.Length; c++)
                    {
                        if (newFeatureInfoFromEntity.HasComponent(children[c].Value))
                        {
                            NewFeatureSpawn newFeatureEntity = newFeatureInfoFromEntity[children[c].Value];
                            Feature feature = FeatureBuffer[newFeatureEntity.Index];
                            feature.feature = children[c].Value;
                            FeatureBuffer[newFeatureEntity.Index] = feature;
                            bufferContainer = newFeatureEntity.BufferContainer;
                            ecbEnd.RemoveComponent<NewFeatureSpawn>(batchIndex, feature.feature);
                        }
                        else
                        {
                            bool cull = true;
                            for (int f = 0; f < FeatureBuffer.Length; f++)
                            {
                                if(FeatureBuffer[f].feature == children[c].Value)
                                {
                                    cull = false;
                                    break;
                                }
                            }
                            if (cull)
                            {
                                ecbEnd.DestroyEntity(batchIndex, children[c].Value);
                            }
                        }
                    }
                    ecbEnd.RemoveComponent<RefreshContainer>(batchIndex, bufferContainer);
                }
            }
        }
    }
}