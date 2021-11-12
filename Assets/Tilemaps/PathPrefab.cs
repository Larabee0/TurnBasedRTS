using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using Hexagons;

public class PathPrefab : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
{
    public GameObject prefabGameObject;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        Entity prefabEntity = conversionSystem.GetPrimaryEntity(prefabGameObject);
        dstManager.AddComponent(prefabEntity, typeof(PathVisualComponent));
        dstManager.AddComponent(prefabEntity, typeof(PathVisualComponentUnSet));
        Hex.PathPrefab = prefabEntity;
    }

    public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
    {
        referencedPrefabs.Add(prefabGameObject);
    }
}
