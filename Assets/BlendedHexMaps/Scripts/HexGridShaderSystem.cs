using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Rendering;

namespace DOTSHexagonsV2
{

    public class HexGridShaderSystem : JobComponentSystem
    {
        const float transitionSpeed = 255f;
        public static HexGridShaderSystem instance;
        public static bool ImmediateMode { get; set; }
        public Texture2D cellTexture;
        EndSimulationEntityCommandBufferSystem ecbEndSystem;
        BeginSimulationEntityCommandBufferSystem ecbBeginSystem;
        private EntityQuery ActiveGridQuery;
        private EntityQuery ReinitialiseShaderQuery;
        private EntityQuery RunUpdateLoopQuery;
        private EntityQuery RefreshAllQuery;
        public static Entity HexCellShaderData;
        protected override void OnCreate()
        {
            ecbEndSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            ecbBeginSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            HexCellShaderData = EntityManager.CreateEntity(typeof(HexCellShaderDataComponent), typeof(HexCellTransitioningCells), typeof(HexCellTextureDataBuffer));
            cellTexture = HexFunctions.cellTexture;
            instance = this;

            ActiveGridQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexGridComponent), typeof(ActiveGridEntity) } });
            ReinitialiseShaderQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexCellShaderDataComponent), typeof(InitialiseHexCellShader) } });
            RunUpdateLoopQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexCellShaderDataComponent), typeof(HexCellShaderRunUpdateLoop) } });
            RefreshAllQuery = GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(HexCellShaderDataComponent), typeof(HexCellShaderRefreshAll) } });

        }
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (!ReinitialiseShaderQuery.IsEmpty)
            {
                InitialiseHexCellShader gridData = EntityManager.GetComponentData<InitialiseHexCellShader>(HexCellShaderData);
                Initialise(gridData.grid, gridData.x, gridData.z);
                return inputDeps;
            }
            if (RunUpdateLoopQuery.IsEmpty || ActiveGridQuery.IsEmpty)
            {
                return inputDeps;
            }

            RefreshAllCellsJob refreshAllJob = new RefreshAllCellsJob
            {
                cellTextureDataBuffer = GetBufferTypeHandle<HexCellTextureDataBuffer>(),
                transitioningCellsBuffer = GetBufferTypeHandle<HexCellTransitioningCells>(),
                entityTypeHandle = GetEntityTypeHandle(),
                shaderDataCompType = GetComponentTypeHandle<HexCellShaderDataComponent>(true),
                cells = EntityManager.GetBuffer<HexCell>(GridAPI.ActiveGridEntity).ToNativeArray(Allocator.TempJob),
                ecbEnd = ecbEndSystem.CreateCommandBuffer().AsParallelWriter()
            };

            JobHandle outputDeps = refreshAllJob.Schedule(RefreshAllQuery, inputDeps);
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
                    cells = EntityManager.GetBuffer<HexCell>(GridAPI.ActiveGridEntity).ToNativeArray(Allocator.TempJob),
                    ecbEnd = ecbEndSystem.CreateCommandBuffer().AsParallelWriter(),
                    delta = intDelta,
                    ecbBegin = ecbBeginSystem.CreateCommandBuffer().AsParallelWriter()
                };
                outputDeps = transition.Schedule(RunUpdateLoopQuery, outputDeps);
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
            GridAPI.ActiveGridEntity = Grid;
            //HexMapCamera.ValidatePosition();
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
                    cellTextureData[i] = new Color32(0, 0, 0, 0);
                }
            }
            EntityCommandBuffer ecbEnd = ecbEndSystem.CreateCommandBuffer();
            EntityCommandBuffer ecbBegin = ecbBeginSystem.CreateCommandBuffer();
            ecbEnd.RemoveComponent<InitialiseHexCellShader>(HexCellShaderData);

            ecbBegin.SetBuffer<HexCellTextureDataBuffer>(HexCellShaderData).CopyFrom(cellTextureData);
            cellTextureData.Dispose();
            ScheduleRefreshAll(ecbBegin);
            ecbBegin.AddComponent<SetFeatureVisability>(HexCellShaderData);
            ViewElevationChanged(ecbBegin);
        }
        public void RefreshTerrian(HexCell cell)
        {
            HexCellTextureDataBuffer data = EntityManager.GetBuffer<HexCellTextureDataBuffer>(HexCellShaderData).ElementAt(cell.Index);
            data.Value.a = (byte)cell.terrianTypeIndex;
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
                Color32 value = EntityManager.GetBuffer<HexCellTextureDataBuffer>(HexCellShaderData).ElementAt(index);
                value.r = cell.IsVisible ? (byte)255 : (byte)0;
                value.g = cell.IsExplored ? (byte)255 : (byte)0;
                ecb.SetBuffer<HexCellTextureDataBuffer>(HexCellShaderData).ElementAt(index) = value;
            }
            else
            {
                ecb.SetBuffer<HexCellTransitioningCells>(HexCellShaderData).Add(new HexCellTransitioningCells { Value = index });
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
            [ReadOnly]
            [DeallocateOnJobCompletion]
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
                        Color32 value = cellTextureData[index];
                        if (shaderData.ImmidateMode)
                        {
                            value.r = cell.IsVisible ? (byte)255 : (byte)0;
                            value.g = cell.IsExplored ? (byte)255 : (byte)0;
                        }
                        else
                        {
                            transitioningCells.Add(new HexCellTransitioningCells { Value = index });
                        }

                        value.a = (byte)cell.terrianTypeIndex;
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

            [ReadOnly]
            [DeallocateOnJobCompletion]
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
                        int index = transitioningCells[c];
                        HexCell cell = cells[index];

                        Color32 data = cellTextureData[index];
                        bool stillUpdating = false;

                        if (cell.IsExplored && data.g < 255)
                        {
                            stillUpdating = true;
                            int t = data.g + delta;
                            data.g = t >= 255 ? (byte)255 : (byte)t;
                        }

                        if (cell.IsVisible)
                        {
                            if (data.r < 255)
                            {
                                stillUpdating = true;
                                int t = data.r + delta;
                                data.r = t >= 255 ? (byte)255 : (byte)t;
                            }
                        }
                        else if (data.r > 0)
                        {
                            stillUpdating = true;
                            int t = data.r - delta;
                            data.r = t < 0 ? (byte)0 : (byte)t;
                        }

                        if (!stillUpdating)
                        {
                            data.b = 0;
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