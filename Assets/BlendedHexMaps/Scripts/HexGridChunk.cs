using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DOTSHexagonsV2
{
    public class HexGridChunk : MonoBehaviour, IEquatable<HexGridChunk>, IComparable<HexGridChunk>
    {
        [SerializeField] private MeshCollider terrianCollider;
        [SerializeField] private MeshFilter terrianMesh;
        [SerializeField] private MeshFilter riverMesh;
        [SerializeField] private MeshFilter waterMesh;
        [SerializeField] private MeshFilter waterShoreMesh;
        [SerializeField] private MeshFilter estuariesMesh;
        [SerializeField] private MeshFilter roadsMesh;
        [SerializeField] private MeshFilter wallsMesh;

        public int ChunkIndex = int.MinValue;
        public HexGridFeatureManager FeatureContainer;
        public MeshCollider Collider { get { return terrianCollider; } }

        public Mesh TerrianMesh { get { return terrianMesh.sharedMesh; } set { terrianMesh.sharedMesh = terrianCollider.sharedMesh = value; } }
        public Mesh RiverMesh { get { return riverMesh.sharedMesh; } set { riverMesh.sharedMesh = value; } }
        public Mesh WaterMesh { get { return waterMesh.sharedMesh; } set { waterMesh.sharedMesh = value; } }
        public Mesh WaterShoreMesh { get { return waterShoreMesh.sharedMesh; } set { waterShoreMesh.sharedMesh = value; } }
        public Mesh EstuariesMesh { get { return estuariesMesh.sharedMesh; } set { estuariesMesh.sharedMesh = value; } }
        public Mesh RoadsMesh { get { return roadsMesh.sharedMesh; } set { roadsMesh.sharedMesh = value; } }
        public Mesh WallsMesh { get { return wallsMesh.sharedMesh; } set { wallsMesh.sharedMesh = value; } }

        public GameObject TerrianObject { get { return terrianMesh.gameObject; } }
        public GameObject RiverObject { get { return riverMesh.gameObject; } }
        public GameObject WaterObject { get { return waterMesh.gameObject; } }
        public GameObject WaterShoreObject { get { return waterShoreMesh.gameObject; } }
        public GameObject EstuariesObject { get { return estuariesMesh.gameObject; } }
        public GameObject RoadsObject { get { return roadsMesh.gameObject; } }
        public GameObject WallsObject { get { return wallsMesh.gameObject; } }
        public GameObject FeatureObject { get { return FeatureContainer.gameObject; } }

        private void Awake()
        {
            TerrianMesh = new Mesh();
            RiverMesh = new Mesh();
            WaterMesh = new Mesh();
            WaterShoreMesh = new Mesh();
            EstuariesMesh = new Mesh();
            RoadsMesh = new Mesh();
            WallsMesh = new Mesh();

            TerrianMesh.MarkDynamic();
            RiverMesh.MarkDynamic();
            WaterMesh.MarkDynamic();
            WaterShoreMesh.MarkDynamic();
            EstuariesMesh.MarkDynamic();
            RoadsMesh.MarkDynamic();
            WallsMesh.MarkDynamic();
        }

        public bool Equals(HexGridChunk other)
        {
            if (other == null) return false;
            return ChunkIndex == other.ChunkIndex;
        }

        public int CompareTo(HexGridChunk other)
        {
            if (other == null) return 1;
            return ChunkIndex.CompareTo(other.ChunkIndex);
        }

        public override int GetHashCode()
        {
            return ChunkIndex;
        }
    }
}