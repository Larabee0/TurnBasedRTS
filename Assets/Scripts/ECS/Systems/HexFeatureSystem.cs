using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

[BurstCompile, UpdateInGroup(typeof(HexSystemGroup))]
public partial struct HexFeatureSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {

        EntityQueryBuilder builder = new(Allocator.Temp);
        NativeArray<EntityQuery> entityQueries = new(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        entityQueries[0] = builder.WithAllRW<HexFeatureSpawnedFeature, HexFeatureRequest>().WithAll<HexFeatureUpdate>().Build(ref state);

        state.RequireAnyForUpdate(entityQueries);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        new HexFeatureJob
        {
            specialPrefabs = SystemAPI.GetSingletonBuffer<HexFeatureSpecialPrefab>(true).ToNativeArray(Allocator.TempJob),
            prefabCollections = SystemAPI.GetSingleton<HexFeatureCollectionComponent>(),
            ecb = GetEndSimEntityCommandBuffer(ref state)
        }.ScheduleParallel();
    }

    [BurstCompile]
    private EntityCommandBuffer.ParallelWriter GetEndSimEntityCommandBuffer(ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
        return ecb.AsParallelWriter();
    }

}

[BurstCompile, WithAll(typeof(HexFeatureUpdate))]
public partial struct HexFeatureJob : IJobEntity
{
    [ReadOnly, DeallocateOnJobCompletion]
    public NativeArray<HexFeatureSpecialPrefab> specialPrefabs;

    public HexFeatureCollectionComponent prefabCollections;

    public EntityCommandBuffer.ParallelWriter ecb;

    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main,
        ref DynamicBuffer<HexFeatureSpawnedFeature> hexFeatureInstances,
        ref DynamicBuffer<HexFeatureRequest> hexFeatureRequests)
    {
        // fill featureRequests HashSet.
        NativeHashSet<HexFeatureRequest> featureRequestsSet = new(hexFeatureRequests.Length, Allocator.Temp);
        for (int i = 0; i < hexFeatureRequests.Length; i++)
        {
            featureRequestsSet.Add(hexFeatureRequests[i]);
        }

        // fill featureInstances Hashset
        NativeHashSet<HexFeatureRequest> featureInstancesSet = new(hexFeatureRequests.Length, Allocator.Temp);
        for (int i = 0; i < hexFeatureInstances.Length; i++)
        {
            featureInstancesSet.Add(hexFeatureInstances[i].request);
        }

        // if a feature Instance no longer exists in the request buffer, destroy the entity remove it form the buffer;
        for (int i = hexFeatureInstances.Length-1; i >= 0; i--)
        {
            if (!featureRequestsSet.Contains(hexFeatureInstances[i].request))
            {
                ecb.DestroyEntity(jobChunkIndex, hexFeatureInstances[i].instanceEntity);
                hexFeatureInstances.RemoveAt(i);
            }
        }

        // remove any features that already exist as Instances from the featureRequests buffer.
        for (int i = hexFeatureRequests.Length - 1; i >=0; i--)
        {
            if (featureInstancesSet.Contains(hexFeatureRequests[i]))
            {
                hexFeatureRequests.RemoveAt(i);
            }
        }

        // for the remaining features requests, instatiate them and append them to the featureInstances buffer via the ecb
        for (int i = 0; i < hexFeatureRequests.Length; i++)
        {
            HexFeatureRequest request = hexFeatureRequests[i];
            Entity prefab = GetFeaturePrefab(request);
            if (prefab != Entity.Null)
            {
                Entity instance = ecb.Instantiate(jobChunkIndex, prefab);
                ecb.AddComponent(jobChunkIndex, instance, new Parent { Value = main });
                ecb.AppendToBuffer(jobChunkIndex, main, new HexFeatureSpawnedFeature { instanceEntity = instance, request = request });
                ApplyTransformToInstance(jobChunkIndex, request, instance);
            }
        }

        // add back unchanged instances to the featureRequest buffer.
        for (int i = 0; i < hexFeatureInstances.Length; i++)
        {
            hexFeatureRequests.Add(hexFeatureInstances[i].request);
        }

        ecb.RemoveComponent<HexFeatureUpdate>(jobChunkIndex, main);
    }

    private void ApplyTransformToInstance(int jobChunkIndex, HexFeatureRequest request, Entity instance)
    {
        switch (request.type)
        {
            case HexFeatureType.Generic:
                ecb.SetComponent(jobChunkIndex, instance, new LocalTransform
                {
                    Scale = 1,
                    Position = request.localPosition,
                    Rotation = quaternion.LookRotation(request.directionForward, math.up())
                });
                break;
            case HexFeatureType.Bridge:
                ecb.SetComponent(jobChunkIndex, instance, new LocalTransform
                {
                    Scale = 1,
                    Position = request.localPosition,
                    Rotation = quaternion.LookRotation(request.directionForward, math.up())
                });
                ecb.AddComponent(jobChunkIndex, instance, new PostTransformMatrix
                {
                    Value = float4x4.TRS(float3.zero, quaternion.identity, request.localScale),
                });
                break;
            case HexFeatureType.Tower:
                ecb.SetComponent(jobChunkIndex, instance, new LocalTransform
                {
                    Scale = 1,
                    Position = request.localPosition,
                    Rotation = FromToRotation(math.right(), request.directionRight, math.up())
                });
                break;
            case HexFeatureType.Special:
                ecb.SetComponent(jobChunkIndex, instance, new LocalTransform
                {
                    Scale = 1,
                    Position = request.localPosition,
                    Rotation = quaternion.LookRotation(request.directionForward, math.up())
                });
                break;
        }
    }

    private quaternion FromToRotation(float3 fromDirectiom, float3 toDirection, float3 up)
    {
        return RotateTowards(quaternion.LookRotation(fromDirectiom, up), quaternion.LookRotation(toDirection, up), float.MaxValue);
    }

    private quaternion RotateTowards(quaternion from, quaternion to, float maxDegreesDelta)
    {
        float num = Angle(from, to);
        if (num == 0f)
        {
            return to;
        }
        float t = math.min(1f, maxDegreesDelta / num);
        return math.slerp(from, to, t);
    }

    private float Angle(quaternion a, quaternion b)
    {
        float f = math.dot(a, b);
        return math.degrees(math.acos(math.min(math.acos(f), 1f)) * 2f);
    }

    private Entity GetFeaturePrefab(HexFeatureRequest request) => request.type switch
    {
        HexFeatureType.Generic => request.prefab,
        HexFeatureType.Special => request.prefab,
        HexFeatureType.Bridge => prefabCollections.bridge,
        HexFeatureType.Tower => prefabCollections.wallTower,
        _ => Entity.Null,
    };
}