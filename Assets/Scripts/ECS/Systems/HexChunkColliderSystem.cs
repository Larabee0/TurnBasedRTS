using Unity.Jobs;
using Unity.Physics;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Collider = Unity.Physics.Collider;

/// <summary>
/// Add collider update component to entities expecting colliders, remove in application job
/// then add query for update reqirement including this component to prevent the system from always running.
/// 
/// </summary>
[UpdateInGroup(typeof(HexSystemGroup)), UpdateBefore(typeof(HexChunkMeshApplicatorSystem))]
public partial class HexChunkColliderSystem : SystemBase
{
    private EntityQuery removeExistingColliderQuery;
    private EntityQuery handleColliderRequest;
    private EntityQuery scheduleColliderDestruction;

    protected override void OnCreate()
    {
        EntityManager.AddComponentData(SystemHandle, new HexChunkColliderArray
        {
            colliderQueue = new UnsafeList<HexChunkColliderQueue>(1, Allocator.Persistent)
        });

        EntityManager.AddComponentData(SystemHandle, new HexChunkColliderBatches());
        EntityQueryBuilder builder = new(Allocator.Temp);
        removeExistingColliderQuery = builder.WithAll<PhysicsCollider,HexChunkColliderRebuild>().WithNone<PhysicsWorldIndex>().Build(EntityManager);
        builder.Reset();
        handleColliderRequest = builder.WithAll<HexChunkMeshEntities,HexChunkColliderRebuildRequest>().WithNone<HexChunkColliderRebuild>().Build(EntityManager);
        builder.Reset();
        scheduleColliderDestruction = builder.WithAll<HexChunkColliderReference, HexMeshData, HexChunkColliderRebuild>().Build(EntityManager);
        builder.Reset();
        EntityQuery generalUpdateQuery = builder.WithAny<HexChunkColliderRebuildRequest, HexChunkColliderRebuild>().Build(EntityManager);

        RequireAnyForUpdate(removeExistingColliderQuery,handleColliderRequest,scheduleColliderDestruction, generalUpdateQuery);
        builder.Dispose();
    }

    protected override void OnDestroy()
    {
        foreach (PhysicsCollider collider in SystemAPI.Query<PhysicsCollider>().WithAll<HexChunkCollier>())
        {
            if (collider.IsValid)
            {
                collider.Value.Dispose();
            }
        }

        HexChunkColliderArray data = EntityManager.GetComponentData<HexChunkColliderArray>(SystemHandle);
        if (data.colliderQueue.Length > 0)
        {
            for (int i = 0; i < data.colliderQueue.Length; i++)
            {
                data.colliderQueue[i].entities.Dispose();
                data.colliderQueue[i].colliderTargetMeshes.Dispose();
            }
        }

        data.colliderQueue.Dispose();

        HexChunkColliderBatches colliderBatchComp = EntityManager.GetComponentData<HexChunkColliderBatches>(SystemHandle);
        if (colliderBatchComp.batches.Count > 0)
        {
            for (int i = 0; i < colliderBatchComp.batches.Count; i++)
            {
                colliderBatchComp.batches[i].Dispose();
            }
        }
        colliderBatchComp.batches.Clear();
    }

    protected override void OnUpdate()
    {
        RefRW<HexChunkColliderArray> colliderDataArray = GetColliderArrayData();
        EntityCommandBuffer.ParallelWriter ecbBegin = GetBeginEntityCommandBuffer();

        ColliderJobs();

        HexChunkColliderBatches colliderBatches = GetColliderBatches();

        UpdateColliderBatchTasks(colliderBatches, ecbBegin);

        if (colliderDataArray.ValueRW.colliderQueue.Length > 0)
        {
            ScheduleThreadingColliders(colliderBatches, colliderDataArray);
        }

    }

    private void ColliderJobs()
    {
        EntityCommandBuffer.ParallelWriter ecbEnd = GetEndEntityCommandBuffer();
        new RemoveExistingColliderQueryJob
        {
            ecbEnd = ecbEnd
        }.ScheduleParallel();

        new HandleColliderRequestJob
        {
            ecbEnd = ecbEnd
        }.ScheduleParallel();

        new ScheduleColliderDestructionJob
        {
            ecbEnd = ecbEnd
        }.ScheduleParallel();
    }

    private void ScheduleThreadingColliders(HexChunkColliderBatches colliderBatchData, RefRW<HexChunkColliderArray> colliderDataArray)
    {
        for (int i = 0; i < colliderDataArray.ValueRW.colliderQueue.Length; i++)
        {
            HexChunkColliderBatch newBatch = new()
            {
                batch = colliderDataArray.ValueRW.colliderQueue[i],
            };
            colliderBatchData.batches.Add(newBatch);

            //Debug.LogFormat("Scheduling bake of {0} colliders batches", newBatch.batch.entities.Length);
            newBatch.Schedule();
        }
        colliderDataArray.ValueRW.colliderQueue.Clear();

    }

    private void UpdateColliderBatchTasks(HexChunkColliderBatches colliderBatchData, EntityCommandBuffer.ParallelWriter ecb)
    {
        bool anyComplete = false;
        for (int i = 0; i < colliderBatchData.batches.Count; i++)
        {
            if (colliderBatchData.batches[i].AllCompleted)
            {
                anyComplete = true;
            }
        }
        if (anyComplete && removeExistingColliderQuery.IsEmpty)
        {
            CompleteColliderBatch(colliderBatchData, ecb);
        }
    }

    private void CompleteColliderBatch(HexChunkColliderBatches colliderBatchData, EntityCommandBuffer.ParallelWriter ecb)
    {
        NativeHashSet<Entity> chunks = new(colliderBatchData.batches.Count, Allocator.Temp);
        NativeList<Entity> entities = new(colliderBatchData.batches.Count, Allocator.TempJob);
        NativeList<BlobAssetReference<Collider>> physicsBlobs = new(colliderBatchData.batches.Count, Allocator.TempJob);
        for (int i = colliderBatchData.batches.Count - 1; i >= 0; i--)
        {
            if (colliderBatchData.batches[i].AllCompleted)
            {
                colliderBatchData.batches[i].GetCompletedData(physicsBlobs, entities, chunks);
                colliderBatchData.batches[i].Dispose();
                colliderBatchData.batches.RemoveAt(i);
            }
        }

        if (entities.Length > 0)
        {
            Dependency = physicsBlobs.Dispose(entities.Dispose(new ColliderApplicatorJob
            {
                colliderPrefab = SystemAPI.GetSingleton<HexPrefabsComponent>().hexChunkCollider,
                colliderChunkTargetEntities = entities,
                physicsBlobs = physicsBlobs,
                ecbEnd = ecb
            }.Schedule(entities.Length, 4, Dependency)));
        }
        else
        {
            entities.Dispose();
            physicsBlobs.Dispose();
        }
    }

    private RefRW<HexChunkColliderArray> GetColliderArrayData()
    {
        return SystemAPI.GetComponentRW<HexChunkColliderArray>(SystemHandle);
    }

    private HexChunkColliderBatches GetColliderBatches()
    {
        return EntityManager.GetComponentData<HexChunkColliderBatches>(SystemHandle);
    }

    private EntityCommandBuffer.ParallelWriter GetEndEntityCommandBuffer()
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(World.Unmanaged);
        return ecb.AsParallelWriter();
    }

    private EntityCommandBuffer.ParallelWriter GetBeginEntityCommandBuffer()
    {
        var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(World.Unmanaged);
        return ecb.AsParallelWriter();
    }
    private EntityCommandBuffer.ParallelWriter GetFixedEntityCommandBuffer()
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(World.Unmanaged);
        return ecb.AsParallelWriter();
    }
}
