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

public struct CountryPopInfo : IComponentData
{
    public Entity Country;
    public int TotalPops;
    public float ProgressLimit;
    public int BaseGrowthRate;
}

public struct TempCountryTotalPop : IComponentData
{
    public int Count;
}

public struct CountryTag : IComponentData { }

public struct CountryPopCentre : IBufferElementData
{
    public Entity Centre;
}

public struct PopInfo : IComponentData 
{
    public Entity Country;
    public CountryPopInfo CountryPopInfo;
    public int Count;
    public float GrowthProgress;
    public float GrowthRateLastTick;
    public float GrowthRate;
    public RateChange GrowthRateChange;
}

public struct CentreJobBases : IComponentData
{
    public float GoldBase;
    public float FoodBase;
    public float ScienceBase;
    public float CultureBase;
    public float MetalBase;
    public float GasBase;
    public float DarkMatterBase;

    public float UpKeepGoldBase;
    public float UpKeepFoodBase;
    public float UpKeepScienceBase;
    public float UpKeepCultureBase;
    public float UpKeepMetalBase;
    public float UpKeepGasBase;
    public float UpKeepDarkMatterBase;
}

public struct Building : IBufferElementData
{
    public JobCategory jobCategory;
    public Resource resourceProduced;
    public float baseProductionRate;
    public int Jobs;
    public float upKeep;

}


public struct JobModifiers : IBufferElementData
{
    public JobCategory jobCategory;
    public Resource resourceProduced;
    public AppliesTo AppliesTo;
    public bool UpKeepModifier;
    public float multiplier;
}

public struct Stockpile : IBufferElementData
{
    public float Stored;
    public float ProductionPerTick;
    public float ProductionLastTick;
    public RateChange ProductionRateChange;
    public Resource Type;
    public StockpileSettings DisplaySettings;
}

public struct GrowthPositiveStaticModifier : IBufferElementData
{
    public float Modifier;
    public ModifierType ModifierType;
}

public struct GrowthNegativeStaticModifier : IBufferElementData
{
    public float Modifier;
    public ModifierType ModifierType;
}

public struct GrowthPositiveMultiplierModifier : IBufferElementData
{
    public float Modifier;
    public ModifierType ModifierType;
}

public struct GrowthNegativeMultiplierModifier : IBufferElementData
{
    public float Modifier;
    public ModifierType ModifierType;
}

public struct GrowPopsTag : IComponentData { }

public struct CalPopModsTag : IComponentData { }
