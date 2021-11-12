using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

public class TurretMovementAuthoring : MonoBehaviour
{

    public float ElevationSpeed = 30f;
    public float MaxElevation = 60f;
    public float MaxDepression = 5f;
    public float TraverseSpeed = 60f;
    public float aimedThreshold=5f;
    public bool hasLimitedTraverse = false;
    [Range(0, 179)] public float LeftLimit = 120f;
    [Range(0, 179)] public float RightLimit = 120f;
}

public struct TurretMovementData : IComponentData
{
    public Entity Self;
    public Entity VerticalEntity;
    public Entity HorizontalEntity;
    public Rotation HorizontalRotation;
    public float3 VerticalAxis;
    public float3 HorizontalAxis;
    public float TraverseSpeed;
    public float ElevationSpeed;
    public float MaxElevation;
    public float MaxDepression;
    public float LeftLimit;
    public float RightLimit;
    public bool hasLimitedTraverse;
    public float limitedTraverseAngle;
    //public bool isIdle;
    public float elevation;
    public float angleToTarget;
    public float aimedThreshold;
    //public bool isAimed;
    //public bool HorizontalAtRest;
    //public bool VerticalAtRest;
}
public struct NewTurret : IComponentData { }
public struct TurretIsIdle : IComponentData { }
public struct TurretIsAimed : IComponentData { }
public struct TurretTarget : IComponentData
{
    //public float3 TargetPosition;
    public Entity Entity;
}
public struct TargetWithPosition
{
    public Entity entity;
    public float3 position;
}
public class TurretMovementConversion : GameObjectConversionSystem
{
    protected override void OnUpdate()
    {
        Entities.ForEach((TurretMovementAuthoring input) =>
        {
            var Entity = GetPrimaryEntity(input);
            DstEntityManager.AddComponentData(Entity, new TurretMovementData
            {
                Self = Entity,
                ElevationSpeed = input.ElevationSpeed,
                MaxElevation = input.MaxElevation,
                MaxDepression = input.MaxDepression,
                TraverseSpeed = input.TraverseSpeed,
                hasLimitedTraverse = input.hasLimitedTraverse,
                LeftLimit = input.LeftLimit,
                RightLimit = input.RightLimit,
                aimedThreshold = input.aimedThreshold
            });
            DstEntityManager.AddComponent(Entity, typeof(NewTurret));
        });
    }
}