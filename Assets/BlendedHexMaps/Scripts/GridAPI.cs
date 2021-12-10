using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine.Rendering;
using Unity.Burst;

namespace DOTSHexagonsV2
{
    public class GridAPI : MonoBehaviour
    {
        [SerializeField] private InternalPrefabContainers internalPrefabs;
        public static GridAPI Instance;
        private World mainWorld;
        private EntityManager entityManager;
        private EndSimulationEntityCommandBufferSystem ecbEndSystem;
        private BeginSimulationEntityCommandBufferSystem ecbBeginSystem;
        private EntityWorldGridAPI entityWorldGridAPI;
        public static Entity ActiveGridEntity = Entity.Null;


        public Transform GridContainer;
        public List<HexGridChunk> GridChunk = new List<HexGridChunk>();

        public int cellCountX = 20;
        public int cellCountZ = 15;
        private int chunkCountX;
        private int chunkCountZ;

        public bool wrapping;
        public float startTime;
        private void Awake()
        {
            mainWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = mainWorld.EntityManager;
            ecbEndSystem = mainWorld.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = mainWorld.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            entityWorldGridAPI = mainWorld.GetOrCreateSystem<EntityWorldGridAPI>();
            entityWorldGridAPI.gameObjectWorld = Instance = this;
        }

        void Start()
        {

        }

        void Update()
        {
            if (ActiveGridEntity != Entity.Null)
            {
                InitialiseGrid(ActiveGridEntity);
                Debug.Log("Repaint Job scheduled for next frame. " + (UnityEngine.Time.realtimeSinceStartup - startTime) * 1000f + "ms");
                enabled = false;
            }
            //if (Input.GetKeyUp(KeyCode.C))
            //{
            //    startTime = Time.realtimeSinceStartup;
            //    Entity gridEntity = entityManager.CreateEntity(typeof(HexGridComponent), typeof(HexGridChild), typeof(HexCell), typeof(HexGridChunkBuffer), typeof(HexGridUnInitialised), typeof(HexHash));
            //    ActiveGridEntity = gridEntity;
            //    CreateMapDataFullJob(gridEntity, 5, 32, 24, true);
            //}
        }

        public void InitialiseGrid(Entity grid)
        {
            HexGridComponent HexComp = entityManager.GetComponentData<HexGridComponent>(grid);
            DynamicBuffer<HexGridChunkBuffer> chunkBuffer = entityManager.GetBuffer<HexGridChunkBuffer>(grid);

            if (GridChunk.Count != chunkBuffer.Length)
            {
                CreateOrModifyGrid(chunkBuffer);
            }
            else
            {
                for (int i = 0; i < GridChunk.Count; i++)
                {
                    GridChunk[i].ChunkIndex = chunkBuffer[i].ChunkIndex;
                }
            }
            for (int i = 0; i < chunkBuffer.Length; i++)
            {
                HexGridChunkComponent comp = entityManager.GetComponentData<HexGridChunkComponent>(chunkBuffer[i].ChunkEntity);
                EntityCommandBuffer ecb = ecbBeginSystem.CreateCommandBuffer();
                ecb.AddComponent<RepaintNow>(comp.entityTerrian);
                ecb.AddComponent<RepaintNow>(comp.entityRiver);
                ecb.AddComponent<RepaintNow>(comp.entityWater);

                ecb.AddComponent<RepaintNow>(comp.entityWaterShore);
                ecb.AddComponent<RepaintNow>(comp.entityEstuaries);
                ecb.AddComponent<RepaintNow>(comp.entityRoads);

                ecb.AddComponent<RepaintNow>(comp.entityWalls);
            }
        }

        private void CreateOrModifyGrid(DynamicBuffer<HexGridChunkBuffer> chunkBuffer)
        {
            if (GridChunk.Count < chunkBuffer.Length)
            {
                int i = 0;
                for (; i < GridChunk.Count; i++)
                {
                    GridChunk[i].ChunkIndex = chunkBuffer[i].ChunkIndex;
                }

                for (; i < chunkBuffer.Length; i++)
                {
                    GridChunk.Add(Instantiate(internalPrefabs.GridChunkPrefab, GridContainer));
                    GridChunk[i].ChunkIndex = chunkBuffer[i].ChunkIndex;
                }
            }
            else if (GridChunk.Count > chunkBuffer.Length)
            {
                for (int i = GridChunk.Count - 1; i > chunkBuffer.Length - 1; i++)
                {
                    Destroy(GridChunk[i].gameObject);
                    GridChunk.RemoveAt(i);
                }

                for (int i = 0; i < GridChunk.Count; i++)
                {
                    GridChunk[i].ChunkIndex = chunkBuffer[i].ChunkIndex;
                }
            }
        }

        private void DestroyGrid()
        {
            for (int i = 0; i < GridChunk.Count; i++)
            {
                Destroy(GridChunk[i].gameObject);
            }
            GridChunk.Clear();
        }

        public bool CreateMapDataFullJob(Entity grid, uint seed, int x, int z, bool wrapping)
        {
            cellCountX = x;
            cellCountZ = z;
            chunkCountX = cellCountX / HexFunctions.chunkSizeX;
            chunkCountZ = cellCountZ / HexFunctions.chunkSizeZ;

            this.wrapping = wrapping;
            int wrapSize = wrapping ? cellCountX : 0;
            if (x <= 0 || x % HexFunctions.chunkSizeX != 0 || z <= 0 || z % HexFunctions.chunkSizeZ != 0)
            {
                Debug.LogError("Unsupported map size.");
                return false;
            }
            entityManager.SetComponentData(grid, new HexGridComponent
            {
                cellCountX = cellCountX,
                cellCountZ = cellCountZ,
                cellCount = cellCountZ * cellCountX,
                chunkCountX = chunkCountX,
                chunkCountZ = chunkCountZ,
                chunkCount = chunkCountX * chunkCountZ,
                seed = seed,
                wrapping = wrapping,
                wrapSize = wrapSize,
                gridEntity = grid,
                currentCentreColumnIndex = -1,
            });
            NativeArray<HexHash> hasGrid = HexFunctions.InitializeHashGrid(seed);
            entityManager.GetBuffer<HexHash>(grid).CopyFrom(hasGrid);
            hasGrid.Dispose();
            return true;
        }
    }


    [UpdateInGroup(typeof(HexGridV2SystemGroup))]
    [UpdateBefore(typeof(HexGridCreateColumnsSystem))]
    public class EntityWorldGridAPI : JobComponentSystem
    {
        public EndSimulationEntityCommandBufferSystem ecbEndSystem;
        public BeginSimulationEntityCommandBufferSystem ecbBeginSystem;

        private EntityQuery repaintChunkQuery;

        public GridAPI gameObjectWorld;

        protected override void OnCreate()
        {
            ecbEndSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            repaintChunkQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexRenderer), typeof(RepaintNow), typeof(RepaintScheduled) } });
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            Mesh.MeshDataArray meshes = Mesh.AllocateWritableMeshData(repaintChunkQuery.CalculateEntityCount());
            NativeArray<HexRenderer> renderers = repaintChunkQuery.ToComponentDataArray<HexRenderer>(Allocator.Temp);
            NativeArray<HexMeshIndex> meshIndices = new NativeArray<HexMeshIndex>(meshes.Length, Allocator.Temp);
            for (int i = 0; i < meshIndices.Length; i++)
            {
                HexMeshIndex meshIndex = meshIndices[i];
                meshIndex.Value = i;
                meshIndices[i] = meshIndex;
            }
            EntityManager.AddComponentData(repaintChunkQuery, meshIndices);
            WriteMapMeshData meshWriteJob = new WriteMapMeshData
            {
                verticesTypeHandle = GetBufferTypeHandle<HexGridVertex>(true),
                trianglesTypeHandle = GetBufferTypeHandle<HexGridTriangles>(true),
                cellIndicesTypeHandle = GetBufferTypeHandle<HexGridIndices>(true),
                weightsTypeHandle = GetBufferTypeHandle<HexGridWeights>(true),
                uv2TypeHandle = GetBufferTypeHandle<HexGridUV2>(true),
                uv4TypeHandle = GetBufferTypeHandle<HexGridUV4>(true),
                rendererTypeHandle = GetComponentTypeHandle<HexRenderer>(true),
                meshIndexTypeHandle = GetComponentTypeHandle<HexMeshIndex>(true),
                entityTypeHandle = GetEntityTypeHandle(),
                meshDataArray = meshes,
                ecbEnd = ecbEndSystem.CreateCommandBuffer().AsParallelWriter()
            };

            //JobHandle MidRunHandle = meshWriteJob.ScheduleParallel(repaintChunkQuery, 1, inputDeps);
            JobHandle MidRunHandle = meshWriteJob.Schedule(repaintChunkQuery, inputDeps);
            ecbEndSystem.AddJobHandleForProducer(MidRunHandle);
            MidRunHandle.Complete();

            Mesh[] updatedMeshes = new Mesh[meshes.Length];
            for (int i = 0; i < meshes.Length; i++)
            {
                updatedMeshes[i] = new Mesh();
            }
            Mesh.ApplyAndDisposeWritableMeshData(meshes, updatedMeshes);

            List<HexGridChunk> chunks = gameObjectWorld.GridChunk;

            NativeArray<Entity> entities = repaintChunkQuery.ToEntityArray(Allocator.Temp);
            updatedMeshes[0].Clear();
            updatedMeshes[0].SetVertices(EntityManager.GetBuffer<HexGridVertex>(entities[0]).Reinterpret<float3>().AsNativeArray());
            updatedMeshes[0].SetColors(EntityManager.GetBuffer<HexGridWeights>(entities[0]).Reinterpret<float4>().AsNativeArray());
            updatedMeshes[0].SetUVs(2,EntityManager.GetBuffer<HexGridIndices>(entities[0]).Reinterpret<float3>().AsNativeArray());
            updatedMeshes[0].SetTriangles(EntityManager.GetBuffer<HexGridTriangles>(entities[0]).Reinterpret<int>().AsNativeArray().ToArray(), 0);

            //updatedMeshes[0].RecalculateNormals();
            updatedMeshes[0].RecalculateBounds();
            //updatedMeshes[0].RecalculateTangents();
            for (int i = 0; i < 1; i++)
            {
                HexRenderer renderer = renderers[i];
                Mesh mesh = updatedMeshes[meshIndices[i].Value];
               // mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                int meshTriangles = mesh.triangles.Length;
                int expectedTriangles = EntityManager.GetBuffer<HexGridTriangles>(entities[i]).Length;
                Debug.Log("Expected " + expectedTriangles + " triangles, got " + meshTriangles + ". Difference: " + (meshTriangles - expectedTriangles));
                HexGridChunk chunk = chunks[renderer.ChunkIndex];
                switch (renderer.rendererID)
                {
                    case RendererID.Terrian:
                        chunk.TerrianMesh = mesh;
                        break;
                    case RendererID.River:
                        chunk.RiverMesh = mesh;
                        break;
                    case RendererID.Water:
                        chunk.WaterMesh = mesh;
                        break;
                    case RendererID.WaterShore:
                        chunk.WaterShoreMesh = mesh;
                        break;
                    case RendererID.Estuaries:
                        chunk.EstuariesMesh = mesh;
                        break;
                    case RendererID.Roads:
                        chunk.RoadsMesh = mesh;
                        break;
                    case RendererID.Walls:
                        chunk.WallsMesh = mesh;
                        break;
                }
            }

            Debug.Log("Repaint Job Run, total time since start:" + (UnityEngine.Time.realtimeSinceStartup - gameObjectWorld.startTime) * 1000f + "ms");
            return inputDeps;
        }

        [BurstCompile]
        private struct WriteMapMeshData : IJobEntityBatchWithIndex
        {
            [ReadOnly]
            public BufferTypeHandle<HexGridVertex> verticesTypeHandle;
            [ReadOnly]
            public BufferTypeHandle<HexGridTriangles> trianglesTypeHandle;

            [ReadOnly]
            public BufferTypeHandle<HexGridIndices> cellIndicesTypeHandle;
            [ReadOnly]
            public BufferTypeHandle<HexGridWeights> weightsTypeHandle;

            [ReadOnly]
            public BufferTypeHandle<HexGridUV2> uv2TypeHandle;
            [ReadOnly]
            public BufferTypeHandle<HexGridUV4> uv4TypeHandle;

            [ReadOnly]
            public ComponentTypeHandle<HexRenderer> rendererTypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<HexMeshIndex> meshIndexTypeHandle;

            [ReadOnly]
            public EntityTypeHandle entityTypeHandle;

            public Mesh.MeshDataArray meshDataArray;

            public EntityCommandBuffer.ParallelWriter ecbEnd;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex, int index)
            {
                NativeArray<Entity> entities = batchInChunk.GetNativeArray(entityTypeHandle);
                NativeArray<HexRenderer> hexRenderers = batchInChunk.GetNativeArray(rendererTypeHandle);
                NativeArray<HexMeshIndex> meshIndices = batchInChunk.GetNativeArray(meshIndexTypeHandle);
                BufferAccessor<HexGridVertex> verticesBA = batchInChunk.GetBufferAccessor(verticesTypeHandle);
                BufferAccessor<HexGridTriangles> trianglesBA = batchInChunk.GetBufferAccessor(trianglesTypeHandle);
                NativeArray<VertexAttributeDescriptor> VertexDescriptors;
                for (int i = 0; i < verticesBA.Length; i++)
                {
                    HexRenderer hexRenderer = hexRenderers[i];
                    DynamicBuffer<HexGridVertex> verticesDB = verticesBA[i];
                    DynamicBuffer<HexGridTriangles> trianglesDB = trianglesBA[i];
                    int meshIndex = meshIndices[i].Value;
                    if (hexRenderer.rendererID != RendererID.Walls)
                    {
                        BufferAccessor<HexGridWeights> weightsBA = batchInChunk.GetBufferAccessor(weightsTypeHandle);
                        BufferAccessor<HexGridIndices> cellIndicesBA = batchInChunk.GetBufferAccessor(cellIndicesTypeHandle);

                        if (hexRenderer.rendererID == RendererID.Terrian || hexRenderer.rendererID == RendererID.Water) // MeshData
                        {
                            VertexDescriptors = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp);
                            VertexDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
                            VertexDescriptors[1] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, 1);
                            VertexDescriptors[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 3, 2);

                            Mesh.MeshData meshData = meshDataArray[meshIndex];
                            meshData.SetVertexBufferParams(verticesDB.Length, VertexDescriptors);
                            meshData.SetIndexBufferParams(trianglesDB.Length, IndexFormat.UInt32);
                            meshData.GetVertexData<HexGridVertex>(0).CopyFrom(verticesDB.AsNativeArray());
                            meshData.GetVertexData<HexGridWeights>(1).CopyFrom(weightsBA[i].AsNativeArray());
                            meshData.GetVertexData<HexGridIndices>(2).CopyFrom(cellIndicesBA[i].AsNativeArray());
                            meshData.GetIndexData<HexGridTriangles>().CopyFrom(trianglesDB.AsNativeArray());
                            meshData.subMeshCount = 1;
                            meshData.SetSubMesh(0, new SubMeshDescriptor(0, trianglesDB.Length, MeshTopology.Triangles), MeshUpdateFlags.Default);
                        }
                        else if (hexRenderer.rendererID == RendererID.Estuaries) // MeshData2UV
                        {
                            BufferAccessor<HexGridUV4> uv4BA = batchInChunk.GetBufferAccessor(uv4TypeHandle);
                            VertexDescriptors = new NativeArray<VertexAttributeDescriptor>(5, Allocator.Temp);
                            VertexDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
                            VertexDescriptors[1] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, 1);
                            VertexDescriptors[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 3);
                            VertexDescriptors[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2, 3);
                            VertexDescriptors[4] = new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 3, 2);

                            Mesh.MeshData meshData = meshDataArray[meshIndex];
                            meshData.SetVertexBufferParams(verticesDB.Length, VertexDescriptors);
                            meshData.SetIndexBufferParams(trianglesDB.Length, IndexFormat.UInt32);
                            meshData.GetVertexData<HexGridVertex>(0).CopyFrom(verticesDB.AsNativeArray());
                            meshData.GetVertexData<HexGridWeights>(1).CopyFrom(weightsBA[i].AsNativeArray());
                            meshData.GetVertexData<HexGridIndices>(2).CopyFrom(cellIndicesBA[i].AsNativeArray());
                            meshData.GetVertexData<HexGridUV4>(3).CopyFrom(uv4BA[i].AsNativeArray());
                            meshData.GetIndexData<HexGridTriangles>().CopyFrom(trianglesDB.AsNativeArray());
                            meshData.subMeshCount = 1;
                            meshData.SetSubMesh(0, new SubMeshDescriptor(0, trianglesDB.Length, MeshTopology.Triangles));
                        }
                        else // MeshDataUV
                        {
                            BufferAccessor<HexGridUV2> uv2BA = batchInChunk.GetBufferAccessor(uv2TypeHandle);
                            VertexDescriptors = new NativeArray<VertexAttributeDescriptor>(4, Allocator.Temp);
                            VertexDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
                            VertexDescriptors[1] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, 1);
                            VertexDescriptors[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 3);
                            VertexDescriptors[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 3, 2);

                            Mesh.MeshData meshData = meshDataArray[meshIndex];
                            meshData.SetVertexBufferParams(verticesDB.Length, VertexDescriptors);
                            meshData.SetIndexBufferParams(trianglesDB.Length, IndexFormat.UInt32);
                            meshData.GetVertexData<HexGridVertex>(0).CopyFrom(verticesDB.AsNativeArray());
                            meshData.GetVertexData<HexGridWeights>(1).CopyFrom(weightsBA[i].AsNativeArray());
                            meshData.GetVertexData<HexGridIndices>(2).CopyFrom(cellIndicesBA[i].AsNativeArray());
                            meshData.GetVertexData<HexGridUV2>(3).CopyFrom(uv2BA[i].AsNativeArray());
                            meshData.GetIndexData<HexGridTriangles>().CopyFrom(trianglesDB.AsNativeArray());
                            meshData.subMeshCount = 1;
                            meshData.SetSubMesh(0, new SubMeshDescriptor(0, trianglesDB.Length, MeshTopology.Triangles));
                        }
                    }
                    else // MeshBasic (walls)
                    {
                        VertexDescriptors = new NativeArray<VertexAttributeDescriptor>(1, Allocator.Temp);
                        VertexDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
                        Mesh.MeshData meshData = meshDataArray[meshIndex];
                        meshData.SetVertexBufferParams(verticesDB.Length, VertexDescriptors);
                        meshData.SetIndexBufferParams(trianglesDB.Length, IndexFormat.UInt32);
                        meshData.GetVertexData<HexGridVertex>(0).CopyFrom(verticesDB.AsNativeArray());
                        meshData.GetIndexData<HexGridTriangles>().CopyFrom(trianglesDB.AsNativeArray());
                        meshData.subMeshCount = 1;
                        meshData.SetSubMesh(0, new SubMeshDescriptor(0, trianglesDB.Length, MeshTopology.Triangles));
                    }

                    ecbEnd.RemoveComponent<RepaintNow>(entities[i].Index, entities[i]);
                    ecbEnd.RemoveComponent<RepaintScheduled>(entities[i].Index, entities[i]);
                    ecbEnd.RemoveComponent<HexMeshIndex>(entities[i].Index, entities[i]);
                    VertexDescriptors.Dispose();
                }
            }
        }
    }
}