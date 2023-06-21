using Unity.Entities;

/// <summary>
/// 26 bytes min
/// Basic grid information is stored here
/// </summary>
public struct HexGridBasic : IComponentData
{
    public uint seed;
    public int cellCountX;
    public int cellCountZ;
    public int chunkCountX;
    public int chunkCountZ;
    public int wrapSize;
    public bool wrapping;
    public int CellCount => cellCountX * cellCountZ;
    public int ChunkCount => chunkCountX * cellCountZ;
}

/// <summary>
/// 12 bytes min
/// Buffer kept on the grid root entity to keep track of all of the chunks in the grid.
/// </summary>
public struct HexGridChunkBuffer : IBufferElementData
{
    public Entity Value;
    public int Index;
}

/// <summary>
/// 12 bytes min
/// Buffer kept on the grid root entity to keep track of all the columns in the grid.
/// </summary>
public struct HexGridColumnBuffer : IBufferElementData
{
    public Entity Value;
    public int Index;
}

/// <summary>
/// 16 bytes min
/// Buffer kept on a number of entities to keep track of hexcells relevant to them. (Grids and chunks mostly)
/// This includes the hexcell entity, the entities index in the full sized HexCellReference buffer kept on the root grid entity
/// and also includes the entities index of the chunk in the <see cref="HexGridChunkBuffer"/>
/// </summary>
public struct HexCellReference : IBufferElementData
{
    public Entity Value;
    public int Index;
    public int ChunkIndex;
}

/// <summary>
/// 8 bytes min
/// used by the <see cref="HexGridCreatorSystem"/> to intialise the correct number of chunks and columns.
/// columns = chunkCountX
/// chunks = chunkCountX * chunkCountZ
/// </summary>
public struct HexGridCreateChunks : IComponentData
{
    public int columns;
    public int chunks;
}

/// <summary>
/// 4 bytes min
/// Component added to a column to keep track of its place in <see cref="HexGridColumnBuffer"/> on the grid root entity
/// </summary>
public struct HexGridColumn : IComponentData
{
    public static implicit operator int(HexGridColumn v) { return v.Index; }
    public static implicit operator HexGridColumn(int v) { return new HexGridColumn { Index = v }; }
    public int Index;
}

/// <summary>
/// 4 bytes min
/// Component added to grid chunks during intiailisation to inform the chunk of which column they are in.
/// </summary>
public struct InitColumnIndex : IComponentData
{
    public static implicit operator int(InitColumnIndex v) { return v.Index; }
    public static implicit operator InitColumnIndex(int v) { return new InitColumnIndex { Index = v }; }
    public int Index;
}

/// <summary>
/// component put on the grid root entity during baking so <see cref="HexGridCreatorSystem"/> knowns to
/// create a grid using this entity.
/// </summary>
public struct HexGridUnInitialised : IComponentData { }

/// <summary>
/// tag component used to indcate to the <see cref="HexGridCreatorSystem"/> to sort the Grids HexCellBuffer by
/// <see cref="HexCellChunkSorter"/> (the chunk index) after neighbours are found the buffer is sorted by <see cref="HexCellIndexSorter"/>
/// </summary>
public struct HexGridSortCells : IComponentData { }

/// <summary>
/// tag component to trigger the <see cref="HexGridCreatorSystem"/> to schedule the <see cref="InitialiseCellsJob"/>
/// </summary>
public struct HexGridInitialiseCells : IComponentData { }

/// <summary>
/// tag component to trigger the <see cref="HexGridCreatorSystem"/> to schedule the <see cref="CompleteNeighboursJob"/>
/// </summary>
public struct HexGridNeighbourEntitySet : IComponentData { }

/// <summary>
/// tag component to trigger the <see cref="HexGridCreatorSystem"/> to schedule the <see cref="SortHexCellIndexJob"/>
/// </summary>
public struct HexGridNeighbourEntitySetUnsorted : IComponentData { }

/// <summary>
/// Used to denote which grid is currently active, for the hex editor system.
/// </summary>
public struct HexGridActive :IComponentData { }