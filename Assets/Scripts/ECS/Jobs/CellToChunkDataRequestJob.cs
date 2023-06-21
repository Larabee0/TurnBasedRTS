using Unity.Burst;
using Unity.Entities;

/// <summary>
/// When a chunk needs to triangulate it needs the cell data about the cells within itself and also that directly neighbour itself.
/// These cell references are stored in two buffers <see cref="HexCellReference"/> which stores the cells inside this chunk
/// and <see cref="HexCellChunkNeighbour"/> which stores the directly adjacent neighbours.
/// 
/// This job loops through both buffers and using the ecbEnd and ecbBegin buffer, adds a <see cref="HexChunkCellBuilder"/> buffer to each hexCell
/// that will be requested by the chunk, then adds the current chunk to that buffer so <see cref="CellWrappersToChunksJob"/> will run.
/// 
/// This job runs in parallel on all chunks being refreshed so we need to take advantage of the entity command buffers running at different parts of
/// the frame render cycle in order to avoid errors.
/// if we did everything here in the ecbEnd buffer, its possible we will throw an error about a missing buffer on the hexCell even thoguh AddBuffer
/// is called first.
/// So instead we make sure to call addbuffer for ecb end, and append items to the buffer in ecb begin.
/// the order the ecbs run here is ecbEnd > ecbBegin. this job will run mid frame render cycle, before ecbEnd but after ecbBegin.
/// This means by the time the next ecbBegin system update occurs, ecbEnd will have run and all the hexCells will have the buffer
/// and thus we can safely add to that buffer, even though it won't exist when we call it here.
/// </summary>
[BurstCompile, WithAll(typeof(HexChunkTag), typeof(HexChunkRefresh)), WithNone(typeof(HexChunkCellWrapper), typeof(HexChunkMeshUpdating))]
public partial struct CellToChunkDataRequestJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecbEnd;
    public EntityCommandBuffer.ParallelWriter ecbBegin;

    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main, in DynamicBuffer<HexCellReference> chunkCellBuffer, in DynamicBuffer<HexCellChunkNeighbour> neighbourChunkCellBuffer)
    {
        HexChunkCellBuilder cellBuilder = new() { Chunk = main };

        for (int i = 0; i < chunkCellBuffer.Length; i++)
        {
            ecbEnd.AddBuffer<HexChunkCellBuilder>(jobChunkIndex, chunkCellBuffer[i].Value);
            ecbBegin.AppendToBuffer(jobChunkIndex, chunkCellBuffer[i].Value, cellBuilder);
        }

        for (int i = 0; i < neighbourChunkCellBuffer.Length; i++)
        {
            ecbEnd.AddBuffer<HexChunkCellBuilder>(jobChunkIndex, neighbourChunkCellBuffer[i].value);
            ecbBegin.AppendToBuffer(jobChunkIndex, neighbourChunkCellBuffer[i].value, cellBuilder);
        }

        ecbEnd.AddBuffer<HexChunkCellWrapper>(jobChunkIndex, main);
    }
}
