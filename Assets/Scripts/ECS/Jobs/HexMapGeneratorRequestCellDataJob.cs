using Unity.Burst;
using Unity.Entities;

/// <summary>
/// Job for the <see cref="HexMapGeneratorSystem"/> to request all hex cell data be provided to the root grid entity.
/// The job adds a HexChunkCellBuilder component to all HexCells, the "Chunk" in this case is the root grid entity.
/// 
/// Next frame the <see cref="CellWrappersToChunksJob"/> job will run on each hexcell and the grid will have everything it needs attached to it for the 
/// <see cref="HexGenerateMapJob"/> to run.
/// </summary>
[BurstCompile, WithAll(typeof(HexMapGenerate),typeof(HexGridBasic)),WithNone(typeof(HexChunkCellWrapper),typeof(HexGridUnInitialised))]
public partial struct HexMapGeneratorRequestCellDataJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecbEnd;
    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main, in DynamicBuffer<HexCellReference> hexCells)
    {
        for (int i = 0; i < hexCells.Length; i++)
        {
            ecbEnd.AddBuffer<HexChunkCellBuilder>(jobChunkIndex, hexCells[i].Value);
            ecbEnd.AppendToBuffer(jobChunkIndex, hexCells[i].Value, new HexChunkCellBuilder { Chunk = main });
        }
        ecbEnd.AddBuffer<HexChunkCellWrapper>(jobChunkIndex, main);
    }
}
