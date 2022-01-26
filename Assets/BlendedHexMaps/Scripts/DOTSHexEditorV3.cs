using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DOTSHexagonsV2
{
    public class DOTSHexEditorV3 : MonoBehaviour
    {
		public static DOTSHexEditorV3 Instance;
		public static List<Entity> GridEntities = new List<Entity>();
		int index = -1;
		public GridAPI grid;
		public HexMapGenerator generator;
		public HexGridComponent hexGridInfo;
		public Material terrianMaterial;
		public LayerMask unitLayerMask;
		public EndSimulationEntityCommandBufferSystem commandBufferSystem;
		public EntityManager entityManager;
		private HexGridShaderSystem shaderData;
		private BuildPhysicsWorld physicsWorld;
		private int activeTerrianTypeIndex = -1;

		private int brushSize;

		private bool applyElevation = false;
		private int activeElevation;
		private int activeUrbanLevel;
		private bool applyUrbanLevel = false;
		private int activeFarmLevel;
		private bool applyFarmLevel = false;
		private int activePlantLevel;
		private bool applyPlantLevel = false;
		private int activeWaterLevel;
		private bool applyWaterLevel = false;
		private int activeSpecialIndex;
		private bool applySpecialIndex = false;

		private OptionalToggle riverMode;
		private OptionalToggle roadMode;
		private OptionalToggle walledMode;

		private bool isDrag;
		private HexDirection dragDirection;
		private HexCell previousCell;
		private DynamicBuffer<HexCell> cells;
		private NativeArray<HexGridChunkBuffer> chunks;
		private NativeHashSet<Entity> chunksToUpdate;

		private void Awake()
		{
			applyElevation = false;
			applyUrbanLevel = false;
			applyFarmLevel = false;
			applyPlantLevel = false;
			applyWaterLevel = false;
			applySpecialIndex = false;
			Instance = this;
			physicsWorld = World.DefaultGameObjectInjectionWorld.GetExistingSystem<BuildPhysicsWorld>();
			commandBufferSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
			entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
			shaderData = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<HexGridShaderSystem>();
			chunksToUpdate = new NativeHashSet<Entity>(6, Allocator.Persistent);
			ShowGrid(true);
			SetEditMode(true);
			Shader.EnableKeyword("BOOLEAN_B964DA9E23BC467FA33B192E46E0502F_ON");
		}

		public void HandleNewGrids()
		{
			if (index > GridEntities.Count - 1)
			{
				index = GridEntities.Count - 1;
			}
			else if (index < 0 && GridEntities.Count != 0)
			{
				index = 0;
			}
			else
			{
				return;
			}
			commandBufferSystem.CreateCommandBuffer().AddComponent<MakeActiveGridEntity>(GridEntities[index]);
		}

		private void GridCycler()
		{
			int limit = GridEntities.Count - 1;
			if (Input.GetKeyUp(KeyCode.KeypadPlus))
			{
				if (index + 1 > limit)
				{
					index = 0;
				}
				else
				{
					index++;
				}
				commandBufferSystem.CreateCommandBuffer().AddComponent<MakeActiveGridEntity>(GridEntities[index]);
			}
			if (Input.GetKeyUp(KeyCode.KeypadMinus))
			{
				if (index - 1 < 0)
				{
					index = limit;
				}
				else
				{
					index--;
				}
				commandBufferSystem.CreateCommandBuffer().AddComponent<MakeActiveGridEntity>(GridEntities[index]);
			}
		}

		public void CreateNewGrid()
		{
			for (int i = 0; i < 3; i++)
			{
				generator.GenerateMap(32, 24, true);
				//generator.GenerateMap(40, 30, true);
				//generator.GenerateMap(80, 60, true);
			}
			//if (GridEntities.Count == 0)
			//{
			//	for (int i = 0; i < 5; i++)
			//	{
			//		generator.GenerateMap(80, 60, true);
			//	}
			//}
			//else
			//{
			//	generator.GenerateMap(80, 60, true);
			//}

		}

		private void Update()
		{
			GridCycler();
			if (Input.GetKeyUp(KeyCode.C))
			{
				CreateNewGrid();
			}
			if (Input.GetMouseButton(0) && !EventSystem.current.IsPointerOverGameObject())
			{
				HandleInput();
                if (chunksToUpdate.Count() > 0)
                {
					NativeArray<Entity> chunksToUpdateArray = chunksToUpdate.ToNativeArray(Allocator.Temp);
					chunksToUpdate.Clear();
					entityManager.AddComponent<RefreshChunk>(chunksToUpdateArray);
					Debug.Log("Refreshing " + chunksToUpdateArray.Length + " Chunks");
				}

				return;
			}
            if (Input.GetKeyUp(KeyCode.R))
            {
				shaderData.ScheduleRefreshAll();
            }
			if (Input.GetKeyUp(KeyCode.U))
			{
				if (Input.GetKey(KeyCode.LeftShift))
				{
					DestroyUnit();
					return;
				}
				CreateUnit();
				return;
			}
			previousCell = HexCell.Null;
		}

		private void CreateUnit()
		{
			HexCell cell = GetCellUnderCursor();
			if (cell)
			{
				grid.AddUnit(cell);
			}
		}

		private void DestroyUnit()
		{
			HexUnit unit = GetUnitUnderCursor();
			if (unit)
			{
				grid.RemoveUnit(unit);
			}
		}

		private void OnDestroy()
		{
			try
			{
				chunksToUpdate.Dispose();
			}
			catch { }
		}

		public void GetGridData()
        {
			hexGridInfo = entityManager.GetComponentData<HexGridComponent>(GridAPI.ActiveGridEntity);
		}

		private void HandleInput()
		{
			HexCell currentCell = GetCellUnderCursor();
			if (previousCell && previousCell!=currentCell)
			{
				ValidateDrag(currentCell);
			}
			else
			{
				isDrag = false;
			}
			EditCells(currentCell);

			previousCell = currentCell;

		}

		private HexUnit GetUnitUnderCursor()
        {
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, unitLayerMask))
			{
				return hit.collider.gameObject.AddComponent<HexUnit>();
			}
			return null;
        }

		private HexCell GetCellUnderCursor()
		{
			cells = entityManager.GetBuffer<HexCell>(GridAPI.ActiveGridEntity);
			chunks = entityManager.GetBuffer<HexGridChunkBuffer>(GridAPI.ActiveGridEntity).ToNativeArray(Allocator.Temp);
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			
			if (Physics.Raycast(ray, out RaycastHit hit))
			{
				return GetCellFromPosition(hit.point);
			}

			return HexCell.Null;
		}

		private void ValidateDrag(HexCell currentCell)
		{
			for (dragDirection = HexDirection.NE; dragDirection <= HexDirection.NW; dragDirection++)
			{
				if (HexCell.GetNeighbourIndex(previousCell, dragDirection) == currentCell.Index)
				{
					isDrag = true;
					return;
				}
			}
			isDrag = false;
		}


		private void Refresh(HexCell cell)
		{
			cells[cell.Index] = cell;
			chunksToUpdate.Add(chunks[cell.ChunkIndex].ChunkEntity);
			for (dragDirection = HexDirection.NE; dragDirection <= HexDirection.NW; dragDirection++)
			{
				HexCell neighbour = HexCell.GetNeighbour(cell, cells, dragDirection);
				if (neighbour && neighbour.ChunkIndex != cell.ChunkIndex)
				{
					chunksToUpdate.Add(chunks[neighbour.ChunkIndex].ChunkEntity);
				}
			}
		}

		private void RefreshSingleOnly(HexCell cell)
		{
			cells[cell.Index] = cell;
			chunksToUpdate.Add(chunks[cell.ChunkIndex].ChunkEntity);
		}

		private HexCell GetCellFromPosition(float3 point)
		{
			Transform gridTransform = GridAPI.Instance.GridContainer;

			float3 position = gridTransform.InverseTransformPoint(point);
			HexCoordinates coordinates = HexCoordinates.FromPosition(position, hexGridInfo.wrapSize);
			return GetCellFromCoordinates(coordinates);
		}

		private HexCell GetCellFromCoordinates(HexCoordinates coordinates)
		{
			int z = coordinates.Z;
			if (z < 0 || z >= hexGridInfo.cellCountZ)
			{
				return HexCell.Null;
			}
			int x = coordinates.X + z / 2;
			if (x < 0 || x >= hexGridInfo.cellCountX)
			{
				return HexCell.Null;
			}
			return cells[x + z * hexGridInfo.cellCountX];
		}

		public void EditCells(HexCell centre)
		{
			int centerX = centre.coordinates.X;
			int centerZ = centre.coordinates.Z;
			for (int r = 0, z = centerZ - brushSize; z <= centerZ; z++, r++)
			{
				for (int x = centerX - r; x <= centerX + brushSize; x++)
				{
					EditCell(GetCellFromCoordinates(new HexCoordinates(x, z)));
				}
			}
			for (int r = 0, z = centerZ + brushSize; z > centerZ; z--, r++)
			{
				for (int x = centerX - brushSize; x <= centerX + r; x++)
				{
					EditCell(GetCellFromCoordinates(new HexCoordinates(x, z)));
				}
			}
		}

		public void EditCell(HexCell cell)
		{
			if (cell)
			{
				if (activeTerrianTypeIndex >= 0)
				{
					if(cell.terrianTypeIndex != activeTerrianTypeIndex)
					{
						cell.terrianTypeIndex = activeTerrianTypeIndex;
						cells[cell.Index] = cell;
						shaderData.RefreshTerrian(cell);
					}
				}
				if (applyElevation)
				{
					if (cell.Elevation != activeElevation)
					{
						int originalViewElevation = cell.ViewElevation;
						cell.Elevation = activeElevation;
						if (cell.ViewElevation != originalViewElevation)
						{
							shaderData.ViewElevationChanged();
						}
						cell = HexCell.ValidateRivers(cells, cell);

						for (HexDirection direction = HexDirection.NE; direction <= HexDirection.NW; direction++)
						{
							HexCell neighbourCell = HexCell.GetNeighbour(cell, cells, direction);
							if (neighbourCell)
							{
								if (cell.GetRoad(direction) && HexCell.GetElevationDifference(cell, neighbourCell) > 1)
								{
									cell = HexCell.SetRoad(cells, cell, direction, false);
								}
							}
						}
						Refresh(cell);
					}
				}
				if (applyWaterLevel)
				{
					if (cell.WaterLevel != activeWaterLevel)
					{
						int originalViewElevation = cell.ViewElevation;
						cell.WaterLevel = activeWaterLevel;
						if (cell.ViewElevation != originalViewElevation)
						{
							shaderData.ViewElevationChanged();
						}
						cell = HexCell.ValidateRivers(cells, cell);
						Refresh(cell);
					}

				}
				if (applySpecialIndex && cell.SpecialIndex != activeSpecialIndex && !cell.HasRiver)
				{
					cell.SpecialIndex = activeSpecialIndex;
					RefreshSingleOnly(cell);
				}
				if (applyUrbanLevel && cell.urbanLevel != activeUrbanLevel)
				{
					cell.urbanLevel = activeUrbanLevel;
					RefreshSingleOnly(cell);
				}
				if (applyFarmLevel && cell.farmLevel != activeFarmLevel)
				{
					cell.farmLevel = activeFarmLevel;
					RefreshSingleOnly(cell);
				}
				if (applyPlantLevel && cell.plantLevel != activePlantLevel)
				{
					cell.plantLevel = activePlantLevel;
					RefreshSingleOnly(cell);
				}
				if (riverMode == OptionalToggle.No)
				{
                    if (cell.HasRiver)
					{
						cell = HexCell.RemoveOutgoingRiver(cells, cell);
						cell = HexCell.RemoveIncomingRiver(cells, cell);
						Refresh(cell);
					}
				}
				if (roadMode == OptionalToggle.No)
				{
                    if (cell.HasRoads)
					{
						for (dragDirection = HexDirection.NE; dragDirection <= HexDirection.NW; dragDirection++)
						{
							cell.SetRoad(dragDirection, false);
						}
						Refresh(cell);
					}
				}
				if (walledMode != OptionalToggle.Ignore)
				{
					if(cell.Walled != (walledMode == OptionalToggle.Yes))
                    {
						cell.Walled = walledMode == OptionalToggle.Yes;
						Refresh(cell);
					}
				}
				if (isDrag)
				{
					HexCell otherCell = HexCell.GetNeighbour(cell, cells, dragDirection.Opposite());
					if (otherCell)
					{
						if (riverMode == OptionalToggle.Yes)
						{
                            if (!HexCell.HasRiverThroughEdge(otherCell, dragDirection))
							{
								otherCell = HexCell.SetOutgoingRiver(cells, otherCell, dragDirection);
								Refresh(otherCell);
							}
						}
						if (roadMode == OptionalToggle.Yes)
						{
							if (!HexCell.HasRoadThroughEdge(otherCell, dragDirection))
							{
								otherCell = HexCell.SetRoad(cells, otherCell, dragDirection);
								Refresh(otherCell);
							}
						}
					}
				}
			}
		}

		public void ToggleElevation(bool enabled)
		{
			applyElevation = enabled;
		}

		public void SetElevation(float elevation)
		{
			activeElevation = (int)elevation;
		}

		public void SetBrushSize(float size)
		{
			brushSize = (int)size;
		}

		public void SetRiverMode(int mode)
		{
			riverMode = (OptionalToggle)mode;
		}

		public void SetRoadMode(int mode)
		{
			roadMode = (OptionalToggle)mode;
		}

		public void SetWalledMode(int mode)
		{
			walledMode = (OptionalToggle)mode;
		}

		public void SetApplyWaterLevel(bool toggle)
		{
			applyWaterLevel = toggle;
		}

		public void SetWaterLevel(float level)
		{
			activeWaterLevel = (int)level;
		}

		public void SetApplyUrbanLevel(bool toggle)
		{
			applyUrbanLevel = toggle;
		}

		public void SetUrbanLevel(float level)
		{
			activeUrbanLevel = (int)level;
		}

		public void SetApplyFarmLevel(bool toggle)
		{
			applyFarmLevel = toggle;
		}

		public void SetFarmLevel(float level)
		{
			activeFarmLevel = (int)level;
		}

		public void SetApplyPlantLevel(bool toggle)
		{
			applyPlantLevel = toggle;
		}

		public void SetPlantLevel(float level)
		{
			activePlantLevel = (int)level;
		}

		public void SetApplySpecialIndex(bool toggle)
		{
			applySpecialIndex = toggle;
		}

		public void SetSpecialIndex(float index)
		{
			activeSpecialIndex = (int)index;
		}

		public void SetTerrianTypeIndex(int index)
		{
			activeTerrianTypeIndex = index;
		}

		public void ShowGrid(bool visible)
		{
			if (visible)
			{
				terrianMaterial.SetFloat("Boolean_997f410a66984644b34cb86711653d7b", 1);
				return;
			}
			terrianMaterial.SetFloat("Boolean_997f410a66984644b34cb86711653d7b", 0);
		}

		public void SetEditMode(bool toggle)
		{
			enabled = toggle;
		}
	}
}