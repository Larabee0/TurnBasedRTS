using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Rendering;
using UnityEngine;

namespace DOTSHexagons
{
    public struct HexCellShaderDataComponent : IComponentData
    {
        public Entity grid;
        public bool ImmidateMode;
    }
    public struct ActiveGridEntity : IComponentData { }
    public struct MakeActiveGridEntity : IComponentData { }
    public struct SetFeatureVisability : IComponentData { }
    public struct ViewElevationChangeHexCellShader : IComponentData { }
    public struct SetMapDataHexCellShader : IComponentData { }
    public struct RefreshVisibilityHexCellShader : IComponentData { }
    public struct RefreshTerrianHexCellShader : IComponentData
    {
        public HexCell cell;
    }
    public struct InitialiseHexCellShader : IComponentData { public int x, z; public Entity grid; }
    public struct HexCellShaderRunUpdateLoop : IComponentData { }
    public struct HexCellShaderRefreshAll : IComponentData { }
    public struct NeedsVisibilityReset : IComponentData { }
    public struct HexCellTextureDataBuffer : IBufferElementData
    {
        public Color32 celltextureData;
    }
    public struct HexCellTransitioningCells : IBufferElementData
    {
        public int transitioningCell;
    }
    public class HexCellShaderSystem : JobComponentSystem
    {
        const float transitionSpeed = 255f;
        public static HexCellShaderSystem instance;
        public static bool ImmediateMode { get; set; }
        public Texture2D cellTexture;
        EndSimulationEntityCommandBufferSystem ecbEndSystem;
        BeginSimulationEntityCommandBufferSystem ecbBeginSystem;
        private readonly EntityQueryDesc ActiveGridQuery = new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(ActiveGridEntity) } };
        private readonly EntityQueryDesc ReinitialiseShader = new EntityQueryDesc { All = new ComponentType[] { typeof(HexCellShaderDataComponent), typeof(InitialiseHexCellShader) } };
        private readonly EntityQueryDesc RunUpdateLoopQuery = new EntityQueryDesc { All = new ComponentType[] { typeof(HexCellShaderDataComponent), typeof(HexCellShaderRunUpdateLoop) } };
        private readonly EntityQueryDesc RefreshAllQuery = new EntityQueryDesc { All = new ComponentType[] { typeof(HexCellShaderDataComponent), typeof(HexCellShaderRefreshAll) } };
        public static Entity HexCellShaderData;
        protected override void OnCreate()
        {
            ecbEndSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            HexCellShaderData = EntityManager.CreateEntity(typeof(HexCellShaderDataComponent), typeof(HexCellTransitioningCells), typeof(HexCellTextureDataBuffer));
            cellTexture = HexMetrics.cellTexture;
            instance = this;
        }
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            EntityQuery newActiveGrid = GetEntityQuery(ReinitialiseShader);
            if (!newActiveGrid.IsEmpty)
            {
                InitialiseHexCellShader gridData = EntityManager.GetComponentData<InitialiseHexCellShader>(HexCellShaderData);
                Initialise(gridData.grid, gridData.x, gridData.z);
                return inputDeps;
            }
            EntityQuery Run = GetEntityQuery(RunUpdateLoopQuery);
            EntityQuery ActiveGridExists = GetEntityQuery(ActiveGridQuery);

            if (Run.IsEmpty || ActiveGridExists.IsEmpty)
            {
                return inputDeps;
            }


            EntityQuery refreshAllQuery = GetEntityQuery(RefreshAllQuery);
            RefreshAllCellsJob refreshAllJob = new RefreshAllCellsJob
            {
                cellTextureDataBuffer = GetBufferTypeHandle<HexCellTextureDataBuffer>(),
                transitioningCellsBuffer = GetBufferTypeHandle<HexCellTransitioningCells>(),
                entityTypeHandle = GetEntityTypeHandle(),
                shaderDataCompType = GetComponentTypeHandle<HexCellShaderDataComponent>(true),
                cells = EntityManager.GetBuffer<HexCell>(HexMetrics.ActiveGridEntity).ToNativeArray(Allocator.TempJob),
                ecbEnd = ecbEndSystem.CreateCommandBuffer().AsParallelWriter()
            };

            JobHandle outputDeps = refreshAllJob.Schedule(refreshAllQuery, inputDeps);
            ecbBeginSystem.AddJobHandleForProducer(outputDeps);
            ecbEndSystem.AddJobHandleForProducer(outputDeps);
            outputDeps.Complete();
            int intDelta = (int)(Time.DeltaTime * transitionSpeed);
            if (intDelta == 0)
            {
                intDelta = 1;
            }
            if (EntityManager.GetBuffer<HexCellTransitioningCells>(HexCellShaderData).Length > 0)
            {
                TransitionCells transition = new TransitionCells
                {
                    cellTextureDataBuffer = GetBufferTypeHandle<HexCellTextureDataBuffer>(),
                    transitioningCellsBuffer = GetBufferTypeHandle<HexCellTransitioningCells>(),
                    entityTypeHandle = GetEntityTypeHandle(),
                    cells = EntityManager.GetBuffer<HexCell>(HexMetrics.ActiveGridEntity).ToNativeArray(Allocator.TempJob),
                    ecbEnd = ecbEndSystem.CreateCommandBuffer().AsParallelWriter(),
                    delta = intDelta,
                    ecbBegin = ecbBeginSystem.CreateCommandBuffer().AsParallelWriter()
                };
                outputDeps = transition.Schedule(Run, outputDeps);
                ecbBeginSystem.AddJobHandleForProducer(outputDeps);
                ecbEndSystem.AddJobHandleForProducer(outputDeps);
                outputDeps.Complete();
            }
            cellTexture.SetPixels32(EntityManager.GetBuffer<HexCellTextureDataBuffer>(HexCellShaderData).Reinterpret<Color32>().AsNativeArray().ToArray());
            cellTexture.Apply();
            return outputDeps;
        }
        public void ScheduleRefreshAll(EntityCommandBuffer ecb)
        {
            ecb.AddComponent<HexCellShaderRunUpdateLoop>(HexCellShaderData);
            ecb.AddComponent<HexCellShaderRefreshAll>(HexCellShaderData);
            DynamicBuffer<HexCell> cells = EntityManager.GetBuffer<HexCell>(HexMetrics.ActiveGridEntity);
            for (int i = 0; i < cells.Length; i++)
            {
                Debug.Log(cells[i].terrianTypeIndex);
            }
        }
        public void ScheduleRefreshAll()
        {
            EntityCommandBuffer ecb = ecbBeginSystem.CreateCommandBuffer();
            ecb.AddComponent<HexCellShaderRunUpdateLoop>(HexCellShaderData);
            ecb.AddComponent<HexCellShaderRefreshAll>(HexCellShaderData);
        }
        public void Initialise(Entity Grid, int x, int z)
        {
            HexCellShaderDataComponent data = EntityManager.GetComponentData<HexCellShaderDataComponent>(HexCellShaderData);
            data.grid = Grid;
            HexMetrics.ActiveGridEntity = Grid;
            HexMapCamera.ValidatePosition();
            EntityManager.SetComponentData(HexCellShaderData, data);
            NativeArray<HexCellTextureDataBuffer> cellTextureData = EntityManager.GetBuffer<HexCellTextureDataBuffer>(HexCellShaderData).ToNativeArray(Allocator.Temp);

            if (cellTexture)
            {
                cellTexture.Resize(x, z);
            }
            else
            {
                cellTexture = new Texture2D(x, z, TextureFormat.RGBA32, false, true)
                {
                    filterMode = FilterMode.Point,
                    wrapModeU = TextureWrapMode.Repeat,
                    wrapModeV = TextureWrapMode.Clamp
                };
                Shader.SetGlobalTexture("Texture2D_2733adbad7a44121b7f9f7fb58b67308", cellTexture);

            }
            Shader.SetGlobalVector("Vector4_60bbbf410de0422c812ba0764d35ecf5", new Vector4(1f / x, 1f / z, x, z));
            if (cellTextureData.Length != x * z)
            {
                cellTextureData.Dispose();
                cellTextureData = new NativeArray<HexCellTextureDataBuffer>(x * z, Allocator.Temp);
            }
            else
            {
                for (int i = 0; i < cellTextureData.Length; i++)
                {
                    cellTextureData[i] = new HexCellTextureDataBuffer { celltextureData = new Color32(0, 0, 0, 0) };
                }
            }
            EntityCommandBuffer ecbEnd = ecbEndSystem.CreateCommandBuffer();
            EntityCommandBuffer ecbBegin = ecbBeginSystem.CreateCommandBuffer();
            ecbEnd.RemoveComponent<InitialiseHexCellShader>(HexCellShaderData);
            ecbEnd.SetBuffer<HexCellTransitioningCells>(HexCellShaderData).Clear();
            ecbBegin.SetBuffer<HexCellTextureDataBuffer>(HexCellShaderData).CopyFrom(cellTextureData);
            cellTextureData.Dispose();
            ScheduleRefreshAll(ecbBegin);
            ecbBegin.AddComponent<SetFeatureVisability>(HexCellShaderData);
            ViewElevationChanged(ecbBegin);
        }
        public void RefreshTerrian(HexCell cell)
        {
            HexCellTextureDataBuffer data = EntityManager.GetBuffer<HexCellTextureDataBuffer>(HexCellShaderData).ElementAt(cell.Index);
            data.celltextureData.a = (byte)cell.terrianTypeIndex;
            EntityManager.GetBuffer<HexCellTextureDataBuffer>(HexCellShaderData).ElementAt(cell.Index) = data;
            EntityCommandBuffer ecb = ecbBeginSystem.CreateCommandBuffer();
            ecb.AddComponent<HexCellShaderRunUpdateLoop>(HexCellShaderData);
        }
        // will be used by the unit system
        public void RefreshVisibility(HexCell cell)
        {
            int index = cell.Index;
            EntityCommandBuffer ecb = ecbBeginSystem.CreateCommandBuffer();
            if (ImmediateMode)
            {
                HexCellTextureDataBuffer value = EntityManager.GetBuffer<HexCellTextureDataBuffer>(HexCellShaderData).ElementAt(index);
                value.celltextureData.r = cell.IsVisible ? (byte)255 : (byte)0;
                value.celltextureData.g = cell.IsExplored ? (byte)255 : (byte)0;
                ecb.SetBuffer<HexCellTextureDataBuffer>(HexCellShaderData).ElementAt(index) = value;
            }
            else
            {
                ecb.SetBuffer<HexCellTransitioningCells>(HexCellShaderData).Add(new HexCellTransitioningCells { transitioningCell = index });
            }
            ecb.AddComponent<HexCellShaderRunUpdateLoop>(HexCellShaderData);
        }
        public void ViewElevationChanged(EntityCommandBuffer ecb)
        {
            ecb.AddComponent<HexCellShaderRunUpdateLoop>(HexCellShaderData);
            ecb.AddComponent<NeedsVisibilityReset>(HexCellShaderData);
        }
        public void ViewElevationChanged()
        {
            EntityCommandBuffer ecb = ecbBeginSystem.CreateCommandBuffer();
            ecb.AddComponent<HexCellShaderRunUpdateLoop>(HexCellShaderData);
            ecb.AddComponent<NeedsVisibilityReset>(HexCellShaderData);
        }
        // unused.
        //public void SetMapData(HexCell cell, float data)
        //{
        //    HexCellTextureDataBuffer value = EntityManager.GetBuffer<HexCellTextureDataBuffer>(HexCellShaderData).ElementAt(cell.Index);
        //    value.celltextureData.b = data < 0 ? (byte)0 : (data < 1f ? (byte)(data * 254f) : (byte)254);
        //    EntityManager.GetBuffer<HexCellTextureDataBuffer>(HexCellShaderData).ElementAt(cell.Index) = value;
        //    EntityCommandBuffer ecb = ecbBeginSystem.CreateCommandBuffer();
        //    ecb.AddComponent<HexCellShaderRunUpdateLoop>(HexCellShaderData);
        //}

        [BurstCompile]
        public struct RefreshAllCellsJob : IJobEntityBatch
        {
            public BufferTypeHandle<HexCellTextureDataBuffer> cellTextureDataBuffer;
            public BufferTypeHandle<HexCellTransitioningCells> transitioningCellsBuffer;
            [ReadOnly]
            public EntityTypeHandle entityTypeHandle;
            [ReadOnly]
            public ComponentTypeHandle<HexCellShaderDataComponent> shaderDataCompType;
            [ReadOnly][DeallocateOnJobCompletion]
            public NativeArray<HexCell> cells;

            public EntityCommandBuffer.ParallelWriter ecbEnd;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                
                BufferAccessor<HexCellTextureDataBuffer> textureDataBufferAccessor = batchInChunk.GetBufferAccessor(cellTextureDataBuffer);
                BufferAccessor<HexCellTransitioningCells> transitioningCellsBufferAccessor = batchInChunk.GetBufferAccessor(transitioningCellsBuffer);
                NativeArray<Entity> entities = batchInChunk.GetNativeArray(entityTypeHandle);
                NativeArray<HexCellShaderDataComponent> shaderDataComponents = batchInChunk.GetNativeArray(shaderDataCompType);
                for (int i = 0; i < entities.Length; i++)
                {
                    HexCellShaderDataComponent shaderData = shaderDataComponents[i];
                    DynamicBuffer<HexCellTextureDataBuffer> cellTextureData = textureDataBufferAccessor[i];

                    NativeList<HexCellTransitioningCells> transitioningCells = new NativeList<HexCellTransitioningCells>(cells.Length, Allocator.Temp);
                    for (int c = 0; c < cells.Length; c++)
                    {
                        HexCell cell = cells[c];
                        int index = cell.Index;
                        HexCellTextureDataBuffer value = cellTextureData[index];
                        if (shaderData.ImmidateMode)
                        {
                            value.celltextureData.r = cell.IsVisible ? (byte)255 : (byte)0;
                            value.celltextureData.g = cell.IsExplored ? (byte)255 : (byte)0;
                        }
                        else
                        {
                            transitioningCells.Add(new HexCellTransitioningCells { transitioningCell = index });
                        }

                        value.celltextureData.a = (byte)cell.terrianTypeIndex;
                        cellTextureData[index] = value;
                    }
                    if (transitioningCells.Length > 0)
                    {
                        transitioningCellsBufferAccessor[i].CopyFrom(transitioningCells);
                    }
                    transitioningCells.Dispose();
                    ecbEnd.RemoveComponent<HexCellShaderRefreshAll>(batchIndex, entities[i]);
                }
            }
        }

        [BurstCompile]
        public struct TransitionCells : IJobEntityBatch
        {
            public BufferTypeHandle<HexCellTextureDataBuffer> cellTextureDataBuffer;
            public BufferTypeHandle<HexCellTransitioningCells> transitioningCellsBuffer;
            [ReadOnly]
            public EntityTypeHandle entityTypeHandle;

            [ReadOnly][DeallocateOnJobCompletion]
            public NativeArray<HexCell> cells;
            public int delta;
            public EntityCommandBuffer.ParallelWriter ecbEnd;
            public EntityCommandBuffer.ParallelWriter ecbBegin;
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                BufferAccessor<HexCellTextureDataBuffer> textureDataBufferAccessor = batchInChunk.GetBufferAccessor(cellTextureDataBuffer);
                BufferAccessor<HexCellTransitioningCells> transitioningCellsBufferAccessor = batchInChunk.GetBufferAccessor(transitioningCellsBuffer);
                NativeArray<Entity> entities = batchInChunk.GetNativeArray(entityTypeHandle);
                for (int i = 0; i < entities.Length; i++)
                {
                    DynamicBuffer<HexCellTextureDataBuffer> cellTextureData = textureDataBufferAccessor[i];
                    NativeList<HexCellTransitioningCells> transitioningCells = new NativeList<HexCellTransitioningCells>(transitioningCellsBufferAccessor[i].Length, Allocator.Temp);
                    transitioningCells.CopyFrom(transitioningCellsBufferAccessor[i].AsNativeArray());                    
                    for (int c = 0; c < transitioningCells.Length; c++)
                    {
                        int index = transitioningCells[c].transitioningCell;
                        HexCell cell = cells[index];

                        HexCellTextureDataBuffer data = cellTextureData[index];
                        bool stillUpdating = false;

                        if (cell.IsExplored && data.celltextureData.g < 255)
                        {
                            stillUpdating = true;
                            int t = data.celltextureData.g + delta;
                            data.celltextureData.g = t >= 255 ? (byte)255 : (byte)t;
                        }

                        if (cell.IsVisible)
                        {
                            if (data.celltextureData.r < 255)
                            {
                                stillUpdating = true;
                                int t = data.celltextureData.r + delta;
                                data.celltextureData.r = t >= 255 ? (byte)255 : (byte)t;
                            }
                        }
                        else if (data.celltextureData.r > 0)
                        {
                            stillUpdating = true;
                            int t = data.celltextureData.r - delta;
                            data.celltextureData.r = t < 0 ? (byte)0 : (byte)t;
                        }

                        if (!stillUpdating)
                        {
                            data.celltextureData.b = 0;
                            transitioningCells[c--] = transitioningCells[transitioningCells.Length - 1];
                            transitioningCells.RemoveAt(transitioningCells.Length - 1);
                        }
                        cellTextureData[index] = data;
                    }
                    transitioningCellsBufferAccessor[i].CopyFrom(transitioningCells);
                    if (transitioningCells.Length == 0)
                    {
                        ecbEnd.RemoveComponent<HexCellShaderRunUpdateLoop>(batchIndex, entities[i]);
                    }
                    transitioningCells.Dispose();
                }
            }
        }
    }
}
