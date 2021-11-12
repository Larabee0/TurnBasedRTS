using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Hexagons;

public class GridPrefab : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
    public GameObject prefabGameObject;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        Entity prefabEntity = conversionSystem.GetPrimaryEntity(prefabGameObject);
        //dstManager.AddComponent(prefabEntity, typeof(Parent));
        //dstManager.AddComponent(prefabEntity, typeof(HexTileComponent));

        Hex.GridPrefab = prefabEntity;
    }

    public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
    {
        referencedPrefabs.Add(prefabGameObject);
    }
}
