using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(HexSystemGroup))]
public partial class HexShaderSystem : SystemBase
{
    private const float transitionSpeed = 255f;

    protected override void OnCreate()
    {
        //Entity shaderEntity = EntityManager.CreateEntity(typeof(HexShaderSettings), typeof(HexShaderCellTexture), typeof(HexShaderTransitioningCells), typeof(HexShaderTextureData));
        //EntityManager.AddComponentObject(shaderEntity, new HexShaderCellTexture() { value = null });
        EntityManager.AddComponent<HexShaderSettings>(SystemHandle);
        EntityManager.AddComponentObject(SystemHandle, new HexShaderCellTexture() { value = null });
        EntityManager.AddComponentData(SystemHandle, new HexShaderTransitioningCells { values = new(Allocator.Persistent) });
        EntityManager.AddComponent<HexShaderTextureData>(SystemHandle);

    }

    protected override void OnDestroy()
    {
        SystemAPI.GetSingleton<HexShaderTransitioningCells>().values.Dispose();
        SystemAPI.GetSingleton<HexShaderTextureData>().values.Dispose();
    }

    protected override void OnUpdate()
    {
        if (SystemAPI.TryGetSingleton(out HexShaderInitialise gridData))
        {
            Initialise(gridData.grid, gridData.x, gridData.z);
            return;
        }
        var ecbEndParallel = GetParallelEntityCommandBuffer();

        int intDelta = (int)(SystemAPI.Time.DeltaTime * transitionSpeed);
        intDelta = intDelta == 0 ? 1 : intDelta;

        if (SystemAPI.HasSingleton<HexShaderTransitionCells>() && SystemAPI.TryGetSingletonBuffer<HexCellShaderRefresh>(out _, true))
        {
            EntityManager.AddComponent<HexShaderAllCellRequest>(SystemHandle);
            EntityManager.AddComponent<HexShaderPaintTexture>(SystemHandle);
            NativeList<int> transitioningCells = SystemAPI.GetSingleton<HexShaderTransitioningCells>().values;
            if (transitioningCells.Length == 0)
            {
                EntityManager.RemoveComponent<HexShaderTransitionCells>(SystemHandle);
                EntityManager.RemoveComponent<HexCellShaderRefresh>(SystemHandle);
            }
            else
            {
                NativeList<int> transitionedCells = new(transitioningCells.Length, Allocator.TempJob);
                Dependency = new TransitionCellsJob
                {
                    transitioningCells = transitioningCells,
                    texutreData = SystemAPI.GetSingleton<HexShaderTextureData>().values,
                    cells = SystemAPI.GetSingletonBuffer<HexCellShaderRefresh>().ToNativeArray(Allocator.TempJob),
                    removeCells = transitionedCells.AsParallelWriter(),
                    delta = intDelta,
                    ecbEnd = ecbEndParallel
                }.Schedule(transitioningCells.Length, 64, Dependency);

                Dependency = new CompleteCellTransition
                {
                    transitioningCells = transitioningCells,
                    transitionedCells = transitionedCells,
                }.Schedule(Dependency);
            }
        }
        
        if (SystemAPI.HasSingleton<HexShaderPaintTexture>())
        {
            Texture2D shaderTexture = EntityManager.GetComponentObject<HexShaderCellTexture>(SystemHandle).value;
            if (shaderTexture != null)
            {
                unsafe
                {
                    UnsafeUtility.MemCpy(shaderTexture.GetPixelData<Color32>(0).GetUnsafePtr(), SystemAPI.GetSingleton<HexShaderTextureData>().values.GetUnsafeReadOnlyPtr(), (shaderTexture.height * shaderTexture.width)*UnsafeUtility.SizeOf<Color32>());
                }
                EntityManager.RemoveComponent<HexShaderPaintTexture>(SystemHandle);
                shaderTexture.Apply();
            }
        }

        OnUpdateRefreshAll(ecbEndParallel);
    }

    private void OnUpdateRefreshAll(EntityCommandBuffer.ParallelWriter ecbEndParallel)
    {
        HexShaderSettings shaderSettings = SystemAPI.GetSingleton<HexShaderSettings>();
        if (SystemAPI.HasSingleton<HexShaderCellDataComplete>() && SystemAPI.HasSingleton<HexShaderRefreshAll>())
        {
            DynamicBuffer<HexCellShaderRefresh> shaderCellData = SystemAPI.GetSingletonBuffer<HexCellShaderRefresh>();
            Dependency = new RefreshAllCellsJob
            {
                shaderSettings = shaderSettings,
                cells = shaderCellData.ToNativeArray(Allocator.TempJob),
                texutreData = SystemAPI.GetSingleton<HexShaderTextureData>().values,
                transitioningCells = SystemAPI.GetSingleton<HexShaderTransitioningCells>().values.AsParallelWriter()
            }.Schedule(shaderCellData.Length, 64, Dependency);
            EntityManager.RemoveComponent<HexShaderCellDataComplete>(SystemHandle);
            EntityManager.RemoveComponent<HexShaderRefreshAll>(SystemHandle);
            EntityManager.RemoveComponent<HexShaderCellDataComplete>(SystemHandle);
            EntityManager.RemoveComponent<HexCellShaderRefresh>(SystemHandle);
            EntityManager.AddComponent<HexShaderPaintTexture>(SystemHandle);
        }
        else if (shaderSettings.grid != Entity.Null && SystemAPI.HasSingleton<HexShaderAllCellRequest>())
        {
            EntityManager.RemoveComponent<HexCellShaderRefresh>(SystemHandle);
            var requestJob = new RequestCellDataForShaderJob
            {
                ecbEnd = ecbEndParallel,
                hexCells = SystemAPI.GetBuffer<HexCellReference>(shaderSettings.grid).ToNativeArray(Allocator.TempJob)
            };
            Dependency = requestJob.Schedule(requestJob.hexCells.Length, 64, Dependency);

            EntityManager.AddComponent<HexCellShaderRefresh>(SystemHandle);
            EntityManager.RemoveComponent<HexShaderAllCellRequest>(SystemHandle);
        }
    }

    public void Initialise(Entity grid, int x, int z)
    {
        SystemAPI.GetSingletonRW<HexShaderSettings>().ValueRW.grid = grid;

        HexShaderCellTexture shaderTexture = EntityManager.GetComponentObject<HexShaderCellTexture>(SystemHandle);

        if (shaderTexture.value)
        {
            shaderTexture.value.Reinitialize(x, z);
        }
        else
        {
            shaderTexture.value = new Texture2D(x, z, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp
            };
            Shader.SetGlobalTexture("_HexCellData", shaderTexture.value);
        }

        Shader.SetGlobalVector("_HexCellData_TexelSize", new Vector4(1f / x, 1f / z, x, z));

        SystemAPI.GetSingletonRW<HexShaderTransitioningCells>().ValueRW.values.SetCapacity(x * z);

        RefRW<HexShaderTextureData> textureData = SystemAPI.GetSingletonRW<HexShaderTextureData>();
        if (textureData.ValueRW.values.Length == x * z)
        {
            unsafe
            {
                Color32* textureDataPtr = (Color32*)textureData.ValueRW.values.GetUnsafePtr();
                UnsafeUtility.MemClear(textureDataPtr, x * z * sizeof(Color32));
            }
        }
        else
        {
            textureData.ValueRW.values.Dispose();
            textureData.ValueRW.values = new NativeArray<Color32>(x * z, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }
        EntityManager.RemoveComponent<HexShaderInitialise>(World.GetExistingSystem<HexGridCreatorSystem>());
        RefreshAll();
        // set feature visability
        // view elevation changed
    }

    public void RefreshAll()
    {
        EntityManager.AddComponent<HexShaderRefreshAll>(SystemHandle);
        EntityManager.AddComponent<HexShaderAllCellRequest>(SystemHandle);
    }

    private EntityCommandBuffer GetEntityCommandBuffer()
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(World.Unmanaged);
        return ecb;
    }

    private EntityCommandBuffer.ParallelWriter GetParallelEntityCommandBuffer()
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(World.Unmanaged).AsParallelWriter();
        return ecb;
    }
}

[BurstCompile]//,WithAll(typeof(HexShaderAllCellRequest))]
public struct RequestCellDataForShaderJob : IJobParallelFor
{
    [ReadOnly, DeallocateOnJobCompletion]
    public NativeArray<HexCellReference> hexCells;

    public EntityCommandBuffer.ParallelWriter ecbEnd;

    public void Execute(int index)
    {
        ecbEnd.AddComponent<HexShaderRefresh>(hexCells.Length, hexCells[index].Value);
    }
    //public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main)
    //{
    //    for (int i = 0; i < hexCells.Length; i++)
    //    {
    //        ecbEnd.AddComponent<HexShaderRefresh>(jobChunkIndex, hexCells[i].Value);
    //    }
    //    ecbEnd.AddBuffer<HexCellShaderRefresh>(jobChunkIndex, main);
    //    ecbEnd.RemoveComponent<HexShaderAllCellRequest>(jobChunkIndex, main);
    //}
}

[BurstCompile]
public struct RefreshAllCellsJob : IJobParallelFor
{
    public HexShaderSettings shaderSettings;

    [WriteOnly]
    public NativeList<int>.ParallelWriter transitioningCells;

    [ReadOnly,DeallocateOnJobCompletion]
    public NativeArray<HexCellShaderRefresh> cells;

    [NativeDisableParallelForRestriction]
    public NativeArray<Color32> texutreData;

    public void Execute(int c)
    {
        HexCellShaderRefresh cell = cells[c];
        int index = cell.index;
        Color32 value = texutreData[index];
        if (shaderSettings.immediateMode)
        {
            value.r = cell.IsVisible ? (byte)255 : (byte)0;
            value.g = cell.IsExplored ? (byte)255 : (byte)0;
        }
        else
        {
            transitioningCells.AddNoResize(index);
        }
        value.a = (byte)cell.terrainTypeIndex;
        texutreData[index] = value;
    }
}

[BurstCompile]//,WithAll(typeof(HexShaderTransitionCells))]
public struct TransitionCellsJob : IJobParallelFor
{
    [ReadOnly]
    public NativeList<int> transitioningCells;

    [WriteOnly]
    public NativeList<int>.ParallelWriter removeCells;

    [NativeDisableParallelForRestriction]
    public NativeArray<Color32> texutreData;

    [ReadOnly, DeallocateOnJobCompletion]
    public NativeArray<HexCellShaderRefresh> cells;

    public int delta;
    public EntityCommandBuffer.ParallelWriter ecbEnd;

    public void Execute(int c)
    {
        int index = transitioningCells[c];
        HexCellShaderRefresh cell = cells[index];

        Color32 data = texutreData[index];
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
            removeCells.AddNoResize(c);
            // transitioningCells[c--] = transitioningCells[transitioningCells.Length - 1];
            // transitioningCells.RemoveAt(transitioningCells.Length - 1);
        }
        texutreData[index] = data;
    }
    /*
    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main, ref DynamicBuffer<HexShaderTransitioningCells> transitioningCells, ref DynamicBuffer<HexShaderTextureData> texutreData, in DynamicBuffer<HexCellShaderRefresh> cells)
    {
        NativeArray<HexCellShaderRefresh> sortableCells = cells.ToNativeArray(Allocator.Temp);
        sortableCells.Sort(new HexCellIndexSorter());

        NativeList<HexShaderTransitioningCells> transitioningCellsList = new (transitioningCells.Length, Allocator.Temp);
        transitioningCellsList.CopyFrom(transitioningCells.AsNativeArray());
        for (int c = 0; c < transitioningCells.Length; c++)
        {
            int index = transitioningCells[c];
            HexCellShaderRefresh cell = cells[index];

            Color32 data = texutreData[index];
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
            texutreData[index] = data;
        }
        transitioningCells.CopyFrom(transitioningCells);
        ecbEnd.RemoveComponent<HexCellShaderRefresh>(jobChunkIndex,main);
        ecbEnd.AddComponent<HexShaderAllCellRequest>(jobChunkIndex, main);
        ecbEnd.AddComponent<HexShaderPaintTexture>(jobChunkIndex, main);
        if (transitioningCells.Length == 0)
        {
            ecbEnd.RemoveComponent<HexShaderTransitionCells>(jobChunkIndex, main);
        }
    }
    */
}

[BurstCompile]
public struct CompleteCellTransition : IJob
{
    public NativeList<int> transitioningCells;
    public NativeList<int> transitionedCells;

    public void Execute()
    {
        transitionedCells.Sort();

        // this may need iterating through backwards
        for (int i = 0; i < transitionedCells.Length; i++)
        {
            transitioningCells.RemoveAt(transitionedCells[i]);
        }
    }
}