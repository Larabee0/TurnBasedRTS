using Unity.Burst;
using Unity.Entities;

/// <summary>
/// This job appends the cell to a target entities <see cref="HexChunkCellWrapper"/> buffer, originally just for chunks but now the map generator
/// uses the same system to get all cell data.
/// 
/// The hexCell contains a buffer called <see cref="HexChunkCellBuilder"/> which contains a list of entities that want data stored in this cell
/// entity. The job loops through the buffer and adds itself as a <see cref="HexChunkCellWrapper"/> then removes the <see cref="HexChunkCellBuilder"/>
/// buffer from itself.
/// We assume if this job is running that the target entity has  a <see cref="HexChunkCellWrapper"/> buffer. If it doesn't the entity command
/// buffer will raise an error in the relevant ecb system.
/// </summary>
[BurstCompile, WithNone(typeof(FindNeighbours), typeof(HexGridNeighbourEntitySet))]
public partial struct CellWrappersToChunksJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecbEnd;
    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main, in DynamicBuffer<HexChunkCellBuilder> neighbourChunkBuffer, in HexCellBasic basic, in HexCellTerrain terrain, in HexCellNeighbours neighbours)
    {
        HexChunkCellWrapper wrapper = new()
        {
            cellBasic = basic,
            cellTerrain = terrain,
            cellNeighbours = neighbours
        };
        for (int i = 0; i < neighbourChunkBuffer.Length; i++)
        {
            ecbEnd.AppendToBuffer(jobChunkIndex, neighbourChunkBuffer[i], wrapper);
            ecbEnd.AddComponent<HexChunkCellDataCompleted>(jobChunkIndex, neighbourChunkBuffer[i]);
        }
        ecbEnd.RemoveComponent<HexChunkCellBuilder>(jobChunkIndex, main);
    }
}
