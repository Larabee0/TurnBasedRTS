using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static class HexExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexDirection Opposite(this HexDirection direction)
    {
        return (int)direction < 3 ? (direction + 3) : (direction - 3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexDirection Previous(this HexDirection direction)
    {
        return direction == HexDirection.NE ? HexDirection.NW : (direction - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexDirection Next(this HexDirection direction)
    {
        return direction == HexDirection.NW ? HexDirection.NE : (direction + 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexDirection Previous2(this HexDirection direction)
    {
        direction -= 2;
        return direction >= HexDirection.NE ? direction : (direction + 6);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexDirection Next2(this HexDirection direction)
    {
        direction += 2;
        return direction <= HexDirection.NW ? direction : (direction - 6);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasRoadThroughEdge(this HexCellTerrain cell, HexDirection direction)
    {
        return direction switch
        {
            HexDirection.NE => cell.RoadsNE,
            HexDirection.E => cell.RoadsE,
            HexDirection.SE => cell.RoadsSE,
            HexDirection.SW => cell.RoadsSW,
            HexDirection.W => cell.RoadsW,
            HexDirection.NW => cell.RoadsNW,
            _ => false,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasRiverThroughEdge(this HexCellTerrain cell, HexDirection direction)
    {
        return cell.hasIncomingRiver && cell.incomingRiver == direction || cell.hasOutgoingRiver && cell.outgoingRiver == direction;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetElevationDifference(this HexCellTerrain cell, HexCellTerrain neighbourCell)
    {
        int difference = cell.elevation - neighbourCell.elevation;
        return difference >= 0 ? difference : -difference;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HexEdgeType GetEdgeType(this HexCellTerrain cell, HexCellTerrain neighbourCell)
    {
        return HexMetrics.GetEdgeType(cell.elevation, neighbourCell.elevation);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DistanceTo(this HexCoordinates from, HexCoordinates to)
    {
        int xy = (from.x < to.x ? to.x - from.x : from.x - to.x) + (from.Y < to.Y ? to.Y - from.Y : from.Y - to.Y);
        if (HexMetrics.Wrapping)
        {
            to.x += HexMetrics.wrapSize;
            int xyWrapped = (from.x < to.x ? to.x - from.x : from.x - to.x) + (from.Y < to.Y ? to.Y - from.Y : from.Y - to.Y);
            if (xyWrapped < xy)
            {
                xy = xyWrapped;
            }
            else
            {
                to.x -= 2 * HexMetrics.wrapSize;
                xyWrapped = (from.x < to.x ? to.x - from.x : from.x - to.x) + (from.Y < to.Y ? to.Y - from.Y : from.Y - to.Y);
                if (xyWrapped < xy)
                {
                    xy = xyWrapped;
                }
            }
        }
        return (xy + (from.z < to.z ? to.z - from.z : from.z - to.z)) / 2;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetNeighbourIndex(this HexCellNeighbours cell, HexDirection direction)
    {
        return direction switch
        {
            HexDirection.NE => cell.NeighbourNE,
            HexDirection.E => cell.NeighbourE,
            HexDirection.SE => cell.NeighbourSE,
            HexDirection.SW => cell.NeighbourSW,
            HexDirection.W => cell.NeighbourW,
            HexDirection.NW => cell.NeighbourNW,
            _ => -1,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetNeighbourIndex(this CellWrapper cell, HexDirection direction)
    {
        return direction switch
        {
            HexDirection.NE => cell.NeighbourNE,
            HexDirection.E => cell.NeighbourE,
            HexDirection.SE => cell.NeighbourSE,
            HexDirection.SW => cell.NeighbourSW,
            HexDirection.W => cell.NeighbourW,
            HexDirection.NW => cell.NeighbourNW,
            _ => -1,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entity GetNeighbourEntity(this HexCellNeighbours cell, HexDirection direction)
    {
        return direction switch
        {
            HexDirection.NE => cell.EntityNE,
            HexDirection.E => cell.EntityE,
            HexDirection.SE => cell.EntitySE,
            HexDirection.SW => cell.EntitySW,
            HexDirection.W => cell.EntityW,
            HexDirection.NW => cell.EntityNW,
            _ => Entity.Null,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasNeighbour(this HexCellNeighbours cell, HexDirection direction)
    {
        return direction switch
        {
            HexDirection.NE => cell.NeighbourNE != -1,
            HexDirection.E => cell.NeighbourE != -1,
            HexDirection.SE => cell.NeighbourSE != -1,
            HexDirection.SW => cell.NeighbourSW != -1,
            HexDirection.W => cell.NeighbourW != -1,
            HexDirection.NW => cell.NeighbourNW != -1,
            _ => false,
        };
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3x4 BundleEdge(float3 corner1,float3 corner2)
    {
        return new()
        {
            c0=corner1,
            c1 = math.lerp(corner1,corner2,1f/2f),
            c2 = math.lerp(corner1, corner2, 2f / 2f),
            c3 = corner2
        };

    }
}