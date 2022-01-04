using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine.Rendering;
using Unity.Burst;
using UnityEngine.Rendering.HighDefinition;

namespace DOTSHexagonsV2
{
    public class GridAPI : MonoBehaviour
    {
        [SerializeField] private InternalPrefabContainers internalPrefabs;
        public InternalPrefabContainers Prefabs { get { return internalPrefabs; } }
        public static GridAPI Instance;
        private World mainWorld;
        private EntityManager entityManager;
        private EndSimulationEntityCommandBufferSystem ecbEndSystem;
        private BeginSimulationEntityCommandBufferSystem ecbBeginSystem;
        private GridAPIMeshSystem GridAPIMesh;
        private GridAPIWrapSystem GridAPIWrap;
        private GridAPIFeatureSystem GridAPIFeature;
        public static Entity ActiveGridEntity = Entity.Null;


        public Transform GridContainer;
        public List<HexGridChunk> GridChunks = new List<HexGridChunk>();
        public Dictionary<int, HexGridChunk> GridChunksDict = new Dictionary<int, HexGridChunk>();
        public List<HexGridColumn> GridColumns = new List<HexGridColumn>();

        public List<HexUnit> units = new List<HexUnit>();

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
            GridAPIMesh = mainWorld.GetOrCreateSystem<GridAPIMeshSystem>();
            GridAPIWrap = mainWorld.GetOrCreateSystem<GridAPIWrapSystem>();
            GridAPIFeature = mainWorld.GetOrCreateSystem<GridAPIFeatureSystem>();
            GridAPIMesh.gameObjectWorld = GridAPIWrap.gameObjectWorld = GridAPIFeature.gameObjectWorld = Instance = this;
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

            CreateOrModifyGrid(hexGridComp, columnBuffer);

            QueueFeatureProcessing();
            DOTSHexEditorV3.Instance.GetGridData();
        }

        private void QueueFeatureProcessing()
        {
            NativeArray<HexGridChunkBuffer> chunks = entityManager.GetBuffer<HexGridChunkBuffer>(ActiveGridEntity).AsNativeArray();
            EntityCommandBuffer ecb = ecbBeginSystem.CreateCommandBuffer();
            for (int i = 0; i < chunks.Length; i++)
            {
                ecb.AddComponent<RepaintScheduled>(chunks[i].ChunkEntity);
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
                Debug.Log("Likely chunks destroyed");
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

        public void RemoveUnit(HexUnit unit)
        {
            units.Remove(unit);
            // decrease visibility;
            Destroy(unit.gameObject);
        }

        public void AddUnit(HexCell cell)
        {
            HexUnit unit = Prefabs.InstantiatePrefab(Prefabs.DefaultUnit);
            units.Add(unit);
            Entity unitEntity = entityManager.CreateEntity(Prefabs.defaultUnitArchetype);
            HexUnitComp unitComp = new HexUnitComp
            {
                GridEntity = cell.grid,
                Self = unitEntity,
                Speed = 24,
                travelSpeed = 4f,
                rotationSpeed = 180f,
                visionRange = 3
            };
            entityManager.SetComponentData(unitEntity, unitComp);
            entityManager.SetComponentData<HexUnitLocation>(unitEntity, cell);
            entityManager.SetComponentData<HexUnitCurrentTravelLocation>(unitEntity, HexCell.Null);

            unit.Entity = unitEntity;
            HexUnit.SetLocation(unit, cell);
        }


        public void MakeChildOfColumn(Transform child, int columnIndex)
        {
            child.SetParent(GridColumns[columnIndex].transform, false);
        }

    }

    public class CellHighlightManager
    {
        public GridAPI grid;

        public List<DecalProjector> projectors;

        public CellHighlightManager (GridAPI grid)
        {
            this.grid = grid;
            projectors = new List<DecalProjector>();
        }

        public void ShowPath(NativeArray<HexCell> path)
        {
            CreateProjectors(path.Length);
            SetAllProjectColour(Color.white);
            SetProjectorColour(0, Color.blue);
            SetProjectorColour(path.Length - 1, Color.red);
            PositionProjectors(path);
            SetEnabledAll(true);
        }

        public void PositionProjectors(NativeArray<HexCell> path)
        {
            for (int i = 0; i < path.Length&& i<projectors.Count; i++)
            {
                PositionProjector(i, path[i]);
            }
        }

        public void PositionProjector(int index, HexCell cell)
        {
            PositionProjector(index, cell.Position);
        }

        public void PositionProjector(int index, Vector3 position)
        {
            position.y += 0.5f;
            projectors[index].transform.position = position;
        }

        public void CreateProjectors(int count)
        {
            if(projectors.Count > 0 && projectors.Count < count)
            {
                for (int i = projectors.Count-1; i < count; i++)
                {
                    InstinateHighlight();
                }
            }
            else if(projectors.Count > 0 && projectors.Count > count)
            {
                for (int i = projectors.Count-1; i > count; i--)
                {
                    grid.Prefabs.DestroyObject(projectors[i].gameObject);
                }
            }
            else if(projectors.Count == 0)
            {
                for (int i = 0; i < count; i++)
                {
                    InstinateHighlight();
                }
            }
        }

        public void InstinateHighlight()
        {
            DecalProjector projector = grid.Prefabs.InstantiatePrefab(grid.Prefabs.CellHighlight);
            projector.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            projectors.Add(projector);
        }

        public void SetProjectorPosition(int index, Vector3 position)
        {
            position.y += 1f;
            projectors[index].transform.position = position;
        }

        public void SetProjectorColour(int index, Color colour)
        {
            projectors[index].material.color = colour;
        }

        public void SetProjectorColourEnd(int end, Color colour)
        {
            for (int i = 0; i < projectors.Count && i < end; i++)
            {
                projectors[i].material.color = colour;
            }
        }

        public void SetProjectorColourStart(int start, Color colour)
        {
            for (int i = start; i < projectors.Count; i++)
            {
                projectors[i].material.color = colour;
            }
        }

        public void SetProjectorColourRange(int start, int end, Color colour)
        {
            for (int i = start; i < projectors.Count && i < end; i++)
            {
                projectors[i].material.color = colour;
            }
        }

        public void SetAllProjectColour(Color colour)
        {
            projectors.ForEach((DecalProjector projector) => projector.material.color = colour);
        }

        public void SetEnabled(int index, bool enabled = false)
        {
            projectors[index].enabled = enabled;
        }

        public void SetEnabledAll(bool enabled = false)
        {
            projectors.ForEach((DecalProjector projector) => projector.enabled = enabled);
        }

        public void SetEnabledEnd(int end, bool enabled = false)
        {
            for (int i = 0; i < projectors.Count && i < end; i++)
            {
                projectors[i].enabled = enabled;
            }
        }

        public void SetEnabledStart(int start, bool enabled = false)
        {
            for (int i = start; i < projectors.Count; i++)
            {
                projectors[i].enabled = enabled;
            }
        }

        public void SetEnabledRange(int start, int end, bool enabled = false)
        {
            for (int i = start; i < projectors.Count && i < end; i++)
            {
                projectors[i].enabled = enabled;
            }
        }

    }

    [UpdateBefore(typeof(HexGridV2SystemGroup))]
    public class HexGridAPISystemGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(HexGridAPISystemGroup))]
    public class GridAPIMeshSystem : JobComponentSystem
    {
        public EndSimulationEntityCommandBufferSystem ecbEndSystem;
        public BeginSimulationEntityCommandBufferSystem ecbBeginSystem;

        private EntityQuery repaintChunkQuery;

        public GridAPI gameObjectWorld;

        protected override void OnCreate()
        {
            ecbEndSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            repaintChunkQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] { typeof(HexRenderer), },
                Any = new ComponentType[] { typeof(RepaintNow), typeof(RepaintScheduled) }
            });
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
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
            JobHandle outputDeps;
            if (BurstCompiler.IsEnabled)
            {
                outputDeps = meshWriteJob.ScheduleParallel(repaintChunkQuery, 1, inputDeps);
            }
            else
            {
                outputDeps = meshWriteJob.Schedule(repaintChunkQuery, inputDeps);
            }
            ecbEndSystem.AddJobHandleForProducer(outputDeps);
            outputDeps.Complete();
            List<MeshCollider> collider = new List<MeshCollider>();
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
                        collider.Add(chunk.Collider);
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

                // mesh colliders need turning off and on again to register the mesh change for some reason.
                if (i < collider.Count)
                {
                    collider[i].enabled = false;
                    collider[i].enabled = true;
                }
            }

            Debug.Log("Repaint Job Run, total time since start:" + (UnityEngine.Time.realtimeSinceStartup - startTime) * 1000f + "ms");

            return outputDeps;
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

    [UpdateInGroup(typeof(HexGridAPISystemGroup))]
    public class GridAPIWrapSystem : ComponentSystem
    {
        private EntityQuery hexColumnQuery;

        public GridAPI gameObjectWorld;

        protected override void OnCreate()
        {
            hexColumnQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexColumn), typeof(ColumnOffset) } });
        }

        protected override void OnUpdate()
        {
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
        }
    }

    [UpdateInGroup(typeof(HexGridAPISystemGroup))]
    public class GridAPIFeatureSystem : ComponentSystem
    {
        private EntityQuery hexFeatureQuery;

        public GridAPI gameObjectWorld;

        protected override void OnCreate()
        {
            hexFeatureQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(CellFeature), typeof(FeatureContainer), typeof(ProcessFeatures) } });
        }

        protected override void OnUpdate()
        {
            if (!hexFeatureQuery.IsEmpty)
            {
                float startTime = UnityEngine.Time.realtimeSinceStartup;
                Dictionary<int, HexGridChunk> GridChunksDict = gameObjectWorld.GridChunksDict;
                Entities.With(hexFeatureQuery).ForEach((Entity Container, ref FeatureContainer GridAndGridChunk) =>
                {
                    Entity GridEntity = GridAndGridChunk.GridEntity;
                    if (GridAPI.ActiveGridEntity == GridEntity)
                    {
                        Entity GridChunk = GridAndGridChunk.ChunkEntity;
                        int ChunkIndex = EntityManager.GetComponentData<HexGridChunkComponent>(GridChunk).chunkIndex;
                        DynamicBuffer<CellFeature> features = EntityManager.GetBuffer<CellFeature>(Container);
                        List<HexGridFeatureInfo> aliveFeatures = new List<HexGridFeatureInfo>(GridChunksDict[ChunkIndex].FeatureContainer.Features);

                        if (aliveFeatures.Count > 0)
                        {
                            for (int i = 0; i < aliveFeatures.Count; i++)
                            {
                                gameObjectWorld.Prefabs.DestroyFeature(aliveFeatures[i]);
                            }
                        }

                        List<HexGridFeatureInfo> featureWriteBacks = new List<HexGridFeatureInfo>(features.Length);
                        for (int i = 0; i < features.Length; i++)
                        {
                            CellFeature newFeature = features[i];
                            HexGridFeatureInfo SpawnedFeautre = null;
                            float3 position = newFeature.position;
                            float3 direction = newFeature.direction;
                            switch (newFeature.featureType)
                            {
                                case FeatureType.WallTower:
                                    SpawnedFeautre = gameObjectWorld.Prefabs.InstinateFeature(newFeature);
                                    SpawnedFeautre.feature = newFeature;
                                    SpawnedFeautre.transform.localPosition = position;
                                    SpawnedFeautre.transform.right = direction;
                                    break;
                                case FeatureType.Bridge:
                                    SpawnedFeautre = gameObjectWorld.Prefabs.InstinateFeature(newFeature);
                                    SpawnedFeautre.transform.localPosition = (position + direction) * 0.5f;
                                    SpawnedFeautre.transform.forward = direction - position;
                                    float length = Vector3.Distance(position, direction);
                                    SpawnedFeautre.transform.localScale = new Vector3(1f, 1f, length * (1f / HexFunctions.bridgeDesignLength));
                                    break;
                                case FeatureType.Special:
                                    SpawnedFeautre = gameObjectWorld.Prefabs.InstinateFeature(newFeature);
                                    SpawnedFeautre.transform.localPosition = position;
                                    SpawnedFeautre.transform.localRotation = Quaternion.Euler(direction);
                                    break;
                                case FeatureType.Urban:
                                    SpawnedFeautre = gameObjectWorld.Prefabs.InstinateFeature(newFeature);
                                    position.y += SpawnedFeautre.transform.localScale.y * 0.5f;
                                    SpawnedFeautre.transform.localPosition = position;
                                    SpawnedFeautre.transform.localRotation = Quaternion.Euler(direction);
                                    break;
                                case FeatureType.Farm:
                                    SpawnedFeautre = gameObjectWorld.Prefabs.InstinateFeature(newFeature);
                                    position.y += SpawnedFeautre.transform.localScale.y * 0.5f;
                                    SpawnedFeautre.transform.localPosition = position;
                                    SpawnedFeautre.transform.localRotation = Quaternion.Euler(direction);
                                    break;
                                case FeatureType.Plant:
                                    SpawnedFeautre = gameObjectWorld.Prefabs.InstinateFeature(newFeature);
                                    position.y += SpawnedFeautre.transform.localScale.y * 0.5f;
                                    SpawnedFeautre.transform.localPosition = position;
                                    SpawnedFeautre.transform.localRotation = Quaternion.Euler(direction);
                                    break;
                            }
                            if (SpawnedFeautre != null)
                            {
                                SpawnedFeautre.feature = newFeature;
                                SpawnedFeautre.transform.SetParent(GridChunksDict[ChunkIndex].FeatureContainer.transform, false);
                                featureWriteBacks.Add(SpawnedFeautre);
                            }
                        }

                        GridChunksDict[ChunkIndex].FeatureContainer.Features= new List<HexGridFeatureInfo>(featureWriteBacks);

                        EntityManager.RemoveComponent<ProcessFeatures>(Container);
                    }
                });

                Debug.Log("Feature Processing time:" + (UnityEngine.Time.realtimeSinceStartup - startTime) * 1000f + "ms");
            }
        }

        private struct FeauturePairs
        {
            public int existingFeatureIndex;
            public HexGridFeatureInfo existingFeature;
            public CellFeature NewFeature;
            public bool RequiresAction ;

            public FeauturePairs(CellFeature feature)
            {
                NewFeature = feature;
                existingFeatureIndex = int.MinValue;
                existingFeature = null;
                RequiresAction = true;
            }
            public FeauturePairs(CellFeature feature, int existingIndex, HexGridFeatureInfo existingInfo, bool requiresAction = true)
            {
                NewFeature = feature;
                existingFeatureIndex = existingIndex;
                existingFeature = existingInfo;
                RequiresAction = requiresAction;
            }
        }
    }
}