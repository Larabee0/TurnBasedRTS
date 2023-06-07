using Unity.Burst;
using Unity.Entities;

[BurstCompile, WithNone(typeof(FindNeighbours), typeof(HexGridNeighbourEntitySet))]
public partial struct CellsToChunksJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecbBegin;
    public EntityCommandBuffer.ParallelWriter ecbEnd;
    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main,in DynamicBuffer<HexChunkCellBuilder> chunks, in HexCellBasic basic, in HexCellTerrain terrain, in HexCellNeighbours neighbours)
    {
        var wrapper = new HexChunkCellWrapper()
        {
            cellBasic = basic,
            cellTerrain = terrain,
            cellNeighbours = neighbours
        };
        for (int i = 0; i < chunks.Length; i++)
        {
            ecbEnd.AppendToBuffer(jobChunkIndex, chunks[i].Chunk, wrapper);
        }
        ecbEnd.RemoveComponent<HexChunkCellBuilder>(jobChunkIndex, main);
        /*
        ecbEnd.AppendToBuffer(jobChunkIndex, chunk.Value, new HexChunkCellWrapper()
        {
            cellBasic = basic,
            cellTerrain = terrain,
            cellNeighbours = neighbours
        });
        ecbEnd.RemoveComponent<HexChunkCellBuilder>(jobChunkIndex, main);

        int chunkX = basic.rawX / HexMetrics.chunkSizeX;
        int chunkZ = basic.rawZ / HexMetrics.chunkSizeZ;
        int localX = basic.rawX - chunkX * HexMetrics.chunkSizeX;
        int localZ = basic.rawZ - chunkZ * HexMetrics.chunkSizeZ;
        if (basic.Index == 4)
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
                    ecbBegin.AppendToBuffer(jobChunkIndex, neighbours.GetNeighbourEntity(HexDirection.SE), new HexCellChunkNeighbour { value = chunk.Value });
                    break;
                case HexMetrics.chunkSizeZ - 1 when neighbours.HasNeighbour(HexDirection.NE):
                    // north east neighbour
                    ecbBegin.AppendToBuffer(jobChunkIndex, neighbours.GetNeighbourEntity(HexDirection.NE), new HexCellChunkNeighbour { value = chunk.Value });
                    break;
            }
            switch (localZ & 1) // even row
            {
                case 0:
                    switch (localX) // left side
                    {
                        case 0 when neighbours.HasNeighbour(HexDirection.W):
                            // west neighbour
                            ecbBegin.AppendToBuffer(jobChunkIndex, neighbours.GetNeighbourEntity(HexDirection.W), new HexCellChunkNeighbour { value = chunk.Value });
                            switch (localZ) // botton left corner
                            {
                                case 0 when neighbours.HasNeighbour(HexDirection.SW):
                                    // south west neighbour
                                    ecbBegin.AppendToBuffer(jobChunkIndex, neighbours.GetNeighbourEntity(HexDirection.SW), new HexCellChunkNeighbour { value = chunk.Value });
                                    break;
                                case HexMetrics.chunkSizeZ - 1 when neighbours.HasNeighbour(HexDirection.NW):
                                    // north west neighbour
                                    ecbBegin.AppendToBuffer(jobChunkIndex, neighbours.GetNeighbourEntity(HexDirection.NW), new HexCellChunkNeighbour { value = chunk.Value });
                                    break;
                            }
                            break;
                        case HexMetrics.chunkSizeX - 1 when neighbours.HasNeighbour(HexDirection.E):
                            // east neighbour
                            ecbBegin.AppendToBuffer(jobChunkIndex, neighbours.GetNeighbourEntity(HexDirection.E), new HexCellChunkNeighbour { value = chunk.Value });
                            break;
                    }
                    break;
                default:
                    switch (localX) // left side
                    {
                        case 0 when neighbours.HasNeighbour(HexDirection.W):
                            // west neighbour
                            ecbBegin.AppendToBuffer(jobChunkIndex, neighbours.GetNeighbourEntity(HexDirection.W), new HexCellChunkNeighbour { value = chunk.Value });
                            if (localZ == HexMetrics.chunkSizeZ - 1 && neighbours.HasNeighbour(HexDirection.NW)) // top left corner
                            {
                                // north west neighbour
                                ecbBegin.AppendToBuffer(jobChunkIndex, neighbours.GetNeighbourEntity(HexDirection.NW), new HexCellChunkNeighbour { value = chunk.Value });
                            }
                            break;
                        case HexMetrics.chunkSizeX - 1 when neighbours.HasNeighbour(HexDirection.E):
                            // east neighbour
                            ecbBegin.AppendToBuffer(jobChunkIndex, neighbours.GetNeighbourEntity(HexDirection.E), new HexCellChunkNeighbour { value = chunk.Value });
                            break;
                    }
                    break;
            }
        }
        */
    }
}
