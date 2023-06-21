using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// This is how you do prefabs in ECS
/// You have a prefabs authoring component to a gameobject in the subscene.
/// To this component you drag in your prefabs you want converted into entity prefabs in the entity world.
/// </summary>
public class HexPrefabsManager : MonoBehaviour
{
    public GameObject hexGridChunkPrefab;
    public GameObject hexCellPrefab;
    public GameObject hexChunkColliderPrefab;

    public ManagedHexFeatureCollection[] urbanCollections;
    public ManagedHexFeatureCollection[] farmCollections;
    public ManagedHexFeatureCollection[] plantCollections;
    public GameObject[] special;
    public GameObject wallTower;
    public GameObject bridge;
}

/// <summary>
/// In the baker you add a component for storing all your prefabs (or multiple components for each prefab(s))
/// and call GetEntity on the prefab reference in the authroing component, this then bakes the prefabs to entities
/// (running any bakers attached to them too)
/// 
/// The component(s) can then be accessed in the EntityWorld using SystemAPI.GetSingleton
/// then you can instantaite the entity prefabs it stores references to
/// </summary>
public class HexPrefabsBaker : Baker<HexPrefabsManager>
{
    public override void Bake(HexPrefabsManager authoring)
    {
        // no transform components are needed for this entity.
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new HexPrefabsComponent
        {
            // these do need them though
            hexGridChunk = GetEntity(authoring.hexGridChunkPrefab, TransformUsageFlags.Dynamic),
            hexCell = GetEntity(authoring.hexCellPrefab, TransformUsageFlags.Dynamic),
            hexChunkCollider = GetEntity(authoring.hexChunkColliderPrefab, TransformUsageFlags.Dynamic)
        });

        /// please ignore how cursed this is.
        /// <see cref="HexFeatureCollectionComponent"/> contain three <see cref="HexFeatureCollection"/>
        /// structs, each of which contain three <see cref="HexFeatureVariants"/> structs each of which contain two
        /// <see cref="HexFeaturePrefab"/> structs.
        /// <see cref="HexFeaturePrefab"/> contains the entity prefab and the localY scale of the prefab.
        /// <see cref="HexFeatureCollection"/> & <see cref="HexFeatureVariants"/> contain indexers for access.
        /// <see cref="HexFeatureCollectionComponent"/> contains everything as its just a singleton for
        /// access by the <see cref="HexFeatureSystem"/>
        AddComponent(entity, new HexFeatureCollectionComponent
        {
            urbanCollections = new HexFeatureCollection
            {
                level1 = new HexFeatureVariants
                {
                    prefab0 = new HexFeaturePrefab
                    {
                        prefab = GetEntity(authoring.urbanCollections[0].prefabs[0], TransformUsageFlags.Dynamic),
                        localYscale = authoring.urbanCollections[0].prefabs[0].transform.localScale.y
                    },
                    prefab1 = new HexFeaturePrefab
                    {
                        prefab = GetEntity(authoring.urbanCollections[0].prefabs[1], TransformUsageFlags.Dynamic),
                        localYscale = authoring.urbanCollections[0].prefabs[1].transform.localScale.y
                    }
                },
                level2 = new HexFeatureVariants
                {
                    prefab0 = new HexFeaturePrefab
                    {
                        prefab = GetEntity(authoring.urbanCollections[1].prefabs[0], TransformUsageFlags.Dynamic),
                        localYscale = authoring.urbanCollections[1].prefabs[0].transform.localScale.y
                    },
                    prefab1 = new HexFeaturePrefab
                    {
                        prefab = GetEntity(authoring.urbanCollections[1].prefabs[1], TransformUsageFlags.Dynamic),
                        localYscale = authoring.urbanCollections[1].prefabs[1].transform.localScale.y
                    }
                },
                level3 = new HexFeatureVariants
                {
                    prefab0 = new HexFeaturePrefab
                    {
                        prefab = GetEntity(authoring.urbanCollections[2].prefabs[0], TransformUsageFlags.Dynamic),
                        localYscale = authoring.urbanCollections[2].prefabs[0].transform.localScale.y
                    },
                    prefab1 = new HexFeaturePrefab
                    {
                        prefab = GetEntity(authoring.urbanCollections[2].prefabs[1], TransformUsageFlags.Dynamic),
                        localYscale = authoring.urbanCollections[2].prefabs[1].transform.localScale.y
                    }
                }
            },
            farmCollections = new HexFeatureCollection
            {
                level1 = new HexFeatureVariants
                {
                    prefab0 = new HexFeaturePrefab
                    {
                        prefab = GetEntity(authoring.farmCollections[0].prefabs[0], TransformUsageFlags.Dynamic),
                        localYscale = authoring.farmCollections[0].prefabs[0].transform.localScale.y
                    },
                    prefab1 = new HexFeaturePrefab
                    {
                        prefab = GetEntity(authoring.farmCollections[0].prefabs[1], TransformUsageFlags.Dynamic),
                        localYscale = authoring.farmCollections[0].prefabs[1].transform.localScale.y
                    }
                },
                level2 = new HexFeatureVariants
                {
                    prefab0 = new HexFeaturePrefab
                    {
                        prefab = GetEntity(authoring.farmCollections[1].prefabs[0], TransformUsageFlags.Dynamic),
                        localYscale = authoring.farmCollections[1].prefabs[0].transform.localScale.y
                    },
                    prefab1 = new HexFeaturePrefab
                    {
                        prefab = GetEntity(authoring.farmCollections[1].prefabs[1], TransformUsageFlags.Dynamic),
                        localYscale = authoring.farmCollections[1].prefabs[1].transform.localScale.y
                    }
                },
                level3 = new HexFeatureVariants
                {
                    prefab0 = new HexFeaturePrefab
                    {
                        prefab = GetEntity(authoring.farmCollections[2].prefabs[0], TransformUsageFlags.Dynamic),
                        localYscale = authoring.farmCollections[2].prefabs[0].transform.localScale.y
                    },
                    prefab1 = new HexFeaturePrefab
                    {
                        prefab = GetEntity(authoring.farmCollections[2].prefabs[1], TransformUsageFlags.Dynamic),
                        localYscale = authoring.farmCollections[2].prefabs[1].transform.localScale.y
                    }
                }
            },
            plantCollections = new HexFeatureCollection
            {
                level1 = new HexFeatureVariants
                {
                    prefab0 = new HexFeaturePrefab
                    {
                        prefab = GetEntity(authoring.plantCollections[0].prefabs[0], TransformUsageFlags.Dynamic),
                        localYscale = authoring.plantCollections[0].prefabs[0].transform.localScale.y
                    },
                    prefab1 = new HexFeaturePrefab
                    {
                        prefab = GetEntity(authoring.plantCollections[0].prefabs[1], TransformUsageFlags.Dynamic),
                        localYscale = authoring.plantCollections[0].prefabs[1].transform.localScale.y
                    }
                },
                level2 = new HexFeatureVariants
                {
                    prefab0 = new HexFeaturePrefab
                    {
                        prefab = GetEntity(authoring.plantCollections[1].prefabs[0], TransformUsageFlags.Dynamic),
                        localYscale = authoring.plantCollections[1].prefabs[0].transform.localScale.y
                    },
                    prefab1 = new HexFeaturePrefab
                    {
                        prefab = GetEntity(authoring.plantCollections[1].prefabs[1], TransformUsageFlags.Dynamic),
                        localYscale = authoring.plantCollections[1].prefabs[1].transform.localScale.y
                    }
                },
                level3 = new HexFeatureVariants
                {
                    prefab0 = new HexFeaturePrefab
                    {
                        prefab = GetEntity(authoring.plantCollections[2].prefabs[0], TransformUsageFlags.Dynamic),
                        localYscale = authoring.plantCollections[2].prefabs[0].transform.localScale.y
                    },
                    prefab1 = new HexFeaturePrefab
                    {
                        prefab = GetEntity(authoring.plantCollections[2].prefabs[1], TransformUsageFlags.Dynamic),
                        localYscale = authoring.plantCollections[2].prefabs[1].transform.localScale.y
                    }
                }
            },
            wallTower = GetEntity(authoring.wallTower, TransformUsageFlags.Dynamic),
            bridge = GetEntity(authoring.bridge, TransformUsageFlags.Dynamic)
        });

        // see the special features live in a dynamic buffer like a normal person would do
        /// <see cref="HexFeatureSpecialPrefab"/> is just a wrapper for an entity.
        DynamicBuffer<HexFeatureSpecialPrefab> buffer =  AddBuffer<HexFeatureSpecialPrefab>(entity);
        for (int i = 0; i < authoring.special.Length; i++)
        {
            buffer.Add(new HexFeatureSpecialPrefab { prefab = GetEntity(authoring.special[i], TransformUsageFlags.Dynamic) });
        }
        
    }
}

/// <summary>
/// All the entity world prefabs for various grid entities.
/// </summary>
public struct HexPrefabsComponent : IComponentData
{
    public Entity hexGridChunk;
    public Entity hexCell;
    public Entity hexChunkCollider;
}