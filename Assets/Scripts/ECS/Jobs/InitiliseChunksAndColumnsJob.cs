using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;


/// <summary>
/// Used to Create the colums and then the grid chunks within those columns
/// The columns enable infinte grid wrapping in the x axis and the chunks provide a good sized container for a group of cells
/// <see cref="HexMetrics.chunkSizeX"/>
/// A column is 1 chunk wide and <see cref="HexMetrics.chunkSizeZ"/> tall.
/// 
/// The chunks provide the mesh rendering capabilities needed to show the map. They also play host the HexCell entities themselves
/// 
/// </summary>
[BurstCompile, WithAll(typeof(HexGridUnInitialised))]
public partial struct InitiliseChunksAndColumnsJob : IJobEntity
{
    public Entity HexGridChunkPrefab;
    public EntityCommandBuffer.ParallelWriter ecbEnd;

    public void Execute([ChunkIndexInQuery] int chunkIndex, Entity main, in HexGridBasic basic, in HexGridCreateChunks createChunks)
    {
        NativeArray<Entity> cols = new(createChunks.columns, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < createChunks.columns; i++)
        {
            Entity temp = cols[i] = ecbEnd.CreateEntity(chunkIndex);
            ecbEnd.AddComponent(chunkIndex, temp, new Parent { Value = main });
            ecbEnd.AddComponent(chunkIndex, temp, new HexGridColumn { Index = i });
            ecbEnd.AddComponent(chunkIndex, temp, new LocalTransform { Position = float3.zero, Scale = 1, Rotation = quaternion.identity });
            ecbEnd.AddComponent<LocalToWorld>(chunkIndex, temp);

            ecbEnd.AppendToBuffer(chunkIndex, main, new HexGridColumnBuffer { Index = i, Value = temp });
        }

        for (int z = 0, i = 0; z < basic.chunkCountZ; z++)
        {
            for (int x = 0; x < basic.chunkCountX; x++, i++)
            {
                Entity temp = ecbEnd.Instantiate(chunkIndex, HexGridChunkPrefab);
                ecbEnd.AddComponent(chunkIndex, temp, new InitColumnIndex { Index = x });
                ecbEnd.AddComponent(chunkIndex, temp, new Parent { Value = cols[x] });
                ecbEnd.AddComponent(chunkIndex, temp, new HexGridReference { Value = main });
                ecbEnd.SetComponent(chunkIndex, temp, new HexChunkTag { Index = i });
                ecbEnd.AppendToBuffer(chunkIndex, main, new HexGridChunkBuffer { Index = i, Value = temp });
            }
        }
        ecbEnd.AddBuffer<HexCellReference>(chunkIndex, main);
        ecbEnd.RemoveComponent<HexGridCreateChunks>(chunkIndex, main);
    }
}

