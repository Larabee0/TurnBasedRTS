using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// Based on;
/// https://www.redblobgames.com/grids/hexagons/
/// </summary>
namespace Hexagons
{
    public struct GridUninitialised : IComponentData { }

    public struct GridPreInitialised : IComponentData { }

    public struct GridInitialised : IComponentData { }

    public struct PlanetTerrian : IComponentData { }
    public struct IntermediateTerrian : IComponentData { }
    public struct SpaceTerrian : IComponentData { }
    public struct GridVisualComponent : IComponentData 
    {
        public Entity Parent;
    }
    public struct PathVisualComponentUnSet : IComponentData
    {

    }
    public struct PathVisualComponent : IComponentData
    {
        public Entity Owner;
        public Entity Grid;
        public Entity Tile;
    }
    public struct GridBasicInfo : IComponentData
    {
        public Entity entity;
        public int ID;
        public int RingCount;
        public float3 Centre;
    }

    public struct HexTileComponent : IComponentData, System.IEquatable<HexTileComponent>
    {
        public int2 ID;
        public Entity Grid;

        public static HexTileComponent Null { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return ShiftAndWrap(Grid.Index.GetHashCode(), ShiftAndWrap(ID.x.GetHashCode(), 2) ^ ID.y.GetHashCode());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ShiftAndWrap(int value, int positions)
        {
            return value ^ positions;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(HexTileComponent other)
        {
            return other.ID.Equals(this.ID) && this.Grid.Equals(other.Grid);
        }
        

    }

    public struct PathStorageBufferElement : IBufferElementData
    {
        public Entity PathComponent;
    }

    public struct HexTileBufferElement : IBufferElementData, System.IEquatable<HexTileBufferElement>
    {
        public Entity entity;
        public int x;
        public int y;
        public int2 ID;
        public NodeTerrian Terrian;
        public NodePassability Passability;
        public int2 neighbour0;
        public int2 neighbour1;
        public int2 neighbour2;
        public int2 neighbour3;
        public int2 neighbour4;
        public int2 neighbour5;
        public float3 position;

        public HexTileBufferElement(int2 id)
        {
            entity = Entity.Null;
            ID = id;
            x = ID.x;
            y = ID.y;
            neighbour0 = neighbour1 = neighbour2 = neighbour3 = neighbour4 = neighbour5 = id;
            position = float3.zero;
            Terrian = NodeTerrian.Unset;
            Passability = NodePassability.Unset;
        }
        public HexTileBufferElement(int2 id, NodeTerrian terrian)
        {
            entity = Entity.Null;
            ID = id;
            x = ID.x;
            y = ID.y;
            neighbour0 = neighbour1 = neighbour2 = neighbour3 = neighbour4 = neighbour5 = id;
            position = float3.zero;
            Terrian = terrian;
            Passability = NodePassability.Unset;
        }

        public HexTileBufferElement(int2 id, float3 position)
        {
            entity = Entity.Null;
            ID = id;
            x = ID.x;
            y = ID.y;
            neighbour0 = neighbour1 = neighbour2 = neighbour3 = neighbour4 = neighbour5 = id;
            this.position = position;
            Terrian = NodeTerrian.Unset;
            Passability = NodePassability.Unset;
        }
        public HexTileBufferElement(int2 id, float3 position, NodeTerrian terrian)
        {
            entity = Entity.Null;
            ID = id;
            x = ID.x;
            y = ID.y;
            neighbour0 = neighbour1 = neighbour2 = neighbour3 = neighbour4 = neighbour5 = id;
            this.position = position;
            Terrian = terrian;
            Passability = NodePassability.Unset;
        }
        public bool Equals(HexTileBufferElement other)
        {
            return other.ID.Equals(this.ID) && this.entity.Equals(other.entity);
        }
    }

    public enum NodeTerrian
    {
        Space,
        Planet,
        Intermediate,
        Invisible,
        Unset,
        All,
    }

    public enum NodePassability
    {
        Normal,
        Impassable,
        Slow,
        FastA,
        FastB,
        Unset,
    }

    public struct HexTile
    {
        public int x;
        public int y;
        public int2 ID;
        public NodeTerrian Type;
        public NodePassability Terrian;
        public int2 neighbour0;
        public int2 neighbour1;
        public int2 neighbour2;
        public int2 neighbour3;
        public int2 neighbour4;
        public int2 neighbour5;
        public float3 position;

        public static HexTile Null { get; }

        public HexTile(int2 id)
        {
            x = id.x;
            y = id.y;
            ID = neighbour0 = neighbour1 = neighbour2 = neighbour3 = neighbour4 = neighbour5 = id;
            position = float3.zero;
            Type = NodeTerrian.Unset;
            Terrian = NodePassability.Unset;
        }

        public HexTile(int2 id, float3 position)
        {
            x = id.x;
            y = id.y;
            ID = neighbour0 = neighbour1 = neighbour2 = neighbour3 = neighbour4 = neighbour5 = id;
            this.position = position;
            Type = NodeTerrian.Unset;
            Terrian = NodePassability.Unset;
        }
    }

    public struct HexWithOffset
    {
        public int2 ID;
        public int2 Vector;

        public HexWithOffset(int2 id, int2 vector)
        {
            ID = id;
            Vector = vector;
        }
    }

    public static class Hex
    {
        public const float size = 1;
        public const float CellGap = 0;

        public const int SystemRingCount = 10;
        public const int SystemIntermediateIndex = 5;
        public const int SystemSpaceIndex = 6;

        public static Entity GridPrefab;
        public static Entity TilePrefab;
        public static Entity PathPrefab;

        public static Material DefaultHexagonMaterial;
        public static Material SelectedHexagonMaterial;

        public const float GridRayCastRange = 20f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeArray<int2> AllNeighbours(int2 origin, NativeHashMap<int2, HexTileBufferElement> Grid)
        {
            NativeArray<int2> Results = new NativeArray<int2>(6, Allocator.Temp);
            for (int i = 0; i < 6; i++)
            {
                int2 tentativeNeighbour = NeighbourAxial(origin, i);
                Results[i] = tentativeNeighbour;
                if (!Grid.ContainsKey(tentativeNeighbour))
                {
                    Results[i] = origin;
                }
            }
            return Results;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeArray<int2> AllNeighbours(int2 origin, NativeHashMap<int2, HexTile> Grid)
        {
            NativeArray<int2> Results = new NativeArray<int2>(6, Allocator.Temp);
            for (int i = 0; i < 6; i++)
            {
                int2 tentativeNeighbour = NeighbourAxial(origin, i);
                Results[i] = tentativeNeighbour;
                if (!Grid.ContainsKey(tentativeNeighbour))
                {
                    Results[i] = origin;
                }
            }
            return Results;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 AxialDirection(int direction)
        {
            return direction switch
            {
                1 => new int2(1, -1),
                2 => new int2(0, -1),
                3 => new int2(-1, 0),
                4 => new int2(-1, 1),
                5 => new int2(0, +1),
                _ => new int2(1, 0),
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 AxialToCube(int2 axial)
        {
            return new int3(axial.x, axial.y, (-axial.x) - axial.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 CubeDirection(int direction)
        {
            return direction switch
            {
                1 => new int3(1, -1, 0),
                2 => new int3(0, -1, 1),
                3 => new int3(-1, 0, 1),
                4 => new int3(-1, 1, 0),
                5 => new int3(0, 1, -1),
                _ => new int3(1, 0, -1),
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 CubeToAxial(int3 cube)
        {
            return new int2(cube.x, cube.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeArray<HexWithOffset> CubeRing(HexTile centre, int radius)
        {
            NativeArray<HexWithOffset> results;
            if (radius <= 0)
            {
                results = new NativeArray<HexWithOffset>(1, Allocator.Temp);
                results[0] = new HexWithOffset(centre.ID, int2.zero);
                return results;
            }
            results = new NativeArray<HexWithOffset>(radius * 6, Allocator.Temp);
            int2 cube = centre.ID + (AxialDirection(4) * radius);
            int resultsIndex = 0;
            for (int i = 0; i < 6; i++)
            {
                for (int k = 0; k < radius; k++)
                {
                    results[resultsIndex] = new HexWithOffset(cube, Vector(cube, centre.ID));
                    cube = NeighbourAxial(cube, i);
                    resultsIndex++;
                }
            }
            return results;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeArray<HexTileBufferElement> CubeRing(HexTileBufferElement centre, int radius, NodeTerrian terrian)
        {
            NativeArray<HexTileBufferElement> results;
            if (radius <= 0)
            {
                results = new NativeArray<HexTileBufferElement>(1, Allocator.Temp);
                results[0] = centre;
                return results;
            }
            results = new NativeArray<HexTileBufferElement>(radius * 6, Allocator.Temp);
            int2 cube = centre.ID + (AxialDirection(4) * radius);
            int resultsIndex = 0;
            for (int i = 0; i < 6; i++)
            {
                for (int k = 0; k < radius; k++)
                {
                    results[resultsIndex] = new HexTileBufferElement(cube, terrian);
                    cube = NeighbourAxial(cube, i);
                    resultsIndex++;
                }
            }
            return results;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeHashMap<int2, HexTileBufferElement> CalculateNeighbours(NativeHashMap<int2, HexTileBufferElement> MapStorage)
        {
            NativeArray<int2> Keys = MapStorage.GetKeyArray(Allocator.Temp);
            for (int i = 0; i < Keys.Length; i++)
            {
                int2 key = Keys[i];
                NativeArray<int2> Neighbours = AllNeighbours(key, MapStorage);
                HexTileBufferElement Tile = MapStorage[key];
                Tile.neighbour0 = Neighbours[0];
                Tile.neighbour1 = Neighbours[1];
                Tile.neighbour2 = Neighbours[2];
                Tile.neighbour3 = Neighbours[3];
                Tile.neighbour4 = Neighbours[4];
                Tile.neighbour5 = Neighbours[5];
                Neighbours.Dispose();

                MapStorage[key] = Tile;
            }
            Keys.Dispose();
            return MapStorage;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeArray<int2> CubeRing(int3 centre, int radius)
        {
            NativeArray<int2> results;
            if (radius <= 0)
            {
                results = new NativeArray<int2>(1, Allocator.Temp);
                results[0] = CubeToAxial(centre);
                return results;
            }
            results = new NativeArray<int2>(radius * 6, Allocator.Temp);
            int3 cube = centre + (CubeDirection(4) * radius);
            int resultsIndex = 0;
            for (int i = 0; i < 6; i++)
            {
                for (int k = 0; k < radius; k++)
                {
                    results[resultsIndex] = CubeToAxial(cube);
                    cube = NeighbourCube(cube, i);
                    resultsIndex++;
                }
            }
            return results;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Distance(int3 a, int3 b)
        {
            return (math.abs(a.x - b.x) + math.abs(a.y - b.y) + math.abs(a.z - b.z)) / 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Distance(int2 a, int2 b)
        {
            return Distance(AxialToCube(a), AxialToCube(b));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 NeighbourAxial(int2 axial, int direction)
        {
            return axial + AxialDirection(direction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 NeighbourAxial(int3 cube, int direction)
        {
            return CubeToAxial(cube) + AxialDirection(direction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 NeighbourCube(int2 axial, int direction)
        {
            return AxialToCube(axial) + CubeDirection(direction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 NeighbourCube(int3 cube, int direction)
        {
            return cube + CubeDirection(direction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeHashMap<int2, HexTileBufferElement> SpawnRing(NativeHashMap<int2, HexTileBufferElement> MapStorage, NativeArray<HexTileBufferElement> Ring, float3 Origin, int ringRadius, float height, float halfHeight, float threeQuaterWidth, float CellGap)
        {
            NativeList<float3> Nodes = new NativeList<float3>(6 * ringRadius, Allocator.Temp)
            {
                Origin
            };

            float Ydist = Origin.y;
            float Xdist = Origin.x;
            float Zdist = Origin.z;
            float halfCellGap = CellGap / 2;
            for (int i = 0; i < ringRadius; i++)
            {
                Xdist += threeQuaterWidth + CellGap;
                Zdist += halfHeight + halfCellGap;
                Nodes.Add(new float3(Xdist, Ydist, Zdist));
            }
            for (int i = 0; i < ringRadius; i++)
            {
                Zdist += height + CellGap;
                Nodes.Add(new float3(Xdist, Ydist, Zdist));
            }
            for (int i = 0; i < ringRadius; i++)
            {
                Xdist -= threeQuaterWidth + CellGap;
                Zdist += halfHeight + halfCellGap;
                Nodes.Add(new float3(Xdist, Ydist, Zdist));
            }

            for (int i = 0; i < ringRadius; i++)
            {
                Xdist -= threeQuaterWidth + CellGap;
                Zdist -= halfHeight + halfCellGap;
                Nodes.Add(new float3(Xdist, Ydist, Zdist));
            }
            for (int i = 0; i < ringRadius; i++)
            {
                Zdist -= height + CellGap;
                Nodes.Add(new float3(Xdist, Ydist, Zdist));
            }
            for (int i = 0; i < ringRadius - 1; i++)
            {
                Xdist += threeQuaterWidth + CellGap;
                Zdist -= halfHeight + halfCellGap;
                Nodes.Add(new float3(Xdist, Ydist, Zdist));
            }

            for (int k = 0; k < Ring.Length; k++)
            {
                HexTileBufferElement tile = Ring[k];
                tile.position = Nodes[k];
                MapStorage.Add(tile.ID, tile);
            }
            Ring.Dispose();
            return MapStorage;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TileCount(int rings)
        {
            rings--;
            return 1 + (3 * rings * (rings + 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 Vector(int3 position, int3 centre)
        {
            int3 vector = position - centre;
            return CubeToAxial(vector);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 Vector(int2 position, int2 centre)
        {
            return Vector(AxialToCube(position), AxialToCube(centre));
        }
    }

    public static class PathFinder
    {
        private const int MOVEMENT_COST = 5;
        private const int SLOW_MOVEMENT_COST = 5;
        private const int FAST_MOVEMENT_COST = 5;
        private const int ALT_FAST_MOVEMENT_COST = 10;

        public struct HexPathTile : System.IEquatable<HexPathTile>
        {
            public int2 ID;
            public Entity entity;
            public int2 cameFromID;
            public int gCost;
            public int hCost;
            public int fCost;
            public NodeTerrian Type;
            public NodePassability Terrian;
            public int2 neighbour0;
            public int2 neighbour1;
            public int2 neighbour2;
            public int2 neighbour3;
            public int2 neighbour4;
            public int2 neighbour5;

            public static HexPathTile Null { get; }
            public HexPathTile(HexTileBufferElement hexTile)
            {
                ID = hexTile.ID;
                entity = hexTile.entity;
                Type = hexTile.Terrian;
                Terrian = hexTile.Passability;
                neighbour0 = hexTile.neighbour0;
                neighbour1 = hexTile.neighbour1;
                neighbour2 = hexTile.neighbour2;
                neighbour3 = hexTile.neighbour3;
                neighbour4 = hexTile.neighbour4;
                neighbour5 = hexTile.neighbour5;
                hCost = 0;
                fCost = gCost = int.MaxValue;
                cameFromID = new int2(-1, -1);
            }
            public HexPathTile(HexTile hexTile)
            {
                ID = hexTile.ID;
                entity = Entity.Null;
                Type = hexTile.Type;
                Terrian = hexTile.Terrian;
                neighbour0 = hexTile.neighbour0;
                neighbour1 = hexTile.neighbour1;
                neighbour2 = hexTile.neighbour2;
                neighbour3 = hexTile.neighbour3;
                neighbour4 = hexTile.neighbour4;
                neighbour5 = hexTile.neighbour5;
                hCost = 0;
                fCost = gCost = int.MaxValue;
                cameFromID = new int2(-1, -1);
            }
            public HexPathTile(HexTileBufferElement hexTile, int hCost)
            {
                ID = hexTile.ID;
                entity = hexTile.entity;
                Type = hexTile.Terrian;
                Terrian = hexTile.Passability;
                neighbour0 = hexTile.neighbour0;
                neighbour1 = hexTile.neighbour1;
                neighbour2 = hexTile.neighbour2;
                neighbour3 = hexTile.neighbour3;
                neighbour4 = hexTile.neighbour4;
                neighbour5 = hexTile.neighbour5;
                this.hCost = hCost;
                fCost = gCost = int.MaxValue;
                cameFromID = new int2(-1, -1);
            }
            public HexPathTile(HexTile hexTile, int hCost)
            {
                ID = hexTile.ID;
                entity = Entity.Null;
                Type = hexTile.Type;
                Terrian = hexTile.Terrian;
                neighbour0 = hexTile.neighbour0;
                neighbour1 = hexTile.neighbour1;
                neighbour2 = hexTile.neighbour2;
                neighbour3 = hexTile.neighbour3;
                neighbour4 = hexTile.neighbour4;
                neighbour5 = hexTile.neighbour5;
                this.hCost = hCost;
                fCost = gCost = int.MaxValue;
                cameFromID = new int2(-1, -1);
            }

            public HexPathTile(HexTile hexTile, int gCost, int hCost)
            {
                ID = hexTile.ID;
                entity = Entity.Null;
                Type = hexTile.Type;
                Terrian = hexTile.Terrian;
                neighbour0 = hexTile.neighbour0;
                neighbour1 = hexTile.neighbour1;
                neighbour2 = hexTile.neighbour2;
                neighbour3 = hexTile.neighbour3;
                neighbour4 = hexTile.neighbour4;
                neighbour5 = hexTile.neighbour5;
                this.gCost = gCost;
                this.hCost = hCost;
                fCost = gCost + hCost;
                cameFromID = new int2(-1, -1);
            }
            public HexPathTile(HexTile hexTile, int previousNode, int gCost, int hCost)
            {
                ID = hexTile.ID;
                entity = Entity.Null;
                Type = hexTile.Type;
                Terrian = hexTile.Terrian;
                neighbour0 = hexTile.neighbour0;
                neighbour1 = hexTile.neighbour1;
                neighbour2 = hexTile.neighbour2;
                neighbour3 = hexTile.neighbour3;
                neighbour4 = hexTile.neighbour4;
                neighbour5 = hexTile.neighbour5;           
                this.gCost = gCost;
                this.hCost = hCost;
                fCost = gCost + hCost;
                cameFromID = previousNode;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Equals(HexPathTile other)
            {
                return ID.Equals(other.ID);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void CalculateFCost()
            {
                fCost = gCost + hCost;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeArray<Entity> FindPath(NativeArray<HexTileBufferElement> hexTiles, int2 startPosition, int2 endPosition)
        {
            return FindPath(hexTiles, startPosition, endPosition, new NativeArray<NodeTerrian>(0, Allocator.Temp), new NativeArray<NodeTerrian>(0, Allocator.Temp));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeArray<Entity> FindPath(NativeArray<HexTileBufferElement> hexTiles, int2 startPosition, int2 endPosition, NativeArray<NodeTerrian> only, NativeArray<NodeTerrian> none)
        {
            HexTileBufferElement endTile = new HexTileBufferElement(startPosition);//= MapStorage[endPosition];
            HexTileBufferElement startTile = new HexTileBufferElement(endPosition);//= MapStorage[startPosition];
            //NativeArray<HexTile> hexTiles = MapStorage.GetValueArray(Allocator.Temp);
            NativeHashMap<int2, HexPathTile> hexPathTiles = new NativeHashMap<int2, HexPathTile>(hexTiles.Length, Allocator.Temp);
            for (int i = 0; i < hexTiles.Length; i++)
            {
                HexTileBufferElement hexTile = hexTiles[i];
                HexPathTile Node = new HexPathTile(hexTile, CalculateDistanceCost(hexTile.ID, endPosition));
                hexPathTiles.Add(Node.ID, Node);
                if(hexTile.ID.Equals(startPosition))
                {
                    startTile = hexTile;
                }
                if (hexTile.ID.Equals(endPosition))
                {
                    endTile = hexTile;
                }
            }
            HexPathTile endNode = hexPathTiles[endTile.ID];
            HexPathTile startNode = hexPathTiles[startTile.ID];
            startNode.gCost = 0;
            startNode.CalculateFCost();
            hexPathTiles[startNode.ID] = startNode;
            NativeHashSet<int2> closedHashSet = new NativeHashSet<int2>(hexTiles.Length, Allocator.Temp);
            NativeHashSet<int2> openHashSet = new NativeHashSet<int2>(hexTiles.Length, Allocator.Temp);
            NativeList<int2> openList = new NativeList<int2>(hexTiles.Length, Allocator.Temp);
            hexTiles.Dispose();
            NativeArray<int2> neighbouringNodes = new NativeArray<int2>(6, Allocator.Temp);
            openHashSet.Add(startNode.ID);
            openList.Add(startNode.ID);
            while (openList.Length > 0)
            {
                int2 currentNodeIndex = GetLowestFCostNode(openList, hexPathTiles);
                HexPathTile currentNode = hexPathTiles[currentNodeIndex];
                if (currentNodeIndex.Equals(endNode.ID))
                {
                    break;
                }
                for (int i = 0; i < openList.Length; i++)
                {
                    if (openList[i].Equals(currentNodeIndex))
                    {
                        openList.RemoveAtSwapBack(i);
                        openHashSet.Remove(i);
                        break;
                    }
                }
                closedHashSet.Add(currentNodeIndex);

                neighbouringNodes[0] = currentNode.neighbour0;
                neighbouringNodes[1] = currentNode.neighbour1;
                neighbouringNodes[2] = currentNode.neighbour2;
                neighbouringNodes[3] = currentNode.neighbour3;
                neighbouringNodes[4] = currentNode.neighbour4;
                neighbouringNodes[5] = currentNode.neighbour5;
                for (int i = 0; i < 6; i++)
                {
                    HexPathTile neighbourNode = hexPathTiles[neighbouringNodes[i]];
                    bool invalidNode = false;
                    for (int k = 0; k < only.Length; k++)
                    {
                        if (neighbourNode.Type == only[i])
                        {
                            invalidNode = false;
                            break;
                        }
                        invalidNode = true;
                    }
                    for (int k = 0; k < none.Length; k++)
                    {
                        if (neighbourNode.Type == only[i])
                        {
                            invalidNode = true;
                            break;
                        }
                        invalidNode = false;
                    }
                    if (neighbourNode.ID.Equals(currentNodeIndex) || invalidNode)
                    {
                        continue;
                    }
                    if (closedHashSet.Contains(neighbourNode.ID))
                    {
                        continue;
                    }

                    int tentativeGCost = currentNode.gCost + CalculateDistanceCost(currentNode, neighbourNode);
                    switch (currentNode.Terrian) // default case is NodeTerrian.Unset
                    {
                        case NodePassability.Normal:
                            break;
                        case NodePassability.Impassable:
                            continue;
                        case NodePassability.Slow:
                            tentativeGCost += SLOW_MOVEMENT_COST;
                            break;
                        case NodePassability.FastA:
                            tentativeGCost -= FAST_MOVEMENT_COST;
                            break;
                        case NodePassability.FastB:
                            tentativeGCost -= ALT_FAST_MOVEMENT_COST;
                            break;
                        default:
                            break;
                    }
                    if (tentativeGCost < neighbourNode.gCost)
                    {
                        neighbourNode.cameFromID = currentNodeIndex;
                        neighbourNode.gCost = tentativeGCost;
                        neighbourNode.hCost = CalculateDistanceCost(neighbourNode.ID, endTile.ID);
                        neighbourNode.CalculateFCost();
                        hexPathTiles[neighbourNode.ID] = neighbourNode;
                        if (!openList.Contains(neighbourNode.ID))
                        {
                            openList.Add(neighbourNode.ID);
                            openHashSet.Add(neighbourNode.ID);
                        }
                    }
                }
            }

            endNode = hexPathTiles[endTile.ID];
            NativeList<Entity> Path = CalculatePathEntity(hexPathTiles, endNode);
            neighbouringNodes.Dispose();
            hexPathTiles.Dispose();
            closedHashSet.Dispose();
            openList.Dispose();
            only.Dispose();            
            none.Dispose();
            return Path;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeArray<int2> FindPath(NativeHashMap<int2, HexTile> MapStorage, int2 startPosition, int2 endPosition)
        {
            return FindPath(MapStorage, startPosition, endPosition, new NativeArray<NodeTerrian>(0, Allocator.Temp), new NativeArray<NodeTerrian>(0, Allocator.Temp));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeArray<int2> FindPath(NativeHashMap<int2, HexTile> MapStorage, int2 startPosition, int2 endPosition, NativeArray<NodeTerrian> only, NativeArray<NodeTerrian> none)
        {
            HexTile endTile = MapStorage[endPosition];
            HexTile startTile = MapStorage[startPosition];

            NativeArray<HexTile> hexTiles = MapStorage.GetValueArray(Allocator.Temp);
            NativeHashMap<int2, HexPathTile> hexPathTiles = new NativeHashMap<int2, HexPathTile>(hexTiles.Length, Allocator.Temp);
            for (int i = 0; i < hexTiles.Length; i++)
            {
                HexTile hexTile = hexTiles[i];
                HexPathTile Node = new HexPathTile(hexTile, CalculateDistanceCost(hexTile, endTile));
                hexPathTiles.Add(Node.ID, Node);
            }
            HexPathTile endNode = hexPathTiles[endTile.ID];
            HexPathTile startNode = hexPathTiles[startTile.ID];
            startNode.gCost = 0;
            startNode.CalculateFCost();
            hexPathTiles[startNode.ID] = startNode;
            NativeHashSet<int2> closedHashSet = new NativeHashSet<int2>(hexTiles.Length, Allocator.Temp);
            NativeHashSet<int2> openHashSet = new NativeHashSet<int2>(hexTiles.Length, Allocator.Temp);
            NativeList<int2> openList = new NativeList<int2>(hexTiles.Length, Allocator.Temp);
            hexTiles.Dispose();
            NativeArray<int2> neighbouringNodes = new NativeArray<int2>(6, Allocator.Temp);
            openHashSet.Add(startNode.ID);
            openList.Add(startNode.ID);
            while (openList.Length > 0)
            {
                int2 currentNodeIndex = GetLowestFCostNode(openList, hexPathTiles);
                HexPathTile currentNode = hexPathTiles[currentNodeIndex];
                if (currentNodeIndex.Equals(endNode.ID))
                {
                    break;
                }
                for (int i = 0; i < openList.Length; i++)
                {
                    if (openList[i].Equals(currentNodeIndex))
                    {
                        openList.RemoveAtSwapBack(i);
                        openHashSet.Remove(i);
                        break;
                    }
                }
                closedHashSet.Add(currentNodeIndex);

                neighbouringNodes[0] = currentNode.neighbour0;
                neighbouringNodes[1] = currentNode.neighbour1;
                neighbouringNodes[2] = currentNode.neighbour2;
                neighbouringNodes[3] = currentNode.neighbour3;
                neighbouringNodes[4] = currentNode.neighbour4;
                neighbouringNodes[5] = currentNode.neighbour5;
                for (int i = 0; i < 6; i++)
                {
                    HexPathTile neighbourNode = hexPathTiles[neighbouringNodes[i]];
                    bool invalidNode = false;
                    for (int k = 0; k < only.Length; k++)
                    {
                        if (neighbourNode.Type == only[i])
                        {
                            invalidNode = false;
                            break;
                        }
                        invalidNode = true;
                    }
                    for (int k = 0; k < none.Length; k++)
                    {
                        if (neighbourNode.Type == only[i])
                        {
                            invalidNode = true;
                            break;
                        }
                        invalidNode = false;
                    }
                    if (neighbourNode.ID.Equals(currentNodeIndex) || invalidNode)
                    {
                        continue;
                    }
                    if (closedHashSet.Contains(neighbourNode.ID))
                    {
                        continue;
                    }

                    int tentativeGCost = currentNode.gCost + CalculateDistanceCost(currentNode, neighbourNode);
                    switch (currentNode.Terrian) // default case is NodeTerrian.Unset
                    {
                        case NodePassability.Normal:
                            break;
                        case NodePassability.Impassable:
                            continue;
                        case NodePassability.Slow:
                            tentativeGCost += SLOW_MOVEMENT_COST;
                            break;
                        case NodePassability.FastA:
                            tentativeGCost -= FAST_MOVEMENT_COST;
                            break;
                        case NodePassability.FastB:
                            tentativeGCost -= ALT_FAST_MOVEMENT_COST;
                            break;
                        default:
                            break;
                    }
                    if (tentativeGCost < neighbourNode.gCost)
                    {
                        neighbourNode.cameFromID = currentNodeIndex;
                        neighbourNode.gCost = tentativeGCost;
                        neighbourNode.hCost = CalculateDistanceCost(neighbourNode, endTile);
                        neighbourNode.CalculateFCost();
                        hexPathTiles[neighbourNode.ID] = neighbourNode;
                        if (!openList.Contains(neighbourNode.ID))
                        {
                            openList.Add(neighbourNode.ID);
                            openHashSet.Add(neighbourNode.ID);
                        }
                    }
                }
            }

            endNode = hexPathTiles[endTile.ID];
            NativeList<int2> Path = CalculatePath(hexPathTiles, endNode);
            neighbouringNodes.Dispose();
            hexPathTiles.Dispose();
            closedHashSet.Dispose();
            openList.Dispose();
            only.Dispose();
            none.Dispose();
            return Path;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeList<HexPathTile> RemoveItem(NativeList<HexPathTile> list, HexPathTile item)
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].Equals(item))
                {
                    list.RemoveAt(i);
                    break;
                }
            }
            return list;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeList<int2> CalculatePath(NativeHashMap<int2, HexPathTile> pathNodeArray, HexPathTile endNode)
        {
            int2 invalidID = new int2(-1, -1);
            if (endNode.cameFromID.Equals(invalidID))
            {
                return new NativeList<int2>(Allocator.Temp);
            }

            NativeList<int2> path = new NativeList<int2>(pathNodeArray.Capacity, Allocator.Temp);
            path.Add(endNode.ID);
            HexPathTile currentNode = endNode;
            while (!currentNode.cameFromID.Equals(invalidID))
            {
                HexPathTile cameFromNode = pathNodeArray[currentNode.cameFromID];
                path.Add(cameFromNode.ID);
                currentNode = cameFromNode;
            }
            return path;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeList<Entity> CalculatePathEntity(NativeHashMap<int2, HexPathTile> pathNodeArray, HexPathTile endNode)
        {
            int2 invalidID = new int2(-1, -1);
            if (endNode.cameFromID.Equals(invalidID))
            {
                return new NativeList<Entity>(Allocator.Temp);
            }

            NativeList<Entity> path = new NativeList<Entity>(pathNodeArray.Capacity, Allocator.Temp);
            path.Add(endNode.entity);
            HexPathTile currentNode = endNode;
            while (!currentNode.cameFromID.Equals(invalidID))
            {
                HexPathTile cameFromNode = pathNodeArray[currentNode.cameFromID];
                path.Add(cameFromNode.entity);
                currentNode = cameFromNode;
            }
            return path;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 GetLowestFCostNode(NativeList<int2> openList, NativeHashMap<int2, HexPathTile> pathNodeArray)
        {
            HexPathTile lowestFCostNode = pathNodeArray[openList[0]];
            for (int i = 1; i < openList.Length; i++)
            {
                HexPathTile testPathNode = pathNodeArray[openList[i]];
                if (testPathNode.fCost < lowestFCostNode.fCost)
                {
                    lowestFCostNode = testPathNode;
                }
            }
            return lowestFCostNode.ID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CalculateDistanceCost(int2 a, int2 b)
        {
            return Hex.Distance(a, b) * MOVEMENT_COST;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CalculateDistanceCost(HexTile a, HexTile b)
        {
            return CalculateDistanceCost(a.ID, b.ID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CalculateDistanceCost(HexPathTile a, HexPathTile b)
        {
            return CalculateDistanceCost(a.ID, b.ID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CalculateDistanceCost(HexPathTile a, HexTile b)
        {
            return CalculateDistanceCost(a.ID, b.ID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CalculateDistanceCost(HexTile a, HexPathTile b)
        {
            return CalculateDistanceCost(a.ID, b.ID);
        }
    }
}