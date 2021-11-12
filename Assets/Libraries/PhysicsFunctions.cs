using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Physics;
using Unity.Physics.Systems;
using System.Runtime.CompilerServices;

public static class PhysicsFunctions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entity Raycast(BuildPhysicsWorld physicsWorld, float3 origin, float3 direction, float rayDistance = 100f)
    {
        return Raycast(physicsWorld, origin, direction, ~0u, ~0u, 0, rayDistance);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entity Raycast(BuildPhysicsWorld physicsWorld, float3 origin, float3 direction, uint BelongsTo, float rayDistance = 100f)
    {
        return Raycast(physicsWorld, origin, direction, BelongsTo, ~0u, 0, rayDistance);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entity Raycast(BuildPhysicsWorld physicsWorld, float3 origin, float3 direction, float rayDistance = 100f, uint CollidesWith = ~0u)
    {
        return Raycast(physicsWorld, origin, direction, ~0u, CollidesWith, 0, rayDistance);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entity Raycast(BuildPhysicsWorld physicsWorld, float3 origin, float3 direction, uint BelongsTo, uint CollidesWith, float rayDistance = 100f)
    {
        return Raycast(physicsWorld, origin, direction, BelongsTo, CollidesWith, 0, rayDistance);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Entity Raycast(BuildPhysicsWorld physicsWorld, float3 origin, float3 direction, uint BelongsTo, uint CollidesWith, int GroupIndex, float rayDistance = 100f)
    {
        CollisionWorld collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld;
        RaycastInput raydcastInput = new RaycastInput
        {
            Start = origin,
            End = (direction * rayDistance) + origin,
            Filter = new CollisionFilter
            {
                BelongsTo = BelongsTo,
                CollidesWith = CollidesWith,
                GroupIndex = GroupIndex
            }
        };
        if (collisionWorld.CastRay(raydcastInput, out Unity.Physics.RaycastHit raycastHit))
        {
            // hit something
            Entity hitEntity = physicsWorld.PhysicsWorld.Bodies[raycastHit.RigidBodyIndex].Entity;
            return hitEntity;
        }
        else
        {
            return Entity.Null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (bool, Unity.Physics.RaycastHit) RaycastHit(BuildPhysicsWorld physicsWorld, float3 origin, float3 direction, uint BelongsTo, uint CollidesWith, int GroupIndex, float rayDistance = 100f)
    {
        CollisionWorld collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld;
        RaycastInput raydcastInput = new RaycastInput
        {
            Start = origin,
            End = (direction * rayDistance) + origin,
            Filter = new CollisionFilter
            {
                BelongsTo = BelongsTo,
                CollidesWith = CollidesWith,
                GroupIndex = GroupIndex
            }
        };
        try
        {
            if (collisionWorld.CastRay(raydcastInput, out Unity.Physics.RaycastHit raycastHit))
            {
                // hit something
                return (true, raycastHit);
            }
        }
        catch { }
        return (false, new Unity.Physics.RaycastHit());
    }
}
