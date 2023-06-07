using Unity.Burst;
using Unity.Entities;

/// <summary>
/// This is kind of awful and you can probably mathematically determine everything here
/// but given the grid sizes change and we have Burst, this is running in parallel on all cells and it will only ever run once per grid, meh
/// </summary>
[BurstCompile]
public partial struct FindCellNeighboursJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecbEnd;
    public void Execute([ChunkIndexInQuery] int chunkIndex, Entity main, ref HexCellNeighbours neighbours, in HexCellBasic basic, in FindNeighbours findNeighbours)
    {
        if (basic.rawX < findNeighbours.cellCountX - 1)
        {
            HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.E, basic.Index + 1);
            if (basic.wrapping && basic.rawX == 0)
            {
                HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.W, basic.Index + findNeighbours.cellCountX - 1);
            }
        }
        if (basic.rawX > 0)
        {
            HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.W, basic.Index - 1);
            if (basic.wrapping && basic.rawX == findNeighbours.cellCountX - 1)
            {
                HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.E, basic.Index - basic.rawX);
            }
        }
        if (basic.rawZ < findNeighbours.cellCountZ - 1)
        {
            switch (basic.rawZ & 1)
            {
                case 0:
                    HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.NE, basic.Index + findNeighbours.cellCountX);
                    if (basic.rawX > 0)
                    {
                        HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.NW, basic.Index + findNeighbours.cellCountX - 1);
                    }
                    else if (basic.wrapping)
                    {
                        HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.NW, basic.Index + findNeighbours.cellCountX * 2 - 1);
                    }
                    break;
                default:
                    HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.NW, basic.Index + findNeighbours.cellCountX);
                    if (basic.rawX < findNeighbours.cellCountX - 1)
                    {
                        HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.NE, basic.Index + findNeighbours.cellCountX + 1);
                    }
                    else if (basic.wrapping)
                    {
                        HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.NE, basic.Index + 1);
                    }
                    break;
            }
        }
        if (basic.rawZ > 0)
        {
            switch (basic.rawZ & 1)
            {
                case 0:
                    HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.SE, basic.Index - findNeighbours.cellCountX);
                    if (basic.rawX > 0)
                    {
                        HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.SW, basic.Index - findNeighbours.cellCountX - 1);
                    }
                    else if (basic.wrapping)
                    {
                        HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.SW, basic.Index - 1);
                    }
                    break;
                default:
                    HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.SW, basic.Index - findNeighbours.cellCountX);
                    if (basic.rawX < findNeighbours.cellCountX - 1)
                    {
                        HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.SE, basic.Index - findNeighbours.cellCountX + 1);
                    }
                    else if (basic.wrapping)
                    {
                        HexCellNeighbours.SetNeighbour(ref neighbours, HexDirection.SE, basic.Index - findNeighbours.cellCountX * 2 + 1);
                    }
                    break;
            }
        }

        ecbEnd.RemoveComponent<FindNeighbours>(chunkIndex, main);
        ecbEnd.AddComponent<HexGridNeighbourEntitySet>(chunkIndex, main);
        ecbEnd.AddBuffer<HexCellSetReferenceInNeighbouringChunk>(chunkIndex, main);
    }
}
