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

public class TurretTestingScript : MonoBehaviour
{
    public Mesh CubeMesh;
    public UnityEngine.Material TargetMaterial;
    private EntityManager entityManager;
    Entity createdTarget;
    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;
    // Start is called before the first frame update
    private void Start()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        endSimulationEntityCommandBufferSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        
        //World.DefaultGameObjectInjectionWorld.CreateSystem<TurretToTargetSystem>();
        //EntityArchetype archTemp = entityManager.CreateArchetype(typeof(Translation), typeof(Target),typeof(LocalToWorld),typeof(Rotation));
        //createdTarget = entityManager.CreateEntity(archTemp);
        //endSimulationEntityCommandBufferSystem.CreateCommandBuffer().SetComponent(createdTarget, new Translation { Value = transform.position });
        //endSimulationEntityCommandBufferSystem.CreateCommandBuffer().SetComponent(createdTarget, new Rotation { Value = transform.localRotation });
        SpawnTargetEntity();

    }
    NativeArray<TurretMovementData> turretControllers;

    private float BaseTargetAngle = 0f;
    private float ElavationTargetAngle = 0f;
    private float3 HoriztonalForward;
    private float3 VerticalForward;
    private float3 Magnitude(float3 input)
    {
        return math.sqrt(input.x * input.x + input.y * input.y + input.z * input.z);
    }
    public float3 elervatedPos;
    public float3 elervatedPosTranslation;
    public float3 localTargetPos;
    public float3 flattenedVecForVertical;
    public float targetElevation;
    public float targetTraverse;
    public float3 verticalLocalAngles;
    public float3 turretUp;
    public float3 BasePos;
    public float3 BasePosTranslation;
    public float3 vecToTarget;
    public float3 flattenedVecForBase;
    public quaternion Rot;
    // Update is called once per frame
    private void Update()
    {
        

        ///for (int i = 0; i < turretControllers.Length; i++)
        ///{
        ///    // it doesn't know where the turret is facing?
        ///    // check the .up,.forward etc compare with the reference GO system
        ///    TurretMovementData turretController = turretControllers[i];
        ///    BasePos = entityManager.GetComponentData<LocalToWorld>(turretController.HorizontalEntity).Position;
        ///    BasePosTranslation = entityManager.GetComponentData<Translation>(turretController.HorizontalEntity).Value;
        ///    elervatedPosTranslation = entityManager.GetComponentData<Translation>(turretController.HorizontalEntity).Value;
        ///    elervatedPos = entityManager.GetComponentData<LocalToWorld>(turretController.VerticalEntity).Position;
        ///    HoriztonalForward = entityManager.GetComponentData<LocalToWorld>(turretController.HorizontalEntity).Forward;
        ///    VerticalForward = entityManager.GetComponentData<LocalToWorld>(turretController.VerticalEntity).Forward;
        ///    float3 turretForward = entityManager.GetComponentData<LocalToWorld>(turretController.Self).Forward;
        ///    turretUp = entityManager.GetComponentData<LocalToWorld>(turretController.Self).Up;
        ///
        ///    vecToTarget = (float3)transform.position - BasePos;
        ///    flattenedVecForBase = ExtraTurretMathsFunctions.ProjectOnPlane(vecToTarget, turretUp);
        ///    targetTraverse = ExtraTurretMathsFunctions.SignedAngle(turretForward, flattenedVecForBase, turretUp);
        ///    turretController.limitedTraverseAngle = ExtraTurretMathsFunctions.MoveTowards(turretController.limitedTraverseAngle, targetTraverse, turretController.TraverseSpeed * Time.deltaTime);
        ///
        ///
        ///    localTargetPos = (float3)transform.position - elervatedPos;
        ///    localTargetPos = math.mul(math.inverse(entityManager.GetComponentData<LocalToWorld>(turretController.HorizontalEntity).Rotation), localTargetPos);            
        ///    flattenedVecForVertical = ExtraTurretMathsFunctions.ProjectOnPlane(localTargetPos, ExtraTurretMathsFunctions.Up);
        ///    targetElevation = ExtraTurretMathsFunctions.Angle(flattenedVecForVertical, localTargetPos);
        ///    targetElevation *= math.sign(localTargetPos.y);
        ///    targetElevation = math.clamp(targetElevation, -turretController.MaxDepression, turretController.MaxElevation);
        ///    turretController.elevation = ExtraTurretMathsFunctions.MoveTowards(turretController.elevation, targetElevation, turretController.ElevationSpeed * Time.deltaTime);
        ///
        ///
        ///    if (math.abs(turretController.elevation) > math.EPSILON)
        ///    {
        ///        turretController.VerticalAxis = verticalLocalAngles = ExtraTurretMathsFunctions.Right * -turretController.elevation;
        ///        entityManager.SetComponentData(turretController.VerticalEntity, new Rotation { Value = quaternion.EulerXYZ(math.radians(turretController.VerticalAxis)) });
        ///    }
        ///
        ///    if (math.abs(turretController.limitedTraverseAngle) > math.EPSILON)
        ///    {
        ///        turretController.HorizontalAxis = ExtraTurretMathsFunctions.Up * turretController.limitedTraverseAngle;
        ///        entityManager.SetComponentData(turretController.HorizontalEntity, new Rotation { Value = quaternion.EulerXYZ(math.radians(turretController.HorizontalAxis)) });
        ///    }
        ///
        ///    turretController.angleToTarget = ExtraTurretMathsFunctions.Angle((float3)transform.position - elervatedPosTranslation, VerticalForward);
        ///    if( turretController.angleToTarget < turretController.aimedThreshold)
        ///    {
        ///        entityManager.AddComponent<TurretIsAimed>(turretController.Self);
        ///    }
        ///
        ///    turretControllers[i] = turretController;
        ///    entityManager.SetComponentData(turretController.Self, turretController);
        ///
        ///    Debug.DrawRay(elervatedPos, VerticalForward * math.sqrt(localTargetPos.x * localTargetPos.x + localTargetPos.y * localTargetPos.y + localTargetPos.z * localTargetPos.z), Color.blue);
        ///    Debug.DrawRay(entityManager.GetComponentData<LocalToWorld>(turretController.Self).Position, turretUp * math.sqrt(localTargetPos.x * localTargetPos.x + localTargetPos.y * localTargetPos.y + localTargetPos.z * localTargetPos.z), Color.green);
        ///    Debug.DrawRay(BasePos, HoriztonalForward * math.sqrt(localTargetPos.x * localTargetPos.x + localTargetPos.y * localTargetPos.y + localTargetPos.z * localTargetPos.z), Color.red);
        ///}
    }



    private void RotateBase(float Angle)
    {
        BaseTargetAngle += Angle;
        EntityQuery turret = entityManager.CreateEntityQuery(typeof(TurretMovementData));
        turretControllers.Dispose();
        turretControllers = turret.ToComponentDataArray<TurretMovementData>(Allocator.Persistent);

    }
    private void Eleverate(float Angle)
    {
        ElavationTargetAngle += Angle;
        EntityQuery turret = entityManager.CreateEntityQuery(typeof(TurretMovementData));
        turretControllers.Dispose();
        turretControllers = turret.ToComponentDataArray<TurretMovementData>(Allocator.Persistent);

    }
    private void GetTurrets()
    {
        EntityQuery turret = entityManager.CreateEntityQuery(typeof(TurretMovementData));
        if(turretControllers.IsCreated)
            turretControllers.Dispose();
        turretControllers = turret.ToComponentDataArray<TurretMovementData>(Allocator.Persistent);

    }
    private void OnDestroy()
    {
        if (Targets.IsCreated)
        {
            Targets.Dispose();
        }
    }

    IEnumerator FailSafe()
    {
        yield return new WaitForSeconds(20f);
        RandomiseTargets();
    }
    NativeArray<Entity> Targets;
    private void SpawnTargetEntity()
    {
        EntityArchetype TargetArch = entityManager.CreateArchetype
        (
            typeof(Translation),
            typeof(LocalToWorld),
            typeof(RenderMesh),
            typeof(RenderBounds),
            typeof(Scale),
            typeof(Target)
        );
        EntityCommandBuffer buffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer();
        Targets = entityManager.CreateEntity(TargetArch, UnityEngine.Random.Range(10, 101),Allocator.Persistent);
        for (int i = 0; i < Targets.Length; i++)
        {
            buffer.SetComponent(Targets[i], new Translation { Value = new float3(UnityEngine.Random.Range(-10f, +10f), UnityEngine.Random.Range(-10f, +10f), UnityEngine.Random.Range(-10f, +10f)) });
            buffer.SetComponent(Targets[i], new Scale { Value = 0.1f });
            buffer.SetSharedComponent(Targets[i], new RenderMesh { material = TargetMaterial, mesh = CubeMesh });
            
        }
        RandomiseTargets();
    }
    private void RandomiseTargets()
    {
        EntityCommandBuffer buffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer();
        for (int i = 0; i < Targets.Length; i++)
        {
            buffer.SetComponent(Targets[i], new Translation { Value = new float3(UnityEngine.Random.Range(-10f, +10f), UnityEngine.Random.Range(-10f, +10f), UnityEngine.Random.Range(-10f, +10f)) });

        }
        //StartCoroutine(FailSafe());
    }
    private void OnGUI()
    {
        if (GUI.Button(new Rect(10, 0, 120, 30), "Rot Base +90"))
        {
            RotateBase(90f);
        }
        if (GUI.Button(new Rect(10, 35, 120, 30), "Rot Base -90"))
        {
            RotateBase(-90f);
        }
    
        if (GUI.Button(new Rect(10, 70, 120, 30), "Elv +5"))
        {
            Eleverate(5f);
        }
    
        if (GUI.Button(new Rect(10, 105, 120, 30), "Elv -5"))
        {
            Eleverate(-5f);
        }
        if (GUI.Button(new Rect(10, 140, 120, 30), "Randomise Targets"))
        {
            RandomiseTargets();
        }
    }

}
