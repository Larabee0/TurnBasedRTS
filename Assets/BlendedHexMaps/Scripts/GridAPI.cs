using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
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
        public List<HexGridChunk> GridChunks = new List<HexGridChunk>();
        public Dictionary<int, HexGridChunk> GridChunksDict = new Dictionary<int, HexGridChunk>();
        public List<HexGridColumn> GridColumns = new List<HexGridColumn>();

        public int cellCountX = 20;
        public int cellCountZ = 15;
        private int chunkCountX;
        private int chunkCountZ;

        public bool wrapping;
        private void Awake()
        {
            mainWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = mainWorld.EntityManager;
            ecbEndSystem = mainWorld.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = mainWorld.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            entityWorldGridAPI = mainWorld.GetOrCreateSystem<EntityWorldGridAPI>();
            entityWorldGridAPI.gameObjectWorld = Instance = this;
        }

        void Update()
        {
            if (ActiveGridEntity != Entity.Null)
            {
                InitialiseGrid(ActiveGridEntity);
                Debug.Log("Repaint Job scheduled for next frame.");
                enabled = false;
            }
        }

        public void InitialiseGrid(Entity grid)
        {
            HexGridComponent hexGridComp = entityManager.GetComponentData<HexGridComponent>(grid);
            DynamicBuffer<HexGridChild> columnBuffer = entityManager.GetBuffer<HexGridChild>(grid);
            DynamicBuffer<HexGridChunkBuffer> chunkBuffer = entityManager.GetBuffer<HexGridChunkBuffer>(grid);

            CreateOrModifyGrid(hexGridComp, columnBuffer);

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

        private void CreateOrModifyGrid(HexGridComponent hexGridComp, DynamicBuffer<HexGridChild> columns)
        {
            if (GridColumns.Count == 0)
            {
                HexGridColumn col = Instantiate(internalPrefabs.GridColumnPrefab, GridContainer.transform);
                for (int i = 0; i < hexGridComp.chunkCountZ; i++)
                {
                    col.AddChunkToColumn(Instantiate(internalPrefabs.GridChunkPrefab));
                }
                GridColumns.Add(col);
            }

            if (hexGridComp.chunkCountX < GridColumns.Count) // remove extra chunks
            {
                for (int i = GridColumns.Count - 1; i > hexGridComp.chunkCountX; i++)
                {
                    Destroy(GridColumns[i].gameObject);
                }
            }
            if (hexGridComp.chunkCountZ > chunkCountZ) // columns need AdditionalChunks
            {
                int difference = hexGridComp.chunkCountZ - chunkCountZ;
                for (int i = 0; i < GridColumns.Count; i++)
                {
                    HexGridColumn col = GridColumns[i];
                    for (int d = 0; d < difference; d++)
                    {
                        col.AddChunkToColumn(Instantiate(internalPrefabs.GridChunkPrefab));
                    }
                }
            }
            else if (hexGridComp.chunkCountZ < chunkCountZ) // columns need ChunksRemoving
            {
                for (int i = 0; i < GridColumns.Count; i++)
                {
                    GridColumns[i].TrimColumnsTo(chunkCountZ);
                }
            }
            if (hexGridComp.chunkCountX > GridColumns.Count)
            {
                HexGridColumn col = GridColumns[0];
                int i = GridColumns.Count;
                for (; i < hexGridComp.chunkCountX; i++)
                {
                    GridColumns.Add(Instantiate(col, GridContainer.transform));
                }
            }
            // reset the chunkList
            GridChunks.Clear();
            GridChunksDict.Clear();
            for (int i = 0; i < columns.Length; i++)
            {
                Entity columnEntity = columns[i];
                GridColumns[i].ColumnIndex = entityManager.GetComponentData<HexColumn>(columnEntity);
                DynamicBuffer<HexGridChild> chunks = entityManager.GetBuffer<HexGridChild>(columnEntity);
                for (int c = 0; c < chunks.Length; c++)
                {
                    int index = entityManager.GetComponentData<HexGridChunkComponent>(chunks[c]).chunkIndex;
                    HexGridChunk chunk = GridColumns[i].chunks[c];
                    chunk.ChunkIndex = index;
                    GridChunksDict.Add(index, chunk);
                    GridChunks.Add(chunk);
                }
            }
            // sort lists by chunk/column index
            GridColumns.Sort();
            GridChunks.Sort();
        }

        private void DestroyGrid()
        {
            for (int i = 0; i < GridChunks.Count; i++)
            {
                Destroy(GridChunks[i].gameObject);
            }
            GridChunks.Clear();
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

        public void DebugMeshSet()
        {
            DynamicBuffer<HexGridChunkBuffer> chunkBuffer = entityManager.GetBuffer<HexGridChunkBuffer>(ActiveGridEntity);
            HexGridChunkComponent chunkComp = entityManager.GetComponentData<HexGridChunkComponent>(chunkBuffer[0].ChunkEntity);

            Vector3[] vertices = entityManager.GetBuffer<HexGridVertex>(chunkComp.entityTerrian).Reinterpret<Vector3>().AsNativeArray().ToArray();
            Vector3[] Indices = entityManager.GetBuffer<HexGridIndices>(chunkComp.entityTerrian).Reinterpret<Vector3>().AsNativeArray().ToArray();
            Color[] colours = entityManager.GetBuffer<HexGridWeights>(chunkComp.entityTerrian).Reinterpret<Color>().AsNativeArray().ToArray();
            uint[] trianglesUint = entityManager.GetBuffer<HexGridTriangles>(chunkComp.entityTerrian).Reinterpret<uint>().AsNativeArray().ToArray();
            int[] triangles = new int[trianglesUint.Length];
            for (int i = 0; i < triangles.Length; i++)
            {
                triangles[i] = (int)trianglesUint[i];
            }

            Mesh terrianMesh = new Mesh();
            terrianMesh.Clear();

            terrianMesh.SetVertices(vertices);
            terrianMesh.SetColors(colours);
            terrianMesh.SetUVs(2, Indices);
            terrianMesh.SetTriangles(triangles, 0);

            terrianMesh.RecalculateNormals();
            GridChunks[0].TerrianMesh = terrianMesh;
            Debug.Log("Debug mesh repaint");
        }
    }


    [UpdateInGroup(typeof(HexGridV2SystemGroup))]
    [UpdateBefore(typeof(HexGridCreateColumnsSystem))]
    public class EntityWorldGridAPI : JobComponentSystem
    {
        public EndSimulationEntityCommandBufferSystem ecbEndSystem;
        public BeginSimulationEntityCommandBufferSystem ecbBeginSystem;

        private EntityQuery repaintChunkQuery;
        private EntityQuery hexColumnQuery;

        public GridAPI gameObjectWorld;

        protected override void OnCreate()
        {
            ecbEndSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            repaintChunkQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexRenderer), typeof(RepaintNow), typeof(RepaintScheduled) } });
            hexColumnQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexColumn), typeof(ColumnOffset) } });
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            JobHandle outputDeps = inputDeps;
            if (!repaintChunkQuery.IsEmpty)
            {
                outputDeps = MeshStuff(inputDeps);
            }
            if (!hexColumnQuery.IsEmpty)
            {
                DynamicBuffer<HexGridChild> children = EntityManager.GetBuffer<HexGridChild>(GridAPI.ActiveGridEntity);
                for (int i = 0; i < children.Length; i++)
                {
                    Entity colEntity = children[i];
                    int columnIndex = EntityManager.GetComponentData<HexColumn>(colEntity);
                    Vector3 columnPosition = EntityManager.GetComponentData<ColumnOffset>(colEntity);
                    gameObjectWorld.GridColumns[columnIndex].transform.position = columnPosition;
                }
            }

            return outputDeps;
        }


        private JobHandle MeshStuff(JobHandle inputDeps)
        {
            float startTime = UnityEngine.Time.realtimeSinceStartup;
            Mesh.MeshDataArray meshes = Mesh.AllocateWritableMeshData(repaintChunkQuery.CalculateEntityCount());
            NativeArray<HexRenderer> renderers = repaintChunkQuery.ToComponentDataArray<HexRenderer>(Allocator.Temp);
            NativeArray<HexMeshIndex> meshIndices = new NativeArray<HexMeshIndex>(meshes.Length, Allocator.Temp);
            for (int i = 0; i < meshIndices.Length; i++)
            {
                meshIndices[i] = i;
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

            JobHandle MidRunHandle = meshWriteJob.ScheduleParallel(repaintChunkQuery, 1, inputDeps);
            //JobHandle MidRunHandle = meshWriteJob.Schedule(repaintChunkQuery, inputDeps);
            ecbEndSystem.AddJobHandleForProducer(MidRunHandle);
            MidRunHandle.Complete();

            Mesh[] updatedMeshes = new Mesh[meshes.Length];
            Dictionary<int, HexGridChunk> chunks = gameObjectWorld.GridChunksDict;
            for (int i = 0; i < renderers.Length; i++)
            {
                HexRenderer renderer = renderers[i];
                HexGridChunk chunk = chunks[renderer.ChunkIndex];
                switch (renderer.rendererID)
                {
                    case RendererID.Terrian:
                        updatedMeshes[meshIndices[i]] = chunk.TerrianMesh;
                        break;
                    case RendererID.River:
                        updatedMeshes[meshIndices[i]] = chunk.RiverMesh;
                        break;
                    case RendererID.Water:
                        updatedMeshes[meshIndices[i]] = chunk.WaterMesh;
                        break;
                    case RendererID.WaterShore:
                        updatedMeshes[meshIndices[i]] = chunk.WaterShoreMesh;
                        break;
                    case RendererID.Estuaries:
                        updatedMeshes[meshIndices[i]] = chunk.EstuariesMesh;
                        break;
                    case RendererID.Roads:
                        updatedMeshes[meshIndices[i]] = chunk.RoadsMesh;
                        break;
                    case RendererID.Walls:
                        updatedMeshes[meshIndices[i]] = chunk.WallsMesh;
                        break;
                }
                updatedMeshes[meshIndices[i]].Clear();
            }

            Mesh.ApplyAndDisposeWritableMeshData(meshes, updatedMeshes);

            for (int i = 0; i < updatedMeshes.Length; i++)
            {
                updatedMeshes[i].RecalculateNormals();
                updatedMeshes[i].RecalculateBounds();
                updatedMeshes[i].MarkModified();
                updatedMeshes[i].UploadMeshData(false);
            }

            Debug.Log("Repaint Job Run, total time since start:" + (UnityEngine.Time.realtimeSinceStartup - startTime) * 1000f + "ms");

            return MidRunHandle;
        }

        [BurstCompile]
        private struct WriteMapMeshData : IJobEntityBatch
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

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
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
                    int meshIndex = meshIndices[i];
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
                            meshData.SetSubMesh(0, new SubMeshDescriptor(0, trianglesDB.Length, MeshTopology.Triangles), MeshUpdateFlags.DontValidateIndices|MeshUpdateFlags.DontRecalculateBounds);
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
                            meshData.SetSubMesh(0, new SubMeshDescriptor(0, trianglesDB.Length, MeshTopology.Triangles), MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
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
                            meshData.SetSubMesh(0, new SubMeshDescriptor(0, trianglesDB.Length, MeshTopology.Triangles), MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
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
                        meshData.SetSubMesh(0, new SubMeshDescriptor(0, trianglesDB.Length, MeshTopology.Triangles), MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
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