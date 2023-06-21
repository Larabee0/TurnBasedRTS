using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// This job adds hexcell entity references to chunks stored in the cells <see cref="HexCellSetReferenceInNeighbouringChunk"/> buffer.
/// The buffer is removed after this job runs.
/// This saves having to compute the neighbouring chunks cells every time a chunk is triangulated.
/// </summary>
[BurstCompile, WithNone(typeof(HexGridNeighbourEntitySet))]
public partial struct ProvideChunkNeighbourCellsJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;
    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main, in DynamicBuffer<HexCellSetReferenceInNeighbouringChunk> targetChunks)
    {
        NativeHashSet<Entity> consumedChunks = new(targetChunks.Length, Allocator.Temp);

        for (int i = 0; i < targetChunks.Length; i++)
        {
            if (!consumedChunks.Contains(targetChunks[i]))
            {
                consumedChunks.Add(targetChunks[i]);
                ecb.AppendToBuffer(jobChunkIndex, targetChunks[i], new HexCellChunkNeighbour { value = main });
            }
        }

        ecb.RemoveComponent<HexCellSetReferenceInNeighbouringChunk>(jobChunkIndex, main);
    }
}
