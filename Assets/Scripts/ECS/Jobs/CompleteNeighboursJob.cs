using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

/// <summary>
/// This is worse, it means only 1 grid can be initilised per frame, but at least we aren't using buffer from entity right guys?? (seriously tho that would be even worse)
/// In the previous job we found the cell indices of the neighbouring cells but not the entities.
/// This job gets the entities and adds that information to each cells HexCellNeighbours.
/// </summary>
[BurstCompile, WithAll(typeof(HexGridNeighbourEntitySet))]
public partial struct CompleteNeighboursJob : IJobEntity
{
    [ReadOnly,DeallocateOnJobCompletion]
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
                    HexCellNeighbours.SetNeighbourEntity(ref neighbours, neighbour, d);
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
