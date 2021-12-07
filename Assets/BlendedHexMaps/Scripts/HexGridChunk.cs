using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HexGridChunk : MonoBehaviour
{
    [SerializeField] private MeshCollider terrianCollider;
    [SerializeField] private MeshFilter terrianMesh;
    [SerializeField] private MeshFilter riverMesh;
    [SerializeField] private MeshFilter waterMesh;
    [SerializeField] private MeshFilter waterShoreMesh;
    [SerializeField] private MeshFilter estuariesMesh;
    [SerializeField] private MeshFilter roadsMesh;
    [SerializeField] private MeshFilter wallsMesh;

    public int ChunkIndex;
    public Transform FeatureContainer;

    public Mesh TerrianMesh { set { terrianMesh.mesh = terrianCollider.sharedMesh = value; } }
    public Mesh RiverMesh { set { riverMesh.mesh = value; } }
    public Mesh WaterMesh { set { waterMesh.mesh = value; } }
    public Mesh WaterShoreMesh { set { waterShoreMesh.mesh = value; } }
    public Mesh EstuariesMesh { set { estuariesMesh.mesh = value; } }
    public Mesh RoadsMesh { set { roadsMesh.mesh = value; } }
    public Mesh WallsMesh { set { wallsMesh.mesh = value; } }

    public GameObject TerrianObject { get { return terrianMesh.gameObject; } }
    public GameObject RiverObject { get { return riverMesh.gameObject; } }
    public GameObject WaterObject { get { return waterMesh.gameObject; } }
    public GameObject WaterShoreObject { get { return waterShoreMesh.gameObject; } }
    public GameObject EstuariesObject { get { return estuariesMesh.gameObject; } }
    public GameObject RoadsObject { get { return roadsMesh.gameObject; } }
    public GameObject WallsObject { get { return wallsMesh.gameObject; } }
    public GameObject FeatureObject { get { return FeatureContainer.gameObject; } }
}
