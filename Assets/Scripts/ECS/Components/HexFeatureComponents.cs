using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public enum HexFeatureType
{
    Generic,
    Bridge,
    Tower,
    Special
}


[System.Serializable]
public struct ManagedHexFeatureCollection
{
    public GameObject[] prefabs;
} 

public struct HexFeatureSpecialPrefab : IBufferElementData
{
    public Entity prefab;
}

public struct HexFeatureUpdate : IComponentData { }

public struct HexFeatureCollectionComponent : IComponentData
{
    public HexFeatureCollection urbanCollections;
    public HexFeatureCollection farmCollections;
    public HexFeatureCollection plantCollections;
    public Entity wallTower;
    public Entity bridge;

}

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

public struct HexFeatureVariants
{
    public int Count;
    public HexFeaturePrefab prefab0;
    public HexFeaturePrefab prefab1;

    private HexFeaturePrefab this[int index] => index switch
    {
        0=>prefab0,
        1=>prefab1,
        _=> prefab0
    };

    public HexFeaturePrefab Pick(float choice) => this[(int)choice * Count];
}

public struct HexFeaturePrefab 
{
    public Entity prefab;
    public float localYscale;
}

public struct HexFeatureRequest : IBufferElementData, IComparer<HexFeatureRequest>, IEquatable<HexFeatureRequest>
{
    public HexFeatureType type;
    public Entity prefab;
    public float3 localPosition;
    public float3 directionRight;
    public float3 directionForward;
    public float3 localScale;

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

    public uint Hash => math.hash(new float3x2()
    {
        c0 = new float3
        {
            x = (float)type,
            y = (float)prefab.Index,
            z = math.lengthsq(localPosition)
        },
        c1 = new float3
        {
            x = math.lengthsq(directionRight),
            y = math.lengthsq(directionForward),
            z = math.lengthsq(localScale)
        }
    });
}

public struct HexFeatureSpawnedFeature : IBufferElementData
{
    public HexFeatureRequest request;
    public Entity instanceEntity;
}