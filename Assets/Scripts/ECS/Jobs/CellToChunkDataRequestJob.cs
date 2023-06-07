using Unity.Burst;
using Unity.Entities;

[BurstCompile, WithAll(typeof(HexChunkTag), typeof(HexChunkRefresh)), WithNone(typeof(HexChunkCellWrapper), typeof(HexChunkMeshUpdating))]
public partial struct CellToChunkDataRequestJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecbEnd;
    public EntityCommandBuffer.ParallelWriter ecbBegin;

    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main, in DynamicBuffer<HexCellReference> chunkCellBuffer, in DynamicBuffer<HexCellChunkNeighbour> neighbourChunkCellBuffer)
    {
        for (int i = 0; i < chunkCellBuffer.Length; i++)
        {
            ecbEnd.AddBuffer<HexChunkCellBuilder>(jobChunkIndex, chunkCellBuffer[i].Value);
            //ecbEnd.RemoveComponent<HexCellChunkNeighbour>(jobChunkIndex, chunkCellBuffer[i].Value);
            //ecbEnd.AddBuffer<HexCellChunkNeighbour>(jobChunkIndex, chunkCellBuffer[i].Value);
            //ecbEnd.AddComponent<HexCellGetChunkNeighbour>(jobChunkIndex, chunkCellBuffer[i].Value);
            ecbBegin.AppendToBuffer(jobChunkIndex, chunkCellBuffer[i].Value, new HexChunkCellBuilder { Chunk = main });
        }

        for (int i = 0; i < neighbourChunkCellBuffer.Length; i++)
        {
            ecbEnd.AddBuffer<HexChunkCellBuilder>(jobChunkIndex, neighbourChunkCellBuffer[i].value);
            ecbBegin.AppendToBuffer(jobChunkIndex, neighbourChunkCellBuffer[i].value, new HexChunkCellBuilder { Chunk = main });
        }

        ecbEnd.AddBuffer<HexChunkCellWrapper>(jobChunkIndex, main);
    }
}
