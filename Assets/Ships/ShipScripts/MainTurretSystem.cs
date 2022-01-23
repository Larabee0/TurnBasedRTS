using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Rendering;
using Unity.Mathematics;
using System.Linq;
using Unity.Physics;
using Unity.Jobs;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.IL2CPP.CompilerServices;
using Unity.Burst;

public class StartTurretSystem : ComponentSystem
{
    private EntityManager entityManager;
    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;
    private EntityCommandBuffer commandBuffer;
    private EntityQueryDesc HoriztonalEntitiesQuery;
    private EntityQueryDesc VerticalEntitiesQuery;
    protected override void OnCreate()
    {
        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        HoriztonalEntitiesQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(Parent), typeof(Rotation), typeof(TurretHoriztonalAxisData), typeof(NewTurret) }
        };
        VerticalEntitiesQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(Parent), typeof(Rotation), typeof(TurretVerticalAxisData), typeof(NewTurret) }
        };
    }

    protected override void OnUpdate()
    {
        float SetUpFirst = UnityEngine.Time.realtimeSinceStartup;
        commandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer();
        NativeArray<Entity>HorizontalEntities = entityManager.CreateEntityQuery(HoriztonalEntitiesQuery).ToEntityArray(Allocator.Temp);
        NativeHashSet<Entity> HorizontalEntitiesSet = new NativeHashSet<Entity>(HorizontalEntities.Length, Allocator.Temp);
        for (int i = 0; i < HorizontalEntities.Length; i++)
        {
            HorizontalEntitiesSet.Add(HorizontalEntities[i]);
        }
        HorizontalEntities.Dispose();

        NativeArray<Entity> VerticalEntities = entityManager.CreateEntityQuery(VerticalEntitiesQuery).ToEntityArray(Allocator.Temp);
        NativeHashSet<Entity> VerticalEntitiesSet = new NativeHashSet<Entity>(VerticalEntities.Length, Allocator.Temp);
        for (int i = 0; i < VerticalEntities.Length; i++)
        {
            VerticalEntitiesSet.Add(VerticalEntities[i]);
        }
        VerticalEntities.Dispose();

        Entities.WithAll<TurretMovementData,Child,NewTurret > ().ForEach((Entity Root, ref TurretMovementData TurretData) =>
        {
            NativeArray<Entity> RootChildren = entityManager.GetBuffer<Child>(Root).Reinterpret<Entity>().ToNativeArray(Allocator.Temp);
            Entity? HoriztonalRotEntity = null;
            for (int i = 0; i < RootChildren.Length; i++)
            {
                if (HorizontalEntitiesSet.Contains(RootChildren[i]))
                {
                    HoriztonalRotEntity = RootChildren[i];
                }
            }
            RootChildren.Dispose();
            if (HoriztonalRotEntity.HasValue)
            {
                NativeArray<Entity> HorizontalChildren = entityManager.GetBuffer<Child>(HoriztonalRotEntity.Value).Reinterpret<Entity>().ToNativeArray(Allocator.Temp);
                Entity? VerticalRotEntity = null;
                for (int i = 0; i < HorizontalChildren.Length; i++)
                {
                    if (VerticalEntitiesSet.Contains(HorizontalChildren[i]))
                    {
                        VerticalRotEntity = HorizontalChildren[i];
                    }
                }
                HorizontalChildren.Dispose();
                if (VerticalRotEntity.HasValue)
                {

                    TurretData.VerticalEntity = VerticalRotEntity.Value;
                    TurretData.HorizontalEntity = HoriztonalRotEntity.Value;
                    TurretData.HorizontalRotation = entityManager.GetComponentData<Rotation>(HoriztonalRotEntity.Value);
                    TurretData.VerticalAxis = ExtraTurretMathsFunctions.ToEuler(entityManager.GetComponentData<Rotation>(VerticalRotEntity.Value).Value);
                    TurretData.HorizontalAxis = ExtraTurretMathsFunctions.ToEuler(entityManager.GetComponentData<Rotation>(HoriztonalRotEntity.Value).Value);

                    commandBuffer.SetComponent(HoriztonalRotEntity.Value, new TurretHoriztonalAxisData
                    {
                        TurretRoot = Root,
                        VerticalAxis = VerticalRotEntity.Value,
                        hasLimitedTraverse = TurretData.hasLimitedTraverse,
                        LeftLimit = TurretData.LeftLimit,
                        RightLimit = TurretData.RightLimit
                    });
                    commandBuffer.RemoveComponent(HoriztonalRotEntity.Value, typeof(NewTurret));
                    commandBuffer.SetComponent(VerticalRotEntity.Value, new TurretVerticalAxisData
                    {
                        TurretRoot = Root,
                        HorizontalAxis = HoriztonalRotEntity.Value,
                        ElevationSpeed = TurretData.ElevationSpeed,
                        MaxElevation = TurretData.MaxElevation,
                        MaxDepression = TurretData.MaxDepression
                    });
                    commandBuffer.RemoveComponent(VerticalRotEntity.Value, typeof(NewTurret));
                }
            }
            
            commandBuffer.RemoveComponent(Root, typeof(NewTurret));
            //commandBuffer.AddComponent(Root, typeof(TurretTarget));
        });

        HorizontalEntitiesSet.Dispose();
        VerticalEntitiesSet.Dispose();
        Debug.Log("Update() Execution Time: " + (UnityEngine.Time.realtimeSinceStartup - SetUpFirst) * 1000f + "ms");

    }

}
[UpdateAfter(typeof(TurretToTargetSystem))]
public class FindTargetForTurretSystem : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationCommandBuffer;
    private EntityQueryDesc TargetQuery;
    private EntityQueryDesc TurretEntitiesQuery;
    private EntityQueryDesc HoriztonalEntitiesQuery;
    private EntityQueryDesc VerticalEntitiesQuery;
    protected override void OnCreate()
    {
        endSimulationCommandBuffer = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        TargetQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(Target), typeof(LocalToWorld) }
        };
        TurretEntitiesQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(TurretMovementData), typeof(LocalToWorld) },
            None = new ComponentType[] { typeof(NewTurret), typeof(TurretTarget) }
        };
        HoriztonalEntitiesQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(Parent), typeof(Translation), typeof(LocalToWorld), typeof(TurretHoriztonalAxisData) }
        };
        VerticalEntitiesQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(Parent), typeof(Translation), typeof(LocalToWorld), typeof(TurretVerticalAxisData) }
        };
    }
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (GetEntityQuery(TurretEntitiesQuery).CalculateEntityCount() == 0) 
        {
            return inputDeps;
        }
        EntityCommandBuffer.ParallelWriter commandBuffer = endSimulationCommandBuffer.CreateCommandBuffer().AsParallelWriter();
        #region Target Entities and Positions
        EntityQuery Targetquery = GetEntityQuery(TargetQuery);
        NativeArray<Entity> targetEntityArray = Targetquery.ToEntityArray(Allocator.Temp);
        NativeArray<LocalToWorld> targetTranslationArray = Targetquery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
        NativeArray<TargetWithPosition> targetArray = new NativeArray<TargetWithPosition>(targetEntityArray.Length, Allocator.TempJob);
        for (int i = 0; i < targetArray.Length; i++)
        {
            targetArray[i] = new TargetWithPosition
            {
                entity = targetEntityArray[i],
                position = targetTranslationArray[i].Position,
            };
        }
        targetEntityArray.Dispose();
        targetTranslationArray.Dispose();
        #endregion
        #region GetTurretComponents

        EntityQuery HorizontalComponents = GetEntityQuery(HoriztonalEntitiesQuery);
        EntityQuery VerticalComponents = GetEntityQuery(VerticalEntitiesQuery);


        NativeArray<LocalToWorld> HorizontalArray = HorizontalComponents.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
        NativeArray<Entity> HorizontalEntityArray = HorizontalComponents.ToEntityArray(Allocator.Temp);
        NativeArray<LocalToWorld> VerticalArray = VerticalComponents.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
        NativeArray<Entity> VerticalEntityArray = VerticalComponents.ToEntityArray(Allocator.Temp);
        NativeHashMap<Entity, LocalToWorld> HorizontalSide = new NativeHashMap<Entity, LocalToWorld>(HorizontalEntityArray.Length, Allocator.TempJob);
        NativeHashMap<Entity, LocalToWorld> VerticalSide = new NativeHashMap<Entity, LocalToWorld>(VerticalEntityArray.Length, Allocator.TempJob);

        for (int i = 0; i < HorizontalEntityArray.Length; i++)
        {
            HorizontalSide.Add(HorizontalEntityArray[i], HorizontalArray[i]);
            VerticalSide.Add(VerticalEntityArray[i], VerticalArray[i]);
        }
        HorizontalArray.Dispose();
        HorizontalEntityArray.Dispose();
        VerticalArray.Dispose();
        VerticalEntityArray.Dispose();
        #endregion
        JobHandle jobHandle = Entities.WithReadOnly(targetArray).WithReadOnly(HorizontalSide).WithReadOnly(VerticalSide).WithAll<TurretMovementData, LocalToWorld>().WithNone<NewTurret, TurretTarget>().ForEach((int entityInQueryIndex, in LocalToWorld turretLocalToWorld, in TurretMovementData turretIn) =>
        {
            TurretMovementData turret = turretIn;
            LocalToWorld HorizontalLocalToWorld = HorizontalSide[turret.HorizontalEntity];
            LocalToWorld VerticalLocalToWorld = VerticalSide[turret.VerticalEntity];
            float3 VerticalPos = VerticalLocalToWorld.Position;
            float3 turretPosition;
            Entity closestTargetEntity = Entity.Null;            
            float3 closetTargetPosition = float3.zero;
            turretPosition = turretLocalToWorld.Position;
            bool TargetTargetable = false;
            for (int targetIndex = 0; targetIndex < targetArray.Length; targetIndex++)
            {
                TargetWithPosition targetWithPosition = targetArray[targetIndex];
                
                if (closestTargetEntity == Entity.Null)
                {
                    closestTargetEntity = targetWithPosition.entity;
                    closetTargetPosition = targetWithPosition.position;
                }
                else
                {
                    float3 targetPosition = targetWithPosition.position;
                    float3 localTargetPos = math.mul(math.inverse(HorizontalLocalToWorld.Rotation), targetPosition - VerticalPos);
                    float3 flattenedVecForVertical = ExtraTurretMathsFunctions.ProjectOnPlane(localTargetPos, ExtraTurretMathsFunctions.Up);
                    float targetElevation = ExtraTurretMathsFunctions.Angle(flattenedVecForVertical, localTargetPos);
                    targetElevation *= math.sign(localTargetPos.y);
                    
                    TargetTargetable = targetElevation == math.clamp(targetElevation, -turret.MaxDepression, turret.MaxElevation);

                    if (math.distance(turretPosition, targetWithPosition.position) < math.distance(turretPosition, closetTargetPosition) && TargetTargetable)
                    {
                        closestTargetEntity = targetWithPosition.entity;
                        closetTargetPosition = targetWithPosition.position;
                    }
                }
            }
            if (TargetTargetable)
            {
                //commandBuffer.AddComponent(entityInQueryIndex, turretIn.Self, new TurretTarget { TargetPosition = closetTargetPosition, TargetEntity = closestTargetEntity });
                commandBuffer.AddComponent(entityInQueryIndex, turretIn.Self, new TurretTarget { Entity = closestTargetEntity });
            }
            
            closestTargetEntity = Entity.Null;
            closetTargetPosition = float3.zero;
        }).Schedule(inputDeps);
        endSimulationCommandBuffer.AddJobHandleForProducer(jobHandle);
        jobHandle = targetArray.Dispose(jobHandle);
        jobHandle = HorizontalSide.Dispose(jobHandle);
        jobHandle = VerticalSide.Dispose(jobHandle);
        return jobHandle;
    }
}
[UpdateBefore(typeof(FindTargetForTurretSystem))]
public class TurretTargetValidSystem : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationCommandBuffer;
    private EntityQueryDesc TargetQuery;
    private EntityQueryDesc TurretEntitiesQuery;
    private EntityQueryDesc HoriztonalEntitiesQuery;
    private EntityQueryDesc VerticalEntitiesQuery;
    protected override void OnCreate()
    {
        endSimulationCommandBuffer = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        TargetQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(Target), typeof(LocalToWorld) }
        };
        TurretEntitiesQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(TurretMovementData), typeof(LocalToWorld), typeof(TurretTarget) },
            None = new ComponentType[] { typeof(NewTurret), typeof(TurretIsIdle), typeof(TurretIsAimed) }
        };
        HoriztonalEntitiesQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(Parent), typeof(Translation), typeof(LocalToWorld), typeof(TurretHoriztonalAxisData) }
        };
        VerticalEntitiesQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(Parent), typeof(Translation), typeof(LocalToWorld), typeof(TurretVerticalAxisData) }
        };
    }
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (GetEntityQuery(TurretEntitiesQuery).CalculateEntityCount() == 0)
        {
            return inputDeps;
        }
        EntityCommandBuffer.ParallelWriter commandBuffer = endSimulationCommandBuffer.CreateCommandBuffer().AsParallelWriter();
        #region Target Entities and Positions
        EntityQuery Targetquery = GetEntityQuery(TargetQuery);
        NativeArray<Entity> targetEntityArray = Targetquery.ToEntityArray(Allocator.Temp);
        NativeArray<LocalToWorld> targetTranslationArray = Targetquery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
        NativeHashMap<Entity,float3> targetArray = new NativeHashMap<Entity, float3>(targetEntityArray.Length, Allocator.TempJob);
        for (int i = 0; i < targetEntityArray.Length; i++)
        {
            targetArray.Add(targetEntityArray[i], targetTranslationArray[i].Position);
        }
        targetEntityArray.Dispose();
        targetTranslationArray.Dispose();
        #endregion
        #region GetTurretComponents
        EntityQuery HorizontalComponents = GetEntityQuery(HoriztonalEntitiesQuery);
        EntityQuery VerticalComponents = GetEntityQuery(VerticalEntitiesQuery);
        NativeArray<LocalToWorld> HorizontalArray = HorizontalComponents.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
        NativeArray<Entity> HorizontalEntityArray = HorizontalComponents.ToEntityArray(Allocator.Temp);
        NativeArray<LocalToWorld> VerticalArray = VerticalComponents.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
        NativeArray<Entity> VerticalEntityArray = VerticalComponents.ToEntityArray(Allocator.Temp);
        NativeHashMap<Entity, LocalToWorld> HorizontalSide = new NativeHashMap<Entity, LocalToWorld>(HorizontalEntityArray.Length, Allocator.TempJob);
        NativeHashMap<Entity, LocalToWorld> VerticalSide = new NativeHashMap<Entity, LocalToWorld>(VerticalEntityArray.Length, Allocator.TempJob);

        for (int i = 0; i < HorizontalEntityArray.Length; i++)
        {
            HorizontalSide.Add(HorizontalEntityArray[i], HorizontalArray[i]);
            VerticalSide.Add(VerticalEntityArray[i], VerticalArray[i]);
        }
        HorizontalArray.Dispose();
        HorizontalEntityArray.Dispose();
        VerticalArray.Dispose();
        VerticalEntityArray.Dispose();
        #endregion
        JobHandle jobHandle = Entities.WithReadOnly(targetArray).WithReadOnly(HorizontalSide).WithReadOnly(VerticalSide).WithAll<TurretMovementData, TurretTarget,LocalToWorld>().WithNone<NewTurret, TurretIsIdle, TurretIsAimed>().ForEach((int entityInQueryIndex, in TurretTarget target, in LocalToWorld turretLocalToWorld, in TurretMovementData turretIn) =>
        {
            TurretMovementData turret = turretIn;
            LocalToWorld HorizontalLocalToWorld = HorizontalSide[turret.HorizontalEntity];
            LocalToWorld VerticalLocalToWorld = VerticalSide[turret.VerticalEntity];
            float3 VerticalPos = VerticalLocalToWorld.Position;
            float3 turretPosition;
            Entity closestTargetEntity = Entity.Null;
            float3 closetTargetPosition = float3.zero;
            turretPosition = turretLocalToWorld.Position;
            bool TargetTargetable = false;
            float3 targetPosition = targetArray[target.Entity];
            float3 localTargetPos = math.mul(math.inverse(HorizontalLocalToWorld.Rotation), targetPosition - VerticalPos);
            float3 flattenedVecForVertical = ExtraTurretMathsFunctions.ProjectOnPlane(localTargetPos, ExtraTurretMathsFunctions.Up);
            float targetElevation = ExtraTurretMathsFunctions.Angle(flattenedVecForVertical, localTargetPos);
            targetElevation *= math.sign(localTargetPos.y);

            TargetTargetable = targetElevation == math.clamp(targetElevation, -turret.MaxDepression, turret.MaxElevation);
            if (!TargetTargetable)
            {
                commandBuffer.RemoveComponent<TurretTarget>(entityInQueryIndex, turretIn.Self);
            }

            closestTargetEntity = Entity.Null;
            closetTargetPosition = float3.zero;
        }).Schedule(inputDeps);
        endSimulationCommandBuffer.AddJobHandleForProducer(jobHandle);
        jobHandle = HorizontalSide.Dispose(jobHandle);
        jobHandle = VerticalSide.Dispose(jobHandle);
        jobHandle = targetArray.Dispose(jobHandle);
        return jobHandle;
    }
}
[UpdateAfter(typeof(TurretTargetValidSystem))]
public class TurretIsAimedSystem : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationCommandBuffer;
    private EntityQueryDesc TargetQuery;
    private EntityQueryDesc TurretEntitiesQuery;
    private EntityQueryDesc HoriztonalEntitiesQuery;
    private EntityQueryDesc VerticalEntitiesQuery;
    protected override void OnCreate()
    {
        endSimulationCommandBuffer = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        TargetQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(Target), typeof(LocalToWorld) }
        };
        TurretEntitiesQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(TurretMovementData), typeof(LocalToWorld), typeof(TurretTarget), typeof(TurretIsAimed) },
            None = new ComponentType[] { typeof(NewTurret), typeof(TurretIsIdle) }
        };
        HoriztonalEntitiesQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(Parent), typeof(Translation), typeof(LocalToWorld), typeof(TurretHoriztonalAxisData) }
        };
        VerticalEntitiesQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(Parent), typeof(Translation), typeof(LocalToWorld), typeof(TurretVerticalAxisData) }
        };
    }
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (GetEntityQuery(TurretEntitiesQuery).CalculateEntityCount() == 0)
        {
            return inputDeps;
        }
        EntityCommandBuffer.ParallelWriter commandBuffer = endSimulationCommandBuffer.CreateCommandBuffer().AsParallelWriter();
        #region Target Entities and Positions
        EntityQuery Targetquery = GetEntityQuery(TargetQuery);
        NativeArray<Entity> targetEntityArray = Targetquery.ToEntityArray(Allocator.Temp);
        NativeArray<LocalToWorld> targetTranslationArray = Targetquery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
        NativeHashMap<Entity, float3> targetArray = new NativeHashMap<Entity, float3>(targetEntityArray.Length, Allocator.TempJob);
        for (int i = 0; i < targetEntityArray.Length; i++)
        {
            targetArray.Add(targetEntityArray[i], targetTranslationArray[i].Position);
        }
        targetEntityArray.Dispose();
        targetTranslationArray.Dispose();
        #endregion
        #region GetTurretComponents

        EntityQuery HorizontalComponents = GetEntityQuery(HoriztonalEntitiesQuery);
        EntityQuery VerticalComponents = GetEntityQuery(VerticalEntitiesQuery);


        NativeArray<LocalToWorld> HorizontalArray = HorizontalComponents.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
        NativeArray<Entity> HorizontalEntityArray = HorizontalComponents.ToEntityArray(Allocator.Temp);
        NativeArray<LocalToWorld> VerticalArray = VerticalComponents.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
        NativeArray<Entity> VerticalEntityArray = VerticalComponents.ToEntityArray(Allocator.Temp);
        NativeHashMap<Entity, LocalToWorld> HorizontalSide = new NativeHashMap<Entity, LocalToWorld>(HorizontalEntityArray.Length, Allocator.TempJob);
        NativeHashMap<Entity, LocalToWorld> VerticalSide = new NativeHashMap<Entity, LocalToWorld>(VerticalEntityArray.Length, Allocator.TempJob);

        for (int i = 0; i < HorizontalEntityArray.Length; i++)
        {
            HorizontalSide.Add(HorizontalEntityArray[i], HorizontalArray[i]);
            VerticalSide.Add(VerticalEntityArray[i], VerticalArray[i]);
        }
        HorizontalArray.Dispose();
        HorizontalEntityArray.Dispose();
        VerticalArray.Dispose();
        VerticalEntityArray.Dispose();
        #endregion
        JobHandle jobHandle = Entities.WithReadOnly(targetArray).WithReadOnly(HorizontalSide).WithReadOnly(VerticalSide).WithAll<TurretIsAimed>().WithAll<TurretMovementData, TurretTarget, LocalToWorld>().WithNone<NewTurret, TurretIsIdle>().ForEach((int entityInQueryIndex, in TurretTarget target, in LocalToWorld turretLocalToWorld, in TurretMovementData turretIn) =>
        {
            TurretMovementData turret = turretIn;
            LocalToWorld HorizontalLocalToWorld = HorizontalSide[turret.HorizontalEntity];
            LocalToWorld VerticalLocalToWorld = VerticalSide[turret.VerticalEntity];
            float3 VerticalPos = VerticalLocalToWorld.Position;
            float3 turretPosition;
            turretPosition = turretLocalToWorld.Position;
            float3 targetPosition = targetArray[target.Entity];
            float3 localTargetPos = math.mul(math.inverse(HorizontalLocalToWorld.Rotation), targetPosition - VerticalPos);
            float3 flattenedVecForVertical = ExtraTurretMathsFunctions.ProjectOnPlane(localTargetPos, ExtraTurretMathsFunctions.Up);
            float targetElevation = ExtraTurretMathsFunctions.Angle(flattenedVecForVertical, localTargetPos);
            targetElevation *= math.sign(localTargetPos.y);

            turret.angleToTarget = ExtraTurretMathsFunctions.Angle(targetPosition - VerticalPos, VerticalLocalToWorld.Forward);
            if (turret.angleToTarget > turret.aimedThreshold)
            {
                commandBuffer.RemoveComponent<TurretIsAimed>(entityInQueryIndex, turret.Self);
            }

        }).Schedule(inputDeps);
        endSimulationCommandBuffer.AddJobHandleForProducer(jobHandle);
        jobHandle = HorizontalSide.Dispose(jobHandle);
        jobHandle = VerticalSide.Dispose(jobHandle);
        jobHandle = targetArray.Dispose(jobHandle);
        return jobHandle;
    }
}
[UpdateAfter(typeof(TurretIsAimedSystem))][DisableAutoCreation]
public class TurretToTargetSystem : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationCommandBuffer;
    private EntityQueryDesc TargetQuery;
    private EntityQueryDesc HoriztonalEntitiesQuery;
    private EntityQueryDesc VerticalEntitiesQuery;
    private EntityQueryDesc TurretEntitiesQuery;
    
    protected override void OnCreate()
    {
        endSimulationCommandBuffer = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        TargetQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(Target), typeof(LocalToWorld) }
        };
        HoriztonalEntitiesQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(Parent), typeof(Translation), typeof(LocalToWorld), typeof(TurretHoriztonalAxisData) }
        };
        VerticalEntitiesQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(Parent), typeof(Translation), typeof(LocalToWorld), typeof(TurretVerticalAxisData) }
        };
        TurretEntitiesQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(TurretTarget), typeof(TurretMovementData), typeof(LocalToWorld) },
            None = new ComponentType[] { typeof(TurretIsIdle), typeof(NewTurret), typeof(TurretIsAimed) }
        };
    }
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (GetEntityQuery(TurretEntitiesQuery).CalculateEntityCount() == 0)
        {
            return inputDeps;
        }
        #region Time and ParallelCommandBuffer
        float deltaTime = Time.DeltaTime;
        EntityCommandBuffer.ParallelWriter commandBuffer = endSimulationCommandBuffer.CreateCommandBuffer().AsParallelWriter();
        #endregion
        #region Target Entities and Positions
        EntityQuery Targetquery = GetEntityQuery(TargetQuery);
        NativeArray<Entity> targetEntityArray = Targetquery.ToEntityArray(Allocator.Temp);
        NativeArray<LocalToWorld> targetTranslationArray = Targetquery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
        NativeHashMap<Entity, float3> targetArray = new NativeHashMap<Entity, float3>(targetEntityArray.Length, Allocator.TempJob);
        for (int i = 0; i < targetEntityArray.Length; i++)
        {
            targetArray.Add(targetEntityArray[i], targetTranslationArray[i].Position);
        }
        targetEntityArray.Dispose();
        targetTranslationArray.Dispose();
        #endregion
        #region GetTurretComponents

        EntityQuery HorizontalComponents = GetEntityQuery(HoriztonalEntitiesQuery);
        EntityQuery VerticalComponents = GetEntityQuery(VerticalEntitiesQuery);
        

        NativeArray<LocalToWorld> HorizontalArray = HorizontalComponents.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
        NativeArray<Entity> HorizontalEntityArray = HorizontalComponents.ToEntityArray(Allocator.Temp);
        NativeArray<LocalToWorld> VerticalArray = VerticalComponents.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
        NativeArray<Entity> VerticalEntityArray = VerticalComponents.ToEntityArray(Allocator.Temp);
        NativeHashMap<Entity, LocalToWorld> HorizontalSide = new NativeHashMap<Entity, LocalToWorld>(HorizontalEntityArray.Length, Allocator.TempJob);
        NativeHashMap<Entity, LocalToWorld> VerticalSide = new NativeHashMap<Entity, LocalToWorld>(VerticalEntityArray.Length, Allocator.TempJob);
        
        for (int i = 0; i < HorizontalEntityArray.Length; i++)
        {
            HorizontalSide.Add(HorizontalEntityArray[i], HorizontalArray[i]);
            VerticalSide.Add(VerticalEntityArray[i], VerticalArray[i]);
        }
        HorizontalArray.Dispose();
        HorizontalEntityArray.Dispose();
        VerticalArray.Dispose();
        VerticalEntityArray.Dispose();
        #endregion
        JobHandle jobHandle = Entities.WithReadOnly(targetArray).WithReadOnly(HorizontalSide).WithReadOnly(VerticalSide).WithAll<TurretTarget, LocalToWorld,TurretMovementData>().WithNone<TurretIsIdle,NewTurret, TurretIsAimed>().ForEach((int entityInQueryIndex,ref LocalToWorld turretLocalToWorld, in TurretMovementData turretIn, in TurretTarget target) =>
        {
            TurretMovementData turret = turretIn;
            LocalToWorld HorizontalLocalToWorld = HorizontalSide[turret.HorizontalEntity];
            LocalToWorld VerticalLocalToWorld = VerticalSide[turret.VerticalEntity];
            float3 targetPosition = targetArray[target.Entity];
            float3 basePos = HorizontalLocalToWorld.Position;
            float3 VerticalPos = VerticalLocalToWorld.Position;
            float3 HorizontalFoward = HorizontalLocalToWorld.Forward;
            float3 turretUp = turretLocalToWorld.Up;
            float3 turretForward = turretLocalToWorld.Forward;

            float3 vecToTarget = targetPosition - basePos;
            float3 flattenedVecForBase = ExtraTurretMathsFunctions.ProjectOnPlane(vecToTarget, turretUp);
            float targetTraverse = ExtraTurretMathsFunctions.SignedAngle(turretForward, flattenedVecForBase, turretUp);
            turret.limitedTraverseAngle = ExtraTurretMathsFunctions.MoveTowards(turret.limitedTraverseAngle, targetTraverse, turret.TraverseSpeed * deltaTime);


            float3 localTargetPos = math.mul(math.inverse(HorizontalLocalToWorld.Rotation), targetPosition - VerticalPos);
            float3 flattenedVecForVertical = ExtraTurretMathsFunctions.ProjectOnPlane(localTargetPos, ExtraTurretMathsFunctions.Up);
            float targetElevation = ExtraTurretMathsFunctions.Angle(flattenedVecForVertical, localTargetPos);
            targetElevation *= math.sign(localTargetPos.y);
            targetElevation = math.clamp(targetElevation, -turret.MaxDepression, turret.MaxElevation);
            turret.elevation = ExtraTurretMathsFunctions.MoveTowards(turret.elevation, targetElevation, turret.ElevationSpeed * deltaTime);

            if (math.abs(turret.elevation) > math.EPSILON)
            {
                turret.VerticalAxis = ExtraTurretMathsFunctions.Right * -turret.elevation;
                commandBuffer.SetComponent(entityInQueryIndex, turret.VerticalEntity, new Rotation { Value = quaternion.EulerXYZ(math.radians(turret.VerticalAxis)) });
            }

            if (math.abs(turret.limitedTraverseAngle) > math.EPSILON)
            {
                
                turret.HorizontalAxis = ExtraTurretMathsFunctions.Up * turret.limitedTraverseAngle;
                commandBuffer.SetComponent(entityInQueryIndex, turret.HorizontalEntity, new Rotation { Value = quaternion.EulerXYZ(math.radians(turret.HorizontalAxis)) });
            }

            turret.angleToTarget = ExtraTurretMathsFunctions.Angle(targetPosition - VerticalPos, VerticalLocalToWorld.Forward);
            if (turret.angleToTarget < turret.aimedThreshold)
            {
                commandBuffer.AddComponent<TurretIsAimed>(entityInQueryIndex, turret.Self);
            }

            commandBuffer.SetComponent(entityInQueryIndex, turret.HorizontalEntity, HorizontalLocalToWorld);
            commandBuffer.SetComponent(entityInQueryIndex, turret.Self, turret);

        }).Schedule(inputDeps);
        jobHandle = HorizontalSide.Dispose(jobHandle);
        jobHandle = VerticalSide.Dispose(jobHandle);
        jobHandle = targetArray.Dispose(jobHandle);
        endSimulationCommandBuffer.AddJobHandleForProducer(jobHandle);
        
        return jobHandle;
    }
}

[UpdateAfter(typeof(TurretIsAimedSystem))]
public class TurretToTargetSystemV2 : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationCommandBuffer;
    private EntityQuery TargetQuery;
    private EntityQuery HoriztonalEntitiesQuery;
    private EntityQuery VerticalEntitiesQuery;
    private EntityQuery TurretEntitiesQuery;

    protected override void OnCreate()
    {
        endSimulationCommandBuffer = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        TargetQuery =GetEntityQuery( new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(Target), typeof(LocalToWorld) }
        });
        HoriztonalEntitiesQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(Parent), typeof(Translation), typeof(LocalToWorld), typeof(TurretHoriztonalAxisData) }
        });
        VerticalEntitiesQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(Parent), typeof(Translation), typeof(LocalToWorld), typeof(TurretVerticalAxisData) }
        });
        TurretEntitiesQuery = GetEntityQuery(new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(TurretTarget), typeof(TurretMovementData), typeof(LocalToWorld) },
            None = new ComponentType[] { typeof(TurretIsIdle), typeof(NewTurret), typeof(TurretIsAimed) }
        });
    }
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (TurretEntitiesQuery.CalculateEntityCount() == 0)
        {
            return inputDeps;
        }
        #region Time and ParallelCommandBuffer
        float deltaTime = Time.DeltaTime;
        EntityCommandBuffer.ParallelWriter commandBuffer = endSimulationCommandBuffer.CreateCommandBuffer().AsParallelWriter();
        #endregion
        #region Target Entities and Positions
        NativeArray<Entity> targetEntityArray = TargetQuery.ToEntityArray(Allocator.Temp);
        NativeArray<LocalToWorld> targetTranslationArray = TargetQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
        NativeHashMap<Entity, float3> targetMap = new NativeHashMap<Entity, float3>(targetEntityArray.Length, Allocator.TempJob);
        for (int i = 0; i < targetEntityArray.Length; i++)
        {
            targetMap.Add(targetEntityArray[i], targetTranslationArray[i].Position);
        }
        #endregion
        #region GetTurretComponents

        NativeArray<LocalToWorld> HorizontalArray = HoriztonalEntitiesQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
        NativeArray<Entity> HorizontalEntityArray = HoriztonalEntitiesQuery.ToEntityArray(Allocator.Temp);
        NativeArray<LocalToWorld> VerticalArray = VerticalEntitiesQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
        NativeArray<Entity> VerticalEntityArray = VerticalEntitiesQuery.ToEntityArray(Allocator.Temp);
        NativeHashMap<Entity, LocalToWorld> HorizontalSide = new NativeHashMap<Entity, LocalToWorld>(HorizontalEntityArray.Length, Allocator.TempJob);
        NativeHashMap<Entity, LocalToWorld> VerticalSide = new NativeHashMap<Entity, LocalToWorld>(VerticalEntityArray.Length, Allocator.TempJob);

        for (int i = 0; i < HorizontalEntityArray.Length; i++)
        {
            HorizontalSide.Add(HorizontalEntityArray[i], HorizontalArray[i]);
            VerticalSide.Add(VerticalEntityArray[i], VerticalArray[i]);
        }
        #endregion

        TurretToTargetJob job = new TurretToTargetJob
        {
            targetMap = targetMap,
            HorizontalSide = HorizontalSide,
            VerticalSide = VerticalSide,
            localToWorldTypeHandle = GetComponentTypeHandle<LocalToWorld>(true),
            turretInTypeHandle = GetComponentTypeHandle<TurretMovementData>(true),
            turretTargetTypeHandle = GetComponentTypeHandle<TurretTarget>(true),
            deltaTime = deltaTime,
            ecb = endSimulationCommandBuffer.CreateCommandBuffer().AsParallelWriter()
        };

        JobHandle outputDeps = job.ScheduleParallel(TurretEntitiesQuery, 64,inputDeps);
        outputDeps = HorizontalSide.Dispose(outputDeps);
        outputDeps = VerticalSide.Dispose(outputDeps);
        outputDeps = targetMap.Dispose(outputDeps);
        endSimulationCommandBuffer.AddJobHandleForProducer(outputDeps);

        return outputDeps;
    }
}

public struct Target : IComponentData { }
public struct IdleTurret : IComponentData { }

[DisableAutoCreation]
public class RandomIdleTurretSystem : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationCommandBuffer;
    
    protected override void OnCreate()
    {
        endSimulationCommandBuffer = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        float deltaTime = Time.DeltaTime;
        EntityCommandBuffer.ParallelWriter commandBuffer = endSimulationCommandBuffer.CreateCommandBuffer().AsParallelWriter();
        //EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        JobHandle jobHandle = Entities.WithAll<TurretMovementData>().WithNone<IdleTurret, NewTurret>().ForEach((int entityInQueryIndex, ref TurretMovementData turret) =>
         {
             // horizontal is y axis, vertical is x axis
             if (math.abs(turret.VerticalAxis.x - (turret.ElevationSpeed * deltaTime)) > turret.MaxElevation)
             {
                 turret.ElevationSpeed = -turret.ElevationSpeed;
             }
             else if (turret.VerticalAxis.x - (turret.ElevationSpeed * deltaTime) > turret.MaxDepression)
             {
                 turret.ElevationSpeed = math.abs(turret.ElevationSpeed);
             }
             turret.HorizontalAxis.y += turret.TraverseSpeed * deltaTime;
             commandBuffer.SetComponent(entityInQueryIndex, turret.HorizontalEntity, new RotationEulerXYZ { Value = math.radians(turret.HorizontalAxis) });

             turret.VerticalAxis.x -= turret.ElevationSpeed * deltaTime;
             commandBuffer.SetComponent(entityInQueryIndex, turret.VerticalEntity, new RotationEulerXYZ { Value = math.radians(turret.VerticalAxis) });
         }).Schedule(inputDeps);
        endSimulationCommandBuffer.AddJobHandleForProducer(jobHandle);
        return jobHandle;
    }
}


//[Il2CppEagerStaticClassConstruction]
public static class ExtraTurretMathsFunctions
{
    public const float kEpsilon = 0.00001F;

    public const float Rad2Deg = 57.29578F;

    public const float kEpsilonNormalSqrt = 1e-15F;

    public static readonly float3 Up = new float3(0, 1, 0);
    public static readonly float3 Foward = new float3(0, 0, 1);
    public static readonly float3 Right = new float3(1, 0, 0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 MultiplyVector(float4x4 matrix, float3 vector)
    {
        float3 res;
        res.x = matrix.c0.w* vector.x + matrix.c0.x * vector.y + matrix.c0.y * vector.z;
        res.y = matrix.c1.w * vector.x + matrix.c1.x * vector.y + matrix.c1.y * vector.z;
        res.z = matrix.c2.w * vector.x + matrix.c2 .x* vector.y + matrix.c2.y * vector.z;
        return res;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 Normalise(float3 vector)
    {
        float mag = math.sqrt(vector.x * vector.x + vector.y * vector.y + vector.z * vector.z);
        if (mag > kEpsilon)
        {
            return vector / mag;
        }
        else
        {
            return float3.zero;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 GetRight(quaternion rotation)
    {
        return math.mul(rotation, Right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 GetUp(quaternion rotation)
    {
        return math.mul(rotation, Up);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 GetForward(quaternion rotation)
    {
        return math.mul(rotation, Foward);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsEqualUsingDot(float dot)
    {
        // Returns false in the presence of NaN values.
        return dot > 1.0f - kEpsilon;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ProjectOnPlane(float3 vector, float3 planeNormal)
    {
        float sqrMag = math.dot(planeNormal, planeNormal);
        if (sqrMag < math.EPSILON)
            return vector;
        else
        {
            var dot = math.dot(vector, planeNormal);
            return new float3(vector.x - planeNormal.x * dot / sqrMag,
                vector.y - planeNormal.y * dot / sqrMag,
                vector.z - planeNormal.z * dot / sqrMag);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Angle(quaternion a, quaternion b)
    {
        float dot = math.dot(a, b);
        return !(dot > 0.999998986721039) ? (float)(math.acos(math.min(math.abs(dot), 1f)) * 2.0) : 0.0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static quaternion RotateTowards(quaternion from, quaternion to, float maxRadiansDelta)
    {
        float num = Angle(from, to);
        return num < float.Epsilon ? to : math.slerp(from, to, math.min(1f, maxRadiansDelta / num));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float MoveTowards(float current, float target, float maxDelta)
    {
        if (math.abs(target - current) <= maxDelta)
            return target;
        //return current + math.sign(target - current) * maxDelta;
        return (current > target) ? current + math.sign(target - current) * maxDelta : current - math.sign(target - current) * maxDelta;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static public float MoveTowardsABS(float current, float target, float maxDelta)
    {
        if (math.abs(target - current) <= maxDelta)
            return target;
        return current + math.sign(target - current) * maxDelta;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 MoveTowards(float3 current, float3 target, float maxDistanceDelta)
    {
        // avoid vector ops because current scripting backends are terrible at inlining
        float toVector_x = target.x - current.x;
        float toVector_y = target.y - current.y;
        float toVector_z = target.z - current.z;

        float sqdist = toVector_x * toVector_x + toVector_y * toVector_y + toVector_z * toVector_z;

        if (sqdist == 0 || (maxDistanceDelta >= 0 && sqdist <= maxDistanceDelta * maxDistanceDelta))
            return target;
        var dist = math.sqrt(sqdist);

        return new float3(current.x + toVector_x / dist * maxDistanceDelta,
            current.y + toVector_y / dist * maxDistanceDelta,
            current.z + toVector_z / dist * maxDistanceDelta);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SqrMagnitude(float3 vector) { return vector.x * vector.x + vector.y * vector.y + vector.z * vector.z; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Angle(float3 from, float3 to)
    {
        // sqrt(a) * sqrt(b) = sqrt(a * b) -- valid for real numbers
        float denominator = math.sqrt(SqrMagnitude(from) * SqrMagnitude(to));
        if (denominator < kEpsilon)
            return 0F;
        
        float dot = math.clamp(math.dot(from, to) / denominator, -1F, 1F);
        return (math.acos(dot)) * Rad2Deg;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SignedAngle(float3 from, float3 to, float3 axis)
    {
        float unsignedAngle = Angle(from, to);
        float cross_x = from.y * to.z - from.z * to.y;
        float cross_y = from.z * to.x - from.x * to.z;
        float cross_z = from.x * to.y - from.y * to.x;
        float sign = math.sign(axis.x * cross_x + axis.y * cross_y + axis.z * cross_z);
        return unsignedAngle * sign;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 ToEuler(quaternion Q)
    {
        float3 euler;
        float sinr_cosp = 2 * (Q.value.w * Q.value.x + Q.value.y * Q.value.z);
        float cosr_cosp = 1 - 2 * (Q.value.x * Q.value.x + Q.value.y * Q.value.y);
        euler.x = math.atan2(sinr_cosp, cosr_cosp);
        float sinp = 2 * (Q.value.w * Q.value.y - Q.value.z * Q.value.x);
        if (math.abs(sinp) >= 1)
        {
            euler.y = Copysign(math.PI / 2, sinp);
        }
        else
        {
            euler.y = math.asin(sinp);
        }

        float siny_cosp = 2 * (Q.value.w * Q.value.z + Q.value.x * Q.value.y);
        float cosy_cosp = 1 - 2 * (Q.value.y * Q.value.y + Q.value.z * Q.value.z);
        euler.z = math.atan2(siny_cosp, cosy_cosp);
        return euler;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Copysign(float x, float y)
    {
        if ((x < 0 && y > 0) || (x > 0 && y < 0))
            return -x;
        return x;
    }
}

[BurstCompile]
public struct FindTargetBurstJob : IJobEntityBatch
{
    [ReadOnly]
    public ComponentTypeHandle<TurretMovementData> turretMovementDataTypeHandle;
    [ReadOnly]
    public ComponentTypeHandle<LocalToWorld> turretLocalToWorldTypeHandle;
    [ReadOnly]
    public NativeArray<TargetWithPosition>.ReadOnly Targets;

    public EntityCommandBuffer.ParallelWriter entityCommandBuffer;
    public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
    {
        NativeArray<TurretMovementData> turretMovementDatas = batchInChunk.GetNativeArray(turretMovementDataTypeHandle);
        NativeArray<LocalToWorld> localToWorlds = batchInChunk.GetNativeArray(turretLocalToWorldTypeHandle);
        float3 turretPosition;
        Entity closestTargetEntity = Entity.Null;
        float3 closetTargetPosition = float3.zero;
        for (int i = 0; i < batchInChunk.Count; i++)
        {
            turretPosition = localToWorlds[i].Position;
            for (int targetIndex = 0; targetIndex < Targets.Length; targetIndex++)
            {
                TargetWithPosition targetWithPosition = Targets[targetIndex];

                if (closestTargetEntity == Entity.Null)
                {
                    closestTargetEntity = targetWithPosition.entity;
                    closetTargetPosition = targetWithPosition.position;
                }
                else
                {
                    if (math.distance(turretPosition, targetWithPosition.position) < math.distance(turretPosition, closetTargetPosition))
                    {
                        closestTargetEntity = targetWithPosition.entity;
                        closetTargetPosition = targetWithPosition.position;
                    }
                }
            }
            entityCommandBuffer.AddComponent(batchIndex, turretMovementDatas[i].Self, new TurretTarget { Entity = closestTargetEntity });
            closestTargetEntity = Entity.Null;
            closetTargetPosition = float3.zero;
        }
    }
}

[BurstCompile]
public struct TurretToTargetJob : IJobEntityBatch
{
    [ReadOnly]
    public NativeHashMap<Entity, float3> targetMap;

    [ReadOnly]
    public NativeHashMap<Entity, LocalToWorld> HorizontalSide;
    [ReadOnly]
    public NativeHashMap<Entity, LocalToWorld> VerticalSide;

    [ReadOnly]
    public ComponentTypeHandle<LocalToWorld> localToWorldTypeHandle;
    [ReadOnly]
    public ComponentTypeHandle<TurretMovementData> turretInTypeHandle;
    [ReadOnly]
    public ComponentTypeHandle<TurretTarget> turretTargetTypeHandle;

    public float deltaTime;

    public EntityCommandBuffer.ParallelWriter ecb;
    public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
    {
        NativeArray<TurretTarget> turretTargetData = batchInChunk.GetNativeArray(turretTargetTypeHandle);
        NativeArray<TurretMovementData> turretInData = batchInChunk.GetNativeArray(turretInTypeHandle);
        NativeArray<LocalToWorld> turretLocalToWorldData = batchInChunk.GetNativeArray(localToWorldTypeHandle);
        for (int i = 0; i < turretInData.Length; i++)
        {
            LocalToWorld turretLocalToWorld = turretLocalToWorldData[i];
            TurretMovementData turret = turretInData[i];
            LocalToWorld HorizontalLocalToWorld = HorizontalSide[turret.HorizontalEntity];
            LocalToWorld VerticalLocalToWorld = VerticalSide[turret.VerticalEntity];
            float3 targetPosition = targetMap[turretTargetData[i].Entity];
            float3 basePos = HorizontalLocalToWorld.Position;
            float3 VerticalPos = VerticalLocalToWorld.Position;
            //float3 HorizontalFoward = HorizontalLocalToWorld.Forward;
            float3 turretUp = turretLocalToWorld.Up;
            float3 turretForward = turretLocalToWorld.Forward;

            float3 vecToTarget = targetPosition - basePos;
            float3 flattenedVecForBase = ExtraTurretMathsFunctions.ProjectOnPlane(vecToTarget, turretUp);
            float targetTraverse = ExtraTurretMathsFunctions.SignedAngle(turretForward, flattenedVecForBase, turretUp);

            turret.limitedTraverseAngle = ExtraTurretMathsFunctions.MoveTowards(turret.limitedTraverseAngle, targetTraverse, turret.TraverseSpeed * deltaTime);

            float3 localTargetPos = math.mul(math.inverse(HorizontalLocalToWorld.Rotation), targetPosition - VerticalPos);
            float3 flattenedVecForVertical = ExtraTurretMathsFunctions.ProjectOnPlane(localTargetPos, ExtraTurretMathsFunctions.Up);
            float targetElevation = ExtraTurretMathsFunctions.Angle(flattenedVecForVertical, localTargetPos);
            targetElevation *= math.sign(localTargetPos.y);
            targetElevation = math.clamp(targetElevation, -turret.MaxDepression, turret.MaxElevation);
            turret.elevation = ExtraTurretMathsFunctions.MoveTowardsABS(turret.elevation, targetElevation, turret.ElevationSpeed * deltaTime);

            if (math.abs(turret.elevation) > math.EPSILON)
            {
                turret.VerticalAxis = ExtraTurretMathsFunctions.Right * -turret.elevation;
                ecb.SetComponent(batchIndex, turret.VerticalEntity, new Rotation { Value = quaternion.EulerXYZ(math.radians(turret.VerticalAxis)) });
            }

            if (math.abs(turret.limitedTraverseAngle) > math.EPSILON)
            {
                turret.HorizontalAxis = ExtraTurretMathsFunctions.Up * turret.limitedTraverseAngle;
                ecb.SetComponent(batchIndex, turret.HorizontalEntity, new Rotation { Value = quaternion.EulerXYZ(math.radians(turret.HorizontalAxis)) });
            }

            turret.angleToTarget = ExtraTurretMathsFunctions.Angle(targetPosition - VerticalPos, VerticalLocalToWorld.Forward);
            if (turret.angleToTarget < turret.aimedThreshold)
            {
                ecb.AddComponent<TurretIsAimed>(batchIndex, turret.Self);
            }

            ecb.SetComponent(batchIndex, turret.HorizontalEntity, HorizontalLocalToWorld);
            ecb.SetComponent(batchIndex, turret.Self, turret);
        }
    }
}