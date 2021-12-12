using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Rendering;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Jobs;
using System.Runtime.CompilerServices;
using Unity.Burst;
namespace DOTSHexagonsV2 
{
    [DisableAutoCreation]
    public class HexGridWrapSystem : ComponentSystem
    {
        protected override void OnUpdate()
        {
            Entities.WithAll<HexGridComponent, CentreMap>().ForEach((Entity gridEntity, ref HexGridComponent gridData, ref CentreMap XPosition) =>
            {
                float xPosition = XPosition.value;
                int centreColumnIndex = (int)(xPosition / (HexFunctions.innerDiameter * HexFunctions.chunkSizeX));
                if (centreColumnIndex != gridData.currentCentreColumnIndex)
                {
                    gridData.currentCentreColumnIndex = centreColumnIndex;
                    int minColumnIndex = centreColumnIndex - gridData.chunkCountX / 2;
                    int maxColumnIndex = centreColumnIndex + gridData.chunkCountX / 2;
                    float3 position = 0f;
                    NativeArray<Child> columns = EntityManager.GetBuffer<Child>(gridEntity).AsNativeArray();
                    for (int i = 0; i < columns.Length; i++)
                    {
                        if (!EntityManager.HasComponent<HexColumn>(columns[i].Value))
                        {
                            continue;
                        }
                        HexColumn col = EntityManager.GetComponentData<HexColumn>(columns[i].Value);
                        if (col.columnIndex < minColumnIndex)
                        {
                            position.x = gridData.chunkCountX * (HexFunctions.innerDiameter * HexFunctions.chunkSizeX);
                        }
                        else if (col.columnIndex > maxColumnIndex)
                        {
                            position.x = gridData.chunkCountX * -(HexFunctions.innerDiameter * HexFunctions.chunkSizeX);
                        }
                        else
                        {
                            position.x = 0f;
                        }
                        EntityManager.SetComponentData(columns[i].Value, new Translation { Value = position });
                    }
                    columns.Dispose();
                }
                EntityManager.RemoveComponent<CentreMap>(gridEntity);
            });
        }
    }
}