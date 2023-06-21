using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

/// <summary>
/// <see cref="HexShaderSystem"/> related job.
/// This sets the cell is visible and explored in the <see cref="HexShaderCellTexture"/> (eventually)
/// OR if immidate mode is disabled, it queues the cells to be transitioned.
/// This job also sets the terrain type index in the shader so the game can give the cell the right colour.
/// </summary>
[BurstCompile]
public struct RefreshAllCellsJob : IJobParallelFor
{
    public HexShaderSettings shaderSettings;

    [WriteOnly]
    public NativeList<int>.ParallelWriter transitioningCells;

    [ReadOnly, DeallocateOnJobCompletion]
    public NativeArray<HexCellShaderRefreshWrapper> cells;

    [NativeDisableParallelForRestriction]
    public NativeArray<Color32> texutreData;

    public void Execute(int c)
    {
        HexCellShaderRefreshWrapper cell = cells[c];
        int index = cell.index;
        Color32 value = texutreData[index];
        if (shaderSettings.immediateMode)
        {
            value.r = cell.IsVisible ? (byte)255 : (byte)0;
            value.g = cell.IsExplored ? (byte)255 : (byte)0;
        }
        else
        {
            transitioningCells.AddNoResize(index);
        }
        value.a = (byte)cell.terrainTypeIndex;
        texutreData[index] = value;
    }
}
