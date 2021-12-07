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
        public static Entity ActiveGridEntity;


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
            if (Input.GetKeyUp(KeyCode.C))
            {
                startTime = Time.realtimeSinceStartup;
                Entity gridEntity = entityManager.CreateEntity(typeof(HexGridComponent), typeof(HexGridChild), typeof(HexCell), typeof(HexGridChunkBuffer), typeof(HexGridUnInitialised), typeof(HexHash));
                ActiveGridEntity = gridEntity;
                CreateMapDataFullJob(gridEntity, 5, 32, 24, true);
            }
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

        public struct RepaintNowJob : IJobParallelFor
        {
            [ReadOnly][DeallocateOnJobCompletion]
            public  NativeArray<HexGridChunkComponent> chunkComps;
            public EntityCommandBuffer.ParallelWriter ecbBegin;

            public void Execute(int batchIndex)
            {
                for (int i = 0; i < chunkComps.Length; i++)
                {
                    HexGridChunkComponent comp = chunkComps[i];
                    ecbBegin.AddComponent<RepaintNow>(batchIndex, comp.entityTerrian);
                    ecbBegin.AddComponent<RepaintNow>(batchIndex, comp.entityTerrian);
                }
            }
        }
    }


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
            repaintChunkQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexRenderer), typeof(RepaintNow) } });
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            Mesh.MeshDataArray meshes = Mesh.AllocateWritableMeshData(repaintChunkQuery.CalculateEntityCount() * 7);
            NativeArray<HexRenderer> renderers = repaintChunkQuery.ToComponentDataArray<HexRenderer>(Allocator.Temp);
            WriteMapMeshData meshWriteJob = new WriteMapMeshData
            {
                verticesTypeHandle = GetBufferTypeHandle<HexGridVertex>(true),
                trianglesTypeHandle = GetBufferTypeHandle<HexGridTriangles>(true),
                cellIndicesTypeHandle = GetBufferTypeHandle<HexGridIndices>(true),
                weightsTypeHandle = GetBufferTypeHandle<HexGridWeights>(true),
                uv2TypeHandle = GetBufferTypeHandle<HexGridUV2>(true),
                uv4TypeHandle = GetBufferTypeHandle<HexGridUV4>(true),
                entityTypeHandle = GetEntityTypeHandle(),
                meshDataArray = meshes,
                ecbEnd = ecbEndSystem.CreateCommandBuffer().AsParallelWriter()
            };

            JobHandle MidRunHandle = meshWriteJob.ScheduleParallel(repaintChunkQuery, 1,inputDeps);
            ecbEndSystem.AddJobHandleForProducer(MidRunHandle);
            MidRunHandle.Complete();

            Mesh[] updatedMeshes = new Mesh[meshes.Length];
            for (int i = 0; i < meshes.Length; i++)
            {
                updatedMeshes[i] = new Mesh();
            }
            Mesh.ApplyAndDisposeWritableMeshData(meshes, updatedMeshes);

            List<HexGridChunk> chunks = gameObjectWorld.GridChunk;

            for (int i = 0; i < renderers.Length; i++)
            {
                Mesh mesh = updatedMeshes[i];
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                HexRenderer renderer = renderers[i];
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
            public EntityTypeHandle entityTypeHandle;

            public Mesh.MeshDataArray meshDataArray;

            public EntityCommandBuffer.ParallelWriter ecbEnd;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex, int index)
            {
                NativeArray<Entity> entities = batchInChunk.GetNativeArray(entityTypeHandle);
                BufferAccessor<HexGridVertex> verticesBA = batchInChunk.GetBufferAccessor(verticesTypeHandle);
                BufferAccessor<HexGridTriangles> trianglesBA = batchInChunk.GetBufferAccessor(trianglesTypeHandle);
                NativeArray<VertexAttributeDescriptor> VertexDescriptors;
                if (batchInChunk.Has(cellIndicesTypeHandle))
                {
                    BufferAccessor<HexGridWeights> weightsBA = batchInChunk.GetBufferAccessor(weightsTypeHandle);
                    BufferAccessor<HexGridIndices> cellIndicesBA = batchInChunk.GetBufferAccessor(cellIndicesTypeHandle);
                    if (batchInChunk.Has(uv2TypeHandle)) // MeshDataUV
                    {
                        BufferAccessor<HexGridUV2> uv2BA = batchInChunk.GetBufferAccessor(uv2TypeHandle);
                        VertexDescriptors = new NativeArray<VertexAttributeDescriptor>(4, Allocator.Temp);
                        VertexDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
                        VertexDescriptors[1] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, 1);
                        VertexDescriptors[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 3);
                        VertexDescriptors[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 3, 2);

                        for (int i = 0; i < verticesBA.Length; i++)
                        {
                            Mesh.MeshData meshData = meshDataArray[index + i];
                            DynamicBuffer<HexGridVertex> verticesDB = verticesBA[i];
                            DynamicBuffer<HexGridTriangles> trianglesDB = trianglesBA[i];
                            meshData.SetVertexBufferParams(verticesDB.Length, VertexDescriptors);
                            meshData.SetIndexBufferParams(trianglesDB.Length, IndexFormat.UInt32);
                            meshData.GetVertexData<HexGridVertex>(0).CopyFrom(verticesDB.AsNativeArray());
                            meshData.GetVertexData<HexGridWeights>(1).CopyFrom(weightsBA[i].AsNativeArray());
                            meshData.GetVertexData<HexGridIndices>(2).CopyFrom(cellIndicesBA[i].AsNativeArray());
                            meshData.GetVertexData<HexGridUV2>(3).CopyFrom(uv2BA[i].AsNativeArray());
                            meshData.GetIndexData<HexGridTriangles>().CopyFrom(trianglesDB.AsNativeArray());
                            meshData.subMeshCount = 1;
                            meshData.SetSubMesh(0, new SubMeshDescriptor(0, trianglesDB.Length, MeshTopology.Triangles));
                            ecbEnd.RemoveComponent<RepaintNow>(entities[i].Index, entities[i]);
                            ecbEnd.RemoveComponent<RepaintScheduled>(entities[i].Index, entities[i]);
                        }
                    }
                    else if (batchInChunk.Has(uv4TypeHandle)) // MeshData2UV
                    {
                        BufferAccessor<HexGridUV4> uv4BA = batchInChunk.GetBufferAccessor(uv4TypeHandle);
                        VertexDescriptors = new NativeArray<VertexAttributeDescriptor>(5, Allocator.Temp);
                        VertexDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
                        VertexDescriptors[1] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, 1);
                        VertexDescriptors[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 3);
                        VertexDescriptors[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2, 3);
                        VertexDescriptors[4] = new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 3, 2);

                        for (int i = 0; i < verticesBA.Length; i++)
                        {
                            Mesh.MeshData meshData = meshDataArray[index + i];
                            DynamicBuffer<HexGridVertex> verticesDB = verticesBA[i];
                            DynamicBuffer<HexGridTriangles> trianglesDB = trianglesBA[i];
                            meshData.SetVertexBufferParams(verticesDB.Length, VertexDescriptors);
                            meshData.SetIndexBufferParams(trianglesDB.Length, IndexFormat.UInt32);
                            meshData.GetVertexData<HexGridVertex>(0).CopyFrom(verticesDB.AsNativeArray());
                            meshData.GetVertexData<HexGridWeights>(1).CopyFrom(weightsBA[i].AsNativeArray());
                            meshData.GetVertexData<HexGridIndices>(2).CopyFrom(cellIndicesBA[i].AsNativeArray());
                            meshData.GetVertexData<HexGridUV4>(3).CopyFrom(uv4BA[i].AsNativeArray());
                            meshData.GetIndexData<HexGridTriangles>().CopyFrom(trianglesDB.AsNativeArray());
                            meshData.subMeshCount = 1;
                            meshData.SetSubMesh(0, new SubMeshDescriptor(0, trianglesDB.Length, MeshTopology.Triangles));
                            ecbEnd.RemoveComponent<RepaintNow>(entities[i].Index, entities[i]);
                            ecbEnd.RemoveComponent<RepaintScheduled>(entities[i].Index, entities[i]);
                        }
                    }
                    else // MeshData
                    {
                        VertexDescriptors = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp);
                        VertexDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
                        VertexDescriptors[1] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4, 1);
                        VertexDescriptors[2] = new VertexAttributeDescriptor(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 3, 2);

                        for (int i = 0; i < verticesBA.Length; i++)
                        {
                            Mesh.MeshData meshData = meshDataArray[index + i];
                            DynamicBuffer<HexGridVertex> verticesDB = verticesBA[i];
                            DynamicBuffer<HexGridTriangles> trianglesDB = trianglesBA[i];
                            meshData.SetVertexBufferParams(verticesDB.Length, VertexDescriptors);
                            meshData.SetIndexBufferParams(trianglesDB.Length, IndexFormat.UInt32);
                            meshData.GetVertexData<HexGridVertex>(0).CopyFrom(verticesDB.AsNativeArray());
                            meshData.GetVertexData<HexGridWeights>(1).CopyFrom(weightsBA[i].AsNativeArray());
                            meshData.GetVertexData<HexGridIndices>(2).CopyFrom(cellIndicesBA[i].AsNativeArray());
                            meshData.GetIndexData<HexGridTriangles>().CopyFrom(trianglesDB.AsNativeArray());
                            meshData.subMeshCount = 1;
                            meshData.SetSubMesh(0, new SubMeshDescriptor(0, trianglesDB.Length, MeshTopology.Triangles));
                            ecbEnd.RemoveComponent<RepaintNow>(entities[i].Index, entities[i]);
                            ecbEnd.RemoveComponent<RepaintScheduled>(entities[i].Index, entities[i]);
                        }
                    }
                }
                else // MeshBasic (walls)
                {
                    VertexDescriptors = new NativeArray<VertexAttributeDescriptor>(1, Allocator.Temp);
                    VertexDescriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
                    for (int i = 0; i < verticesBA.Length; i++)
                    {
                        Mesh.MeshData meshData = meshDataArray[index + i];
                        DynamicBuffer<HexGridVertex> verticesDB = verticesBA[i];
                        DynamicBuffer<HexGridTriangles> trianglesDB = trianglesBA[i];
                        meshData.SetVertexBufferParams(verticesDB.Length, VertexDescriptors);
                        meshData.SetIndexBufferParams(trianglesDB.Length, IndexFormat.UInt32);
                        meshData.GetVertexData<HexGridVertex>(0).CopyFrom(verticesDB.AsNativeArray());
                        meshData.GetIndexData<HexGridTriangles>().CopyFrom(trianglesDB.AsNativeArray());
                        meshData.subMeshCount = 1;
                        meshData.SetSubMesh(0, new SubMeshDescriptor(0, trianglesDB.Length, MeshTopology.Triangles));
                        ecbEnd.RemoveComponent<RepaintNow>(entities[i].Index, entities[i]);
                        ecbEnd.RemoveComponent<RepaintScheduled>(entities[i].Index, entities[i]);
                    }
                }
                
                VertexDescriptors.Dispose();
            }
        }

    }
}