using Mono.Cecil.Cil;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Physics;
using UnityEngine;
using UnityEngine.UIElements;
using System;

public enum OptionalToggle
{
    Ignore,
    Yes,
    No
}

public class HexMapUIInterface : MonoBehaviour
{
    public const float maxDistance = 1000f;
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

    private SystemHandle editorSystemHandle;

    private EntityManager EntityManager =>World.DefaultGameObjectInjectionWorld.EntityManager;

    void Awake()
    {
        Instance = this;
        QueryUI();
    }

    private void QueryUI()
    {
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
        plantToggle = RootVisualElement.Q<Toggle>("PlantToggle");
        plantSilder = RootVisualElement.Q<SliderInt>("PlantSlider");
        specialToggle = RootVisualElement.Q<Toggle>("SpecialToggle");
        specialSilder = RootVisualElement.Q<SliderInt>("SpecialSlider");
        SetCallbakcs();
    }

    private void SetCallbakcs()
    {
        editMode.value = true;
        editMode.RegisterValueChangedCallback(SetEditMode);
        gridMode.RegisterValueChangedCallback(ShowGrid);
        gridMode.value = true;
        elevationSilder.RegisterValueChangedCallback(ev => UpdateUIState());
        elevationToggle.RegisterValueChangedCallback(ev => UpdateUIState());
        waterSilder.RegisterValueChangedCallback(ev=>UpdateUIState());
        waterToggle.RegisterValueChangedCallback(ev => UpdateUIState());
        terrainGroup.RegisterValueChangedCallback(ev=>UpdateUIState());
        riverGroup.RegisterValueChangedCallback(ev => UpdateUIState());
        roadGroup.RegisterValueChangedCallback(ev => UpdateUIState());
        walledGroup.RegisterValueChangedCallback(ev => UpdateUIState());

        urbanSilder.RegisterValueChangedCallback(ev => UpdateUIState());
        urbanToggle.RegisterValueChangedCallback(ev => UpdateUIState());
        farmSilder.RegisterValueChangedCallback(ev => UpdateUIState());
        farmToggle.RegisterValueChangedCallback(ev => UpdateUIState());

        plantSilder.RegisterValueChangedCallback(ev => UpdateUIState());
        plantToggle.RegisterValueChangedCallback(ev => UpdateUIState());
        specialSilder.RegisterValueChangedCallback(ev => UpdateUIState());
        specialToggle.RegisterValueChangedCallback(ev => UpdateUIState());
    }

    private void UpdateUIState()
    {
        HexMapEditorUIState uiState = new()
        {
            activeTerrainTypeIndex = terrainGroup.value > 4 ? -1 : terrainGroup.value,
            elevationToggle = elevationToggle.value,
            elevationSilder = elevationSilder.value,
            waterToggle = waterToggle.value,
            waterSilder = waterSilder.value,
            river = (OptionalToggle)riverGroup.value,
            road = (OptionalToggle)roadGroup.value,
            walled = (OptionalToggle)walledGroup.value,
            urbanToggle = urbanToggle.value,
            urbanSilder = urbanSilder.value,
            farmToggle = farmToggle.value,
            farmSilder = farmSilder.value,
            plantToggle = plantToggle.value,
            plantSilder = plantSilder.value,
            specialToggle = specialToggle.value,
            specialSilder = specialSilder.value
        };
        EntityManager.SetComponentData(editorSystemHandle, uiState);
    }

    private void Start()
    {
        inputManager = InputManager.Instance;
        inputManager.OnLeftMouseDown += HandleInputDown;
        inputManager.OnLeftMouseUpUI += HandleInputUp;

    }
    private void Update()
    {

        HandleCursorUpdate();

    }
    private void OnDestroy()
    {
        Instance = null;
    }

    private void HandleCursorUpdate()
    {
        editorSystemHandle = World.DefaultGameObjectInjectionWorld.GetExistingSystem<HexMapEditorSystem>();
        UnityEngine.Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (EntityManager.HasComponent(editorSystemHandle, ComponentType.ReadOnly<HexMapEditorLeftMouseHeld>()))
        {
            RefRW<HexMapEditorLeftMouseHeld> input = EntityManager.GetComponentDataRW<HexMapEditorLeftMouseHeld>(editorSystemHandle);
            input.ValueRW.maxDistance = maxDistance;
            input.ValueRW.ray = ray;
        }
    }

    private void HandleInputDown()
    {
        
        if (EntityManager.HasComponent(editorSystemHandle, ComponentType.ReadWrite<HexMapEditorLeftMouseHeld>()))
        {
            RefRW<HexMapEditorLeftMouseHeld> input = EntityManager.GetComponentDataRW<HexMapEditorLeftMouseHeld>(editorSystemHandle);
            input.ValueRW.leftMouseHeld = true;
        }
    }

    private void HandleInputUp()
    {
        if (EntityManager.HasComponent(editorSystemHandle, ComponentType.ReadOnly<HexMapEditorLeftMouseHeld>()))
        {
            RefRW<HexMapEditorLeftMouseHeld> input = EntityManager.GetComponentDataRW<HexMapEditorLeftMouseHeld>(editorSystemHandle);
            input.ValueRW.leftMouseHeld = false;
        }
    }

    private void ShowGrid(ChangeEvent<bool> visible)
    {
        if (visible.newValue)
        {
            HexMetrics.Terrain.EnableKeyword("_SHOW_GRID");
        }
        else
        {
            HexMetrics.Terrain.DisableKeyword("_SHOW_GRID");
        }
    }

    private void SetEditMode(ChangeEvent<bool> callback)
    {
        enabled = callback.newValue;
    }
}
