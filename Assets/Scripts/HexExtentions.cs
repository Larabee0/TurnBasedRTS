using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using UnityEngine.UIElements;

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
    public static unsafe HexChunkCellWrapper GetNeighbour(this HexChunkCellWrapper cell, NativeArray<HexChunkCellWrapper> cells, HexDirection direction)
    {
        int neighbourIndex = direction switch
        {
            HexDirection.NE => cell.cellNeighbours.NeighbourNE,
            HexDirection.E => cell.cellNeighbours.NeighbourE,
            HexDirection.SE => cell.cellNeighbours.NeighbourSE,
            HexDirection.SW => cell.cellNeighbours.NeighbourSW,
            HexDirection.W => cell.cellNeighbours.NeighbourW,
            HexDirection.NW => cell.cellNeighbours.NeighbourNW,
            _ => 0,
        };

        return cells[neighbourIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetOutgoingRiver(ref this HexChunkCellWrapper cell, NativeArray<HexChunkCellWrapper> cells, HexDirection direction)
    {
        switch (cell.cellTerrain.hasOutgoingRiver && cell.cellTerrain.outgoingRiver == direction)
        {
            case false:
                int neighbourIndex = cell.GetNeighbourIndex(direction);
                switch (neighbourIndex)
                {
                    case >=0:
                        HexChunkCellWrapper neighbour = cells[neighbourIndex];
                        switch (IsValidRiverDestination(cell, neighbour))
                        {
                            case true:
                                cell.RemoveOutgoingRiver(cells);
                                switch (cell.cellTerrain.hasIncomingRiver && cell.cellTerrain.incomingRiver == direction)
                                {
                                    case true:
                                        cell.RemoveIncomingRiver(cells);
                                        break;
                                }
                                cell.cellTerrain.hasOutgoingRiver = true;
                                cell.cellTerrain.outgoingRiver = direction;
                                cell.cellTerrain.specialIndex = 0;
                                cells[cell.Index] = cell;
                                neighbour. RemoveIncomingRiver(cells);
                                neighbour.cellTerrain.hasIncomingRiver = true;
                                neighbour.cellTerrain.incomingRiver = direction.Opposite();
                                neighbour.cellTerrain.specialIndex = 0;
                                cells[neighbour.Index] = neighbour;
                                break;
                        }
                        break;
                }
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ValidateRivers(ref this HexChunkCellWrapper cell,NativeArray<HexChunkCellWrapper> cells)
    {

        if (cell.cellTerrain.hasOutgoingRiver && !cell.IsValidRiverDestination(cell.GetNeighbour(cells, cell.cellTerrain.outgoingRiver)))
        {
            cell. RemoveOutgoingRiver(cells);
        }
        if (cell.cellTerrain.hasIncomingRiver && !IsValidRiverDestination(cell.GetNeighbour(cells, cell.cellTerrain.outgoingRiver), cell))
        {
            cell .RemoveIncomingRiver(cells);
        }
        cells[cell.Index] = cell;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidRiverDestination( this HexChunkCellWrapper cell, HexChunkCellWrapper neighbour)
    {
        return cell.Elevation >= neighbour.Elevation || cell.cellTerrain.waterLevel == neighbour.Elevation;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RemoveIncomingRiver(ref this HexChunkCellWrapper cell,NativeArray<HexChunkCellWrapper> cells)
    {
        switch (cell.cellTerrain.hasIncomingRiver)
        {
            case true:
                cell.cellTerrain.hasOutgoingRiver = false;
                HexChunkCellWrapper neighbor = cell.GetNeighbour(cells, cell.cellTerrain.outgoingRiver);
                neighbor.cellTerrain.hasOutgoingRiver = false;
                cells[cell.Index] = cell;
                cells[neighbor.Index] = neighbor;
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RemoveOutgoingRiver(ref this HexChunkCellWrapper cell, NativeArray<HexChunkCellWrapper> cells)
    {
        switch (cell.cellTerrain.hasOutgoingRiver)
        {
            case true:
                cell.cellTerrain.hasOutgoingRiver = false;
                HexChunkCellWrapper neighbor = cell.GetNeighbour(cells, cell.cellTerrain.outgoingRiver);
                neighbor.cellTerrain.hasIncomingRiver = false;
                cells[cell.Index] = cell;
                cells[neighbor.Index] = neighbor;
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RefreshPosition(ref this HexCellBasic cell,NativeArray<float4> noiseColours, int elevation, int wrapSize)
    {
        float3 position = cell.Position;
        position.y = elevation * HexMetrics.elevationStep;
        position.y += (HexMetrics.SampleNoise(noiseColours, position, wrapSize).y * 2f - 1f) * HexMetrics.elevationPerturbStrength;
        cell.Position = position;
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
    public static int GetNeighbourIndex(this HexChunkCellWrapper cell, HexDirection direction)
    {
        return direction switch
        {
            HexDirection.NE => cell.cellNeighbours.NeighbourNE,
            HexDirection.E => cell.cellNeighbours.NeighbourE,
            HexDirection.SE => cell.cellNeighbours.NeighbourSE,
            HexDirection.SW => cell.cellNeighbours.NeighbourSW,
            HexDirection.W => cell.cellNeighbours.NeighbourW,
            HexDirection.NW => cell.cellNeighbours.NeighbourNW,
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
    public static int GetNeighbourChunkIndex(this HexCellNeighbours cell, HexDirection direction)
    {
        return direction switch
        {
            HexDirection.NE => cell.ChunkNE,
            HexDirection.E => cell.ChunkE,
            HexDirection.SE => cell.ChunkSE,
            HexDirection.SW => cell.ChunkSW,
            HexDirection.W => cell.ChunkW,
            HexDirection.NW => cell.ChunkNW,
            _ => -1,
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
    public static float3x4 BundleEdge(float3 corner1, float3 corner2)
    {
        return new()
        {
            c0 = corner1,
            c1 = math.lerp(corner1, corner2, 1f / 2f),
            c2 = math.lerp(corner1, corner2, 2f / 2f),
            c3 = corner2
        };

    }

    public static float4 Lerp(float4 a, float4 b, float t)
    {
        t = math.clamp(t, 0, 1);
        return new float4(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);
    }


    public static bool Raycast(CollisionWorld collisionWorld, float3 RayForm, float3 RayTo, out HitInfoRaycast hitInfo)
    {
        CollisionFilter Filter = new()
        {
            BelongsTo = ~0u,
            CollidesWith = ~0u,
            GroupIndex = 0
        };
        return Raycast(collisionWorld, RayForm, RayTo, out hitInfo, Filter);
    }

    public static bool Raycast(CollisionWorld collisionWorld, float3 RayFrom, float3 RayTo, out HitInfoRaycast hitInfo, CollisionFilter filter)
    {
        RaycastInput input = new()
        {
            Start = RayFrom,
            End = RayTo,
            Filter = filter
        };

        bool hasHit = collisionWorld.CastRay(input, out Unity.Physics.RaycastHit raycastHit);

        hitInfo = new HitInfoRaycast
        {
            raycastHit = raycastHit,
            Distance = hasHit ? math.distance(RayFrom, raycastHit.Position) : math.distance(RayFrom, RayTo)
        };

        return hasHit;
    }
}