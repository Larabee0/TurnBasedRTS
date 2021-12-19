using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace DOTSHexagonsV2 
{
    public class HexGridWrapSystem : ComponentSystem
    {
        protected override void OnUpdate()
        {
            Entities.WithAll<HexGridComponent, CentreMap>().ForEach((Entity gridEntity, ref HexGridComponent gridData, ref CentreMap XPosition) =>
            {
                float xPosition = XPosition.Value;
                int centreColumnIndex = (int)(xPosition / (HexFunctions.innerDiameter * HexFunctions.chunkSizeX));
                if (centreColumnIndex != gridData.currentCentreColumnIndex)
                {
                    gridData.currentCentreColumnIndex = centreColumnIndex;
                    int minColumnIndex = centreColumnIndex - gridData.chunkCountX / 2;
                    int maxColumnIndex = centreColumnIndex + gridData.chunkCountX / 2;
                    float3 position = 0f;
                    NativeArray<HexGridChild> columns = EntityManager.GetBuffer<HexGridChild>(gridEntity).AsNativeArray();
                    for (int i = 0; i < columns.Length; i++)
                    {
                        if (!EntityManager.HasComponent<HexColumn>(columns[i].Value))
                        {
                            continue;
                        }
                        HexColumn col = EntityManager.GetComponentData<HexColumn>(columns[i].Value);
                        if (col.Value < minColumnIndex)
                        {
                            position.x = gridData.chunkCountX * (HexFunctions.innerDiameter * HexFunctions.chunkSizeX);
                        }
                        else if (col.Value > maxColumnIndex)
                        {
                            position.x = gridData.chunkCountX * -(HexFunctions.innerDiameter * HexFunctions.chunkSizeX);
                        }
                        else
                        {
                            position.x = 0f;
                        }
                        EntityManager.SetComponentData(columns[i].Value, new ColumnOffset { Value = position });
                    }
                    columns.Dispose();
                }
                EntityManager.RemoveComponent<CentreMap>(gridEntity);
            });
        }
    }


}