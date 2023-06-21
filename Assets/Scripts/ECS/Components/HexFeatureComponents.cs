using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// enum to set the hex feature type for a <see cref="HexFeatureRequest"/>
/// </summary>
public enum HexFeatureType : byte
{
    Generic, // farm/urban/plant
    Bridge,
    Tower,
    Special
}

/// <summary>
/// Editor only component for feature collection authoring
/// </summary>
[Serializable]
public struct ManagedHexFeatureCollection
{
    public GameObject[] prefabs;
} 

/// <summary>
/// 8 bytes min
/// The special prefabs are stored in in this buffer
/// </summary>
public struct HexFeatureSpecialPrefab : IBufferElementData
{
    public Entity prefab;
    public static implicit operator Entity(HexFeatureSpecialPrefab v) { return v.prefab; }
    public static implicit operator HexFeatureSpecialPrefab(Entity v) { return new HexFeatureSpecialPrefab { prefab = v }; }
}

/// <summary>
/// Tag component to notify <see cref="HexFeatureSystem"/> to run its OnUpdate method.
/// </summary>
public struct HexFeatureUpdate : IComponentData { }

/// <summary>
/// 232 bytes min
/// Main hex feature collection component, this contains all entity prefab references apart from the special prefabs
/// This should exist only as a singleton.
/// </summary>
public struct HexFeatureCollectionComponent : IComponentData
{
    public HexFeatureCollection urbanCollections;
    public HexFeatureCollection farmCollections;
    public HexFeatureCollection plantCollections;
    public Entity wallTower;
    public Entity bridge;

}

/// <summary>
/// 72 bytes min
/// Stores 3 prefab levels variants for farm/urban/plant features
/// </summary>
public struct HexFeatureCollection
{
    public HexFeatureVariants level1;
    public HexFeatureVariants level2;
    public HexFeatureVariants level3;
    public HexFeatureVariants this[int index] => index switch
    {
        0 => level1,
        1 => level2,
        2 => level3,
        _ => level1,
    };
}

/// <summary>
/// 24 bytes min
/// stores two prefabs for a level of the feature for more variatety
/// </summary>
public struct HexFeatureVariants
{
    public HexFeaturePrefab prefab0;
    public HexFeaturePrefab prefab1;

    private HexFeaturePrefab this[int index] => index switch
    {
        0=>prefab0,
        1=>prefab1,
        _=> prefab0
    };

    public HexFeaturePrefab Pick(float choice) => this[(int)choice *2];
}

/// <summary>
/// 12 bytes min
/// Stores a single prefab for a farm/urban/plant feature,
/// including its local Y Scale which is needed to compute Y position
/// </summary>
public struct HexFeaturePrefab 
{
    public Entity prefab;
    public float localYscale;
}

/// <summary>
/// 57 bytes min
/// Contains all the information needed for the <see cref="HexFeatureSystem"/> to spawn features for cells
/// This is done at the chunk level, the chunk contains a buffer of them.
/// </summary>
public struct HexFeatureRequest : IBufferElementData, IComparer<HexFeatureRequest>, IEquatable<HexFeatureRequest>
{
    public HexFeatureType type;
    public Entity prefab;
    public float3 localPosition;
    public float3 directionRight;
    public float3 directionForward;
    public float3 localScale;

    // this gets used in a NativeHashSet, which means this needs to implement a burst compatible GetHashCode
    // this is also a easy to do equality too
    public uint Hash => math.hash(new float3x2()
    {
        c0 = new float3
        {
            x = (float)type,
            y = prefab.Index,
            z = math.lengthsq(localPosition)
        },
        c1 = new float3
        {
            x = math.lengthsq(directionRight),
            y = math.lengthsq(directionForward),
            z = math.lengthsq(localScale)
        }
    });

    public int Compare(HexFeatureRequest x, HexFeatureRequest y)
    {
        return x.Hash.CompareTo(y.Hash);
    }

    public override int GetHashCode()
    {
        return (int)Hash;
    }

    public bool Equals(HexFeatureRequest other)
    {
        return Hash == other.Hash;
    }
}

/// <summary>
/// 12 bytes min
/// The chunks also store a buffer to keep track of its spawned features.
/// This also contains the hash code of the feature request that spawned it.
/// This gets used in the <see cref="HexFeatureJob"/> for determining if it should replace the feature.
/// </summary>
public struct HexFeatureSpawnedFeature : IBufferElementData
{
    public uint hash;
    public Entity instanceEntity;
}