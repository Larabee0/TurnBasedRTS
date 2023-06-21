using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

/// <summary>
/// This transitions the visiblity and exploration of a cell for the hex units which have not yet been added.
/// Fog of war basically.
/// </summary>
[BurstCompile]
public struct TransitionCellsJob : IJobParallelFor
{
    [ReadOnly]
    public NativeList<int> transitioningCells;

    [WriteOnly]
    public NativeList<int>.ParallelWriter removeCells;

    [NativeDisableParallelForRestriction]
    public NativeArray<Color32> texutreData;

    [ReadOnly, DeallocateOnJobCompletion]
    public NativeArray<HexCellShaderRefreshWrapper> cells;

    public int delta;
    public EntityCommandBuffer.ParallelWriter ecbEnd;

    public void Execute(int c)
    {
        int index = transitioningCells[c];
        HexCellShaderRefreshWrapper cell = cells[index];

        Color32 data = texutreData[index];
        bool stillUpdating = false;

        if (cell.IsExplored && data.g < 255)
        {
            stillUpdating = true;
            int t = data.g + delta;
            data.g = t >= 255 ? (byte)255 : (byte)t;
        }

        if (cell.IsVisible)
        {
            if (data.r < 255)
            {
                stillUpdating = true;
                int t = data.r + delta;
                data.r = t >= 255 ? (byte)255 : (byte)t;
            }
        }
        else if (data.r > 0)
        {
            stillUpdating = true;
            int t = data.r - delta;
            data.r = t < 0 ? (byte)0 : (byte)t;
        }

        if (!stillUpdating)
        {
            data.b = 0;
            removeCells.AddNoResize(c);
        }

        texutreData[index] = data;
    }
}
