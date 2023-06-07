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

    void Start()
    {
        StartCoroutine(TestBakeColliderCoroutine());
    }

    protected IEnumerator TestBakeColliderCoroutine()
    {
        yield return new WaitForSeconds(10f);
        Debug.LogFormat("Testing Colliders: {0}", Time.realtimeSinceStartup);
        Mesh.MeshDataArray meshDataArray = Mesh.AcquireReadOnlyMeshData(bakeMeshCollider);
        var colliderTask = TestBakeColliderTask(meshDataArray);
        
        yield return new WaitUntil(() => colliderTask.IsCompleted);

        meshDataArray.Dispose();
        if (colliderTask.IsFaulted) // this only runs in the editor as android has no easily accessible logs.
        {
            Debug.LogException(colliderTask.Exception);
        }
        if (colliderTask.IsCompletedSuccessfully) // we have data to read
        {
            Debug.Log(colliderTask.Result.Value.MemorySize);
            
            colliderTask.Result.Dispose();
            Debug.LogFormat("Tested Colliders: {1} | {0}", Time.realtimeSinceStartup);
        }
    }

    public Task<BlobAssetReference<Collider>> TestBakeColliderTask(Mesh.MeshDataArray meshDataArray)
    {
        return Task<BlobAssetReference<Collider>>.Factory.StartNew(()=>BakeCollider(meshDataArray));
    }

    private static BlobAssetReference<Collider> BakeCollider(Mesh.MeshDataArray meshDataArray)
    {
        var triangles = new NativeArray<int>(meshDataArray[0].GetSubMesh(0).indexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        meshDataArray[0].GetIndices(triangles, 0);
        var tempTrianges = new NativeArray<int3>(triangles.Length / 3, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        unsafe
        {
            UnsafeUtility.MemCpy(tempTrianges.GetUnsafePtr(), triangles.GetUnsafePtr(), triangles.Length * UnsafeUtility.SizeOf<int>());
        }

        var vertices = new NativeArray<Vector3>(meshDataArray[0].GetSubMesh(0).vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        meshDataArray[0].GetVertices(vertices);
        BlobAssetReference<Collider> collider = MeshCollider.CreateManaged(vertices.Reinterpret<float3>(), tempTrianges, CollisionFilter.Default, Unity.Physics.Material.Default);
        triangles.Dispose();
        tempTrianges.Dispose();
        vertices.Dispose();
        return collider;
    }
}
