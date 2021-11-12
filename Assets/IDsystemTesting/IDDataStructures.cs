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

/// ID def contains component id, and type
/// type could be weapon infomation
/// resource information
/// population information
/// job information
/// 
public struct ThingDef
{
    public int ID;
    public ThingType Type;

    public static ThingDef Null { get; }
}

public struct WeaponDef
{
    public ThingDef Us;
}

public struct PopDef
{
    public ThingDef Us;
}

public struct ResourceDef
{
    public ThingDef Us;
    public string Name;

}

public struct JobDef
{
    public ThingDef Us;
    public int JobCategoryID;
    public int Employs;
    public int ProducedResID;
    public int ConsumedResID;
    public int ProductionPerPop;
    public int ConsumptionPerPop;
}

public struct JobCategoryDef
{
    public ThingDef Us;
}

public enum ThingType
{
    Weapon,
    Pop,
    Resource,
    Job,
    JobCategory,
    NONE
}