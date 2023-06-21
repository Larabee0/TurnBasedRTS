using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

/// <summary>
/// Instantiates the HexCells as children of the chunks, adds them to the HexGrid root entity's HexCellReference buffer &
/// the chunks local HexCellReference buffers, which contain only cells within the chunk at this point.
/// At Instantiate time the cells only know their parent (chunk) and its chunk index
/// 
/// This job runs in parallel on all chunk entities.
/// </summary>
[BurstCompile, WithNone(typeof(HexCellReference))]
public partial struct InstantiateCellsJob : IJobEntity
{
    public Entity HexCellPrefab;
    public EntityCommandBuffer.ParallelWriter ecbEnd;
    public void Execute([ChunkIndexInQuery] int chunkIndex, Entity main, in HexChunkTag chunkTag, in HexGridReference gridReference)
    {
        NativeArray<HexCellReference> chunkCells = new(HexMetrics.chunkSizeX * HexMetrics.chunkSizeZ, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < chunkCells.Length; i++)
        {
            Entity temp = ecbEnd.Instantiate(chunkIndex, HexCellPrefab);
            ecbEnd.AddComponent(chunkIndex, temp, new Parent { Value = main });
            chunkCells[i] = new() { Value = temp, ChunkIndex = chunkTag.Index };
            ecbEnd.AppendToBuffer(chunkIndex, gridReference, chunkCells[i]);
        }
        ecbEnd.AddBuffer<HexCellReference>(chunkIndex, main).CopyFrom(chunkCells);
        ecbEnd.AddComponent<HexGridSortCells>(chunkIndex, gridReference);
    }
}

