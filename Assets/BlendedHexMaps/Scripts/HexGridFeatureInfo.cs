using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DOTSHexagonsV2
{
    public class HexGridFeatureInfo : MonoBehaviour, IEquatable<HexGridFeatureInfo>, IComparable<HexGridFeatureInfo>
    {
        public CellFeature feature;

        public int CompareTo(HexGridFeatureInfo other)
        {
            return feature.CompareTo(other.feature);
        }

        public bool Equals(HexGridFeatureInfo other)
        {
            return feature.Equals(other.feature);
        }
    }
}