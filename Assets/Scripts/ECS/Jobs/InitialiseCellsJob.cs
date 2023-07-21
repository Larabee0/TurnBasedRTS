using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Initilises the cells in the grid
/// </summary>
[BurstCompile, WithAll(typeof(HexGridInitialiseCells)), WithNone(typeof(HexGridSortCells), typeof(HexGridCreateChunks))]
public partial struct InitialiseCellsJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecbEnd;

    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main,
        ref DynamicBuffer<HexCellReference> cellBuffer,
        in HexGridBasic basic, in DynamicBuffer<HexGridChunkBuffer> chunkBuffer)
    {
        int cellCountX = basic.cellCountX;
        int cellCountZ = basic.cellCountZ;
        int chunkSize = HexMetrics.chunkSizeX * HexMetrics.chunkSizeZ;

        for (int gridZ = 0, cellIndex = 0; gridZ < cellCountZ; gridZ++)
        {
            for (int gridX = 0; gridX < cellCountX; gridX++, cellIndex++)
            {
                int chunkX = gridX / HexMetrics.chunkSizeX;
                int chunkZ = gridZ / HexMetrics.chunkSizeZ;

                // cells are stored sorted by chunk Index at this part of initilisation.
                int chunkIndex = chunkX + chunkZ * basic.chunkCountX;

                // compute index of current cell in main Grid cellBuffer
                int localX = gridX - chunkX * HexMetrics.chunkSizeX;
                int localZ = gridZ - chunkZ * HexMetrics.chunkSizeZ;
                int cellBufferIndex = localX + localZ * HexMetrics.chunkSizeX + (chunkIndex * chunkSize);

                // set cellIndex in HexCellReference buffer
                HexCellReference cell = cellBuffer[cellBufferIndex];
                cell.Index = cellIndex;
                cellBuffer[cellBufferIndex] = cell;

                // set HexCellBasic
                HexCellBasic cellBasic = new()
                {
                    Index = cellIndex,
                    ColumnIndex = gridX / HexMetrics.chunkSizeX,
                    rawX = gridX,
                    rawZ = gridZ,
                    wrapping = basic.wrapping,
                    Position = new float3()
                    {
                        x = (gridX + gridZ * 0.5f - gridZ / 2) * (HexMetrics.innerRadius * 2f),
                        y = 0f,
                        z = gridZ * (HexMetrics.outerRadius * 1.5f)
                    },
                    Coorindate = HexCoordinates.FromOffsetCoordinates(gridX, gridZ, basic.wrapSize)
                };

                // give cell an entity reference to the grid - obsolete
                // ecbEnd.SetComponent(jobChunkIndex, cell.Value, new HexGridReference { Value = main });
                // set HexCellBasic data on entity
                ecbEnd.SetComponent(jobChunkIndex, cell.Value, cellBasic);
                // add component used when finding cell neighbours
                ecbEnd.AddComponent(jobChunkIndex, cell.Value, new FindNeighbours { cellCountX = cellCountX, cellCountZ = cellCountZ, chunkCountX = basic.chunkCountX });
                // add chunk entity reference based on the cells chunkIndex
                ecbEnd.AddComponent(jobChunkIndex, cell.Value, new HexCellChunkReference { Value = chunkBuffer[chunkIndex].Value,chunkIndex = chunkIndex });
            }
        }

        // create an array equal to the number of cells in a chunk
        NativeArray<HexCellReference> chunkCells = new(chunkSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

        // fill the array with all cells in the chunk (because the cellBuffer was sorted this can be easily computed)
        // we loop through the whole array and the cells are ordered such that all cells in the same chunk are next to each other
        // we can start at i = 0 and chunkIndex = 0 and using the mod operator (%) can fill this native array with all
        // the cells for a chunk, then copy that array to the chunk before overrwriting it again with the next chunk
        for (int i = 0, cellToChunkIndex = 0; i < cellBuffer.Length; i++, cellToChunkIndex = (cellToChunkIndex + 1) % chunkSize)
        {
            chunkCells[cellToChunkIndex] = cellBuffer[i];
            // when we fill the array, set the HexCellReference buffer of the given chunk
            // this way we reuse the same array for every chunk rather than allocating memory for each chunk.
            if (cellToChunkIndex == chunkSize - 1)
            {
                ecbEnd.SetBuffer<HexCellReference>(jobChunkIndex, chunkBuffer[cellBuffer[i].ChunkIndex].Value).CopyFrom(chunkCells);
            }
        }

        // finished Cell Initialisation, remove comp to prevent rerunning the job
        ecbEnd.RemoveComponent<HexGridInitialiseCells>(jobChunkIndex, main);

        // call for cellNeighbour setting
        ecbEnd.AddComponent<HexGridNeighbourEntitySetUnsorted>(jobChunkIndex, main);
    }
}
