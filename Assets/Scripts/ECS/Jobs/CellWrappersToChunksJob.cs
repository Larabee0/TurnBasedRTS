using Unity.Burst;
using Unity.Entities;

[BurstCompile, WithNone(typeof(FindNeighbours), typeof(HexGridNeighbourEntitySet))]
public partial struct CellWrappersToChunksJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecbEnd;
    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main, in DynamicBuffer<HexChunkCellBuilder> neighbourChunkBuffer, in HexCellBasic basic, in HexCellTerrain terrain, in HexCellNeighbours neighbours)
    {
        for (int i = 0; i < neighbourChunkBuffer.Length; i++)
        {
            ecbEnd.AppendToBuffer(jobChunkIndex, neighbourChunkBuffer[i], new HexChunkCellWrapper()
            {
                cellBasic = basic,
                cellTerrain = terrain,
                cellNeighbours = neighbours
            });
            ecbEnd.AddComponent<HexChunkCellDataCompleted>(jobChunkIndex, neighbourChunkBuffer[i]);
        }
        ecbEnd.RemoveComponent<HexChunkCellBuilder>(jobChunkIndex, main);
    }
}
