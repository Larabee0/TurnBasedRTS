using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

/// <summary>
/// User Input state component used as a singleton on the HexMapEditorSystem.
/// Contained is the left mouse state and raycast for mouse pointer.
/// </summary>
public struct HexMapEditorLeftMouseHeld : IComponentData
{
    public bool leftMouseHeld;
    public UnityEngine.Ray ray;
    public float maxDistance;
    public float3 StartPoint => ray.origin;
    public float3 EndPoint => ray.GetPoint(maxDistance);
}

/// <summary>
/// UI state component used as a singleton on the HexMapEditorSystem.
/// Provides UI Edit settings for editing the map (elevation level, water level, etc)
/// 32 bytes Min
/// </summary>
public struct HexMapEditorUIState : IComponentData
{
    public int activeTerrainTypeIndex;
    public bool elevationToggle;
    public int elevationSilder;
    public bool waterToggle;
    public int waterSilder;
    public OptionalToggle river;
    public OptionalToggle road;
    public OptionalToggle walled;
    public bool urbanToggle;
    public int urbanSilder;
    public bool farmToggle;
    public int farmSilder;
    public bool plantToggle;
    public int plantSilder;
    public bool specialToggle;
    public int specialSilder;
}

/// <summary>
/// HitInfoRaycast taken from previous  ECS project to extend RaycastHit to provide Distance.
/// Also sortable by distance
/// </summary>
public struct HitInfoRaycast : IComparer<HitInfoRaycast>
{
    public float Distance;
    public RaycastHit raycastHit;

    public float Fraction => raycastHit.Fraction;
    public int RigidBodyIndex => raycastHit.RigidBodyIndex;
    public ColliderKey ColliderKey => raycastHit.ColliderKey;
    public Material Material => raycastHit.Material;
    public Entity Entity => raycastHit.Entity;
    public float3 SurfaceNormal => raycastHit.SurfaceNormal;
    public float3 Position => raycastHit.Position;

    public int Compare(HitInfoRaycast x, HitInfoRaycast y)
    {
        return x.Distance.CompareTo(y.Distance);
    }
}