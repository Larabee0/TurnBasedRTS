using Unity.Entities;
using UnityEngine;

/// <summary>
/// Baker Monobehaviour for HexCell prefab.
/// HexCells are the most numerous entity in the HexMap project.
/// Each entity takes up a minimum of 212 bytes from components added in baking.
/// On a 300 cell grid this is approximety 63.6 kilobytes.
/// This does not include transform components or temporay components added and removed during runtime.
/// </summary>
public class HexCellBaker : MonoBehaviour { }

/// <summary>
/// Baker Class for HexCell, adds all required IComponentData components to the entity.
/// </summary>
public class HexCellBaking : Baker<HexCellBaker>
{
    public override void Bake(HexCellBaker authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent<HexCellBasic>(entity);
        // AddComponent<HexGridReference>(entity);
        AddComponent<HexCellTerrain>(entity);
        AddComponent<HexCellNav>(entity);
        AddComponent(entity, HexCellNeighbours.Empty);
    }
}