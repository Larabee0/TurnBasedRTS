using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.UIElements;

/// <summary>
/// 34 bytes min
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
/// 8 bytes min
/// </summary>
public struct HexCellNav : IComponentData
{
    public bool explorable;
    public bool explored;
    public int visibility;

    public bool IsVisible => visibility > 0 && explorable;
    public bool IsExplored => explored && explorable;
}


public struct HexCellShaderRefresh : IBufferElementData
{
    public int index;
    public int terrainTypeIndex;
    public HexCellNav hexCellNav;

    public bool IsVisible => hexCellNav.IsVisible;
    public bool IsExplored => hexCellNav.IsExplored;
}

/// <summary>
/// 12 bytes min
/// </summary>
public struct FindNeighbours : IComponentData
{
    public int cellCountX;
    public int cellCountZ;
    public int chunkCountX;
}

/// <summary>
/// 8 bytes min
/// </summary>
public struct HexGridReference : IComponentData
{
    public Entity Value;
    public static implicit operator Entity(HexGridReference v) { return v.Value; }
    public static implicit operator HexGridReference(Entity v) { return new HexGridReference { Value = v }; }
}

/// <summary>
/// 12 bytes min
/// </summary>
public struct HexCellChunkReference : IComponentData
{
    public Entity Value;
    public int chunkIndex;
}

public struct HexCellSetReferenceInNeighbouringChunk : IBufferElementData
{
    public Entity chunk;
}

/// <summary>
/// 120 bytes min
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

    public static HexCellNeighbours Empty()
    {
        return new HexCellNeighbours
        {
            NeighbourNE = -1,
            NeighbourE = -1,
            NeighbourSE = -1,
            NeighbourSW = -1,
            NeighbourW = -1,
            NeighbourNW = -1
        };
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetNeighbour(ref HexCellNeighbours cell, HexDirection direction, int neighbourIndex)
    {
        switch (direction)
        {
            case HexDirection.NE:
                cell.NeighbourNE = neighbourIndex;
                break;
            case HexDirection.E:
                cell.NeighbourE = neighbourIndex;
                break;
            case HexDirection.SE:
                cell.NeighbourSE = neighbourIndex;
                break;
            case HexDirection.SW:
                cell.NeighbourSW = neighbourIndex;
                break;
            case HexDirection.W:
                cell.NeighbourW = neighbourIndex;
                break;
            case HexDirection.NW:
                cell.NeighbourNW = neighbourIndex;
                break;
        }
    }

    public static void SetNeighbourEntity(ref HexCellNeighbours cell, HexCellReference neighbour, HexDirection direction)
    {
        switch (direction)
        {
            case HexDirection.NE:
                cell.EntityNE = neighbour.Value;
                cell.ChunkNE = neighbour.ChunkIndex;
                break;
            case HexDirection.E:
                cell.EntityE = neighbour.Value;
                cell.ChunkE = neighbour.ChunkIndex;
                break;
            case HexDirection.SE:
                cell.EntitySE = neighbour.Value;
                cell.ChunkSE = neighbour.ChunkIndex;
                break;
            case HexDirection.SW:
                cell.EntitySW = neighbour.Value;
                cell.ChunkSW = neighbour.ChunkIndex;
                break;
            case HexDirection.W:
                cell.EntityW = neighbour.Value;
                cell.ChunkW = neighbour.ChunkIndex;
                break;
            case HexDirection.NW:
                cell.EntityNW = neighbour.Value;
                cell.ChunkNW = neighbour.ChunkIndex;
                break;
        }
    }
}

public struct HexCellChunkSorter : IComparer<HexCellReference>
{
    public int Compare(HexCellReference a, HexCellReference b)
    {
        return a.ChunkIndex.CompareTo(b.ChunkIndex);
    }
}

public struct HexCellIndexSorter : IComparer<HexCellReference>, IComparer<HexCellShaderRefresh>
{
    public int Compare(HexCellReference a, HexCellReference b)
    {
        return a.Index.CompareTo(b.Index);
    }

    public int Compare(HexCellShaderRefresh a, HexCellShaderRefresh b)
    {
        return a.index.CompareTo(b.index);
    }
}

public struct WrappedCellIndexSorter : IComparer<HexChunkCellWrapper>
{
    public int Compare(HexChunkCellWrapper a, HexChunkCellWrapper b)
    {
        return a.Index.CompareTo(b.Index);
    }
}
