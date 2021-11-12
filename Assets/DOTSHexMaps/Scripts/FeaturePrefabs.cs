using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;

namespace DOTSHexagons
{
    [System.Serializable]
    public struct GameObjectFeatureCollection
    {
        public GameObject[] prefabs;
    }

    public struct NewPrefabTag : IComponentData { }

    public class FeaturePrefabs : MonoBehaviour, IDeclareReferencedPrefabs, IConvertGameObjectToEntity
    {
        public GameObject WallTower;
        public GameObject Bridge;

        public GameObject[] specialFeatures;
        public GameObjectFeatureCollection[] urbanCollections;
        public GameObjectFeatureCollection[] farmCollections;
        public GameObjectFeatureCollection[] plantCollections;
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            List<Entity> allPrefabs=new List<Entity>();
            FeatureDecisionSystem.WallTower = conversionSystem.GetPrimaryEntity(WallTower);
            FeatureDecisionSystem.Bridge = conversionSystem.GetPrimaryEntity(Bridge);
            dstManager.AddComponent<Rotation>(FeatureDecisionSystem.WallTower);
            dstManager.AddComponent<Translation>(FeatureDecisionSystem.WallTower);
            dstManager.AddComponent<LocalToParent>(FeatureDecisionSystem.WallTower);
            dstManager.AddComponent<Rotation>(FeatureDecisionSystem.Bridge);
            dstManager.AddComponent<Translation>(FeatureDecisionSystem.Bridge);
            dstManager.AddComponent<LocalToParent>(FeatureDecisionSystem.Bridge);

            FeatureDecisionSystem.special = new Entity[specialFeatures.Length];
            for (int i = 0; i < specialFeatures.Length; i++)
            {
                FeatureDecisionSystem.special[i] = conversionSystem.GetPrimaryEntity(specialFeatures[i]);

                dstManager.AddComponent<Rotation>(FeatureDecisionSystem.special[i]);
                dstManager.AddComponent<Translation>(FeatureDecisionSystem.special[i]);
                dstManager.AddComponent<LocalToParent>(FeatureDecisionSystem.special[i]);
            }
            FeatureDecisionSystem.urbanCollections = new HexFeaturePrefabContainer[urbanCollections.Length];
            for (int i = 0; i < urbanCollections.Length; i++)
            {
                Entity[] array = new Entity[urbanCollections[i].prefabs.Length];
                for (int j = 0; j < urbanCollections[i].prefabs.Length; j++)
                {
                    array[j] = conversionSystem.GetPrimaryEntity(urbanCollections[i].prefabs[j]);
                    
                    allPrefabs.Add(array[j]);
                }
                FeatureDecisionSystem.urbanCollections[i].Set(array);
            }
            FeatureDecisionSystem.farmCollections = new HexFeaturePrefabContainer[farmCollections.Length];
            for (int i = 0; i < farmCollections.Length; i++)
            {
                Entity[] array = new Entity[farmCollections[i].prefabs.Length];
                for (int j = 0; j < farmCollections[i].prefabs.Length; j++)
                {
                    array[j] = conversionSystem.GetPrimaryEntity(farmCollections[i].prefabs[j]);
                    allPrefabs.Add(array[j]);
                }
                FeatureDecisionSystem.farmCollections[i].Set(array);
            }
            FeatureDecisionSystem.plantCollections = new HexFeaturePrefabContainer[plantCollections.Length];
            for (int i = 0; i < plantCollections.Length; i++)
            {
                Entity[] array = new Entity[plantCollections[i].prefabs.Length];
                for (int j = 0; j < plantCollections[i].prefabs.Length; j++)
                {
                    array[j] = conversionSystem.GetPrimaryEntity(plantCollections[i].prefabs[j]);
                    allPrefabs.Add(array[j]);
                }
                FeatureDecisionSystem.plantCollections[i].Set(array);
            }

            for (int i = 0; i < allPrefabs.Count; i++)
            {
                dstManager.AddComponent<NonUniformScale>(allPrefabs[i]);
                dstManager.AddComponent<Rotation>(allPrefabs[i]);
                dstManager.AddComponent<Translation>(allPrefabs[i]);
                dstManager.AddComponent<LocalToParent>(allPrefabs[i]);
            }
        }

        public void DeclareReferencedPrefabs(List<GameObject> referencedPrefabs)
        {
            referencedPrefabs.Add(WallTower);
            referencedPrefabs.Add(Bridge);
            referencedPrefabs.AddRange(specialFeatures);
            for (int i = 0; i < urbanCollections.Length; i++)
            {
                referencedPrefabs.AddRange(urbanCollections[i].prefabs);
            }
            for (int i = 0; i < farmCollections.Length; i++)
            {
                referencedPrefabs.AddRange(farmCollections[i].prefabs);
            }
            for (int i = 0; i < plantCollections.Length; i++)
            {
                referencedPrefabs.AddRange(plantCollections[i].prefabs);
            }
        }
    }

    [UpdateBefore(typeof(TransformSystemGroup))][DisableAutoCreation]//[UpdateAfter(typeof(GameObjectConversionSystem))]
    public class FeatureUpdate : ComponentSystem
    {
        protected override void OnUpdate()
        {
            Entities.WithAll<Prefab, NewPrefabTag>().ForEach((Entity e) =>
            {
                Debug.Log("proccessing entity " + e.ToString());
                NativeArray<LinkedEntityGroup> group = EntityManager.GetBuffer<LinkedEntityGroup>(e).ToNativeArray(Allocator.Temp);
                NativeArray<Child> children = new NativeArray<Child>(group.Length-1, Allocator.Temp);
                if(children.Length != 0)
                {

                    for (int i = 1; i < group.Length; i++)
                    {
                        children[i - 1] = new Child { Value = group[i].Value };
                    }
                    EntityManager.AddBuffer<Child>(e).CopyFrom(children);
                }
                children.Dispose();
                group.Dispose();
                EntityManager.RemoveComponent<NewPrefabTag>(e);
            });
        }
    }
}