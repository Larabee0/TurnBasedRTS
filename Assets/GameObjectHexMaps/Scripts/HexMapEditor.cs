using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameObjectHexagons
{
	public class HexMapEditor : MonoBehaviour
	{
		public HexGrid hexGrid;
		public Material terrianMaterial;

		private int activeTerrianTypeIndex;

		private int brushSize;

		private bool applyElevation = true;
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
		private HexDirection dragDirection;
		private HexCell previousCell;

		private void Awake()
        {
			ShowGrid(true);
			SetEditMode(true);
		}

        // Start is called before the first frame update
        private void Start()
		{
			/// next part;
			/// https://catlikecoding.com/unity/tutorials/hex-map/part-26/
			/// Something is fucked with UV mapping for water probably anyway it throws errors - Fixed
			/// this seems to just be with the shore mapping. Have to ensure the number of verts equals the number of uv coordinates.
			/// - Fixed the above, this fixed the shore effect but it seems a little weak.
			/// - Rivers now blend into bodies of water more convincingly
			/// - Fixed shore effect
			/// - Fixed terrain base vision not refogging
		}

		private void Update()
		{
            if (true)
            {
				if (Input.GetMouseButton(0) && !EventSystem.current.IsPointerOverGameObject())
				{
					HandleInput();
					return;
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
			}
			previousCell = null;

		}

		private void HandleInput()
		{
			HexCell currentCell = GetCellUnderCursor();
			if (previousCell && previousCell != currentCell)
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
			return hexGrid.GetCell(Camera.main.ScreenPointToRay(Input.mousePosition));
		}

		private void ValidateDrag(HexCell currentCell)
		{
			for (
				dragDirection = HexDirection.NE;
				dragDirection <= HexDirection.NW;
				dragDirection++
			)
			{
				if (previousCell.GetNeighbour(dragDirection) == currentCell)
				{
					isDrag = true;
					return;
				}
			}
			isDrag = false;
		}

		private void CreateUnit()
        {
			HexCell cell = GetCellUnderCursor();
			if (cell && !cell.Unit)
			{
				hexGrid.AddUnit(Instantiate(HexUnit.unitPrefab), cell, Random.Range(0f, 360f));
			}
        }

		private void DestroyUnit()
        {
			HexCell cell = GetCellUnderCursor();
            if (cell && cell.Unit)
            {
				hexGrid.RemoveUnit(cell.Unit);
			}
        }

		public void EditCells(HexCell centre)
		{
			int centerX = centre.coordinates.X;
			int centerZ = centre.coordinates.Z;
			for (int r = 0, z = centerZ - brushSize; z <= centerZ; z++, r++)
			{
				for (int x = centerX - r; x <= centerX + brushSize; x++)
				{
					EditCell(hexGrid.GetCell(new HexCoordinates(x, z)));
				}
			}
			for (int r = 0, z = centerZ + brushSize; z > centerZ; z--, r++)
			{
				for (int x = centerX - brushSize; x <= centerX + r; x++)
				{
					EditCell(hexGrid.GetCell(new HexCoordinates(x, z)));
				}
			}
		}

		public void EditCell(HexCell cell)
		{
			if (cell)
			{
				if (activeTerrianTypeIndex >= 0)
				{
					cell.TerrianTypeIndex = activeTerrianTypeIndex;
				}
				if (applyElevation)
				{
					cell.Elevation = activeElevation;
				}
				if (applyWaterLevel)
				{
					cell.WaterLevel = activeWaterLevel;
				}
				if (applySpecialIndex)
				{
					cell.SpecialIndex = activeSpecialIndex;
				}
				if (applyUrbanLevel)
				{
					cell.UrbanLevel = activeUrbanLevel;
				}
				if (applyFarmLevel)
				{
					cell.Farmlevel = activeFarmLevel;
				}
				if (applyPlantLevel)
				{
					cell.PlantLevel = activePlantLevel;
				}
				if (riverMode == OptionalToggle.No)
				{
					cell.RemoveRiver();
				}
				if (roadMode == OptionalToggle.No)
				{
					cell.RemoveRoads();
				}
				if (walledMode != OptionalToggle.Ignore)
				{
					cell.Walled = walledMode == OptionalToggle.Yes;
				}
				if (isDrag)
				{
					HexCell otherCell = cell.GetNeighbour(dragDirection.Opposite());
					if (otherCell)
					{
						if (riverMode == OptionalToggle.Yes)
						{
							otherCell.SetOutgoingRiver(dragDirection);
						}
						if (roadMode == OptionalToggle.Yes)
						{
							otherCell.AddRoad(dragDirection);
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

		public void ShowUI(bool visible)
		{
			hexGrid.ShowUI(visible);
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