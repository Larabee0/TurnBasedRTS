using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
public class TurretVerticalAxis : MonoBehaviour { }
public struct TurretVerticalAxisData : IComponentData
{
    public Entity TurretRoot;
    public Entity HorizontalAxis;
    public float ElevationSpeed;
    public float MaxElevation;
    public float MaxDepression;
}
public class TurretVerticalAxisConversion : GameObjectConversionSystem
{
    protected override void OnUpdate()
    {
        Entities.ForEach((TurretVerticalAxis input) =>
        {
            var Entity = GetPrimaryEntity(input);
            DstEntityManager.AddComponent(Entity, typeof(TurretVerticalAxisData));
            DstEntityManager.AddComponent(Entity, typeof(NewTurret));
        });
    }
}