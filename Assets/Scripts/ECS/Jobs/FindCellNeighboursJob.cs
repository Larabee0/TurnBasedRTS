using Unity.Burst;
using Unity.Entities;

/// <summary>
/// This is kind of awful and you can probably mathematically determine everything here
/// but given the grid sizes change and we have Burst, this is running in parallel on all cells and it will only ever run once per grid, meh
/// This computationally works out what cells a cell neighbours in all <see cref="HexDirection"/>
/// </summary>
[BurstCompile]
public partial struct FindCellNeighboursJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecbEnd;
    public void Execute([ChunkIndexInQuery] int chunkIndex, Entity main, ref HexCellNeighbours curNeighbours, in HexCellBasic curBasic, in FindNeighbours curFindNeighbours)
    {
        if (curBasic.rawX < curFindNeighbours.cellCountX - 1)
        {
            curNeighbours.SetNeighbour(HexDirection.E, curBasic.Index + 1);
            if (curBasic.wrapping && curBasic.rawX == 0)
            {
                curNeighbours.SetNeighbour(HexDirection.W, curBasic.Index + curFindNeighbours.cellCountX - 1);
            }
        }
        if (curBasic.rawX > 0)
        {
            curNeighbours.SetNeighbour(HexDirection.W, curBasic.Index - 1);
            if (curBasic.wrapping && curBasic.rawX == curFindNeighbours.cellCountX - 1)
            {
                curNeighbours.SetNeighbour(HexDirection.E, curBasic.Index - curBasic.rawX);
            }
        }
        if (curBasic.rawZ < curFindNeighbours.cellCountZ - 1)
        {
            switch (curBasic.rawZ & 1)
            {
                case 0:
                    curNeighbours.SetNeighbour(HexDirection.NE, curBasic.Index + curFindNeighbours.cellCountX);
                    if (curBasic.rawX > 0)
                    {
                        curNeighbours.SetNeighbour(HexDirection.NW, curBasic.Index + curFindNeighbours.cellCountX - 1);
                    }
                    else if (curBasic.wrapping)
                    {
                        curNeighbours.SetNeighbour(HexDirection.NW, curBasic.Index + curFindNeighbours.cellCountX * 2 - 1);
                    }
                    break;
                default:
                    curNeighbours.SetNeighbour(HexDirection.NW, curBasic.Index + curFindNeighbours.cellCountX);
                    if (curBasic.rawX < curFindNeighbours.cellCountX - 1)
                    {
                        curNeighbours.SetNeighbour(HexDirection.NE, curBasic.Index + curFindNeighbours.cellCountX + 1);
                    }
                    else if (curBasic.wrapping)
                    {
                        curNeighbours.SetNeighbour(HexDirection.NE, curBasic.Index + 1);
                    }
                    break;
            }
        }
        if (curBasic.rawZ > 0)
        {
            switch (curBasic.rawZ & 1)
            {
                case 0:
                    curNeighbours.SetNeighbour(HexDirection.SE, curBasic.Index - curFindNeighbours.cellCountX);
                    if (curBasic.rawX > 0)
                    {
                        curNeighbours.SetNeighbour(HexDirection.SW, curBasic.Index - curFindNeighbours.cellCountX - 1);
                    }
                    else if (curBasic.wrapping)
                    {
                        curNeighbours.SetNeighbour(HexDirection.SW, curBasic.Index - 1);
                    }
                    break;
                default:
                    curNeighbours.SetNeighbour(HexDirection.SW, curBasic.Index - curFindNeighbours.cellCountX);
                    if (curBasic.rawX < curFindNeighbours.cellCountX - 1)
                    {
                        curNeighbours.SetNeighbour( HexDirection.SE, curBasic.Index - curFindNeighbours.cellCountX + 1);
                    }
                    else if (curBasic.wrapping)
                    {
                        curNeighbours.SetNeighbour( HexDirection.SE, curBasic.Index - curFindNeighbours.cellCountX * 2 + 1);
                    }
                    break;
            }
        }

        ecbEnd.RemoveComponent<FindNeighbours>(chunkIndex, main);
        ecbEnd.AddComponent<HexGridNeighbourEntitySet>(chunkIndex, main);
        ecbEnd.AddBuffer<HexCellSetReferenceInNeighbouringChunk>(chunkIndex, main);
    }
}
