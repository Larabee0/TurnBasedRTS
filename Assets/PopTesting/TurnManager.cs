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
using Unity.Burst;
using System;
// https://youtu.be/fkJ-7pqnRGo?t=1224
public class TurnManager : ComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;
    private EntityCommandBuffer commandBuffer;
    public event OnEndTurnEventDelegate OnEndTurnEvent;
    public delegate void OnEndTurnEventDelegate(int TurnCount);
    public event OnStartTurnEventEventDelegate OnStartTurnEvent;
    public delegate void OnStartTurnEventEventDelegate(int TurnCount);
    protected override void OnCreate()
    {
        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        commandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer();
        Entities.WithAll(typeof(OnEndTurnEventComponent),typeof(TurnManagerTag)).ForEach((Entity entity, ref TurnManagerTag turnManagerTag) =>
        {
            commandBuffer.RemoveComponent(entity, typeof(OnEndTurnEventComponent));
            OnEndTurnEvent?.Invoke(turnManagerTag.Turns);
            commandBuffer.AddComponent(entity, typeof(OnStartTurnEventComponent));
        });
        Entities.WithAll(typeof(OnStartTurnEventComponent), typeof(TurnManagerTag)).ForEach((Entity entity, ref TurnManagerTag turnManagerTag) =>
        {
            commandBuffer.RemoveComponent(entity, typeof(OnStartTurnEventComponent));
            turnManagerTag.Turns++;
            OnStartTurnEvent?.Invoke(turnManagerTag.Turns);
        });
    }
}
