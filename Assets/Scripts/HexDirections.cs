using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MeshType
{
    Terrain,
    Rivers,
    Water,
    WaterShore,
    Estuaries,
    Roads,
    Walls
}

public enum HexDirection
{
    NE,
    E,
    SE,
    SW,
    W,
    NW
}

public enum HexEdgeType
{
    Flat,
    Slope,
    Cliff
}
