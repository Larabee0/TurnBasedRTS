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

namespace DOTSHexagons
{
    //[DisableAutoCreation]
    public class UpdateColliderSystem : JobComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem commandBufferSystemEnd;
        private readonly EntityQueryDesc terrianColliderQuery = new EntityQueryDesc { All = new ComponentType[] { typeof(Float3ForCollider), typeof(UintForCollider), typeof(PhysicsCollider), typeof(SetCollider) } };

        protected override void OnCreate()
        {
            commandBufferSystemEnd = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            float start = UnityEngine.Time.realtimeSinceStartup;
            SetCollidersBatch colliderJob = new SetCollidersBatch
            {
                entityType = this.GetEntityTypeHandle(),
                verticesForColliderBufferType = this.GetBufferTypeHandle<Float3ForCollider>(true),
                trianglesForColliderBufferType = this.GetBufferTypeHandle<UintForCollider>(true),
                colliderTypeHandle = this.GetComponentTypeHandle<PhysicsCollider>(),
                ecbEnd = commandBufferSystemEnd.CreateCommandBuffer().AsParallelWriter()
            };

            EntityQuery colliderquery = GetEntityQuery(terrianColliderQuery);
            //JobHandle outputDeps = colliderJob.Schedule(colliderquery, inputDeps);
            JobHandle outputDeps = colliderJob.ScheduleParallel(colliderquery,32, inputDeps);
            commandBufferSystemEnd.AddJobHandleForProducer(outputDeps);
            Debug.Log("collider Job " + (UnityEngine.Time.realtimeSinceStartup - start) + "ms Entity Count " + colliderquery.CalculateEntityCount());
            return outputDeps;
        }

        [BurstCompile]
        private struct SetCollidersBatch : IJobEntityBatch
        {
            [ReadOnly]
            public EntityTypeHandle entityType;
            [ReadOnly]
            public BufferTypeHandle<Float3ForCollider> verticesForColliderBufferType;

            [ReadOnly]
            public BufferTypeHandle<UintForCollider> trianglesForColliderBufferType;

            public ComponentTypeHandle<PhysicsCollider> colliderTypeHandle;
            public EntityCommandBuffer.ParallelWriter ecbEnd;
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
               //if(batchIndex > 1000)
               //{
               //    return;
               //}
                BufferAccessor<Float3ForCollider> verticesForColliderBufferAccessor = batchInChunk.GetBufferAccessor(verticesForColliderBufferType);
                BufferAccessor<UintForCollider> trianglesForColliderBufferAccessor = batchInChunk.GetBufferAccessor(trianglesForColliderBufferType);
                NativeArray<Entity> colliderContainers = batchInChunk.GetNativeArray(entityType);
                NativeArray<PhysicsCollider> colliders = batchInChunk.GetNativeArray(colliderTypeHandle);
                PhysicsCollider collider;
                for (int i = 0; i < colliderContainers.Length; i++)
                {
                    Entity colliderContainer = colliderContainers[i];
                    NativeArray<float3> vertices = verticesForColliderBufferAccessor[i].ToNativeArray(Allocator.Temp).Reinterpret<float3>();
                    NativeArray<int> triangles = trianglesForColliderBufferAccessor[i].ToNativeArray(Allocator.Temp).Reinterpret<int>();
                    NativeArray<int3> trianglesCollider = new NativeArray<int3>(triangles.Length / 3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    for (int t = 0; t < trianglesCollider.Length; t++)
                    {
                        int startIndex = t * 3;
                        trianglesCollider[t] = new int3(triangles[startIndex], triangles[startIndex + 1], triangles[startIndex + 2]);
                    }
                    triangles.Dispose();
                    collider = colliders[i];
                    switch (collider.IsValid)
                    {
                        case true:
                            collider.Value.Dispose();
                            break;
                    }
                    collider = new PhysicsCollider()
                    {
                        Value = Unity.Physics.MeshCollider.Create(vertices, trianglesCollider)
                    };
                    colliders[i] = collider;
                    vertices.Dispose();
                    trianglesCollider.Dispose();
                    ecbEnd.SetBuffer<Float3ForCollider>(batchIndex, colliderContainer).Clear();
                    ecbEnd.SetBuffer<UintForCollider>(batchIndex, colliderContainer).Clear();
                    ecbEnd.RemoveComponent<SetCollider>(batchIndex, colliderContainer);
                }
            }
        }

        public struct Float3ForCollider : IBufferElementData
        {
            public float3 v;
        }

        public struct UintForCollider : IBufferElementData
        {
            public uint t;
        }

        public struct SetCollider : IComponentData { }
    }

    public struct ChunkIndexWithGrid : System.IEquatable<ChunkIndexWithGrid>
    {
        public int chunkIndex;
        public int EntityIndex;
        public bool Equals(ChunkIndexWithGrid other)
        {
            return other.chunkIndex == this.chunkIndex && other.EntityIndex == this.EntityIndex;
        }
    }
    [UpdateAfter(typeof(TransformSystemGroup))]
    public class UpdateMeshSystem : ComponentSystem
    {
        private EndSimulationEntityCommandBufferSystem commandBufferSystemEnd;
        private BeginSimulationEntityCommandBufferSystem commandBufferSystemBegin;
        private EntityCommandBuffer commandBufferEnd;

        private readonly EntityQueryDesc HexGridChunkQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(HexGridChunkComponent), typeof(RefreshChunk) },
            None = new ComponentType[] { typeof(Generate) }
        };

        protected override void OnCreate()
        {
            commandBufferSystemEnd = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            commandBufferSystemBegin = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
        }
        protected override void OnUpdate()
        {
            float start = UnityEngine.Time.realtimeSinceStartup;
            commandBufferEnd = commandBufferSystemEnd.CreateCommandBuffer();
            EntityQuery RefreshQuery = GetEntityQuery(HexGridChunkQuery);
            NativeArray<HexGridChunkComponent> refreshingChunkComps = RefreshQuery.ToComponentDataArray<HexGridChunkComponent>(Allocator.Temp);
            int entities = refreshingChunkComps.Length;
            int meshCount = entities * 7;
            if (meshCount < 1)
            {
                return;
            }
            NativeHashMap<int2, int> chunkProcessingIndice = new NativeHashMap<int2, int>(entities, Allocator.TempJob);
            for (int i = 0; i < entities; i++)
            {
                chunkProcessingIndice.Add(new int2(refreshingChunkComps[i].chunkIndex, refreshingChunkComps[i].gridEntity.Index), i);
            }
            refreshingChunkComps.Dispose();
            Mesh.MeshDataArray meshes = Mesh.AllocateWritableMeshData(meshCount);
            TriangulatorEverything triangulateAll = new TriangulatorEverything
            {
                
                noiseColours = HexMetrics.noiseColours,                
                chunkProcessingIndice = chunkProcessingIndice,
                chunkEntityType = this.GetEntityTypeHandle(),
                hexGridChunkComponentType = this.GetComponentTypeHandle<HexGridChunkComponent>(true),
                hexGridCellBufferType = this.GetBufferTypeHandle<HexGridCellBuffer>(true),
                hexCellBufferType = this.GetBufferFromEntity<HexCell>(true),
                childBufferType = this.GetBufferFromEntity<Child>(true),
                hexHashData = this.GetBufferFromEntity<HexHash>(true),
                featureDataComponentType = this.GetComponentDataFromEntity<FeatureDataContainer>(true),
                gridDataComponentType = this.GetComponentDataFromEntity<HexGridComponent>(true),
                meshDataArray = meshes,
                ecbBegin = commandBufferSystemBegin.CreateCommandBuffer().AsParallelWriter(),
                ecbEnd = commandBufferSystemEnd.CreateCommandBuffer().AsParallelWriter(),
            };
            JobHandle handle = new JobHandle();
            JobHandle job;
            if (BurstCompiler.IsEnabled)
            {
                job = triangulateAll.ScheduleParallel(RefreshQuery, 20, handle);
            }
            else
            {
                job = triangulateAll.Schedule(RefreshQuery, handle);
            }            
            commandBufferSystemBegin.AddJobHandleForProducer(job);
            commandBufferSystemEnd.AddJobHandleForProducer(job);
            job.Complete();
            NativeArray<HexGridChunkComponent> chunkComps = RefreshQuery.ToComponentDataArray<HexGridChunkComponent>(Allocator.Temp);
            Mesh[] updatedMeshes = new Mesh[meshCount];
            for (int i = 0; i < meshCount; i++)
            {
                updatedMeshes[i] = new Mesh();
            }

            Mesh.ApplyAndDisposeWritableMeshData(meshes, updatedMeshes);
            for (int i = 0; i < entities; i++)
            {
                HexGridChunkComponent chunkComp = chunkComps[i];
                int mesh = chunkProcessingIndice[new int2(chunkComp.chunkIndex, chunkComp.gridEntity.Index)] * 7;
                HexMeshContainer terrianMesh = new HexMeshContainer(MeshMaterial.Terrian, updatedMeshes[mesh], chunkComp.entityTerrian);
                HexMeshContainer riverMesh = new HexMeshContainer(MeshMaterial.River, updatedMeshes[mesh + 1], chunkComp.entityRiver);
                HexMeshContainer waterMesh = new HexMeshContainer(MeshMaterial.Water, updatedMeshes[mesh + 2], chunkComp.entityWater);
                HexMeshContainer waterShoreMesh = new HexMeshContainer(MeshMaterial.WaterShore, updatedMeshes[mesh + 3], chunkComp.entityWaterShore);
                HexMeshContainer estuariesMesh = new HexMeshContainer(MeshMaterial.Estuaries, updatedMeshes[mesh + 4], chunkComp.entityEstuaries);
                HexMeshContainer roadsMesh = new HexMeshContainer(MeshMaterial.Roads, updatedMeshes[mesh + 5], chunkComp.entityRoads);
                HexMeshContainer wallsMesh = new HexMeshContainer(MeshMaterial.Walls, updatedMeshes[mesh + 6], chunkComp.entityWalls);

                SetMesh(terrianMesh, true);
                SetMesh(riverMesh, false);
                SetMesh(waterMesh, false);
                SetMesh(waterShoreMesh, false);
                SetMesh(estuariesMesh, false);
                SetMesh(roadsMesh, false);
                SetMesh(wallsMesh, true);
            }
            chunkProcessingIndice.Dispose();
            chunkComps.Dispose();
            commandBufferEnd.AddComponent<HexCellShaderRefreshAll>(HexCellShaderSystem.HexCellShaderData);
            Debug.Log("Total Mesh Time " + (UnityEngine.Time.realtimeSinceStartup - start) * 1000f + "ms");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetMesh(HexMeshContainer hexMeshContainer, bool shadows = false)
        {
            RenderMeshDescription desc = shadows switch
            {
                true => new RenderMeshDescription(hexMeshContainer.mesh, HexMetrics.GetMaterial(hexMeshContainer.material), UnityEngine.Rendering.ShadowCastingMode.On, true),
                false => new RenderMeshDescription(hexMeshContainer.mesh, HexMetrics.GetMaterial(hexMeshContainer.material), UnityEngine.Rendering.ShadowCastingMode.Off, true)
            };
            RenderMeshUtility.AddComponents(hexMeshContainer.entity, commandBufferEnd, desc);
        }
    }

}