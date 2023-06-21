using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Unity.Physics;
using UnityEngine.EventSystems;

[UpdateInGroup(typeof(HexSystemGroup)),UpdateBefore(typeof(HexChunkColliderSystem))]
public partial class HexMapEditorSystem : SystemBase
{
    private static readonly int cellHighlightingId = Shader.PropertyToID("_CellHighlighting");

    private HexDirection dragDirection;
    private bool isDrag = false;

    private Entity previousCell;

    protected override void OnCreate()
    {
        EntityManager.AddComponent<HexMapEditorUIState>(SystemHandle);
        EntityManager.AddComponent<HexMapEditorLeftMouseHeld>(SystemHandle);
        HexMetrics.Terrain.DisableKeyword("_SHOW_GRID");
        Shader.EnableKeyword("_HEX_MAP_EDIT_MODE");
    }

    protected override void OnDestroy()
    {

    }

    protected override void OnUpdate()
    {

        if (SystemAPI.TryGetSingleton(out HexMapEditorLeftMouseHeld input) && SystemAPI.TryGetSingletonEntity<HexGridActive>(out Entity activeGrid))
        {
            HexMapEditorUIState uiState = SystemAPI.GetSingleton<HexMapEditorUIState>();
            EntityCommandBuffer ecb = GetEntityCommandBuffer();

            HexGridBasic currentGridInfo = SystemAPI.GetComponent<HexGridBasic>(activeGrid);
            if (!EventSystem.current.IsPointerOverGameObject())
            {
                HandleInput(input, activeGrid, uiState, ecb, currentGridInfo);
            }
            else
            {
                previousCell = Entity.Null;
                ClearCellHighlightData();
            }
        }
    }

    private void HandleInput(HexMapEditorLeftMouseHeld input,
        Entity activeGrid,
        HexMapEditorUIState uiState,
        EntityCommandBuffer ecb,
        HexGridBasic currentGridInfo)
    {
        CollisionWorld collisionWorld = GetCollisionWorld();
        if (input.leftMouseHeld && HandleInput(collisionWorld, input, out float3 hitPosition))
        {
            int cellIndex = GetCell(currentGridInfo, hitPosition);
            Entity cell = SystemAPI.GetBuffer<HexCellReference>(activeGrid)[cellIndex].Value;

            if (previousCell != Entity.Null && cell != previousCell)
            {
                ValidateDrag(cell);
            }
            else
            {
                isDrag = false;
            }

            EditCell(uiState, ecb, SystemAPI.GetBuffer<HexGridChunkBuffer>(activeGrid), cell);
            previousCell = cell;
            UpdateCellHighlightData(cell);
        }
        else
        {
            previousCell = Entity.Null;
            int cellIndex = GetCell(collisionWorld, currentGridInfo, input);
            Entity cell = cellIndex < 0 ? Entity.Null : SystemAPI.GetBuffer<HexCellReference>(activeGrid)[cellIndex].Value;
            UpdateCellHighlightData(cell);
        }
    }

    private void UpdateCellHighlightData(Entity cell)
    {
        if(cell == Entity.Null)
        {
            ClearCellHighlightData();
            return;
        }

        HexCellBasic cellData = SystemAPI.GetComponent<HexCellBasic>(cell);
        Shader.SetGlobalVector(cellHighlightingId, new Vector4(cellData.Coorindate.HexX, cellData.Coorindate.HexZ, 0.5f, HexMetrics.wrapSize));
    }
    void ClearCellHighlightData() => Shader.SetGlobalVector(cellHighlightingId, new Vector4(0f, 0f, -1f, 0f));

    private CollisionWorld GetCollisionWorld()
    {
        return SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld.CollisionWorld;
    }

    void ValidateDrag(Entity currentCell)
    {
        for (
            dragDirection = HexDirection.NE;
            dragDirection <= HexDirection.NW;
            dragDirection++
        )
        {
            if (SystemAPI.GetComponent<HexCellNeighbours>(previousCell).GetNeighbourEntity(dragDirection) == currentCell)
            {
                isDrag = true;
                return;
            }
        }
        isDrag = false;
    }

    private bool HandleInput(CollisionWorld collisionWorld, HexMapEditorLeftMouseHeld input, out float3 hitPosition)
    {
        hitPosition = float3.zero;
        bool hit = false;
        try
        {
            if (HexExtensions.Raycast(collisionWorld, input.StartPoint, input.EndPoint, out HitInfoRaycast hitInfo))
            {
                hitPosition = hitInfo.Position;
                hit = true;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
        return hit;
    }

    public int GetCell(CollisionWorld collisionWorld, HexGridBasic currentGridInfo, HexMapEditorLeftMouseHeld input)
    {
        if (HexExtensions.Raycast(collisionWorld, input.StartPoint, input.EndPoint, out HitInfoRaycast hitInfo))
        {
            return GetCell(currentGridInfo,hitInfo.Position);
        }
        return -1;
    }

    public int GetCell(HexGridBasic currentGridInfo, float3 position)
    {
        float3 localPosition = TransformHelpers.InverseTransformPoint(in float4x4.identity, in position);
        HexCoordinates coordinates = HexCoordinates.FromPosition(localPosition, currentGridInfo.wrapSize);
        int index = coordinates.X + coordinates.Z * currentGridInfo.cellCountX + coordinates.Z / 2;
        return index;
    }

    private void EditCell(HexMapEditorUIState uiState, EntityCommandBuffer ecbEnd,DynamicBuffer<HexGridChunkBuffer> chunks,Entity cell)
    {
        bool needsRefresh = false;

        RefRW<HexCellBasic> basicCellInformation = SystemAPI.GetComponentRW<HexCellBasic>(cell);
        RefRW<HexCellTerrain> terrainInformation = SystemAPI.GetComponentRW<HexCellTerrain>(cell);
        if (uiState.elevationToggle && terrainInformation.ValueRW.elevation != uiState.elevationSilder)
        {
            terrainInformation.ValueRW.elevation = uiState.elevationSilder;
            basicCellInformation.ValueRW.RefreshPosition(HexMetrics.noiseColours, uiState.elevationSilder, HexMetrics.wrapSize);
            needsRefresh = true;
        }
        if(uiState.waterToggle && terrainInformation.ValueRW.waterLevel != uiState.waterSilder)
        {
            terrainInformation.ValueRW.waterLevel = uiState.waterSilder;
            // ViewElevationChanged(Cell)
            ValidateRivers(terrainInformation, cell);
            needsRefresh = true;
        }
        if(uiState.specialToggle && terrainInformation.ValueRW.specialIndex != uiState.specialSilder)
        {
            terrainInformation.ValueRW.specialIndex = uiState.specialSilder;
            needsRefresh = true;
        }
        if (uiState.urbanToggle && terrainInformation.ValueRW.urbanlevel != uiState.urbanSilder)
        {
            terrainInformation.ValueRW.urbanlevel = uiState.urbanSilder;
            needsRefresh = true;
        }
        if (uiState.farmToggle && terrainInformation.ValueRW.farmLevel != uiState.farmSilder)
        {
            terrainInformation.ValueRW.farmLevel = uiState.farmSilder;
            needsRefresh = true;
        }
        if (uiState.plantToggle && terrainInformation.ValueRW.plantLevel != uiState.plantSilder)
        {
            terrainInformation.ValueRW.plantLevel = uiState.plantSilder;
            needsRefresh = true;
        }
        if (uiState.river == OptionalToggle.No)
        {
            RemoveRiver(terrainInformation, cell);
            needsRefresh = true;
        }
        if (uiState.road == OptionalToggle.No)
        {
            RemoveRoads(terrainInformation, cell);
            needsRefresh = true;
        }
        if (uiState.walled != OptionalToggle.Ignore)
        {
            terrainInformation.ValueRW.walled = uiState.walled == OptionalToggle.Yes;
            needsRefresh = true;
        }
        if (isDrag)
        {
            
            Entity otherCell = SystemAPI.GetComponent<HexCellNeighbours>(cell).GetNeighbourEntity(dragDirection.Opposite());
            if (otherCell != Entity.Null)
            {
                RefRW<HexCellTerrain> otherCellTerrain = SystemAPI.GetComponentRW<HexCellTerrain>(otherCell);
                if (uiState.river == OptionalToggle.Yes)
                {
                    SetOutGoingRiver(otherCellTerrain, otherCell, dragDirection);
                    needsRefresh = true;
                }
                if (uiState.road == OptionalToggle.Yes)
                {
                    AddRoad(otherCellTerrain, otherCell, dragDirection);
                    needsRefresh = true;
                }
            }
        }
        if (needsRefresh)
        {
            ScheduleGridRefresh(ecbEnd, chunks, cell);
        }
        if (uiState.activeTerrainTypeIndex >= 0)
        {
            if (terrainInformation.ValueRW.terrainTypeIndex != uiState.activeTerrainTypeIndex)
            {
                terrainInformation.ValueRW.terrainTypeIndex = uiState.activeTerrainTypeIndex;
                RefreshTerrain(basicCellInformation.ValueRW.Index, uiState.activeTerrainTypeIndex);
            }
        }
        //ScheduleGridRefresh(ecbEnd, chunks, cell);
    }

    public void ScheduleGridRefresh(EntityCommandBuffer ecbEnd, DynamicBuffer<HexGridChunkBuffer> chunks,Entity cell)
    {
        HexCellNeighbours neighbours = SystemAPI.GetComponent<HexCellNeighbours>(cell);
        NativeHashSet<int> targetChunksSet = new(7, Allocator.Temp)
        {
            SystemAPI.GetComponent<HexCellChunkReference>(cell).chunkIndex
        };
        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
        {
            int chunk = neighbours.GetNeighbourChunkIndex(d);
            if (chunk == -1)
            {
                continue;
            }
            targetChunksSet.Add(chunk);
        }
        NativeArray<int> targetChunks = targetChunksSet.ToNativeArray(Allocator.Temp);
        for (int i = 0; i < targetChunksSet.Count; i++)
        {
            Entity chunk = chunks[targetChunks[i]].Value;
            ecbEnd.AddComponent<HexChunkRefreshRequest>(chunk);
        }
    }

    public void RefreshTerrain(int index, int terrainTypeIndex)
    {
        NativeArray<Color32> textureData = SystemAPI.GetSingletonRW<HexShaderTextureData>().ValueRW.values;
        Color32 data = textureData[index];
        data.a = (byte)terrainTypeIndex;
        textureData[index] = data;
        EntityManager.AddComponent<HexShaderPaintTexture>(World.GetExistingSystem<HexShaderSystem>());
    }

    private EntityCommandBuffer GetEntityCommandBuffer()
    {
        var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(World.Unmanaged);
        return ecb;
    }

    public void RemoveRoads(RefRW<HexCellTerrain> mainTerrain,Entity mainCell)
    {
        for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
        {
            if (mainTerrain.ValueRW.HasRoadThroughEdge(d))
            {
                SetRoad(mainTerrain, mainCell, d, false);
            }
        }
    }

    public void AddRoad(RefRW<HexCellTerrain> mainTerrain, Entity mainCellEntity, HexDirection direction)
    {
        Entity neighbourEntity = SystemAPI.GetComponent<HexCellNeighbours>(mainCellEntity).GetNeighbourEntity(direction);
        if (neighbourEntity != null)
        {
            HexCellTerrain neighbour = SystemAPI.GetComponent<HexCellTerrain>(neighbourEntity);
            if (!mainTerrain.ValueRW.HasRoadThroughEdge(direction) && !mainTerrain.ValueRW.HasRiverThroughEdge(direction)
                && !mainTerrain.ValueRW.IsSpeical && mainTerrain.ValueRW.GetElevationDifference(neighbour) <= 1)
            {
                SetRoad(mainTerrain, mainCellEntity, direction, true);
            }
        }
    }

    private void ValidateRivers(RefRW<HexCellTerrain> mainTerrain, Entity mainCellEntity)
    {
        HexCellNeighbours neighbours = SystemAPI.GetComponent<HexCellNeighbours>(mainCellEntity);

        if (mainTerrain.ValueRW.hasOutgoingRiver)
        {
            Entity neighbour = neighbours.GetNeighbourEntity(mainTerrain.ValueRW.outgoingRiver);
            if (neighbour != Entity.Null && !IsValidRiverDestination(SystemAPI.GetComponentRW<HexCellTerrain>(neighbour), mainTerrain))
            {
                RemoveOutgoingRiver(mainTerrain, mainCellEntity);
            }

        }
        if (mainTerrain.ValueRW.hasIncomingRiver)
        {
            Entity neighbour = neighbours.GetNeighbourEntity(mainTerrain.ValueRW.incomingRiver);
            if (neighbour != Entity.Null && !IsValidRiverDestination(SystemAPI.GetComponentRW<HexCellTerrain>(neighbour), mainTerrain))
            {
                RemoveIncomingRiver(mainTerrain, mainCellEntity);
            }
        }
    }

    public void SetOutGoingRiver(RefRW<HexCellTerrain> mainTerrain, Entity mainCellEntity, HexDirection direction)
    {
        if (mainTerrain.ValueRW.hasOutgoingRiver && mainTerrain.ValueRW.outgoingRiver == direction)
        {
            return;
        }
        Entity neighbourEntity = SystemAPI.GetComponent<HexCellNeighbours>(mainCellEntity).GetNeighbourEntity(direction);
        if (neighbourEntity != Entity.Null)
        {
            RefRW<HexCellTerrain> neighbour = SystemAPI.GetComponentRW<HexCellTerrain>(neighbourEntity);
            if (!IsValidRiverDestination(neighbour,mainTerrain))
            {
                return;
            }
            RemoveOutgoingRiver(mainTerrain, mainCellEntity);
            if(mainTerrain.ValueRW.hasIncomingRiver && mainTerrain.ValueRW.incomingRiver == direction)
            {
                RemoveIncomingRiver(mainTerrain, mainCellEntity);
            }
            mainTerrain.ValueRW.hasOutgoingRiver = true;
            mainTerrain.ValueRW.outgoingRiver = direction;
            mainTerrain.ValueRW.specialIndex = 0;
            RemoveIncomingRiver(neighbour, neighbourEntity);
            neighbour.ValueRW.hasIncomingRiver = true;
            neighbour.ValueRW.incomingRiver = direction.Opposite();
            neighbour.ValueRW.specialIndex = 0;

            SetRoad(mainTerrain, mainCellEntity, direction, false);
        }
    }

    private void RemoveRiver(RefRW<HexCellTerrain> mainTerrain, Entity mainCellEntity)
    {
        RemoveOutgoingRiver(mainTerrain,mainCellEntity);
        RemoveIncomingRiver(mainTerrain, mainCellEntity);
    }

    private void RemoveOutgoingRiver(RefRW<HexCellTerrain> mainTerrain, Entity mainCellEntity)
    {
        if (!mainTerrain.ValueRW.hasOutgoingRiver)
        {
            return;
        }
        mainTerrain.ValueRW.hasOutgoingRiver = false;

        Entity neighbourEntity = SystemAPI.GetComponent<HexCellNeighbours>(mainCellEntity).GetNeighbourEntity(mainTerrain.ValueRW.outgoingRiver);
        if(neighbourEntity != Entity.Null)
        {
            SystemAPI.GetComponentRW<HexCellTerrain>(neighbourEntity).ValueRW.hasIncomingRiver = false;
            // neighbour refresh self
        }

        // refresh self only
    }

    void SetRoad(RefRW<HexCellTerrain> mainTerrain,Entity mainEntity,HexDirection direction, bool state)
    {
        SetRoadsWithoutNotify(mainTerrain, direction, state);
        Entity neighbourEntity = SystemAPI.GetComponent<HexCellNeighbours>(mainEntity).GetNeighbourEntity(direction);
        if(neighbourEntity != Entity.Null)
        {
            SetRoadsWithoutNotify(SystemAPI.GetComponentRW<HexCellTerrain>(neighbourEntity),direction.Opposite(), state);
        }
        // neighbors[index].RefreshSelfOnly();
        // RefreshSelfOnly();
    }

    private void SetRoadsWithoutNotify(RefRW<HexCellTerrain> mainTerrain, HexDirection direction, bool state)
    {
        switch (direction)
        {
            case HexDirection.NE:
                mainTerrain.ValueRW.RoadsNE = state;
                break;
            case HexDirection.E:
                mainTerrain.ValueRW.RoadsE = state;
                break;
            case HexDirection.SE:
                mainTerrain.ValueRW.RoadsSE = state;
                break;
            case HexDirection.SW:
                mainTerrain.ValueRW.RoadsSW = state;
                break;
            case HexDirection.W:
                mainTerrain.ValueRW.RoadsW = state;
                break;
            case HexDirection.NW:
                mainTerrain.ValueRW.RoadsNW = state;
                break;
        }
    }

    public void RemoveIncomingRiver(RefRW<HexCellTerrain> mainTerrain, Entity mainCellEntity)
    {
        if (!mainTerrain.ValueRW.hasIncomingRiver)
        {
            return;
        }
        mainTerrain.ValueRW.hasIncomingRiver = false;
        //RefreshSelfOnly();

        Entity neighbourEntity = SystemAPI.GetComponent<HexCellNeighbours>(mainCellEntity).GetNeighbourEntity(mainTerrain.ValueRW.incomingRiver);
        if(neighbourEntity != Entity.Null)
        {
            SystemAPI.GetComponentRW<HexCellTerrain>(neighbourEntity).ValueRW.hasOutgoingRiver = false;
            // neighbor.RefreshSelfOnly();
        }
    }

    private bool IsValidRiverDestination(RefRW<HexCellTerrain> neighbor, RefRW<HexCellTerrain> main) => main.ValueRW.elevation >= neighbor.ValueRW.elevation || main.ValueRW.waterLevel == neighbor.ValueRW.elevation;
}
