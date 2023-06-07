using Unity.Entities;

public struct HexGridBasic : IComponentData
{
    public uint seed;
    public int cellCountX;
    public int cellCountZ;
    public int chunkCountX;
    public int chunkCountZ;
    public int wrapSize;
    public bool wrapping;
    public int CellCount => cellCountX + cellCountZ;
    public int ChunkCount => chunkCountX + cellCountZ;
}

public struct HexGridChunkBuffer : IBufferElementData
{
    public Entity Value;
    public int Index;
}

public struct HexGridColumnBuffer : IBufferElementData
{
    public Entity Value;
    public int Index;
}

public struct HexCellReference : IBufferElementData
{
    public Entity Value;
    public int Index;
    public int ChunkIndex;
}

public struct HexGridCreateChunks : IComponentData
{
    public int columns;
    public int chunks;
}

public struct HexGridColumn : IComponentData
{
    public static implicit operator int(HexGridColumn v) { return v.Index; }
    public static implicit operator HexGridColumn(int v) { return new HexGridColumn { Index = v }; }
    public int Index;
}
public struct InitColumnIndex : IComponentData
{
    public static implicit operator int(InitColumnIndex v) { return v.Index; }
    public static implicit operator InitColumnIndex(int v) { return new InitColumnIndex { Index = v }; }
    public int Index;
}

public struct HexGridUnInitialised : IComponentData { }
public struct HexGridSortCells : IComponentData { }
public struct HexGridInitialiseCells : IComponentData { }
public struct HexGridNeighbourEntitySet : IComponentData { }
public struct HexGridNeighbourEntitySetUnsorted : IComponentData { }
public struct HexGridActive :IComponentData { }