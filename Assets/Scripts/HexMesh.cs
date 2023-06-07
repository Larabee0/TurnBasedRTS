using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using System;
using Unity.Rendering;
using UnityEngine.Rendering;
using Unity.Transforms;

public class HexMesh : MonoBehaviour, IConvertGameObjectToEntity
{
    public HexMeshData hexMeshData;

    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        transform.eulerAngles = new Vector3(transform.eulerAngles.x, 0f, transform.eulerAngles.z);
        MeshRef meshComp = new()
        {
            mesh = new Mesh() { name = hexMeshData.type.ToString(), subMeshCount = 1 },
            material = GetComponent<MeshRenderer>().sharedMaterial
        };
        dstManager.AddComponentData(entity, meshComp);
        dstManager.AddComponentData(entity, hexMeshData);
        dstManager.AddComponent<RegisterMeshEntity>(entity);

        RenderMesh renderMesh = dstManager.GetSharedComponentData<RenderMesh>(entity);
        renderMesh.mesh = meshComp.mesh;
        dstManager.SetSharedComponentData(entity, renderMesh);
    }
}

[Serializable]
public struct HexMeshData : IComponentData
{
    public MeshType type;
    public bool useCollider;
    public bool useCellData;
    public bool useUVCoordinates;
    public bool useUV2Coordinates;
}

public struct RegisterMeshEntity : IComponentData { }

public struct HexMeshReference : IBufferElementData
{
    public static implicit operator Entity(HexMeshReference v) { return v.Value; }
    public static implicit operator HexMeshReference(Entity v) { return new HexMeshReference { Value = v }; }
    public Entity Value;
}

public class MeshRef : IComponentData
{
    public Mesh mesh;
    public Material material;
}

public struct MeshDataWrapper 
{
    public double TimeStamp;
    public NativeParallelHashSet<int> chunksIncluded;
    public Mesh.MeshDataArray meshDataArray;

    public MeshDataWrapper(double timeStamp, NativeParallelHashSet<int> chunksIncluded, Mesh.MeshDataArray meshDataArray)
    {
        TimeStamp = timeStamp;
        this.chunksIncluded = chunksIncluded;
        this.meshDataArray = meshDataArray;
    }
}

[UpdateBefore(typeof(HexGridChunkSystem))]
public class HexMeshInitiliser : ComponentSystem
{
    EntityQuery newHexMesh;
    BeginSimulationEntityCommandBufferSystem ecbBeginSys;
    protected override void OnCreate()
    {
        var query = new EntityQueryDesc()
        {
            All = new ComponentType[]
            {
                typeof(HexMeshData),
                typeof(MeshRef),
                typeof(RegisterMeshEntity)
            }
        };
        newHexMesh = GetEntityQuery(query);
        ecbBeginSys = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
    }
    protected override void OnUpdate()
    {
        EntityCommandBuffer ecbBegin = ecbBeginSys.CreateCommandBuffer();
        MeshRef[] meshRefs = newHexMesh.ToComponentDataArray<MeshRef>();
        NativeArray<Entity> entities = newHexMesh.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < meshRefs.Length; i++)
        {
            string name = meshRefs[i].mesh.name;
            meshRefs[i].mesh = new Mesh() { name = name + "(Instance)", subMeshCount = 1 };
            meshRefs[i].material = new Material(meshRefs[i].material);
            var desc = new RenderMeshDescription(meshRefs[i].mesh, meshRefs[i].material);
            RenderMeshUtility.AddComponents(entities[i], ecbBegin, desc);
            ecbBegin.RemoveComponent<RegisterMeshEntity>(entities[i]);
        }

        

    }
}