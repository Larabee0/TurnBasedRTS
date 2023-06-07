using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.EventSystems;
using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Physics;
using Unity.Physics.Systems;

public enum OptionalToggle
{
    Ignore,
    Yes,
    No
}

public class HexMapUIInterface : MonoBehaviour
{
    private const float maxDistance = 1000f;
    public static HexMapUIInterface Instance { get; private set; }

    [SerializeField] private UIDocument document;
    private InputManager inputManager;
    private VisualElement RootVisualElement => document.rootVisualElement;
    #region Left
    private RadioButtonGroup terrainGroup;

    private Toggle elevationToggle;
    private SliderInt elevationSilder;

    private Toggle waterToggle;
    private SliderInt waterSilder;

    private Toggle brushToggle;
    private SliderInt brushSilder;

    private RadioButtonGroup riverGroup;
    private RadioButtonGroup roadGroup;
    private RadioButtonGroup walledGroup;
    #endregion
    #region Right
    private Button newMapsButton;
    private Button saveButton;
    private Button loadButton;

    private Toggle gridMode;
    private Toggle editMode;

    private Toggle urbanToggle;
    private SliderInt urbanSilder;

    private Toggle farmToggle;
    private SliderInt farmSilder;

    private Toggle plantToggle;
    private SliderInt plantSilder;

    private Toggle specialToggle;
    private SliderInt specialSilder;
    #endregion

    private EndSimulationEntityCommandBufferSystem ecbEndSys;
    private BuildPhysicsWorld physicsWorld; 
    private EntityManager entityManager;
    private EntityCommandBuffer ecbEnd;

    private HexGridBasic currentGridInfo;
    private NativeArray<HexCellReference> gridCellReferences;
    private NativeArray<HexGridChunkBuffer> gridChunkReferences;

    private OptionalToggle riverMode;
    private OptionalToggle roadMode;
    private OptionalToggle walledMode;

    private void Awake()
    {
        Instance = this;

        terrainGroup = RootVisualElement.Q<RadioButtonGroup>("TerrainGroup");
        elevationToggle = RootVisualElement.Q<Toggle>("ElevationToggle");
        elevationSilder = RootVisualElement.Q<SliderInt>("ElevationSlider");
        waterToggle = RootVisualElement.Q<Toggle>("WaterLevelToggle");
        waterSilder = RootVisualElement.Q<SliderInt>("WaterSlider");
        brushToggle = RootVisualElement.Q<Toggle>("BrushSizeToggle");
        brushSilder = RootVisualElement.Q<SliderInt>("BrushSizeSlider");
        riverGroup = RootVisualElement.Q<RadioButtonGroup>("RiverGroup");
        roadGroup = RootVisualElement.Q<RadioButtonGroup>("RoadGroup");
        walledGroup = RootVisualElement.Q<RadioButtonGroup>("WalledGroup");

        newMapsButton = RootVisualElement.Q<Button>("NewMapsButton");
        saveButton = RootVisualElement.Q<Button>("SaveButton");
        loadButton = RootVisualElement.Q<Button>("LoadButton");
        gridMode = RootVisualElement.Q<Toggle>("GridToggle");
        editMode = RootVisualElement.Q<Toggle>("EditToggle");
        urbanToggle = RootVisualElement.Q<Toggle>("UrbanToggle");
        urbanSilder = RootVisualElement.Q<SliderInt>("UrbanSlider");
        farmToggle = RootVisualElement.Q<Toggle>("FarmToggle");
        farmSilder = RootVisualElement.Q<SliderInt>("FarmSlider");
        specialToggle = RootVisualElement.Q<Toggle>("SpecialToggle");
        specialSilder = RootVisualElement.Q<SliderInt>("SpecialSlider");

        editMode.RegisterValueChangedCallback(SetEditMode);
    }

    private void Start()
    {
        inputManager = InputManager.Instance;
        inputManager.OnLeftMouseHeld += HandleInput;
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        ecbEndSys = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        physicsWorld = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<BuildPhysicsWorld>();

    }

    private void Update()
    {
        ecbEnd = ecbEndSys.CreateCommandBuffer();
    }

    private void OnDestroy()
    {
        HexMetrics.CleanUpNoiseColours();
        if (gridCellReferences.IsCreated)
        {
            gridCellReferences.Dispose();
        }
        if (gridChunkReferences.IsCreated)
        {
            gridChunkReferences.Dispose();
        }
    }

    public void SetMap(Entity gridRoot)
    {
        if (gridCellReferences.IsCreated)
        {
            gridCellReferences.Dispose();
        }
        if (gridChunkReferences.IsCreated)
        {
            gridChunkReferences.Dispose();
        }
        currentGridInfo = entityManager.GetComponentData<HexGridBasic>(gridRoot);
        gridCellReferences = entityManager.GetBuffer<HexCellReference>(gridRoot).ToNativeArray(Allocator.Persistent);
        gridChunkReferences = entityManager.GetBuffer<HexGridChunkBuffer>(gridRoot).ToNativeArray(Allocator.Persistent);
        gridCellReferences.Sort(new HexCellIndexSorter());
        gridChunkReferences.Sort(new HexChunkSorter());
    }

    private void HandleInput()
    {
        UnityEngine.Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Raycast(ray.origin, ray.origin + maxDistance * ray.direction, out HitInfoRaycast hitInfo))
        {
            Entity cell = GetCell(hitInfo.Position);
            EditCell(cell);
        }
    }

    public Entity GetCell(float3 position)
    {
        position = transform.InverseTransformPoint(position);
        HexCoordinates coordinates = HexCoordinates.FromPosition(position,HexMetrics.wrapSize);
        int index = coordinates.X + coordinates.Z * currentGridInfo.cellCountX + coordinates.Z / 2;
        return gridCellReferences[index].Value;
    }

    public void ScheduleGridRefresh()
    {
        for (int i = 0; i < gridChunkReferences.Length; i++)
        {
            ecbEnd.AddComponent<ChunkRefresh>(gridChunkReferences[i].Value);
        }
    }

    private void EditCell(Entity cell)
    {
        bool needsRefresh = false;
        bool setBasicData = false;
        bool setTerrainData = false;
        HexCellBasic basicCellInformation = entityManager.GetComponentData<HexCellBasic>(cell);
        HexCellTerrain terrainInformation = entityManager.GetComponentData<HexCellTerrain>(cell);
        if (elevationToggle.value && terrainInformation.elevation != elevationSilder.value)
        {
            float3 position = basicCellInformation.Position;
            terrainInformation.elevation = elevationSilder.value;
            position.y = terrainInformation.elevation * HexMetrics.elevationStep;
            position.y += (HexMetrics.SampleNoise(HexMetrics.noiseColours, position, HexMetrics.wrapSize).y * 2f - 1f) * HexMetrics.elevationPerturbStrength;
            basicCellInformation.Position = position;
            needsRefresh = setBasicData= setTerrainData = true;
        }

        if (setBasicData)
        {
            ecbEnd.SetComponent(cell, basicCellInformation);
        }

        if (setTerrainData)
        {
            ecbEnd.SetComponent(cell, terrainInformation);
        }
        
        if (needsRefresh)
        {
            ScheduleGridRefresh();
        }
    }

    private void ShowGrid(ChangeEvent<bool> visible)
    {
        if (visible.newValue)
        {
            // set shader keyword
        }
        else
        {

        }
    }

    private void SetEditMode(ChangeEvent<bool> callback)
    {
        enabled = callback.newValue;
    }


    public bool Raycast(float3 RayForm, float3 RayTo, out HitInfoRaycast hitInfo)
    {
        CollisionFilter Filter = new()
        {
            BelongsTo = ~0u,
            CollidesWith = ~0u,
            GroupIndex = 0
        };
        return Raycast(RayForm, RayTo, out hitInfo, Filter);
    }

    public bool Raycast(float3 RayFrom, float3 RayTo, out HitInfoRaycast hitInfo, CollisionFilter filter)
    {
        CollisionWorld collisionWorld = physicsWorld.PhysicsWorld.CollisionWorld;
        RaycastInput input = new()
        {
            Start = RayFrom,
            End = RayTo,
            Filter = filter
        };

        bool hasHit = collisionWorld.CastRay(input, out Unity.Physics.RaycastHit raycastHit);

        hitInfo = new HitInfoRaycast
        {
            raycastHit = raycastHit,
            Distance = hasHit ? math.distance(RayFrom, raycastHit.Position) : math.distance(RayFrom, RayTo)
        };

        return hasHit;
    }
}

public struct HitInfoRaycast : IComparer<HitInfoRaycast>
{
    public float Distance;
    public Unity.Physics.RaycastHit raycastHit;

    public float Fraction => raycastHit.Fraction;
    public int RigidBodyIndex => raycastHit.RigidBodyIndex;
    public ColliderKey ColliderKey => raycastHit.ColliderKey;
    public Unity.Physics.Material Material => raycastHit.Material;
    public Entity Entity => raycastHit.Entity;
    public float3 SurfaceNormal => raycastHit.SurfaceNormal;
    public float3 Position => raycastHit.Position;
    public int Compare(HitInfoRaycast x, HitInfoRaycast y)
    {
        return x.Distance.CompareTo(y.Distance);
    }
}