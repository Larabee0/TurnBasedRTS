using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

public class DisableTesting : MonoBehaviour
{
    public static Entity obj;
    public EntityManager entityManager;
    private Entity Spawned = Entity.Null;
    // Start is called before the first frame update
    void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyUp(KeyCode.G) && Spawned == Entity.Null)
        {
            // spawn
            Spawned = entityManager.Instantiate(obj);
        }
        if (Input.GetKeyUp(KeyCode.H))
        {
            // hide
            entityManager.AddComponent<DisableRendering>(Spawned);
           // entityManager.RemoveComponent<WorldRenderBounds>(Spawned);
        }
        if(Input.GetKeyUp(KeyCode.H) && Input.GetKey(KeyCode.LeftShift))
        {
            // un-hide
            entityManager.RemoveComponent<DisableRendering>(Spawned);
            //entityManager.AddComponent<WorldRenderBounds>(Spawned);
        }
    }
}
