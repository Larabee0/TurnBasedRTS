using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public class TurretHoriztonalAxis : MonoBehaviour { }

public struct TurretHoriztonalAxisData : IComponentData
{
    public Entity TurretRoot;
    public Entity VerticalAxis;
    public bool hasLimitedTraverse;
    public float LeftLimit;
    public float RightLimit;
}

public class TurretHoriztonalAxisConversion : GameObjectConversionSystem
{
    protected override void OnUpdate()
    {
        Entities.ForEach((TurretHoriztonalAxis input) =>
        {
            var Entity = GetPrimaryEntity(input);
            DstEntityManager.AddComponent(Entity, typeof(TurretHoriztonalAxisData));
            DstEntityManager.AddComponent(Entity, typeof(NewTurret));
        });
    }
}