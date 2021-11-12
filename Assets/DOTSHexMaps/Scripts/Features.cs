using Unity.Entities;
using Unity.Mathematics;

namespace DOTSHexagons
{
    public enum FeatureCollection
    {
        None,
        WallTower,
        Bridge,
        Special,
        Urban,
        Farm,
        Plant
    }

    public struct FeatureGridInfo : IComponentData
    {
        public Entity GridEntity;
    }

    public struct FeatureContainer : IComponentData
    {
        public Entity GridEntity;
    }

    public struct CheckAndSpawnFeatures : IComponentData { }

    public struct RefreshContainer : IComponentData { }

    public struct CellContainer : IBufferElementData
    {
        public Entity container;
        public int cellIndex;
    }

    public struct CellFeature : IBufferElementData
    {
        public int cellIndex;
        public int featureLevelIndex;
        public int featureSubIndex;
        public FeatureCollection featureType;
        public float3 position;
        public float3 direction;
        public Entity feature;
        public bool UpdateCellFeatures;
        public bool UpdateFeaturePosition;
        public int ID
        {
            get
            {
                if (feature == Entity.Null)
                {
                    return cellIndex ^ featureSubIndex ^ (int)featureType;
                }
                return (cellIndex ^ featureSubIndex ^ (int)featureType) ^ feature.Index;
            }
        }
    }

    public struct Feature : IBufferElementData
    {
        public int cellIndex;
        public int featureLevelIndex;
        public int featureSubIndex;
        public FeatureCollection featureType;
        public float3 position;
        public float3 direction;
        public Entity feature;
        public int ID
        {
            get
            {
                if (feature == Entity.Null)
                {
                    return cellIndex ^ featureSubIndex ^ (int)featureType;
                }
                return (cellIndex ^ featureSubIndex ^ (int)featureType) ^ feature.Index;
            }
        }
    }

    public struct HexFeaturePrefabContainer
    {
        public Entity Level1;
        public Entity Level2;
        public Entity Level3;
        public Entity Level4;
        public Entity Level5;
        public Entity Level6;
        public Entity Level7;
        public Entity Level8;
        public Entity Level9;
        public int utilizedSlots;

        public void Set(Entity[] input)
        {
            if (input.Length < 9)
            {
                for (int i = 0; i < input.Length; i++)
                {
                    this[i] = input[i];
                }
                utilizedSlots = input.Length;
            }
        }

        public Entity Pick(float choice)
        {
            return this[(int)(choice * utilizedSlots)];
        }
        public int PickIndex(float choice)
        {
            return (int)(choice * utilizedSlots);
        }

        unsafe public Entity this[int index]
        {
            get
            {
                fixed (HexFeaturePrefabContainer* array = &this) { return ((Entity*)array)[index]; }
            }
            set
            {
                fixed (Entity* array = &Level1) { array[index] = value; }
            }
        }
    }

    public struct FeatureDataContainer : IComponentData 
    {
        public int cellIndex;
        public Entity containerEntity;
        public Entity GridEntity;
    }

    public struct PossibleFeaturePosition : IBufferElementData
    {
        public int cellIndex;
        public float3 position;
        public float3 direction;
        public FeatureCollection ReservedFor;
    }
        
    public struct NewFeatureSpawn : IComponentData
    {
        public Entity BufferContainer;
        public int Index;
    }

    public struct RefreshCellFeatures : IComponentData { }
}