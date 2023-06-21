using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Job to spawn features and rotate & move them to the correct position and give them the correct parent.
/// The job also decides whether features need to be replaced or can stay even if the chunk has been retriangulated.
/// 
/// It will compare the features it previously had spawned, to the newly requested features by the retriangulation,
/// skipping any features of exactly the same type that it has already spawned in the same position & rotation.
/// 
/// </summary>
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
        NativeHashSet<uint> featureRequestsSet = new(hexFeatureRequests.Length, Allocator.Temp);
        for (int i = 0; i < hexFeatureRequests.Length; i++)
        {
            featureRequestsSet.Add(hexFeatureRequests[i].Hash);
        }

        // fill featureInstances Hashset
        NativeHashSet<uint> featureInstancesSet = new(hexFeatureRequests.Length, Allocator.Temp);
        for (int i = 0; i < hexFeatureInstances.Length; i++)
        {
            featureInstancesSet.Add(hexFeatureInstances[i].hash);
        }

        // if a feature Instance no longer exists in the request buffer, destroy the entity remove it form the buffer;
        for (int i = hexFeatureInstances.Length-1; i >= 0; i--)
        {
            if (!featureRequestsSet.Contains(hexFeatureInstances[i].hash))
            {
                ecb.DestroyEntity(jobChunkIndex, hexFeatureInstances[i].instanceEntity);
                hexFeatureInstances.RemoveAt(i);
            }
        }

        // remove any features that already exist as Instances from the featureRequests buffer.
        NativeList<HexFeatureRequest> instancedRequests = new(hexFeatureRequests.Length, Allocator.Temp);
        for (int i = hexFeatureRequests.Length - 1; i >=0; i--)
        {
            if (featureInstancesSet.Contains(hexFeatureRequests[i].Hash))
            {
                instancedRequests.Add(hexFeatureRequests[i]);
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
                ecb.AppendToBuffer(jobChunkIndex, main, new HexFeatureSpawnedFeature { instanceEntity = instance, hash = request.Hash });
                ApplyTransformToInstance(jobChunkIndex, request, instance);
            }
        }

        // add back unchanged instances to the featureRequest buffer.
        for (int i = 0; i < instancedRequests.Length; i++)
        {
            hexFeatureRequests.Add(instancedRequests[i]);
        }

        ecb.RemoveComponent<HexFeatureUpdate>(jobChunkIndex, main);
    }

    /// <summary>
    /// Sets the correct transform settings for a feature based on its type
    /// </summary>
    /// <param name="jobChunkIndex">Sorting index needed for the entity command buffer</param>
    /// <param name="request">The request that produced this feature</param>
    /// <param name="instance">The target feature instance</param>
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
                // bridges are scaled nonuniformly to bridge the gap between rivers, so a PTM is needed, this is only having changes in scale
                // hence we are using float3.zero and quaternion.identity for the rest of hte TRS components.
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

    /// <summary>
    /// In the sample project the Tower positioning set UnityEngine.Transform.Right, which is not a built in feature of 
    /// Unity.Transforms so this is a DOTS version of how setting UnityEngine.Transform.Right works behind the scenes.
    /// </summary>
    /// <param name="fromDirectiom">Starting direction</param>
    /// <param name="toDirection">Target direction</param>
    /// <param name="up">Normal plane</param>
    /// <returns>Rotation of the product of RotateTowards</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private quaternion FromToRotation(float3 fromDirectiom, float3 toDirection, float3 up)
    {
        return RotateTowards(quaternion.LookRotation(fromDirectiom, up), quaternion.LookRotation(toDirection, up), float.MaxValue);
    }

    /// <summary>
    /// Yeah there is no Unity.Mathematics.RotateTowards for Unity.Mathematics.quaternion. We could use UnityEngine.Mathf or
    /// UnityEngine.Quaternion.RotateTowards as quaternion is implicitly convertable to Quaternion, but methods from Mathf, are not intrinsically
    /// Burst optimised, where as Unity.Mathematics
    /// </summary>
    /// <param name="from">Start rotation</param>
    /// <param name="to">End Rotation</param>
    /// <param name="maxDegreesDelta">Max amount of degrees to move this call</param>
    /// <returns>Resultant rotation</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    /// <summary>
    /// measure the angle in degrees between two quaternions Again because Unity.Mathematics lacks the method
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns>Angle between a and b</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float Angle(quaternion a, quaternion b)
    {
        float f = math.dot(a, b);
        return math.degrees(math.acos(math.min(math.acos(f), 1f)) * 2f);
    }

    /// <summary>
    /// Depending on if the feature type, the prefab will be within the <see cref="HexFeatureRequest"/>
    /// componnet, or from the <see cref="HexFeatureCollectionComponent"/> because thats how I chose to do it.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Entity GetFeaturePrefab(HexFeatureRequest request) => request.type switch
    {
        HexFeatureType.Generic => request.prefab,
        HexFeatureType.Special => request.prefab,
        HexFeatureType.Bridge => prefabCollections.bridge,
        HexFeatureType.Tower => prefabCollections.wallTower,
        _ => Entity.Null,
    };
}