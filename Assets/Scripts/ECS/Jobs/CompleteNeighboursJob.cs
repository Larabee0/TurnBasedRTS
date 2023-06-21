using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// This is worse, it means only 1 grid can be initilised per frame, but at least we aren't using buffer from entity right guys??
/// (seriously tho that would be even worse)
/// In the previous job we found the cell indices of the neighbouring cells but not the entities.
/// This job gets the entities and adds that information to each cells HexCellNeighbours.
/// 
/// So what actually happens here is the each cell knows the neigbour index but not lacks the entity reference.
/// The gird HexCellReference buffer contains a reference to the cell entities and it is ordered so the indicies
/// of <see cref="HexCellNeighbours"/> would return the correct entity, so this job goes through all the cells
/// and gets the entity reference for each valid neighbour.
/// becuase <see cref="HexCellReference"/> contains the chunk index.
/// It is also able to determine if the cell should be added to any directly adjacent neighbouring chunks simply by
/// checking if hte neighbouring chunk index is equal  to our current cell's chunk index.
/// If they are not equal we add append ourselves to the neighbouring cell's <see cref="HexCellSetReferenceInNeighbouringChunk"/> buffer
/// and another job will add the cell to the neighbouring chunk's <see cref="HexCellReference"/> buffer.
/// </summary>
[BurstCompile, WithAll(typeof(HexGridNeighbourEntitySet))]
public partial struct CompleteNeighboursJob : IJobEntity
{
    [ReadOnly, DeallocateOnJobCompletion]
    public NativeArray<HexCellReference> hexCells;
    public EntityCommandBuffer.ParallelWriter ecbEnd;

    public void Execute([ChunkIndexInQuery] int chunkIndex, Entity main, ref HexCellNeighbours neighbours, in HexCellChunkReference hexCellChunk)
    {
        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
        {
            int index = neighbours.GetNeighbourIndex(d);
            switch (index)
            {
                case -1:
                    continue;
                default:
                    HexCellReference neighbour = hexCells[index];
                    neighbours.SetNeighbourEntity(neighbour, d);
                    if (neighbour.ChunkIndex != hexCellChunk.chunkIndex)
                    {
                        ecbEnd.AppendToBuffer(chunkIndex, neighbour.Value, new HexCellSetReferenceInNeighbouringChunk { chunk = hexCellChunk.Value });
                    }
                    break;
            }
        }
        ecbEnd.RemoveComponent<HexGridNeighbourEntitySet>(chunkIndex, main);
    }
}
