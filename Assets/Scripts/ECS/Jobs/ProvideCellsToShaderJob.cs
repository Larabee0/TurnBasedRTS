using Unity.Burst;
using Unity.Entities;

/// <summary>
/// This runs on all HexCells and triggers them to provide their current data to the <see cref="HexCellShaderRefreshWrapper"/> buffer
/// on the <see cref="HexShaderSystem"/>.
/// It provides the <see cref="HexCellBasic"/>, <see cref="HexCellTerrain"/> and <see cref="HexCellNav"/> components.
/// </summary>
[BurstCompile, WithAll(typeof(HexShaderRefresh))]
public partial struct ProvideCellsToShaderJob : IJobEntity
{
    public Entity shaderEntity;
    public EntityCommandBuffer.ParallelWriter ecbEnd;
    public void Execute([ChunkIndexInQuery] int jobChunkIndex, Entity main, in HexCellBasic basic, in HexCellTerrain terrain, in HexCellNav navigation)
    {
        ecbEnd.AppendToBuffer(jobChunkIndex, shaderEntity, new HexCellShaderRefreshWrapper
        { 
            hexCellNav = navigation,
            index = basic.Index,
            terrainTypeIndex = terrain.terrainTypeIndex
        });
        ecbEnd.AddComponent<HexShaderCellDataComplete>(jobChunkIndex, shaderEntity);
        ecbEnd.RemoveComponent<HexShaderRefresh>(jobChunkIndex, main);
    }
}