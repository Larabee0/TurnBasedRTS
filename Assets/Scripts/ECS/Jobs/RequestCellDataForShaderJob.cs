using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

/// <summary>
/// Job run by the <see cref="HexShaderSystem"/> when it needs all the cell data in the <see cref="HexCellShaderRefreshWrapper"/> buffer to be
/// updated.
/// this runs in parallel on the hexCells native array provided, taken from the grid root entity.
/// </summary>
[BurstCompile]
public struct RequestCellDataForShaderJob : IJobParallelFor
{
    [ReadOnly, DeallocateOnJobCompletion]
    public NativeArray<HexCellReference> hexCells;

    public EntityCommandBuffer.ParallelWriter ecbEnd;

    public void Execute(int index)
    {
        ecbEnd.AddComponent<HexShaderRefresh>(hexCells.Length, hexCells[index].Value);
    }
}
