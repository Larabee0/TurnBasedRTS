using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.UIElements;

/// <summary>
/// 34 bytes min
/// This stores basic information about a Cell. Position, cell index, column index, virtual coordinates and HexCoordinates
/// </summary>
public struct HexCellBasic : IComponentData, IComparable<HexCellBasic>
{
    public bool wrapping;
    public int Index;
    public int ColumnIndex;
    public int rawX;
    public int rawZ;
    public float3 Position;
    public HexCoordinates Coorindate;

    public int CompareTo(HexCellBasic other)
    {
        return Index.CompareTo(other);
    }

}

/// <summary>
/// 50 bytes min
/// Contains purely terrain information, primarily used by <see cref="HexChunkTriangulatorSystem"/> in the <see cref="HexChunkCellWrapper"/>
/// This information will also be used by the <see cref="HexShaderSystem"/> and modified by the <see cref="HexMapEditorSystem"/>
/// </summary>
public struct HexCellTerrain : IComponentData
{
    public bool walled;
    public bool hasIncomingRiver;
    public bool hasOutgoingRiver;
    public HexDirection incomingRiver;
    public HexDirection outgoingRiver;
    public int terrainTypeIndex;
    public int elevation;
    public int waterLevel;
    public int urbanlevel;
    public int farmLevel;
    public int plantLevel;
    public int specialIndex;

    public bool RoadsNE;
    public bool RoadsE;
    public bool RoadsSE;
    public bool RoadsSW;
    public bool RoadsW;
    public bool RoadsNW;

    public int ViewElevation => elevation >= waterLevel ? elevation : waterLevel;
    public bool IsUnderwater => waterLevel > elevation;
    public bool HasRiver => hasIncomingRiver || hasOutgoingRiver;
    public bool HasRiverBeginOrEnd => hasIncomingRiver != hasOutgoingRiver;
    public bool HasRoads => RoadsNE || RoadsE || RoadsSE || RoadsSW || RoadsW || RoadsNW;
    public bool IsSpeical => specialIndex > 0;
    public float StreamBedY => (elevation + HexMetrics.streamBedElevationOffset) * HexMetrics.elevationStep;
    public float RiverSurfaceY => (elevation + HexMetrics.waterElevationOffset) * HexMetrics.elevationStep;
    public float WaterSurfaceY => (waterLevel + HexMetrics.waterElevationOffset) * HexMetrics.elevationStep;
    public HexDirection IncomingRiver => incomingRiver;
    public HexDirection OutgoingRiver => outgoingRiver;
    public HexDirection RiverBeginOrEndDirection => hasIncomingRiver ? incomingRiver : outgoingRiver;
}

/// <summary>
/// 6 bytes min
/// Used by the <see cref="HexShaderSystem"/> primarily in the <see cref="HexCellShaderRefreshWrapper"/> buffer
/// </summary>
public struct HexCellNav : IComponentData
{
    public bool explorable;
    public bool explored;
    public int visibility;

    public bool IsVisible => visibility > 0 && explorable;
    public bool IsExplored => explored && explorable;
}

/// <summary>
/// 16 bytes min
/// contains <see cref="HexCellNav"/> and other required information for the <see cref="HexShaderSystem"/> in
/// the <see cref="RefreshAllCellsJob"/> and <see cref="TransitionCellsJob"/>
/// </summary>
public struct HexCellShaderRefreshWrapper : IBufferElementData
{
    public int index;
    public int terrainTypeIndex;
    public HexCellNav hexCellNav;

    public bool IsVisible => hexCellNav.IsVisible;
    public bool IsExplored => hexCellNav.IsExplored;
}

/// <summary>
/// 12 bytes min
/// Component used during grid creatation <see cref="HexGridCreatorSystem"/>
/// This contains information about a cell for finding neighbouring cells.
/// This gets attached to a cell and contains the grid dimentions and the number of chunks in the x dimention
/// This is added in hte <see cref="InitialiseCellsJob"/> and removed in the <see cref="FindCellNeighboursJob"/>
/// </summary>
public struct FindNeighbours : IComponentData
{
    public int cellCountX;
    public int cellCountZ;
    public int chunkCountX;
}

/// <summary>
/// 12 bytes min
/// </summary>
public struct HexCellChunkReference : IComponentData
{
    public Entity Value;
    public int chunkIndex;
}

/// <summary>
/// 8 bytes min
/// Added to cell in <see cref="FindCellNeighboursJob"/> This instructs the cell to add itself to the chunk entity provided
/// as it is a cell directly adjacent to that chunk, thus would be needed for triangulation, so the chunk needs to know to ask
/// for its data when it is triangulated.
/// It is removed in <see cref="ProvideChunkNeighbourCellsJob"/>
/// </summary>
public struct HexCellSetReferenceInNeighbouringChunk : IBufferElementData
{
    public Entity chunk;
    public static implicit operator Entity(HexCellSetReferenceInNeighbouringChunk v) { return v.chunk; }
    public static implicit operator HexCellSetReferenceInNeighbouringChunk(Entity v) { return new HexCellSetReferenceInNeighbouringChunk { chunk = v }; }
}

/// <summary>
/// 120 bytes min
/// If you thought <see cref="HexCellTerrain"/> was a big component, this is more than twice the size.
/// This stores the neighbouring cell Indices and their entities as well as the chunk index of those cells.
/// </summary>
public struct HexCellNeighbours : IComponentData
{
    public int NeighbourNE;
    public int NeighbourE;
    public int NeighbourSE;
    public int NeighbourSW;
    public int NeighbourW;
    public int NeighbourNW;

    public Entity EntityNE;
    public Entity EntityE;
    public Entity EntitySE;
    public Entity EntitySW;
    public Entity EntityW;
    public Entity EntityNW;

    public int ChunkNE;
    public int ChunkE;
    public int ChunkSE;
    public int ChunkSW;
    public int ChunkW;
    public int ChunkNW;

    public static HexCellNeighbours Empty => new()
    {
        NeighbourNE = -1,
        NeighbourE = -1,
        NeighbourSE = -1,
        NeighbourSW = -1,
        NeighbourW = -1,
        NeighbourNW = -1,
        ChunkNE = -1,
        ChunkE = -1,
        ChunkSE = -1,
        ChunkSW = -1,
        ChunkW = -1,
        ChunkNW = -1,
    };
}


/// <summary>
/// Sorter struct for NativeArray.Sort. This sorts an array of HexCellReference's by their chunk index
/// </summary>
public struct HexCellChunkSorter : IComparer<HexCellReference>
{
    public int Compare(HexCellReference a, HexCellReference b)
    {
        return a.ChunkIndex.CompareTo(b.ChunkIndex);
    }
}


/// <summary>
/// Sorter struct for NativeArray.Sort. This sorts an array of HexCellReference's by their cell index.
/// </summary>
public struct HexCellIndexSorter : IComparer<HexCellReference>, IComparer<HexCellShaderRefreshWrapper>
{
    public int Compare(HexCellReference a, HexCellReference b)
    {
        return a.Index.CompareTo(b.Index);
    }

    public int Compare(HexCellShaderRefreshWrapper a, HexCellShaderRefreshWrapper b)
    {
        return a.index.CompareTo(b.index);
    }
}

/// <summary>
///  Sorter struct for NativeArray.Sort. This sorts an array of HexChunkCellWrapper's by their cell index.
/// </summary>
public struct WrappedCellIndexSorter : IComparer<HexChunkCellWrapper>
{
    public int Compare(HexChunkCellWrapper a, HexChunkCellWrapper b)
    {
        return a.Index.CompareTo(b.Index);
    }
}
