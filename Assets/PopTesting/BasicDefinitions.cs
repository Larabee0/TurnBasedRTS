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

public struct TurnManagerTag : IComponentData 
{
    public int Turns;
}

public struct TurnManagerStarted : IComponentData { }
public struct OnEndTurnEventComponent : IComponentData { }
public struct OnStartTurnEventComponent : IComponentData { }

public struct CountryContainer : IBufferElementData
{
    public Entity Country;
}

public struct CentreContainer : IBufferElementData
{
    public Entity Centre;
}

public struct PopConstants
{
    public static EntityQueryDesc PopInfoQuery;
    public static EntityQueryDesc PopModUpdateQuery;
    public static EntityQueryDesc PopCountUpdateQuery;
    public static EntityQueryDesc CountryQuery;
    public static EntityQueryDesc CountryUpdatePopCountQuery;
    public static float ProgressLimit = 200f;
    public static int BaseGrowthRate = 1;

    public static void CreatePopQueries()
    {
        PopConstants.PopInfoQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(PopInfo),
            }

        };
        PopConstants.PopModUpdateQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(PopInfo),
                typeof(CalPopModsTag)
            }

        };
        PopConstants.PopCountUpdateQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(PopInfo),
                typeof(GrowPopsTag)
            }

        };
        PopConstants.CountryQuery = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(CountryTag) }
        };
        PopConstants.CountryUpdatePopCountQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                typeof(CountryTag),
                typeof(TempCountryTotalPop),
                typeof(CountryPopInfo)
            }

        };
    }
}

public struct SurplusModifiers
{
    public static readonly float2 Negative200 = new float2 { x = -2f, y = -2f };             //-200%
    public static readonly float2 Negative175 = new float2 { x = -1.75f, y = -1.75f };       //-175%
    public static readonly float2 Negative150 = new float2 { x = -1.5f, y = -1.5f };         //-150%
    public static readonly float2 Negative125 = new float2 { x = -1.25f, y = -1.25f };       //-125%
    public static readonly float2 Negative100 = new float2 { x = -1f, y = -1f };             //-100%
    public static readonly float2 Negative75 = new float2 { x = -0.75f, y = -0.75f };       //-075%
    public static readonly float2 Negative50 = new float2 { x = -0.5f, y = -0.5f };         //-050%
    public static readonly float2 Negative25 = new float2 { x = -0.25f, y = -0.25f };       //-025%
    //public static readonly float2        Zero = new float2 { x = 0, y = 0 };                 //+000%
    public static readonly float2 Positive25 = new float2 { x = 0.25f, y = 1f };            //+025%
    public static readonly float2 Positive50 = new float2 { x = 0.5f, y = 1.25f };          //+050%
    public static readonly float2 Positive75 = new float2 { x = 0.75f, y = 1.5f };          //+075%
    public static readonly float2 Positive100 = new float2 { x = 1f, y = 1.75f };            //+100%
    public static readonly float2 Positive125 = new float2 { x = 1.25f, y = 2f };            //+125%
    public static readonly float2 Positive150 = new float2 { x = 1.5f, y = 2.25f };          //+150%
    public static readonly float2 Positive175 = new float2 { x = 1.75f, y = 2.5f };          //+175%
    public static readonly float2 Positive200 = new float2 { x = 2f, y = 2.75f };            //+200%

    /// <summary>
    /// 
    /// </summary>
    /// <param name="SurplusMult">mult the net food is over the consumed food</param>
    /// <returns></returns>
    public static float3 GetProducts(float SurplusMult)
    {
        Debug.Log("SurplusMult: " + SurplusMult);
        float3 Value;
        if (SurplusMult >= 0)
        {
            if (SurplusMult < Positive25.x)
            {
                Value = new float3 { x = 0, y = Positive25.y };
            }
            else if (SurplusMult < Positive50.x)
            {
                Value = new float3 { x = Positive25.y, y = Positive50.y };
            }
            else if (SurplusMult < Positive75.x)
            {
                Value = new float3 { x = Positive50.y, y = Positive75.y };
            }
            else if (SurplusMult < Positive100.x)
            {
                Value = new float3 { x = Positive75.y, y = Positive100.y };
            }
            else if (SurplusMult < Positive125.x)
            {
                Value = new float3 { x = Positive100.y, y = Positive125.y };
            }
            else if (SurplusMult < Positive150.x)
            {
                Value = new float3 { x = Positive125.y, y = Positive150.y };
            }
            else if (SurplusMult < Positive175.x)
            {
                Value = new float3 { x = Positive150.y, y = Positive175.y };
            }
            else if (SurplusMult < Positive200.x)
            {
                Value = new float3 { x = Positive175.y, y = Positive200.y };
            }
            else
            {
                Value = new float3 { x = Positive200.y, y = Positive200.y };
            }
        }
        else
        {
            if (SurplusMult > Negative25.x)
            {
                Value = new float3 { x = 0, y = Negative25.y };
            }
            else if (SurplusMult > Negative50.x)
            {
                Value = new float3 { x = Negative25.y, y = Negative50.y };
            }
            else if (SurplusMult > Negative75.x)
            {
                Value = new float3 { x = Negative50.y, y = Negative75.y };
            }
            else if (SurplusMult > Negative100.x)
            {
                Value = new float3 { x = Negative75.y, y = Negative100.y };
            }
            else if (SurplusMult > Negative125.x)
            {
                Value = new float3 { x = Negative100.y, y = Negative125.y };
            }
            else if (SurplusMult > Negative150.x)
            {
                Value = new float3 { x = Negative125.y, y = Negative150.y };
            }
            else if (SurplusMult > Negative175.x)
            {
                Value = new float3 { x = Negative150.y, y = Negative175.y };
            }
            else if (SurplusMult > Negative200.x)
            {
                Value = new float3 { x = Negative175.y, y = Negative200.y };
            }
            else
            {
                Value = new float3 { x = Negative200.y, y = Negative200.y };
            }
        }
        return Value;
    }

    public static float3 GetProducts(float Consumed, float Net)
    {
        Debug.Log("Net input: " + Net);
        if (Net >= 0)// we are not losing food.
        {
            float input = Net / math.abs(Consumed);
            float3 Value = GetProducts(input);
            Value.z = input;
            return Value;
        }
        else
        {
            float3 Value = GetProducts(math.abs(Net) / Consumed);
            Value.z = math.abs(Consumed) / math.abs(Net);
            return Value;
        }
    }
    public static float GetProductsLerped(float Consumed, float Net)
    {
        float3 LerpXYS = GetProducts(Consumed, Net);
        if (LerpXYS.x == LerpXYS.y)
        {
            return LerpXYS.x;
        }
        return math.lerp(LerpXYS.x, LerpXYS.y, LerpXYS.z);

    }
}
public struct BaseJobProductions
{
    public const float GoldBase = 1;
    public const float FoodBase = 1;
    public const float ScienceBase = 1;
    public const float CultureBase = 1;
    public const float MetalBase = 1;
    public const float GasBase = 1;
    public const float DarkMatterBase = 1;

    public const float UpKeepGoldBase = 1;
    public const float UpKeepFoodBase = 1;
    public const float UpKeepScienceBase = 1;
    public const float UpKeepCultureBase = 1;
    public const float UpKeepMetalBase = 1;
    public const float UpKeepGasBase = 1;
    public const float UpKeepDarkMatterBase = 1;
}

public enum ModifierType
{
    Static,
    Multiplier
}

public enum RateChange
{
    None,
    Up,
    Down
}

public enum JobCategory
{
    Agri,
    Industrial,
    Scientific,
    Cultural
}

public enum Resource
{
    Gold,
    Food,
    Science,
    Culture,
    Metal,
    Gas,
    DarkMatter
}

public enum StockpileSettings
{
    FullValues,
    StoredOnly,
    ProductionOnly
}

public enum AppliesTo
{
    jobCategory,
    jobResource,
    both
}
