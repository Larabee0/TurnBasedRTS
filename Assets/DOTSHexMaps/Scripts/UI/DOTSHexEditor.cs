using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DOTSHexagons
{
	public class DOTSHexEditor : MonoBehaviour
	{
		public static DOTSHexEditor Instance;
		public static List<Entity> GridEntities = new List<Entity>();
		int index = -1;
		public HexMapGenerator generator;
		public HexGridComponent hexGridInfo;
		public Material terrianMaterial;
		public EndSimulationEntityCommandBufferSystem commandBufferSystem;
		public EntityManager entityManager;
		private HexCellShaderSystem shaderData;
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
		private bool applyWaterLevel = true;
		private int activeSpecialIndex;
		private bool applySpecialIndex = false;

		private OptionalToggle riverMode;
		private OptionalToggle roadMode;
		private OptionalToggle walledMode;

		private bool isDrag;
		private HexDirection direction;
		private HexCell previousCell;
		private NativeArray<HexCell> cells;
		private NativeArray<HexGridChunkBuffer> chunks;
		private NativeHashSet<Entity> chunksToUpdate;


		// Awake is called before Start
		private void Awake()
		{
			Instance = this;
			physicsWorld = World.DefaultGameObjectInjectionWorld.GetExistingSystem<BuildPhysicsWorld>();
			commandBufferSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
			entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
			shaderData = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<HexCellShaderSystem>();
			chunksToUpdate = new NativeHashSet<Entity>(6, Allocator.Persistent);
			ShowGrid(true);
			SetEditMode(true);
		}

		public void HandleNewGrids()
        {
			if(index > GridEntities.Count - 1)
            {
				index = GridEntities.Count - 1;
            }
			else if(index < 0 && GridEntities.Count != 0)
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
		private void CreateNewGrid()
        {
            if (Input.GetKeyUp(KeyCode.C))
            {
				for (int i = 0; i < 25; i++)
				{
					generator.GenerateMap(32, 24, true);
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
        }

		// Update is called once per frame
		private void Update()
		{
			GridCycler();
			CreateNewGrid();
			if (Input.GetMouseButton(0) && !EventSystem.current.IsPointerOverGameObject())
			{
				PreInputActions();
				HandleInput();
				PostInputActions();
				return;
			}
			//if (Input.GetKeyUp(KeyCode.U))
			//{
			//	if (Input.GetKey(KeyCode.LeftShift))
			//	{
			//		DestroyUnit();
			//		return;
			//	}
			//	CreateUnit();
			//	return;
			//}
			previousCell = HexCell.Null;
		}

        private void LateUpdate()
        {
            if (chunksToUpdate.Count() > 0 && Input.GetMouseButtonUp(0) || isDrag)
            {
				EntityCommandBuffer commandBuffer = commandBufferSystem.CreateCommandBuffer();
				NativeArray<Entity> chunksToUpdateArray = chunksToUpdate.ToNativeArray(Allocator.Temp);				
				chunksToUpdate.Clear();

				for (int i = 0; i < chunksToUpdateArray.Length; i++)
                {
					Debug.Log("Cunk entity " + chunksToUpdateArray[i].Index + " scheduled refresh at t = " + Time.realtimeSinceStartup);
					commandBuffer.AddComponent<RefreshChunk>(chunksToUpdateArray[i]);
				}

				chunksToUpdateArray.Dispose();

			}
        }

        private void OnDestroy()
        {
            try
            {
				cells.Dispose();
            }
            catch { }
			try
			{
				chunks.Dispose();
			}
			catch { }
			try
			{
				chunksToUpdate.Dispose();
			}
			catch { }
		}

        private void PreInputActions()
        {
			cells = entityManager.GetBuffer<HexCell>(HexMetrics.ActiveGridEntity).ToNativeArray(Allocator.Temp);
			chunks = entityManager.GetBuffer<HexGridChunkBuffer>(HexMetrics.ActiveGridEntity).ToNativeArray(Allocator.Temp);
			hexGridInfo = entityManager.GetComponentData<HexGridComponent>(HexMetrics.ActiveGridEntity);
		}

		private void PostInputActions()
        {
			entityManager.GetBuffer<HexCell>(HexMetrics.ActiveGridEntity).CopyFrom(cells);
			cells.Dispose();
			chunks.Dispose();
		}

		private void HandleInput()
		{
			HexCell currentCell = GetCellUnderCursor();
			if (!previousCell.Equals(HexCell.Null) && !previousCell.Equals(currentCell))
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

		private HexCell GetCellUnderCursor()
		{
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			(bool hit, Unity.Physics.RaycastHit raycastHit)= PhysicsFunctions.RaycastHit(physicsWorld, ray.origin, ray.direction * 100, 100, ~0u, 100);
            if (hit)
            {
				return GetCellFromPosition(raycastHit.Position);
			}

			return HexCell.Null;
		}

		private void ValidateDrag(HexCell currentCell)
		{
			for (direction = HexDirection.NE; direction <= HexDirection.NW; direction++)
			{
				if (HexCell.GetNeighbourIndex(previousCell, direction) == currentCell.Index)
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
			for (direction = HexDirection.NE; direction <= HexDirection.NW; direction++)
			{
				HexCell neighbour = HexCell.GetNeighbour(cell, cells,  direction);
				if (!neighbour.Equals(HexCell.Null))
				{
					if(neighbour.ChunkIndex != cell.ChunkIndex)
                    {
						chunksToUpdate.Add(chunks[neighbour.ChunkIndex].ChunkEntity);
                    }
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
			LocalToWorld gridTransform = entityManager.GetComponentData<LocalToWorld>(HexMetrics.ActiveGridEntity);

			float3 position = math.transform(math.inverse(gridTransform.Value), point);
			HexCoordinates coordinates = HexCoordinates.FromPosition(position);
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
			if (!cell.Equals(HexCell.Null))
			{
				if (activeTerrianTypeIndex >= 0)
				{
					cell.terrianTypeIndex = activeTerrianTypeIndex;
					cells[cell.Index] = cell;
					shaderData.RefreshTerrian(cell);
				}
				if (applyElevation)
				{
					if(cell.Elevation != activeElevation)
					{
						int originalViewElevation = cell.ViewElevation;
						cell.Elevation = activeElevation;
						if (cell.ViewElevation != originalViewElevation)
						{
							shaderData.ViewElevationChanged();
						}
						cell = HexCell.ValidateRivers(cells,cell);

                        for (HexDirection direction = HexDirection.NE; direction <= HexDirection.NW; direction++)
                        {
							HexCell neighbourCell = HexCell.GetNeighbour(cell, cells, direction);
                            if (!neighbourCell.Equals(HexCell.Null))
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
					if(cell.WaterLevel != activeWaterLevel)
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
					cell = HexCell.RemoveOutgoingRiver(cells, cell);
					cell = HexCell.RemoveIncomingRiver(cells, cell);
					Refresh(cell);
				}
				if (roadMode == OptionalToggle.No)
				{
					for (direction = HexDirection.NE; direction <= HexDirection.NW; direction++)
					{
						cell.SetRoad(direction, false);
					}
					Refresh(cell);
				}
				if (walledMode != OptionalToggle.Ignore)
				{
					cell.Walled = walledMode == OptionalToggle.Yes;
					Refresh(cell);
				}
				if (isDrag)
				{
					HexCell otherCell = HexCell.GetNeighbour(cell, cells, direction.Opposite());
					if (!otherCell.Equals(HexCell.Null))
					{
						if (riverMode == OptionalToggle.Yes)
						{
							otherCell = HexCell.SetOutgoingRiver(cells, otherCell, direction);
							Refresh(otherCell);
						}
						if (roadMode == OptionalToggle.Yes)
						{
							otherCell = HexCell.SetRoad(cells, cell, direction);
							Refresh(otherCell);
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