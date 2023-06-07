using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;

public struct HexCellPriorityQueue : INativeDisposable
{
    public static HexCellPriorityQueue Null { get; set; }
    private NativeList<int> list;
    public NativeArray<HexCellQueueElement> elements;
    private int count;
    private int minimum;
    public int searchPhase;
    private Allocator allocatedWith;
    public Allocator AllocatedWith => allocatedWith;

    public int Count => count;

    public HexCellPriorityQueue(NativeArray<HexCellQueueElement> cellsIn, Allocator allocator = Allocator.Temp)
    {
        elements = cellsIn;
        list = new NativeList<int>(cellsIn.Length, allocator);
        count = 0;
        minimum = int.MaxValue;
        searchPhase = 0;
        allocatedWith = allocator;
    }

    public void Reallocate(Allocator As)
    {
        NativeArray<int> temp = list.ToArray(Allocator.Temp);
        NativeArray<HexCellQueueElement> elementsTemp = new(elements, Allocator.Temp);
        list.Dispose();
        elements.Dispose();
        list = new NativeList<int>(temp.Length, As);
        list.CopyFrom(temp);
        elements = new NativeArray<HexCellQueueElement>(elementsTemp, As);
        temp.Dispose();
        elementsTemp.Dispose();
        allocatedWith = As;
    }

    public void SetElements(NativeArray<HexCellQueueElement> cellsIn, Allocator allocator = Allocator.Temp)
    {
        try
        {
            elements.Dispose();
        }
        catch { }
        try
        {
            list.Dispose();
        }
        catch { }
        elements = cellsIn;
        list = new NativeList<int>(cellsIn.Length, allocator);
    }

    public void Enqueue(HexCellQueueElement cell)
    {
        count += 1;
        int priority = cell.SearchPriority;
        if (priority < minimum)
        {
            minimum = priority;
        }
        if (priority > list.Capacity)
        {
            list.Capacity = priority + 1;
        }
        while (priority >= list.Length)
        {
            list.Add(int.MinValue);
        }

        cell.NextWithSamePriority = list[priority];
        elements[cell.cellIndex] = cell;
        list[priority] = cell.cellIndex;
    }

    public int DequeueIndex()
    {
        count -= 1;
        for (; minimum < list.Length; minimum++)
        {
            int potentialCell = list[minimum];
            if (potentialCell != int.MinValue)
            {
                list[minimum] = elements[potentialCell].NextWithSamePriority;
                return potentialCell;
            }
        }
        return int.MinValue;
    }

    public HexCellQueueElement DequeueElement()
    {
        count -= 1;
        for (; minimum < list.Length; minimum++)
        {
            int potentialCell = list[minimum];
            if (potentialCell != int.MinValue)
            {
                list[minimum] = elements[potentialCell].NextWithSamePriority;
                return elements[potentialCell];
            }
        }
        return HexCellQueueElement.Null;
    }

    public void Change(HexCellQueueElement cell, int oldPriority)
    {
        elements[cell.cellIndex] = cell;
        int current = list[oldPriority];
        int next = elements[current].NextWithSamePriority;

        if (current == cell.cellIndex)
        {
            list[oldPriority] = next;
        }
        else
        {
            while (next != cell.cellIndex)
            {
                current = next;
                next = elements[current].NextWithSamePriority;
            }
            HexCellQueueElement currentElement = elements[current];
            currentElement.NextWithSamePriority = cell.NextWithSamePriority;
            elements[current] = currentElement;
            Enqueue(cell);
            count -= 1;
        }
    }

    public void Clear()
    {
        list.Clear();
        count = 0;
        minimum = int.MaxValue;
    }

    public JobHandle Dispose(JobHandle inputDeps)
    {
        return elements.Dispose(list.Dispose(inputDeps));
    }

    public void Dispose()
    {
        elements.Dispose();
        list.Dispose();
    }
}

public struct HexCellQueueElement : IBufferElementData
{
    public static readonly HexCellQueueElement Null = new() { cellIndex = int.MinValue, NextWithSamePriority = int.MinValue, SearchPhase = int.MinValue, PathFrom = int.MinValue };
    public int cellIndex;
    public int SearchPriority => Distance + SearchHeuristic;
    public int NextWithSamePriority;
    public int SearchPhase;
    public int Distance;
    public int SearchHeuristic;
    public int PathFrom;

    public static bool operator ==(HexCellQueueElement lhs, HexCellQueueElement rhs)
    {
        return lhs.cellIndex == rhs.cellIndex;
    }

    public static bool operator !=(HexCellQueueElement lhs, HexCellQueueElement rhs)
    {
        return !(lhs == rhs);
    }


    public bool Equals(HexCellQueueElement other)
    {
        return other == this;
    }

    public override bool Equals(object obj)
    {
        return base.Equals(obj);
    }

    public override int GetHashCode()
    {
        return cellIndex.GetHashCode();
    }
}
