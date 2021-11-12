using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Hexagons;

public class HexMapSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject CellPrefab;
    [Range(0f, 1f)]
    [SerializeField] private float CellGap = 0.1f;
    [SerializeField] private int gridSize = 4;

    private NativeHashMap<int2, HexTile> MapStorage;
    private readonly Dictionary<int2, HexNode> MapStorageManaged = new Dictionary<int2, HexNode>();
    private readonly List<int2> MapListManaged = new List<int2>();
    [SerializeField] private LayerMask hexLayer;

    private readonly List<HexNode[]> StartEndNodes = new List<HexNode[]>();
    private readonly List<Vector3[]> StartEndNodesVector3 = new List<Vector3[]>();
    [SerializeField] private List<MeshRenderer> LineRenderer;
    [SerializeField] private List<GameObject> Indicator;
    [SerializeField] private Transform IndicatorContainer;
    [SerializeField] private GameObject IndicatorPrefab;
    [Range(0.001f, 5f)]
    [SerializeField] private float ScaleFactor = 0.25f;
    [Range(0f, 2f)]
    [SerializeField] private float HeightOffset = 0.5f;
    private int SubPaths = -1;
    private bool enableLeftShiftUpTrigger = false;
    private readonly List<HexNode> CurrentPath = new List<HexNode>();

    private readonly float size = 1;
    private float width;
    private float threeQuaterWidth;
    private float height;
    private float halfHeight;

    // Start is called before the first frame update
    private void Start()
    {
        MapStorage = new NativeHashMap<int2, HexTile>(7, Allocator.Persistent);
        float TotalExtrudeTime = Time.realtimeSinceStartup;
        CoordinatesFirst(this.transform.position, gridSize);
        print("Total Start Time: " + (Time.realtimeSinceStartup - TotalExtrudeTime) * 1000f + "ms");
    }
    
    
    /// <summary>
    /// 1 rings = 1 tile
    /// 2 rings = 7 tiles  | 2 * 3 = 6
    /// 3 rings = 19 tiles | 3 * 6 = 18
    /// 4 rings = 37 tiles | 4 * 9 = 36
    /// 5 rings = 37 tiles | 5 * 12 = 60
    /// 6 rings = 37 tiles | 6 * 15 = 90
    /// 7 rings = 37 tiles | 7 * 18 = 126
    /// 8 rings = 37 tiles | 8 * 21 = 168
    /// </summary>
    
    private void Update()
    {
        CheckRightMouseAll();
    }

    private void OnDestroy()
    {
        if (MapStorage.IsCreated)
        {
            MapStorage.Dispose();
        }
    }

    private void CheckRightMouseAll()
    {
        CheckRightMouseDown();
        CheckRightMouse();
        CheckRightMouseUp();
    }
    
    private void CheckRightMouseDown()
    {
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetMouseButtonDown(1))
        {
            if (SubPaths >= 0)
            {
                HexNode Temp = StartEndNodes[SubPaths][1];
                SubPaths++;
                StartEndNodes.Add(new HexNode[2]);
                StartEndNodesVector3.Add(new Vector3[2]);
                StartEndNodes[SubPaths][0] = Temp;
                StartEndNodesVector3[SubPaths][0] = new Vector3(Temp.transform.position.x, HeightOffset, Temp.transform.position.z);
                Indicator.Add(Instantiate(IndicatorPrefab, IndicatorContainer.position, Quaternion.identity, IndicatorContainer));
                LineRenderer.Add(Indicator[SubPaths].GetComponent<MeshRenderer>());
                enableLeftShiftUpTrigger = true;
            }
            else
            {
                GameObject Temp = DoGridRayCastForObject();
                if (Temp != null)
                {
                    SubPaths++;
                    StartEndNodes.Add(new HexNode[2]);
                    StartEndNodesVector3.Add(new Vector3[2]);
                    StartEndNodes[SubPaths][0] = Temp.GetComponent<HexNode>();
                    StartEndNodesVector3[SubPaths][0] = new Vector3(Temp.transform.position.x, HeightOffset, Temp.transform.position.z);
                    Indicator.Add(Instantiate(IndicatorPrefab, IndicatorContainer.position, Quaternion.identity, IndicatorContainer));
                    LineRenderer.Add(Indicator[SubPaths].GetComponent<MeshRenderer>());
                }
            }
        }
        else if (Input.GetMouseButtonDown(1))
        {
            GameObject Temp = DoGridRayCastForObject();
            if (Temp != null)
            {
                SubPaths++;
                StartEndNodes.Add(new HexNode[2]);
                StartEndNodesVector3.Add(new Vector3[2]);
                StartEndNodes[SubPaths][0] = Temp.GetComponent<HexNode>();
                StartEndNodesVector3[SubPaths][0] = new Vector3(Temp.transform.position.x, HeightOffset, Temp.transform.position.z);
                Indicator.Add(Instantiate(IndicatorPrefab, IndicatorContainer.position, Quaternion.identity, IndicatorContainer));
                LineRenderer.Add(Indicator[SubPaths].GetComponent<MeshRenderer>());
            }
        }
    }

    private void CheckRightMouse()
    {
        if (Input.GetMouseButton(1))
        {
            if (StartEndNodes[SubPaths][0] != null)
            {
                GameObject Temp = DoGridRayCastForObject();
                if (Temp != null)
                {
                    StartEndNodesVector3[SubPaths][1] = new Vector3(Temp.transform.position.x, HeightOffset, Temp.transform.position.z);
                    UpdateVisuals();
                    StartEndNodes[SubPaths][1] = Temp.GetComponent<HexNode>();
                    if (!LineRenderer[SubPaths].enabled && StartEndNodesVector3[SubPaths][0] != StartEndNodesVector3[SubPaths][1])
                    {
                        LineRenderer[SubPaths].enabled = true;
                    }
                    else if (LineRenderer[SubPaths].enabled && StartEndNodesVector3[SubPaths][0] == StartEndNodesVector3[SubPaths][1])
                    {
                        StartEndNodes[SubPaths][1] = null;
                        LineRenderer[SubPaths].enabled = false;
                    }
                }
            }
        }
    }

    private void CheckRightMouseUp()
    {
        if (Input.GetKey(KeyCode.LeftShift) && Input.GetMouseButtonUp(1))
        {
            if (StartEndNodes[SubPaths][0] != null && StartEndNodes[SubPaths][1] != null)
            {
                if (CurrentPath.Count > 0)
                {
                    CurrentPath.RemoveAt(CurrentPath.Count - 1);
                }
                float TotalExtrudeTime = Time.realtimeSinceStartup;
                NativeArray<int2> Path = PathFinder.FindPath(MapStorage, StartEndNodes[SubPaths][0].xy, StartEndNodes[SubPaths][1].xy);
                print("Path time: " + (Time.realtimeSinceStartup - TotalExtrudeTime) * 1000f + "ms");
                for (int i = 0; i < Path.Length; i++)
                {
                    CurrentPath.Add(MapStorageManaged[Path[i]]);
                }
                Path.Dispose();
            }
        }
        else if (Input.GetMouseButtonUp(1) || (Input.GetKeyUp(KeyCode.LeftShift) && enableLeftShiftUpTrigger))
        {
            if (StartEndNodes[SubPaths][0] != null && StartEndNodes[SubPaths][1] != null)
            {
                MapListManaged.ForEach(i => MapStorageManaged[i].ResetMaterial());
                float TotalExtrudeTime = Time.realtimeSinceStartup;

                NativeArray<int2> Path = PathFinder.FindPath(MapStorage, StartEndNodes[SubPaths][0].xy, StartEndNodes[SubPaths][1].xy);

                print("Path time: " + (Time.realtimeSinceStartup - TotalExtrudeTime) * 1000f + "ms");
                for (int i = 0; i < Path.Length; i++)
                {
                    CurrentPath.Add(MapStorageManaged[Path[i]]);
                }
                Path.Dispose();
                PlotPath(CurrentPath);
            }
            ClearPath();
        }
    }

    void PlotPath(List<HexNode> Path)
    {
        for (int i = 0; i < Path.Count; i++)
        {
            Path[i].HighlightCell();
        }
    }

    private void UpdateVisuals()
    {
        Vector3 EdgeAveragePositon = (StartEndNodesVector3[SubPaths][0] + StartEndNodesVector3[SubPaths][1]) / 2;
        float EdgeDistance = Vector3.Distance(StartEndNodesVector3[SubPaths][0], StartEndNodesVector3[SubPaths][1]);
        Indicator[SubPaths].transform.localScale = new Vector3(ScaleFactor, ScaleFactor, EdgeDistance);
        Indicator[SubPaths].transform.position = Quaternion.Euler(IndicatorContainer.localRotation.eulerAngles) * EdgeAveragePositon;
        Indicator[SubPaths].transform.LookAt(Quaternion.Euler(IndicatorContainer.localRotation.eulerAngles) * StartEndNodesVector3[SubPaths][0]);
    }

    private void ClearPath()
    {
        StartEndNodes.Clear();
        LineRenderer.Clear();
        Indicator.ForEach(i => Destroy(i));
        Indicator.Clear();
        StartEndNodesVector3.Clear();
        CurrentPath.Clear();
        SubPaths = -1;
        enableLeftShiftUpTrigger = false;
    }

    public GameObject DoGridRayCastForObject()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hitInfo,int.MaxValue, hexLayer))
        {
            return hitInfo.collider.gameObject;
        }
        return null;
    }

    public void CoordinatesFirst(Vector3 Centre, int rings)
    {
        float TotalExtrudeTime = Time.realtimeSinceStartup;
        width = 2 * size;
        threeQuaterWidth = width * 0.75f;

        float root3 = math.sqrt(3);
        height = root3 * size;
        halfHeight = height / 2;
        int CentreColumn = (rings * 2) - 1;
        int2 centre = new int2(CentreColumn / 2, CentreColumn / 2);
        HexTile Origin = new HexTile(centre, float3.zero);

        HexNode originNode = Instantiate(CellPrefab, Centre, Quaternion.identity).GetComponent<HexNode>();
        originNode.gameObject.name = centre.ToString();
        MapStorageManaged.Add(centre, originNode);
        MapStorage.Add(centre, Origin);
        MapListManaged.Add(centre);
        for (int i = 1; i < rings; i++)
        {
            NativeArray<HexWithOffset> Ring = Hex.CubeRing(Origin, i);
            for (int k = 0; k < Ring.Length; k++)
            {
                MapListManaged.Add(Ring[k].ID);
                MapStorage.Add(Ring[k].ID, new HexTile(Ring[k].ID));
            }
            float startpos = (height + CellGap) * (i);
            SpawnRing(new Vector3(Centre.x, Centre.y, Centre.z - startpos), i, Ring);
            Ring.Dispose();

        }
        print("Grid Gen (GameObject) time: " + (Time.realtimeSinceStartup - TotalExtrudeTime) * 1000f + "ms");
        TotalExtrudeTime = Time.realtimeSinceStartup;
        CalculateNeighbours();
        print("Neighbour (GameObject) time: " + (Time.realtimeSinceStartup - TotalExtrudeTime) * 1000f + "ms");
    }

    private void CalculateNeighbours()
    {
        NativeArray<int2> Keys = MapStorage.GetKeyArray(Allocator.Temp);
        for (int i = 0; i < Keys.Length; i++)
        {
            int2 key = Keys[i];
            NativeArray<int2> Neighbours = Hex.AllNeighbours(key, MapStorage);
            if (!MapStorage.IsCreated)
            {
                Debug.Log("Deallocated");
            }
            HexTile Tile = MapStorage[key];
            Tile.neighbour0 = Neighbours[0];
            Tile.neighbour1 = Neighbours[1];
            Tile.neighbour2 = Neighbours[2];
            Tile.neighbour3 = Neighbours[3];
            Tile.neighbour4 = Neighbours[4];
            Tile.neighbour5 = Neighbours[5];
            Neighbours.Dispose();

            MapStorage[key] = Tile;
        }
        Keys.Dispose();
        for (int i = 0; i < MapListManaged.Count; i++)
        {
            int2 key = MapListManaged[i];
            HexNode GONode = MapStorageManaged[key];
            HexTile tile = MapStorage[key];
            HexNode[] neighbours = new HexNode[] 
            {
                MapStorageManaged[tile.neighbour0],
                MapStorageManaged[tile.neighbour1],
                MapStorageManaged[tile.neighbour2],
                MapStorageManaged[tile.neighbour3],
                MapStorageManaged[tile.neighbour4],
                MapStorageManaged[tile.neighbour5],
            };
            GONode.SetNeighbours( neighbours, key);
        }
    }
    /// Size 1
    /// height 1.73206
    /// hex width, 'w' = 2 * size * Height, 'h' = sqrt(3) * size
    /// hex h = sqrt(3) * size
    /// hex w = 2 * size * h
    /// horizontal distance between adjacent hex centers is hex w * 3/4
    /// vertical distance between adjacent hex centers is hex h

    /// 1 rings = 1 tile
    /// 2 rings = 7 tiles  | 2 * 3 = 6
    /// 3 rings = 19 tiles | 3 * 6 = 18
    /// 4 rings = 37 tiles | 4 * 9 = 36
    /// 5 rings = 61 tiles | 5 * 12 = 60
    /// 6 rings = 91 tiles | 6 * 15 = 90
    /// 7 rings = 127 tiles | 7 * 18 = 126
    /// 8 rings = 169 tiles | 8 * 21 = 168
    /// Rings * 3 - 3 or Rings * 3 - 2 for the central tile
    private void OutInSpawnMap(Vector3 Centre, int rings, int CutOff = 0, bool CentreTile = true, bool CentreTileOverride = false)
    {
        width = 2 * size;
        threeQuaterWidth = width * 0.75f;

        float root3 = math.sqrt(3);
        height = root3 * size;
        halfHeight = height / 2;
        if (rings < 1)
        {
            rings = 1;
        }
        if (CutOff >= rings)
        {
            CutOff = rings - 1;
        }
        if ((CentreTile && CutOff == 0) || (CentreTile && CentreTileOverride))
        {
            Instantiate(CellPrefab, Centre, quaternion.identity).name = "Origin";
        }
        float startpos =  (height + CellGap)* (rings);
        Debug.Log(startpos);
        while (rings != 0)
        {
            SpawnRing(new Vector3(Centre.x, Centre.y, Centre.z - startpos), rings);
            rings--;
            startpos -= height + CellGap;
            if (rings == CutOff)
            {
                break;
            }
        }
    }

    private void OutInSpawnMap(Vector3 Centre, int innerRing, int outerRing, int Gap, bool CentreTile = true)
    {
        
        if (Gap < 0)
        {
            Gap = 0;
        }
        if(innerRing < 1)
        {
            innerRing = 1;
        }
        if(outerRing < 1)
        {
            outerRing = 1;
        }
        OutInSpawnMap(Centre, outerRing + Gap + innerRing, Gap + innerRing);
        OutInSpawnMap(Centre, innerRing, 0, CentreTile);
    }

    private void SpawnRing(Vector3 Origin, int ringRadius, NativeArray<HexWithOffset> RingCoordinates)
    {
        List<HexNode> Nodes = new List<HexNode>();
        Nodes.Add(Instantiate(CellPrefab, Origin, Quaternion.identity).GetComponent<HexNode>());
        float Ydist = Origin.y;
        float Xdist = Origin.x;
        float Zdist = Origin.z;
        float halfCellGap = CellGap / 2;
        for (int i = 0; i < ringRadius; i++)
        {
            Xdist += threeQuaterWidth + CellGap;
            Zdist += halfHeight + halfCellGap;
            Nodes.Add(Instantiate(CellPrefab, new Vector3(Xdist, Ydist, Zdist), Quaternion.identity).GetComponent<HexNode>());
        }
        for (int i = 0; i < ringRadius; i++)
        {
            Zdist += height + CellGap;
            Nodes.Add(Instantiate(CellPrefab, new Vector3(Xdist, Ydist, Zdist), Quaternion.identity).GetComponent<HexNode>());
        }
        for (int i = 0; i < ringRadius; i++)
        {
            Xdist -= threeQuaterWidth + CellGap;
            Zdist += halfHeight + halfCellGap;
            Nodes.Add(Instantiate(CellPrefab, new Vector3(Xdist, Ydist, Zdist), Quaternion.identity).GetComponent<HexNode>());
        }

        for (int i = 0; i < ringRadius; i++)
        {
            Xdist -= threeQuaterWidth + CellGap;
            Zdist -= halfHeight + halfCellGap;
            Nodes.Add(Instantiate(CellPrefab, new Vector3(Xdist, Ydist, Zdist), Quaternion.identity).GetComponent<HexNode>());
        }
        for (int i = 0; i < ringRadius; i++)
        {
            Zdist -= height + CellGap;
            Nodes.Add(Instantiate(CellPrefab, new Vector3(Xdist, Ydist, Zdist), Quaternion.identity).GetComponent<HexNode>());
        }
        for (int i = 0; i < ringRadius - 1; i++)
        {
            Xdist += threeQuaterWidth + CellGap;
            Zdist -= halfHeight + halfCellGap;
            Nodes.Add(Instantiate(CellPrefab, new Vector3(Xdist, Ydist, Zdist), Quaternion.identity).GetComponent<HexNode>());
        }
        for (int i = 0; i < Nodes.Count; i++)
        {
            Nodes[i].gameObject.name = RingCoordinates[i].ID.ToString();
            MapStorageManaged.Add(RingCoordinates[i].ID, Nodes[i]);
        }
    }

    private void SpawnRing(Vector3 Origin, int ringRadius)
    {
        Instantiate(CellPrefab, Origin, Quaternion.identity);
        float Ydist = Origin.y;
        float Xdist = Origin.x;
        float Zdist = Origin.z;
        float halfCellGap = CellGap / 2;
        for (int i = 0; i < ringRadius; i++)
        {
            Xdist += threeQuaterWidth + CellGap;
            Zdist += halfHeight + halfCellGap;
            Instantiate(CellPrefab, new Vector3(Xdist, Ydist, Zdist), Quaternion.identity);
        }
        for (int i = 0; i < ringRadius; i++)
        {
            Zdist += height + CellGap;
            Instantiate(CellPrefab, new Vector3(Xdist, Ydist, Zdist), Quaternion.identity);
        }
        for (int i = 0; i < ringRadius; i++)
        {
            Xdist -= threeQuaterWidth + CellGap;
            Zdist += halfHeight + halfCellGap;
            Instantiate(CellPrefab, new Vector3(Xdist, Ydist, Zdist), Quaternion.identity);
        }

        for (int i = 0; i < ringRadius; i++)
        {
            Xdist -= threeQuaterWidth + CellGap;
            Zdist -= halfHeight + halfCellGap;
            Instantiate(CellPrefab, new Vector3(Xdist, Ydist, Zdist), Quaternion.identity);
        }
        for (int i = 0; i < ringRadius; i++)
        {
            Zdist -= height + CellGap;
            Instantiate(CellPrefab, new Vector3(Xdist, Ydist, Zdist), Quaternion.identity);
        }
        for (int i = 0; i < ringRadius-1; i++)
        {
            Xdist += threeQuaterWidth + CellGap;
            Zdist -= halfHeight + halfCellGap;
            Instantiate(CellPrefab, new Vector3(Xdist, Ydist, Zdist), Quaternion.identity);
        }
    }
}
