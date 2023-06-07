using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

/// <summary>
/// Instantiates the HexCells as children of the chunks, adds them to the HexGrid entities HexCellReference buffer & the chunks local HexCellReference buffer.
/// At Instantiate time the cells only know their parent (chunk) and its chunk index
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
            ecbEnd.AppendToBuffer(chunkIndex, gridReference.Value, chunkCells[i]);
        }
        ecbEnd.AddBuffer<HexCellReference>(chunkIndex, main).CopyFrom(chunkCells);
        ecbEnd.AddComponent<HexGridSortCells>(chunkIndex, gridReference.Value);
    }
}

