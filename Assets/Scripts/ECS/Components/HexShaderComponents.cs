using UnityEngine;
using Unity.Entities;
using Unity.Collections;

/// <summary>
/// Tag to trigger <see cref="HexShaderSystem"/> to repaint the terrain texture with the current information
/// from the <see cref="HexShaderTextureData"/> buffer
/// </summary>
public struct HexShaderPaintTexture : IComponentData { }

/// <summary>
/// Tag to trigger <see cref="HexShaderSystem"/> to run the <see cref="TransitionCellsJob"/>  until <see cref="HexShaderTransitioningCells"/>
/// contains 0 items.
/// </summary>
public struct HexShaderTransitionCells : IComponentData { }

/// <summary>
/// tag to indicate that the <see cref="ProvideCellsToShaderJob"/> job has run and that if <see cref="HexShaderRefreshAll"/> is present
/// it is time to schedule the <see cref="RefreshAllCellsJob"/> job.
/// </summary>
public struct HexShaderCellDataComplete : IComponentData { }

/// <summary>
/// Tag to request <see cref="HexShaderSystem"/> to begin a whole map refresh of its data, even if it is currenly in the process
/// of doing so.
/// </summary>
public struct  HexShaderAllCellRequest : IComponentData { }

/// <summary>
/// Tag for HexCells to trigger them to provide their data to <see cref="HexShaderSystem"/> in the <see cref="HexCellShaderRefreshWrapper"/> buffer
/// </summary>
public struct HexShaderRefresh : IComponentData { }

/// <summary>
/// Tag to indicate <see cref="HexShaderSystem"/> is currently in the process of doing a whole map refresh of its data.
/// </summary>
public struct HexShaderRefreshAll : IComponentData { }

/// <summary>
/// Requests the <see cref="HexShaderSystem"/> to reinitialise itself for the target grid provided.
/// </summary>
public struct HexShaderInitialise : IComponentData { public int x, z; public Entity grid; }

/// <summary>
/// Managed component to store a reference to the terrain texture.
/// </summary>
public class HexShaderCellTexture : IComponentData
{
    public Texture2D value;
}

/// <summary>
/// 10 bytes min
/// Component to store whether the shader should be immediate mode and the current grid the system is targeting.
/// </summary>
public struct HexShaderSettings : IComponentData
{
    public Entity grid;
    public bool immediateMode;
}

/// <summary>
/// Systems can store buffers but to avoid buffer reinterepting and other nonsense, we're just storing a native list of ints in a list.
/// This stores which cells are currently transitioning.
/// This should only be a singleton attached to the <see cref="HexShaderSystem"/>
/// </summary>
public struct HexShaderTransitioningCells : IComponentData
{
    public NativeList<int> values;
}

/// <summary>
/// This stores a NativeContainer version of the texture in <see cref="HexShaderCellTexture"/>
/// When working with the texture, this array is modified then <see cref="HexShaderPaintTexture"/> is added to copy this array to the actual texture.
/// This is because textures are managed and we can't use them in burst jobs.
/// <seealso cref="HexShaderSystem.PaintTerrainTexture"/>
/// This should only be a singleton attached to the <see cref="HexShaderSystem"/>
/// </summary>
public struct HexShaderTextureData : IComponentData
{
    public NativeArray<Color32> values;
}