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

public class PopTestingManager : MonoBehaviour
{
    private EntityManager entityManager;
    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;
    private EntityCommandBuffer entityCommandBuffer;
    private List<Entity> SpawnedCentres = new List<Entity>();
    [SerializeField] int SpawnedCentresCount = 0;
    EntityArchetype CountryArch;
    EntityArchetype CentreArch;
    // Start is called before the first frame update
    private void Start()
    {
        PopConstants.CreatePopQueries();
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        endSimulationEntityCommandBufferSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        CountryArch = entityManager.CreateArchetype(typeof(CountryTag), typeof(CountryPopInfo));
        CentreArch = entityManager.CreateArchetype(typeof(PopInfo),typeof(GrowPopsTag));
        this.enabled = false;
    }

    float Net = 0;
    float consumed = 0;
    // Update is called once per frame
    private void Update()
    {
        SpawnedCentresCount = SpawnedCentres.Count;
        Net =UnityEngine.Random.Range(-50f, 50f);
        consumed = UnityEngine.Random.Range(-50f, -0.001f);
    }

    private void OnGUI()
    {
        if (GUI.Button(new Rect(10, 0, 120, 30), "Create Centre"))
        {
            CreateCentre();
        }
        if (GUI.Button(new Rect(10, 35, 120, 30), "Get Surplus Modifier"))
        {
            GetSurplusModifier();
        }

        if (GUI.Button(new Rect(10, 70, 120, 30), "Update Modifers"))
        {
            UpdateModifers();
        }

        if (GUI.Button(new Rect(10, 105, 120, 30), "Update Count"))
        {
            UpdateCount();
        }
    }

    private void GetSurplusModifier()
    {
        float Mod = SurplusModifiers.GetProductsLerped(consumed, Net);
        float3 Producer = SurplusModifiers.GetProducts(consumed, Net);

        Debug.Log("LerpMin: " + Producer.x + " | LerpMax: " + Producer.y + " | %: " + Producer.z);
        Debug.Log("Modifier: " + Mod + " | Consumed: " + consumed + " | Net: " + Net);
        Debug.Log("");
    }

    private void UpdateModifers()
    {
        EntityQuery PopQuery = entityManager.CreateEntityQuery(PopConstants.PopInfoQuery);
        endSimulationEntityCommandBufferSystem.CreateCommandBuffer().AddComponent(PopQuery, typeof(CalPopModsTag));
    }
    private void UpdateCount()
    {
        EntityQuery PopQuery = entityManager.CreateEntityQuery(PopConstants.PopInfoQuery);
        endSimulationEntityCommandBufferSystem.CreateCommandBuffer().AddComponent(PopQuery, typeof(GrowPopsTag));
    }

    private void AddRandomGrowthModifier()
    {
        AddRandomGrowthModifier(SpawnedCentres[UnityEngine.Random.Range(0, SpawnedCentres.Count)]);
    }

    private void AddRandomGrowthModifier(Entity Centre)
    {
        DynamicBuffer<GrowthPositiveStaticModifier> PosStatModifiers = entityManager.GetBuffer<GrowthPositiveStaticModifier>(Centre);
        DynamicBuffer<GrowthNegativeStaticModifier> NegStatModifiers = entityManager.GetBuffer<GrowthNegativeStaticModifier>(Centre);
        DynamicBuffer<GrowthPositiveMultiplierModifier> PosMultModifiers = entityManager.GetBuffer<GrowthPositiveMultiplierModifier>(Centre);
        DynamicBuffer<GrowthNegativeMultiplierModifier> NegMultModifiers = entityManager.GetBuffer<GrowthNegativeMultiplierModifier>(Centre);
        ModifierType ModifierType;
        float modifier = 0;
        if (UnityEngine.Random.Range(0, 2) == 0)
        {
            ModifierType = ModifierType.Static;
            while (modifier == 0)
            {
                modifier = UnityEngine.Random.Range(-5f, 5f);
            }
        }
        else
        {
            ModifierType = ModifierType.Multiplier;
            while (modifier == 0)
            {
                int WholeNumber = UnityEngine.Random.Range(0, 2);
                float decimial = UnityEngine.Random.Range(0f, 50f) / 100;
                modifier = decimial + WholeNumber;
            }
        }

        if(ModifierType == ModifierType.Static)
        {
            if(modifier > 0)
            {
                PosStatModifiers.Add(new GrowthPositiveStaticModifier
                {
                    Modifier = modifier,
                    ModifierType = ModifierType
                });
            }
            else
            {
                NegStatModifiers.Add(new GrowthNegativeStaticModifier
                {
                    Modifier = modifier,
                    ModifierType = ModifierType
                });
            }
        }
        else
        {
            if (modifier > 1)
            {
                PosMultModifiers.Add(new GrowthPositiveMultiplierModifier
                {
                    Modifier = modifier,
                    ModifierType = ModifierType
                });
            }
            else
            {
                NegMultModifiers.Add(new GrowthNegativeMultiplierModifier
                {
                    Modifier = modifier,
                    ModifierType = ModifierType
                });
            }
        }
    }

    private void CreateCentre()
    {

        Entity CountryMain = entityManager.CreateEntity(CountryArch);
        CountryPopInfo countryPopInfo = new CountryPopInfo()
        {
            Country = CountryMain,
            ProgressLimit = PopConstants.ProgressLimit,
            BaseGrowthRate = PopConstants.BaseGrowthRate,
        };
        entityManager.AddBuffer<CountryPopCentre>(CountryMain);
        entityManager.SetComponentData(CountryMain, countryPopInfo);
        for (int i = 0; i < 10000; i++)
        {
            Entity CentreTemp = entityManager.CreateEntity(CentreArch);
            DynamicBuffer<CountryPopCentre> CentreBuffer = entityManager.GetBuffer<CountryPopCentre>(CountryMain);
            CentreBuffer.Add(new CountryPopCentre { Centre = CentreTemp });
            entityManager.AddBuffer<GrowthPositiveStaticModifier>(CentreTemp);
            entityManager.AddBuffer<GrowthNegativeStaticModifier>(CentreTemp);
            entityManager.AddBuffer<GrowthPositiveMultiplierModifier>(CentreTemp);
            entityManager.AddBuffer<GrowthNegativeMultiplierModifier>(CentreTemp);
            entityManager.SetComponentData(CentreTemp, new PopInfo()
            {
                Country = CountryMain,
                CountryPopInfo = countryPopInfo,
                Count = 1,
                GrowthProgress = 0,
                GrowthRateLastTick = PopConstants.BaseGrowthRate,
                GrowthRate = PopConstants.BaseGrowthRate,
                GrowthRateChange = RateChange.Up
            });
            SpawnedCentres.Add(CentreTemp);
            for (int k = 0; k < 100; k++)
            {
                AddRandomGrowthModifier(CentreTemp);
            }
        }
    }
}
