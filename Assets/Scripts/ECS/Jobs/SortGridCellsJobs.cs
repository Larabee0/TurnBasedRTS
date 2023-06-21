using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// Because <see cref="InstantiateCellsJob"/> runs asyncronously there is the danger cells are added
/// to the grid's <see cref="HexCellReference"/> buffer out of order (not grouped in chunks). So it must be sorted by chunk index
/// for <see cref="InitialiseCellsJob"/> to run properly
/// </summary>
[BurstCompile, WithAll(typeof(HexGridBasic), typeof(HexGridSortCells))]
public partial struct SortHexCellChunkJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecbEnd;
    public void Execute([ChunkIndexInQuery] int chunkIndex, Entity main, ref DynamicBuffer<HexCellReference> hexCellBuffer)
    {
        // reinterpret the buffer an array so it can be sorted
        NativeArray<HexCellReference> cells = hexCellBuffer.AsNativeArray();
        cells.Sort(new HexCellChunkSorter()); // sort the array by chunk index
        ecbEnd.RemoveComponent<HexGridSortCells>(chunkIndex, main);
        ecbEnd.AddComponent<HexGridInitialiseCells>(chunkIndex, main);
    }
}

/// <summary>
/// We don't actually want the grid  <see cref="HexCellReference"/> buffer sorted by chunk index after creation has finished
/// so we run this job to sort it by cell index
/// </summary>
[BurstCompile, WithAll(typeof(HexGridBasic), typeof(HexGridNeighbourEntitySetUnsorted))]
public partial struct SortHexCellIndexJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecbEnd;
    public void Execute([ChunkIndexInQuery] int chunkIndex, Entity main, ref DynamicBuffer<HexCellReference> hexCellBuffer)
    {
        // reinterpret the buffer an array so it can be sorted
        NativeArray<HexCellReference> cells = hexCellBuffer.AsNativeArray();
        cells.Sort(new HexCellIndexSorter()); // sort the array by chunk index
        ecbEnd.RemoveComponent<HexGridNeighbourEntitySetUnsorted>(chunkIndex, main);
        ecbEnd.RemoveComponent<HexGridUnInitialised>(chunkIndex, main);
        ecbEnd.AddComponent<HexGridNeighbourEntitySet>(chunkIndex, main);
    }
}