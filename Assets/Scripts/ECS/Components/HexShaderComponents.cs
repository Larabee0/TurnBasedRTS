using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;

public struct HexShaderPaintTexture : IComponentData { }

public struct HexShaderTransitionCells : IComponentData { }

public struct HexShaderCellDataComplete : IComponentData { }

public struct  HexShaderAllCellRequest : IComponentData { }

public struct HexShaderRefresh : IComponentData { }
public struct HexShaderRefreshAllComplete : IComponentData { }

public struct HexShaderRefreshAll : IComponentData { }

public struct HexShaderInitialise : IComponentData { public int x, z; public Entity grid; }

public class HexShaderCellTexture : IComponentData
{
    public Texture2D value;
}

public struct HexShaderSettings : IComponentData
{
    public Entity grid;
    public bool immediateMode;
}

public struct HexShaderTransitioningCells : IComponentData
{
    public NativeList<int> values;
}

public struct HexShaderTextureData : IComponentData
{
    public NativeArray<Color32> values;
}