using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

[UpdateInGroup(typeof(HexSystemGroup))]
public partial class HexShaderSystem : SystemBase
{
    private const float transitionSpeed = 255f;

    protected override void OnCreate()
    {
        EntityManager.AddComponent<HexShaderSettings>(SystemHandle);
        EntityManager.AddComponentObject(SystemHandle, new HexShaderCellTexture() { value = null });
        EntityManager.AddComponentData(SystemHandle, new HexShaderTransitioningCells { values = new(Allocator.Persistent) });
        EntityManager.AddComponent<HexShaderTextureData>(SystemHandle);
        NativeArray<EntityQuery> queries = new(5, Allocator.Temp);
        EntityQueryBuilder builder = new(Allocator.Temp);
        queries[0] = builder.WithAll<HexShaderInitialise>().Build(EntityManager);
        builder.Reset();
        queries[1] = builder.WithAll<HexShaderPaintTexture>().Build(EntityManager);
        builder.Reset();
        queries[2] = builder.WithAll<HexShaderTransitionCells, HexCellShaderRefreshWrapper>().Build(EntityManager);
        builder.Reset();
        queries[3] = builder.WithAll< HexShaderAllCellRequest >().Build(EntityManager);
        builder.Reset();
        queries[4] = builder.WithAll<HexShaderCellDataComplete, HexShaderRefreshAll>().Build(EntityManager);
        //RequireAnyForUpdate(queries);

        builder.Dispose();

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

        TransitionCells(ecbEndParallel);

        PaintTerrainTexture();

        OnUpdateRefreshAll(ecbEndParallel);
    }

    private void PaintTerrainTexture()
    {
        // check if texture paint has been requested
        if (SystemAPI.HasSingleton<HexShaderPaintTexture>())
        {
            // get texture instance
            Texture2D shaderTexture = EntityManager.GetComponentObject<HexShaderCellTexture>(SystemHandle).value;
            if (shaderTexture != null)
            {
                // get texture data buffer (buffer of Color32) also get the PixelData from the texture.
                // using the UnsafeUtility we can copy the buffer to the PixelData Array.
                // this is done by getting the underlying memory pointer for the native arrays (GetUnsafePtr)
                // This can only be done in an unsafe context.
                
                unsafe
                {
                    UnsafeUtility.MemCpy(shaderTexture.GetPixelData<Color32>(0).GetUnsafePtr(),
                        SystemAPI.GetSingleton<HexShaderTextureData>().values.GetUnsafeReadOnlyPtr(),
                        shaderTexture.height * shaderTexture.width * UnsafeUtility.SizeOf<Color32>());
                }
                // remove repaint tag
                EntityManager.RemoveComponent<HexShaderPaintTexture>(SystemHandle);
                // upload texture data to the GPU frame buffer
                shaderTexture.Apply();
            }
        }
    }

    private void TransitionCells(EntityCommandBuffer.ParallelWriter ecbEndParallel)
    {
        if (SystemAPI.HasSingleton<HexShaderTransitionCells>() && SystemAPI.TryGetSingletonBuffer<HexCellShaderRefreshWrapper>(out _, true))
        {
            EntityManager.AddComponent<HexShaderAllCellRequest>(SystemHandle);
            EntityManager.AddComponent<HexShaderPaintTexture>(SystemHandle);
            NativeList<int> transitioningCells = SystemAPI.GetSingleton<HexShaderTransitioningCells>().values;
            if (transitioningCells.Length == 0)
            {
                EntityManager.RemoveComponent<HexShaderTransitionCells>(SystemHandle);
                EntityManager.RemoveComponent<HexCellShaderRefreshWrapper>(SystemHandle);
            }
            else
            {
                int intDelta = (int)(SystemAPI.Time.DeltaTime * transitionSpeed);
                intDelta = intDelta == 0 ? 1 : intDelta;
                NativeList<int> transitionedCells = new(transitioningCells.Length, Allocator.TempJob);
                Dependency = new TransitionCellsJob
                {
                    transitioningCells = transitioningCells,
                    texutreData = SystemAPI.GetSingleton<HexShaderTextureData>().values,
                    cells = SystemAPI.GetSingletonBuffer<HexCellShaderRefreshWrapper>().ToNativeArray(Allocator.TempJob),
                    removeCells = transitionedCells.AsParallelWriter(),
                    delta = intDelta,
                    ecbEnd = ecbEndParallel
                }.Schedule(transitioningCells.Length, 64, Dependency);

                Dependency = new CompleteCellTransitionJob
                {
                    transitioningCells = transitioningCells,
                    transitionedCells = transitionedCells,
                }.Schedule(Dependency);
            }
        }
    }

    private void OnUpdateRefreshAll(EntityCommandBuffer.ParallelWriter ecbEndParallel)
    {
        HexShaderSettings shaderSettings = SystemAPI.GetSingleton<HexShaderSettings>();
        if (SystemAPI.HasSingleton<HexShaderCellDataComplete>() && SystemAPI.HasSingleton<HexShaderRefreshAll>())
        {
            DynamicBuffer<HexCellShaderRefreshWrapper> shaderCellData = SystemAPI.GetSingletonBuffer<HexCellShaderRefreshWrapper>();
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
            EntityManager.RemoveComponent<HexCellShaderRefreshWrapper>(SystemHandle);
            EntityManager.AddComponent<HexShaderPaintTexture>(SystemHandle);
        }
        else if (shaderSettings.grid != Entity.Null && SystemAPI.HasSingleton<HexShaderAllCellRequest>())
        {
            EntityManager.RemoveComponent<HexCellShaderRefreshWrapper>(SystemHandle);
            var requestJob = new RequestCellDataForShaderJob
            {
                ecbEnd = ecbEndParallel,
                hexCells = SystemAPI.GetBuffer<HexCellReference>(shaderSettings.grid).ToNativeArray(Allocator.TempJob)
            };
            Dependency = requestJob.Schedule(requestJob.hexCells.Length, 64, Dependency);

            EntityManager.AddComponent<HexCellShaderRefreshWrapper>(SystemHandle);
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

    private EntityCommandBuffer.ParallelWriter GetParallelEntityCommandBuffer()
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(World.Unmanaged).AsParallelWriter();
        return ecb;
    }
}
