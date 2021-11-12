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

public class IDMono : MonoBehaviour
{
    public int totalKeys = 1000;
    public NativeHashMap<int, ThingDef> ThingDefs ;
    public NativeQueue<int> FreeIDs = new NativeQueue<int>(Allocator.Persistent);

    public NativeHashMap<int, WeaponDef> WeaponDefs;
    public NativeHashMap<int, PopDef> PopDefs;
    public NativeHashMap<int, ResourceDef> ResourceDefs;
    public NativeHashMap<int, JobDef> JobDefs;
    public NativeHashMap<int, JobCategoryDef> JobCategoryDefs;
    
    // Start is called before the first frame update
    private void Start()
    {
        ThingDefs = new NativeHashMap<int, ThingDef>(totalKeys, Allocator.Persistent);
        //Entity temp = Entity.Null;
        PopDefs = new NativeHashMap<int, PopDef>(totalKeys / 5, Allocator.Persistent);
        ResourceDefs = new NativeHashMap<int, ResourceDef>(totalKeys / 5, Allocator.Persistent);
        JobDefs = new NativeHashMap<int, JobDef>(totalKeys / 5, Allocator.Persistent);
        JobCategoryDefs = new NativeHashMap<int, JobCategoryDef>(totalKeys / 5, Allocator.Persistent);

        for (int i = 0; i < totalKeys; i++)
        {
            ThingDefs.Add(i, ThingDef.Null);
            FreeIDs.Enqueue(i);
        }
    }

    private void RemoveThing(int ID)
    {
        ThingDefs[ID]= ThingDef.Null;
        FreeIDs.Enqueue(ID);
        
    }

    /// <summary>
    /// Adds a thing to the master Map
    /// </summary>
    /// <param name="Thing"> takes thing def</param>
    /// <returns> ID the thing is assigned </returns>
    private int AddThing(ThingDef Thing)
    {
        if(FreeIDs.IsEmpty())
        {
            totalKeys++;
            Thing.ID = totalKeys;
            ThingDefs.Add(totalKeys, Thing);
            return totalKeys;
        }
        Thing.ID = FreeIDs.Dequeue();
        ThingDefs[Thing.ID] = Thing;
        return Thing.ID;
    }

    // Update is called once per frame
    private void Update()
    {
        
    }
    private void OnDestroy()
    {
        ThingDefs.Dispose();
        FreeIDs.Dispose();
        WeaponDefs.Dispose();
        PopDefs.Dispose();
        ResourceDefs.Dispose();
        JobDefs.Dispose();
        JobCategoryDefs.Dispose();
    }
}
