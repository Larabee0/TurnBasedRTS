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

public class PopGrowthModifierSystem : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;
    protected override void OnCreate()
    {
        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        CalPopGrowthModJob popGrowthRateJob = new CalPopGrowthModJob
        {
            PopGrowthPosStatModifiers = this.GetBufferTypeHandle<GrowthPositiveStaticModifier>(true),
            PopGrowthNegStatModifiers = this.GetBufferTypeHandle<GrowthNegativeStaticModifier>(true),
            PopGrowthPosMultModifiers = this.GetBufferTypeHandle<GrowthPositiveMultiplierModifier>(true),
            PopGrowthNegMultModifiers = this.GetBufferTypeHandle<GrowthNegativeMultiplierModifier>(true),
            PopInfoTypeHandle = this.GetComponentTypeHandle<PopInfo>()
        };

        EntityQuery PopQuery = GetEntityQuery(PopConstants.PopModUpdateQuery);

        JobHandle GrowthRateHandle = popGrowthRateJob.ScheduleParallel(PopQuery, 1, inputDeps);
        endSimulationEntityCommandBufferSystem.CreateCommandBuffer().RemoveComponentForEntityQuery(PopQuery, typeof(CalPopModsTag));
        return GrowthRateHandle;
    }
}
[BurstCompile]
public struct CalPopGrowthModJob : IJobEntityBatch
{
    [ReadOnly]
    public BufferTypeHandle<GrowthPositiveStaticModifier> PopGrowthPosStatModifiers;
    [ReadOnly]
    public BufferTypeHandle<GrowthNegativeStaticModifier> PopGrowthNegStatModifiers;
    [ReadOnly]
    public BufferTypeHandle<GrowthPositiveMultiplierModifier> PopGrowthPosMultModifiers;
    [ReadOnly]
    public BufferTypeHandle<GrowthNegativeMultiplierModifier> PopGrowthNegMultModifiers;

    public ComponentTypeHandle<PopInfo> PopInfoTypeHandle;
    public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
    {
        BufferAccessor<GrowthPositiveStaticModifier> PosStatAccessor = batchInChunk.GetBufferAccessor(PopGrowthPosStatModifiers);
        BufferAccessor<GrowthNegativeStaticModifier> NegStatAccessor = batchInChunk.GetBufferAccessor(PopGrowthNegStatModifiers);
        BufferAccessor<GrowthPositiveMultiplierModifier> PosMultAccessor = batchInChunk.GetBufferAccessor(PopGrowthPosMultModifiers);
        BufferAccessor<GrowthNegativeMultiplierModifier> NegMultAccessor = batchInChunk.GetBufferAccessor(PopGrowthNegMultModifiers);
        NativeArray<PopInfo> PopInfos = batchInChunk.GetNativeArray(PopInfoTypeHandle);

        for (int bICIndex = 0; bICIndex < batchInChunk.Count; bICIndex++) // bICIndex - batchInChunkIndex
        {
            DynamicBuffer<GrowthPositiveStaticModifier> CurrentPosStatModBuffer = PosStatAccessor[bICIndex];
            DynamicBuffer<GrowthNegativeStaticModifier> CurrentNegStatModBuffer = NegStatAccessor[bICIndex];
            DynamicBuffer<GrowthPositiveMultiplierModifier> CurrentPosMultModBuffer = PosMultAccessor[bICIndex];
            DynamicBuffer<GrowthNegativeMultiplierModifier> CurrentNegMultModBuffer = NegMultAccessor[bICIndex];
            int CPSMBLength = CurrentPosStatModBuffer.Length;
            int CNSMBLength = CurrentNegStatModBuffer.Length;
            int CPMMBLength = CurrentPosMultModBuffer.Length;
            int CNMMBLength = CurrentNegMultModBuffer.Length;
            float PositiveStaticModifiers = 0;
            float NegativeStaticModifiers = 0;

            for (int i = 0; i < CPSMBLength; i++)
            {
                PositiveStaticModifiers += CurrentPosStatModBuffer[i].Modifier;
            }
            for (int i = 0; i < CNSMBLength; i++)
            {
                NegativeStaticModifiers += CurrentNegStatModBuffer[i].Modifier;
            }

            PopInfo CurrentInfo = PopInfos[bICIndex];
            float GrowthRate = CurrentInfo.CountryPopInfo.BaseGrowthRate + PositiveStaticModifiers;
            float multipliedGrowthRate = GrowthRate;

            for (int i = 0; i < CPMMBLength; i++)
            {
                multipliedGrowthRate += GrowthRate * CurrentPosMultModBuffer[i].Modifier;
            }
            for (int i = 0; i < CNMMBLength; i++)
            {
                multipliedGrowthRate += GrowthRate * CurrentNegMultModBuffer[i].Modifier;
            }

            multipliedGrowthRate += NegativeStaticModifiers;

            if (CurrentInfo.GrowthRate != multipliedGrowthRate)
            {
                CurrentInfo.GrowthRate = multipliedGrowthRate;
                if (CurrentInfo.GrowthRateLastTick < multipliedGrowthRate)
                {
                    CurrentInfo.GrowthRateChange = RateChange.Up;
                }
                else
                {
                    CurrentInfo.GrowthRateChange = RateChange.Down;
                }
            }
            else
            {
                CurrentInfo.GrowthRateChange = RateChange.None;
            }

            PopInfos[bICIndex] = CurrentInfo;
        }
    }
}
[BurstCompile]
public struct GrowPopsJob : IJobEntityBatch
{
    public ComponentTypeHandle<PopInfo> PopInfoTypeHandle;

    public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
    {
        NativeArray<PopInfo> PopInfos = batchInChunk.GetNativeArray(PopInfoTypeHandle);

        for (int bICIndex = 0; bICIndex < batchInChunk.Count; bICIndex++) // bICIndex - batchInChunkIndex
        {
            PopInfo CurrentInfo = PopInfos[bICIndex];
            CurrentInfo.GrowthProgress += CurrentInfo.GrowthRateLastTick = CurrentInfo.GrowthRate;            
            while (CurrentInfo.GrowthProgress >= CurrentInfo.CountryPopInfo.ProgressLimit)
            {
                CurrentInfo.GrowthProgress -= CurrentInfo.CountryPopInfo.ProgressLimit;
                if(CurrentInfo.GrowthProgress < 0)
                {
                    CurrentInfo.GrowthProgress = 0;
                }
                CurrentInfo.Count++;
            }
            PopInfos[bICIndex] = CurrentInfo;
        }
    }
}

[BurstCompile]
public struct CalTotalPosForCountry : IJobEntityBatch
{
    [ReadOnly]
    public ComponentTypeHandle<PopInfo> PopInfoTypeHandle;
    [ReadOnly]
    public NativeArray<Entity> Countries;
    [ReadOnly]
    public EntityTypeHandle PopInfoEntityTypeHandle;

    public NativeHashMap<Entity,int> TotalPopsForCountry;

    public EntityCommandBuffer.ParallelWriter entityCommandBuffer;

    public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
    {
        NativeArray<PopInfo> PopInfos = batchInChunk.GetNativeArray(PopInfoTypeHandle);
        NativeArray<Entity> PopInfoEntities = batchInChunk.GetNativeArray(PopInfoEntityTypeHandle);
        for (int bICIndex = 0; bICIndex < batchInChunk.Count; bICIndex++) // bICIndex - batchInChunkIndex
        {
            TotalPopsForCountry[PopInfos[bICIndex].Country] += PopInfos[bICIndex].Count;
            entityCommandBuffer.RemoveComponent<GrowPopsTag>(batchIndex, PopInfoEntities[bICIndex]);
        }
        for (int i = 0; i < Countries.Length; i++)
        {
            entityCommandBuffer.AddComponent(batchIndex, Countries[i], new TempCountryTotalPop() { Count = TotalPopsForCountry[Countries[i]] });
        }
    }
}

[BurstCompile]
public struct SetTotalPopsForCountries : IJobEntityBatch
{

    [ReadOnly]
    public ComponentTypeHandle<TempCountryTotalPop> TempCountryTotalPopTypeHandle;
    [ReadOnly]
    public EntityTypeHandle PopInfoEntityTypeHandle;
    public ComponentTypeHandle<CountryPopInfo> CountryPopInfoTypeHandle;
    public EntityCommandBuffer.ParallelWriter entityCommandBuffer;
    public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
    {
        NativeArray<TempCountryTotalPop> TempPopInfos = batchInChunk.GetNativeArray(TempCountryTotalPopTypeHandle);
        NativeArray<CountryPopInfo> PopInfos = batchInChunk.GetNativeArray(CountryPopInfoTypeHandle);
        NativeArray<Entity> CountryEntities = batchInChunk.GetNativeArray(PopInfoEntityTypeHandle);
        for (int bICIndex = 0; bICIndex < batchInChunk.Count; bICIndex++) // bICIndex - batchInChunkIndex
        {
            CountryPopInfo PopInfo = PopInfos[bICIndex];
            PopInfo.TotalPops = TempPopInfos[bICIndex].Count;
            PopInfos[bICIndex] = PopInfo;
            entityCommandBuffer.RemoveComponent<TempCountryTotalPop>(batchIndex, CountryEntities[bICIndex]);
        }
    }
}

public class SetPopCountForCountry : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;

    protected override void OnCreate()
    {
        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        SetTotalPopsForCountries SetPopsJob = new SetTotalPopsForCountries
        {
            entityCommandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter(),
            TempCountryTotalPopTypeHandle = this.GetComponentTypeHandle<TempCountryTotalPop>(true),
            CountryPopInfoTypeHandle = this.GetComponentTypeHandle<CountryPopInfo>(),
            PopInfoEntityTypeHandle = this.GetEntityTypeHandle(),

        };
        EntityQuery PopQuery = GetEntityQuery(PopConstants.CountryUpdatePopCountQuery);
        JobHandle SetPopsJobHandle = SetPopsJob.ScheduleParallel(PopQuery, 1, inputDeps);
        endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(SetPopsJobHandle);
        return SetPopsJobHandle;
    }
}



public class GrowPopsSystem : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;
    
    protected override void OnCreate()
    {
        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        PopConstants.CreatePopQueries();
    }
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {

        CalPopGrowthModJob popGrowthRateJob = new CalPopGrowthModJob
        {
            PopGrowthPosStatModifiers = this.GetBufferTypeHandle<GrowthPositiveStaticModifier>(true),
            PopGrowthNegStatModifiers = this.GetBufferTypeHandle<GrowthNegativeStaticModifier>(true),
            PopGrowthPosMultModifiers = this.GetBufferTypeHandle<GrowthPositiveMultiplierModifier>(true),
            PopGrowthNegMultModifiers = this.GetBufferTypeHandle<GrowthNegativeMultiplierModifier>(true),
            PopInfoTypeHandle = this.GetComponentTypeHandle<PopInfo>()
        };

        EntityQuery PopQuery = GetEntityQuery(PopConstants.PopCountUpdateQuery);
        JobHandle GrowthRateHandle = popGrowthRateJob.ScheduleParallel(PopQuery, 1, inputDeps);
        //JobHandle GrowthRateHandle = popGrowthRateJob.Schedule(PopQuery, inputDeps);
        GrowPopsJob growPopsJob = new GrowPopsJob
        {
            PopInfoTypeHandle = this.GetComponentTypeHandle<PopInfo>()
        };

        JobHandle PopCountHandle = growPopsJob.ScheduleParallel(PopQuery, 1, GrowthRateHandle);
        EntityCommandBuffer.ParallelWriter CommandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();

        EntityQuery CountryQuery = GetEntityQuery(PopConstants.CountryQuery);
        NativeArray<Entity> Countries = CountryQuery.ToEntityArray(Allocator.TempJob);
        NativeHashMap<Entity,int> TempPopsForCountry = new NativeHashMap<Entity, int>(Countries.Length, Allocator.TempJob);
        for (int i = 0; i < Countries.Length; i++)
        {
            TempPopsForCountry.Add(Countries[i], 0);
        }
        CalTotalPosForCountry totalPopJob = new CalTotalPosForCountry
        {
            PopInfoTypeHandle = this.GetComponentTypeHandle<PopInfo>(true),
            Countries = Countries,
            TotalPopsForCountry = TempPopsForCountry,
            PopInfoEntityTypeHandle = this.GetEntityTypeHandle(),
            entityCommandBuffer = CommandBuffer
        };
        
        JobHandle PopTotalCountHandle = totalPopJob.Schedule(PopQuery,PopCountHandle);
        JobHandle DisposalHandle = Countries.Dispose(TempPopsForCountry.Dispose(PopTotalCountHandle));
        //JobHandle PopCountHandle = growPopsJob.Schedule(PopQuery, GrowthRateHandle);
        endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(DisposalHandle);
        return DisposalHandle;
    }
}