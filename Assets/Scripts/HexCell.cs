using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using UnityEngine;

public class HexCell : MonoBehaviour, IConvertGameObjectToEntity
{
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        dstManager.AddComponent<HexCellBasic>(entity);
        dstManager.AddComponent<HexGridReference>(entity);
        dstManager.AddComponent<HexCellTerrain>(entity);
        dstManager.AddComponentData(entity, HexCellNeighbours.Empty());
    }
}

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

public struct HexCellNav : IComponentData
{
    public bool explorable;
    public bool explored;
    public int visibility;

    public bool IsVisible => visibility > 0 && explorable;
    public bool IsExplored => explored && explorable;
}

public struct FindNeighbours : IComponentData
{
    public int cellCountX;
    public int cellCountZ;
    public int chunkCountX;
}

public struct CellWrapper : IBufferElementData, IComparable<CellWrapper>
{
    public HexCellBasic cellBasic;
    public HexCellTerrain cellTerrain;

    public int NeighbourNE;
    public int NeighbourE;
    public int NeighbourSE;
    public int NeighbourSW;
    public int NeighbourW;
    public int NeighbourNW;

    public CellWrapper(HexCellBasic cellBasic, HexCellTerrain cellTerrain, HexCellNeighbours neighbours)
    {
        this.cellBasic = cellBasic;
        this.cellTerrain = cellTerrain;
        NeighbourNE = neighbours.NeighbourNE;
        NeighbourE = neighbours.NeighbourE;
        NeighbourSE = neighbours.NeighbourSE;
        NeighbourSW = neighbours.NeighbourSW;
        NeighbourW = neighbours.NeighbourW;
        NeighbourNW = neighbours.NeighbourNW;
    }

    public bool Wrapping => cellBasic.wrapping;
    public int Index =>cellBasic.Index;
    public int ColumnIndex=> cellBasic.ColumnIndex;
    public int RawX =>cellBasic.rawX;
    public int RawZ => cellBasic.rawZ;
    public float3 Position=>cellBasic.Position;
    public HexCoordinates Coorindate=>cellBasic.Coorindate;

    public bool Walled => cellTerrain.walled;
    public bool HasIncomingRiver => cellTerrain.hasIncomingRiver;
    public bool HasOutgoingRiver => cellTerrain.hasOutgoingRiver;
    public HexDirection IncomingRiver => cellTerrain.incomingRiver;
    public HexDirection OutgoingRiver => cellTerrain.outgoingRiver;
    public int TerrainTypeIndex => cellTerrain.terrainTypeIndex;
    public int Elevation => cellTerrain.elevation;
    public int WaterLevel => cellTerrain.waterLevel;
    public int Urbanlevel => cellTerrain.urbanlevel;
    public int FarmLevel => cellTerrain.farmLevel;
    public int PlantLevel => cellTerrain.plantLevel;
    public int SpecialIndex => cellTerrain.specialIndex;

    public bool RoadsNE => cellTerrain.RoadsNE;
    public bool RoadsE => cellTerrain.RoadsE;
    public bool RoadsSE => cellTerrain.RoadsSE;
    public bool RoadsSW => cellTerrain.RoadsSW;
    public bool RoadsW => cellTerrain.RoadsW;
    public bool RoadsNW => cellTerrain.RoadsNW;

    public int ViewElevation => cellTerrain.ViewElevation;
    public bool IsUnderwater => cellTerrain.IsUnderwater;
    public bool HasRiver => cellTerrain.HasRiver;
    public bool HasRiverBeginOrEnd => cellTerrain.HasRiverBeginOrEnd;
    public bool HasRoads => cellTerrain.HasRoads;
    public bool IsSpeical => cellTerrain.IsSpeical;
    public float StreamBedY => cellTerrain.StreamBedY;
    public float RiverSurfaceY => cellTerrain.RiverSurfaceY;
    public float WaterSurfaceY => cellTerrain.WaterSurfaceY;
    public HexDirection RiverBeginOrEndDirection => cellTerrain.RiverBeginOrEndDirection;

    public int CompareTo(CellWrapper other)
    {
        return Index.CompareTo(other.Index);
    }
}

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetNeighbourIndex(HexCellNeighbours cell, HexDirection direction)
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

    public static void SetNeighbourEntity(ref HexCellNeighbours cell, Entity neighbour, HexDirection direction)
    {
        switch (direction)
        {
            case HexDirection.NE:
                cell.EntityNE = neighbour;
                break;
            case HexDirection.E:
                cell.EntityE = neighbour;
                break;
            case HexDirection.SE:
                cell.EntitySE = neighbour;
                break;
            case HexDirection.SW:
                cell.EntitySW = neighbour;
                break;
            case HexDirection.W:
                cell.EntityW = neighbour;
                break;
            case HexDirection.NW:
                cell.EntityNW = neighbour;
                break;
        }
    }
}

public struct HexCellReference : IBufferElementData
{
    public Entity Value;
    public int Index;
    public int ChunkIndex;
}

public struct HexCellChunkSorter : IComparer<HexCellReference>
{
    public int Compare(HexCellReference a, HexCellReference b)
    {
        return a.ChunkIndex.CompareTo(b.ChunkIndex);
    }
}

public struct HexChunkSorter : IComparer<HexGridChunkBuffer>
{
    public int Compare(HexGridChunkBuffer a, HexGridChunkBuffer b)
    {
        return a.Index.CompareTo(b.Index);
    }
}


public struct HexCellIndexSorter : IComparer<HexCellReference>
{
    public int Compare(HexCellReference a, HexCellReference b)
    {
        return a.Index.CompareTo(b.Index);
    }
}

public struct WrappedCellIndexSorter : IComparer<CellWrapper>
{
    public int Compare(CellWrapper a, CellWrapper b)
    {
        return a.Index.CompareTo(b.Index);
    }
}

public struct HexCellChunkReference : IComponentData
{
    public static implicit operator Entity(HexCellChunkReference v) { return v.Value; }
    public static implicit operator HexCellChunkReference(Entity v) { return new HexCellChunkReference { Value = v }; }
    public Entity Value;
}

public struct HexCellChunkNeighbour : IBufferElementData
{
    public static implicit operator Entity(HexCellChunkNeighbour v) { return v.targetChunk; }
    public static implicit operator HexCellChunkNeighbour(Entity v) { return new HexCellChunkNeighbour { targetChunk = v }; }
    public Entity targetChunk;
}

public struct HexCellChunkBuilder : IComponentData
{
    public static implicit operator Entity(HexCellChunkBuilder v) { return v.Chunk; }
    public static implicit operator HexCellChunkBuilder(Entity v) { return new HexCellChunkBuilder { Chunk = v }; }
    public Entity Chunk;
}

public partial class HexCellSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem ecbEndSys;
    private BeginSimulationEntityCommandBufferSystem ecbBeginSys;

    protected override void OnCreate()
    {
        ecbEndSys = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        ecbBeginSys = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer.ParallelWriter ecbEnd = ecbEndSys.CreateCommandBuffer().AsParallelWriter();
        EntityCommandBuffer.ParallelWriter ecbBegin = ecbBeginSys.CreateCommandBuffer().AsParallelWriter();

        Dependency = Entities.WithAll<HexCellBasic, HexCellTerrain,HexCellChunkNeighbour>().WithAll<HexCellNeighbours>()
            .WithNone<FindNeighbours, HexGridNeighbourEntitySet, HexCellChunkBuilder>()
            .ForEach((ref Entity main,in DynamicBuffer<HexCellChunkNeighbour> neighbourChunkBuffer, in HexCellBasic basic, in HexCellTerrain terrain, in HexCellNeighbours neighbours) =>
            {
                for (int i = 0; i < neighbourChunkBuffer.Length; i++)
                {
                    ecbEnd.AppendToBuffer(main.Index, neighbourChunkBuffer[i], new CellWrapper(basic, terrain, neighbours));
                    ecbEnd.AddComponent<UnsortedChunkCellDataCompleted>(main.Index, neighbourChunkBuffer[i]);
                }
                ecbEnd.RemoveComponent<HexCellChunkNeighbour>(main.Index, main);
                
            }).ScheduleParallel(Dependency);


        Dependency = Entities.WithAll<HexCellBasic, HexCellTerrain, HexCellChunkBuilder>()
            .WithNone<FindNeighbours, HexGridNeighbourEntitySet>()
            .ForEach((ref Entity main, ref HexCellChunkReference chunk, in HexCellBasic basic, in HexCellTerrain terrain, in HexCellChunkBuilder builder, in HexCellNeighbours neighbours) =>
            CellsToChunk(ecbEnd,ecbBegin,ref main,ref chunk,in basic,in terrain,in neighbours)).ScheduleParallel(Dependency);

        ecbEndSys.AddJobHandleForProducer(Dependency);
        ecbBeginSys.AddJobHandleForProducer(Dependency);
    }

    public static void CellsToChunk(EntityCommandBuffer.ParallelWriter ecbEnd, EntityCommandBuffer.ParallelWriter ecbBegin, ref Entity main, ref HexCellChunkReference chunk, in HexCellBasic basic, in HexCellTerrain terrain, in HexCellNeighbours neighbours)
    {
        ecbEnd.AppendToBuffer(chunk.Value.Index, chunk.Value, new CellWrapper(basic, terrain, neighbours));
        ecbEnd.RemoveComponent<HexCellChunkBuilder>(main.Index, main);

        int chunkX = basic.rawX / HexMetrics.chunkSizeX;
        int chunkZ = basic.rawZ / HexMetrics.chunkSizeZ;
        int localX = basic.rawX - chunkX * HexMetrics.chunkSizeX;
        int localZ = basic.rawZ - chunkZ * HexMetrics.chunkSizeZ;
        if(basic.Index == 4)
        {

             chunkX = basic.rawX / HexMetrics.chunkSizeX;
             chunkZ = basic.rawZ / HexMetrics.chunkSizeZ;
             localX = basic.rawX - chunkX * HexMetrics.chunkSizeX;
             localZ = basic.rawZ - chunkZ * HexMetrics.chunkSizeZ;
        }

        // targeting 34 cells in a chunk refresh buffer w/chunk size 4x4
        if (localX == 0 || localX == HexMetrics.chunkSizeX - 1 || localZ == 0 || localZ == HexMetrics.chunkSizeZ - 1)
        {
            switch (localZ) // bottom edge
            {
                case 0 when neighbours.HasNeighbour(HexDirection.SE):
                    // south east neighbour
                    ecbBegin.AppendToBuffer(chunk.Value.Index, neighbours.GetNeighbourEntity(HexDirection.SE), new HexCellChunkNeighbour { targetChunk = chunk.Value });
                    break;
                case HexMetrics.chunkSizeZ - 1 when neighbours.HasNeighbour(HexDirection.NE):
                    // north east neighbour
                    ecbBegin.AppendToBuffer(chunk.Value.Index, neighbours.GetNeighbourEntity(HexDirection.NE), new HexCellChunkNeighbour { targetChunk = chunk.Value });
                    break;
            }
            switch (localZ & 1) // even row
            {
                case 0:
                    switch (localX) // left side
                    {
                        case 0 when neighbours.HasNeighbour(HexDirection.W):
                            // west neighbour
                            ecbBegin.AppendToBuffer(chunk.Value.Index, neighbours.GetNeighbourEntity(HexDirection.W), new HexCellChunkNeighbour { targetChunk = chunk.Value });
                            switch (localZ) // botton left corner
                            {
                                case 0 when neighbours.HasNeighbour(HexDirection.SW):
                                    // south west neighbour
                                    ecbBegin.AppendToBuffer(chunk.Value.Index, neighbours.GetNeighbourEntity(HexDirection.SW), new HexCellChunkNeighbour { targetChunk = chunk.Value });
                                    break;
                                case HexMetrics.chunkSizeZ - 1 when neighbours.HasNeighbour(HexDirection.NW):
                                    // north west neighbour
                                    ecbBegin.AppendToBuffer(chunk.Value.Index, neighbours.GetNeighbourEntity(HexDirection.NW), new HexCellChunkNeighbour { targetChunk = chunk.Value });
                                    break;
                            }
                            break;
                        case HexMetrics.chunkSizeX - 1 when neighbours.HasNeighbour(HexDirection.E):
                            // east neighbour
                            ecbBegin.AppendToBuffer(chunk.Value.Index, neighbours.GetNeighbourEntity(HexDirection.E), new HexCellChunkNeighbour { targetChunk = chunk.Value });
                            break;
                    }
                    break;
                default:
                    switch (localX) // left side
                    {
                        case 0 when neighbours.HasNeighbour(HexDirection.W):
                            // west neighbour
                            ecbBegin.AppendToBuffer(chunk.Value.Index, neighbours.GetNeighbourEntity(HexDirection.W), new HexCellChunkNeighbour { targetChunk = chunk.Value });
                            if (localZ == HexMetrics.chunkSizeZ - 1 && neighbours.HasNeighbour(HexDirection.NW)) // top left corner
                            {
                                // north west neighbour
                                ecbBegin.AppendToBuffer(chunk.Value.Index, neighbours.GetNeighbourEntity(HexDirection.NW), new HexCellChunkNeighbour { targetChunk = chunk.Value });
                            }
                            break;
                        case HexMetrics.chunkSizeX - 1 when neighbours.HasNeighbour(HexDirection.E):
                            // east neighbour
                            ecbBegin.AppendToBuffer(chunk.Value.Index, neighbours.GetNeighbourEntity(HexDirection.E), new HexCellChunkNeighbour { targetChunk = chunk.Value });
                            break;
                    }
                    break;
            }
        }
    }
}