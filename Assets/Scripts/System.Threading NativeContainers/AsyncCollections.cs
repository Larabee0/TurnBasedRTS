using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Entities;
using Collider = Unity.Physics.Collider;
using MeshCollider = Unity.Physics.MeshCollider;
using System;
using Unity.Collections.LowLevel.Unsafe;
    
public class AsyncCollections : MonoBehaviour
{
    [SerializeField] private Mesh bakeMeshCollider;



    NativeArray<ExampleStruct> array = new NativeArray<ExampleStruct>(3,Allocator.Persistent);

    public void Method()
    {
        Debug.Log(array[0].structValue);
        ExampleStruct value = array[0];


        value.ExtensionExample(3);

        NativeArray<int> int2Array = array.Reinterpret<int>();
        Debug.Log(int2Array[0]);
        Debug.Log(array[0]); // prints 0
        SetExample(array);
        Debug.Log(array[0]); // prints 3

        
    }

    public void SetExample(NativeArray<ExampleStruct> array)
    {
        ExampleStruct workingValue = array[0];
        workingValue.structValue = 3;
        array[0] = workingValue;
    }
}

// 8 bytes
public struct ExampleStruct
{
    public int structValue;

    public int GetValue()
    {
        return structValue;
    }
}

public static class Extenstions
{
    // extension method
    public static void ExtensionExample(ref this ExampleStruct data, int value)
    {
        // readonly reference, cannot be modified
        data.structValue = value;
    }

    // static method
    public static void SetExample(ref ExampleStruct data)
    {
        // readonly reference, cannot be modified
        data.structValue = 3;
    }

}