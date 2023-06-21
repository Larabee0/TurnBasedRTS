using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

[UpdateInGroup(typeof(HexSystemGroup)), UpdateAfter(typeof(HexChunkTriangulatorSystem))]
public partial class HexChunkMeshApplicatorSystem : SystemBase
{
    private EntityQuery TriangulatorCompleteQuery;
    private EntityQuery hexMeshInitQuery;

    protected override void OnCreate()
    {
        EntityQueryBuilder builder = new(Allocator.Temp);
        TriangulatorCompleteQuery = builder.WithAll<HexChunkTag, HexChunkRefresh, HexCellReference, HexChunkMeshEntities, HexChunkMeshUpdating>().WithNone<HexChunkCellDataCompleted, HexChunkCellWrapper>().Build(EntityManager);
        builder.Reset();
        hexMeshInitQuery = builder.WithAll<HexMeshUninitilised,HexMeshData>().Build(EntityManager);

        RequireAnyForUpdate(TriangulatorCompleteQuery, hexMeshInitQuery);

        builder.Dispose();
    }

    protected override void OnStartRunning()
    {
        RefRW<HexChunkTriangulatorArray> data = GetDataWrapperComp();
        if (!data.ValueRW.noiseColours.IsCreated)
        {
            data.ValueRW.noiseColours = HexMetrics.noiseColours;
        }
        if (!data.ValueRW.hashGrid.IsCreated)
        {
            data.ValueRW.hashGrid = new NativeArray<HexHash>(HexMetrics.hashGrid, Allocator.Persistent);
        }
    }
    
    protected override void OnUpdate()
    {
        EntityCommandBuffer ecbEnd = GetEntityCommandBuffer();

        DebugHexMeshes();

        if (!hexMeshInitQuery.IsEmpty)
        {
            InitialiseMeshes(ecbEnd);
        }

        if (!TriangulatorCompleteQuery.IsEmpty)
        {
            CompleteTriangulator(ecbEnd);
        }
    }

    private void DebugHexMeshes()
    {
        foreach ((RefRW<HexMeshDebugger> meshDebug, MaterialMeshInfo meshInfo, Entity meshEntity) in SystemAPI.Query<RefRW<HexMeshDebugger>, MaterialMeshInfo>().WithEntityAccess())
        {
            RenderMeshArray renderMeshArray = EntityManager.GetSharedComponentManaged<RenderMeshArray>(meshEntity);

            Mesh mesh = renderMeshArray.GetMesh(meshInfo);
            if (mesh.subMeshCount == 0)
            {
                meshDebug.ValueRW.subMesh = -1;
                meshDebug.ValueRW.vertexArrayCount = 0;
                meshDebug.ValueRW.triangleArrayCount = 0;
            }
            else
            {
                meshDebug.ValueRW.subMesh = meshInfo.Submesh;
                meshDebug.ValueRW.vertexArrayCount = mesh.vertexCount;
                meshDebug.ValueRW.triangleArrayCount = mesh.GetIndexCount(meshInfo.Submesh);
            }
        }
    }

    private void InitialiseMeshes(EntityCommandBuffer ecbEnd)
    {
        int hexMeshEntities = hexMeshInitQuery.CalculateEntityCount();
        NativeArray<Entity> entities = hexMeshInitQuery.ToEntityArray(Allocator.Temp);
        NativeArray<HexMeshData> meshData = hexMeshInitQuery.ToComponentDataArray<HexMeshData>(Allocator.Temp);
        Material[] materials = HexMetrics.HexMeshMaterials;
        Mesh[] meshes = new Mesh[hexMeshEntities];

        for (int i = 0; i < hexMeshEntities; i++)
        {
            meshes[i] = new Mesh() { name = string.Format("{1} Index {0}", i, meshData[i].type) };
            HexMeshData data = meshData[i];
            data.meshIndex = i;
            ecbEnd.SetComponent(entities[i], data);
        }
        var rendererSettings = new RenderMeshDescription(ShadowCastingMode.On, true);
        var renderMeshArray = new RenderMeshArray(materials, meshes);

        for (int i = 0; i < hexMeshEntities; i++)
        {
            RenderMeshUtility.AddComponents(
            entities[i],
            EntityManager,
            rendererSettings,
            renderMeshArray,
            MaterialMeshInfo.FromRenderMeshArrayIndices((int)meshData[i].type, i));
        }
        ecbEnd.RemoveComponent<HexMeshUninitilised>(entities);
    }

    private void CompleteTriangulator(EntityCommandBuffer ecbEnd)
    {
        int chunkEntities = TriangulatorCompleteQuery.CalculateEntityCount();
        NativeArray<Entity> chunks = TriangulatorCompleteQuery.ToEntityArray(Allocator.Temp);
        NativeArray<HexChunkMeshEntities> chunkMeshEntities = TriangulatorCompleteQuery.ToComponentDataArray<HexChunkMeshEntities>(Allocator.Temp);
        NativeArray<HexChunkMeshUpdating> chunkWitnesses = TriangulatorCompleteQuery.ToComponentDataArray<HexChunkMeshUpdating>(Allocator.Temp);
        RenderMeshArray renderMeshArray = EntityManager.GetSharedComponentManaged<RenderMeshArray>(chunkMeshEntities[0][0]);
        Mesh[] meshes = new Mesh[chunkEntities * 7];
        double stamp = chunkWitnesses[0].timeStamp;
        for (int i = 0, m = 0; i < chunkMeshEntities.Length; i++, m += 7)
        {
            if (stamp != chunkWitnesses[i].timeStamp)
            {
                Debug.LogError("Missaligned chunks! Mesh applicaiton will fail");
            }

            HexChunkMeshEntities meshEntities = chunkMeshEntities[i];
            for (int e = 0; e < 7; e++)
            {
                MaterialMeshInfo meshInfo = SystemAPI.GetComponent<MaterialMeshInfo>(meshEntities[e]);
                meshes[SystemAPI.GetComponent<HexMeshChunkIndex>(meshEntities[e]).meshArrayIndex] = renderMeshArray.GetMesh(meshInfo);
            }
        }

        RefRW<HexChunkTriangulatorArray> meshDataWrappers = GetDataWrapperComp();

        int wrapperIndex = GetWrapperIndex(stamp, meshDataWrappers.ValueRW.meshDataWrappers);
        if (wrapperIndex != -1)
        {
            ApplyMeshesToEntities(ecbEnd, chunks, chunkMeshEntities, chunkWitnesses, meshes, stamp, meshDataWrappers, wrapperIndex);
        }
    }

    private void ApplyMeshesToEntities(EntityCommandBuffer ecbEnd,
        NativeArray<Entity> chunks,
        NativeArray<HexChunkMeshEntities> chunkMeshEntities,
        NativeArray<HexChunkMeshUpdating> chunkWitnesses,
        Mesh[] meshes,
        double stamp,
        RefRW<HexChunkTriangulatorArray> meshDataWrappers,
        int wrapperIndex)
    {
        Mesh.ApplyAndDisposeWritableMeshData(meshDataWrappers.ValueRW.meshDataWrappers[wrapperIndex].meshDataArray, meshes);
        meshDataWrappers.ValueRW.meshDataWrappers[wrapperIndex].chunksIncluded.Dispose();
        meshDataWrappers.ValueRW.meshDataWrappers.RemoveAt(wrapperIndex);

        UnsafeList<Entity> colliderEntities = new(chunkMeshEntities.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        colliderEntities.Resize(chunkMeshEntities.Length);
        Mesh[] meshesForColliders = new Mesh[chunkMeshEntities.Length];
        for (int i = 0, m = 0; i < chunkMeshEntities.Length; i++, m += 7)
        {
            if (stamp != chunkWitnesses[i].timeStamp)
            {
                Debug.LogError("Missaligned chunks! Mesh applicaiton will fail");
            }
            ecbEnd.RemoveComponent<HexChunkMeshUpdating>(chunks[i]);
            ecbEnd.RemoveComponent<HexChunkRefresh>(chunks[i]);

            HexChunkMeshEntities meshEntities = chunkMeshEntities[i];

            meshesForColliders[i] = meshes[SystemAPI.GetComponent<HexMeshChunkIndex>(meshEntities.Terrain).meshArrayIndex];
            colliderEntities[i] = meshEntities.Terrain;

            CalculateBounds(ecbEnd, meshes, meshEntities);
        }

        RefRW<HexChunkColliderArray> data = GetColliderArrayData();
        data.ValueRW.colliderQueue.Add(new HexChunkColliderQueue() { colliderTargetMeshes = Mesh.AcquireReadOnlyMeshData(meshesForColliders), entities = colliderEntities });
    }

    private void CalculateBounds(EntityCommandBuffer ecbEnd, Mesh[] meshes, HexChunkMeshEntities meshEntities)
    {
        for (int e = 0; e < 7; e++)
        {
            int meshIndex = SystemAPI.GetComponent<HexMeshChunkIndex>(meshEntities[e]).meshArrayIndex;
            ecbEnd.RemoveComponent<HexMeshChunkIndex>(meshEntities[e]);
            if (meshes[meshIndex].subMeshCount == 0)
            {
                continue;
            }
            meshes[meshIndex].RecalculateBounds();
            AABB bounds = meshes[meshIndex].bounds.ToAABB();
            ecbEnd.SetComponent(meshEntities[e], new RenderBounds { Value = bounds });
        }
    }

    private int GetWrapperIndex(double timeStamp, UnsafeList<MeshDataWrapper> wrappers)
    {
        for (int i = 0; i < wrappers.Length; i++)
        {
            if(timeStamp == wrappers[i].TimeStamp)
            {
                return i;
            }
        }
        return -1;
    }

    private RefRW<HexChunkTriangulatorArray> GetDataWrapperComp()
    {
        return SystemAPI.GetComponentRW<HexChunkTriangulatorArray>(World.GetExistingSystem<HexChunkTriangulatorSystem>());
    }

    private RefRW<HexChunkColliderArray> GetColliderArrayData()
    {
        return SystemAPI.GetComponentRW<HexChunkColliderArray>(World.GetExistingSystem<HexChunkColliderSystem>());
    }

    private EntityCommandBuffer GetEntityCommandBuffer()
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(World.Unmanaged);
        return ecb;
    }
}
