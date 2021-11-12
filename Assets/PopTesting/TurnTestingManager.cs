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

public class TurnTestingManager : MonoBehaviour
{
    private EntityManager entityManager;
    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;
    Entity TurnManagerEntity;
    EntityArchetype TurnManagerArch;
    EntityCommandBuffer commandBuffer;


    private void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<TurnManager>().OnEndTurnEvent += TTM_OnEndTurn;
        World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<TurnManager>().OnStartTurnEvent += TTM_OnStartTurn;
        endSimulationEntityCommandBufferSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        TurnManagerArch = entityManager.CreateArchetype(typeof(TurnManagerTag));
        TurnManagerEntity = entityManager.CreateEntity(TurnManagerArch);
        CreateCommandBuffer();
        commandBuffer.AddBuffer<CountryPopCentre>(TurnManagerEntity);
        commandBuffer.AddComponent(TurnManagerEntity, new TurnManagerTag { Turns = 0 });
    }

    private void TTM_OnEndTurn(int turns)
    {
        Debug.Log("End Turn Count: " + turns);
    }
    private void TTM_OnStartTurn(int turns)
    {
        Debug.Log("Start Turn Count: " + turns);
    }

    private void CreateCommandBuffer()
    {
        commandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer();
    }
    
    private void EndTurn()
    {
        CreateCommandBuffer();
        commandBuffer.AddComponent<OnEndTurnEventComponent>(TurnManagerEntity);
    }
    private void OnGUI()
    {
        if (GUI.Button(new Rect(10, 0, 120, 30), "Advance Turn Cycle"))
        {
            EndTurn();
        }
        //if (GUI.Button(new Rect(10, 35, 120, 30), "Get Surplus Modifier"))
        //{
        //    Debug.LogError("Not implemented");
        //}
        //
        //if (GUI.Button(new Rect(10, 70, 120, 30), "Update Modifers"))
        //{
        //    Debug.LogError("Not implemented");
        //}
        //
        //if (GUI.Button(new Rect(10, 105, 120, 30), "Update Count"))
        //{
        //    Debug.LogError("Not implemented");
        //}
    }
}
