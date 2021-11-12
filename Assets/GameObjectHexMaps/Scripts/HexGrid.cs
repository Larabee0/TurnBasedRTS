using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
namespace GameObjectHexagons
{
	public class HexGrid : MonoBehaviour
	{
		public int cellCountX = 20;
		public int cellCountZ = 15;

		public bool wrapping;

		public bool MapCreated { private set; get; }

		public int seed;

		public Texture2D noiseSource;

		public HexGridChunk chunkPrefab;
		public HexCell cellPrefab;
		public Text cellLabelPrefab;
		public HexUnit unitPrefab;

		public bool HasPath
        {
            get
            {
				return currentPathExists;
            }
        }

		private int chunkCountX;
		private int chunkCountZ;

		private int currentCentreColumnIndex = -1;

		private int searchFrontierPhase;
		private bool currentPathExists;
		private HexCell currentPathFrom;
		private HexCell currentPathTo;
		private HexCellPriorityQueue searchFrontier;
		private HexCellShaderData cellShaderData;
		private HexCell[] cells;
		private Transform[] columns;
        private HexGridChunk[] chunks;
		private List<HexUnit> units = new List<HexUnit>();

		private void Awake()
		{
			HexMetrics.noiseSource = noiseSource;
			HexMetrics.InitializeHashGrid(seed);
			HexUnit.unitPrefab = unitPrefab;
			cellShaderData = gameObject.AddComponent<HexCellShaderData>();
			cellShaderData.Grid = this;
			float start = Time.realtimeSinceStartup;
			MapCreated = CreateMap(cellCountX, cellCountZ, wrapping);
			Debug.Log("map Time " + (Time.realtimeSinceStartup - start) * 1000f + "ms");
		}

		public void ResetVisibility()
		{
            for (int i = 0; i < cells.Length; i++)
            {
				cells[i].ResetVisibility();
            }

            for (int i = 0; i < units.Count; i++)
            {
				HexUnit unit = units[i];
				IncreaseVisibility(unit.Location, unit.VisionRange);
            }
		}

		public void AddUnit(HexUnit unit, HexCell location, float oreintation)
		{
			units.Add(unit);
			unit.Grid = this;
			//unit.transform.SetParent(transform, false);
			unit.Location = location;
			unit.Oreientation = oreintation;
		}

		public void RemoveUnit(HexUnit unit)
		{
			units.Remove(unit);
			unit.Die();
		}

		public bool CreateMap(int x, int z,bool wrapping)
		{
			cellCountX = x;
			cellCountZ = z;
			this.wrapping = wrapping;
			currentCentreColumnIndex = -1;
			HexMetrics.wrapSize = wrapping ? cellCountX : 0;
			cellShaderData.Initialise(cellCountX, cellCountZ);
			if (x <= 0 || x % HexMetrics.chunkSizeX != 0 || z <= 0 || z % HexMetrics.chunkSizeZ != 0)
			{
				Debug.LogError("Unsupported map size.");
				return false;
			}
			ClearPath();
			ClearUnits();
			if (columns != null)
			{
				for (int i = 0; i < columns.Length; i++)
				{
					Destroy(columns[i].gameObject);
				}
			}
			chunkCountX = cellCountX / HexMetrics.chunkSizeX;
			chunkCountZ = cellCountZ / HexMetrics.chunkSizeZ;
			CreateChunks();
			CreateCells();
			return true;
		}

		public void CentreMap(float xPosition)
        {
			int centreColumnIndex = (int)(xPosition / (HexMetrics.innerDiameter * HexMetrics.chunkSizeX));
			if(centreColumnIndex == currentCentreColumnIndex)
            {
				return;
            }
			currentCentreColumnIndex = centreColumnIndex;
			int minColumnIndex = centreColumnIndex - chunkCountX / 2;
			int maxColumnIndex = centreColumnIndex + chunkCountX / 2;
			float3 position = new float3(0f);
            for (int i = 0; i < columns.Length; i++)
            {
                if (i < minColumnIndex)
                {
					position.x = chunkCountX * (HexMetrics.innerDiameter * HexMetrics.chunkSizeX);
                }
				else if (i > maxColumnIndex)
                {
					position.x = chunkCountX * -(HexMetrics.innerDiameter * HexMetrics.chunkSizeX);
				}
                else
                {
					position.x = 0f;
				}
				columns[i].localPosition = position;
            }
		}

		public void MakeChildOfColumn(Transform child, int columnIndex)
        {
			child.SetParent(columns[columnIndex], false);
        }

		private void CreateChunks()
		{
			columns = new Transform[chunkCountX];
			chunks = new HexGridChunk[chunkCountX * chunkCountZ];

			for (int x = 0; x < chunkCountX; x++)
            {
				columns[x] = new GameObject("Column").transform;
				columns[x].SetParent(transform, false);
			}

			for (int z = 0, i = 0; z < chunkCountZ; z++)
			{
				for (int x = 0; x < chunkCountX; x++)
				{
					HexGridChunk chunk = chunks[i++] = Instantiate(chunkPrefab);
					chunk.transform.SetParent(columns[x], false);
				}
			}
		}

		private void CreateCells()
		{
			cells = new HexCell[cellCountZ * cellCountX];

			for (int z = 0, i = 0; z < cellCountZ; z++)
			{
				for (int x = 0; x < cellCountX; x++)
				{
					CreateCell(x, z, i++);
				}
			}
		}
		private void CreateCell(int x, int z, int i)
		{
			HexCell cell = cells[i] = Instantiate(cellPrefab);

			cell.Index = i;
			cell.coordinates = HexCoordinates.FromOffsetCoordinates(x, z);

			Vector3 position;
			position.x = ((x + z * 0.5f - z / 2) * HexMetrics.innerDiameter);
			position.y = 0f;
			position.z = (z * (HexMetrics.outerRadius * 1.5f));
			cell.transform.localPosition = position;
			cell.ColumnIndex = x / HexMetrics.chunkSizeX;
			cell.ShaderData = cellShaderData;

            if (wrapping)
            {
				cell.Explorable = z > 0 && z < cellCountZ - 1;
			}
            else
            {
				cell.Explorable = x > 0 && z > 0 && x < cellCountX - 1 && z < cellCountZ - 1;
			}
			
			if (x > 0)
			{
				cell.SetNeighbour(HexDirection.W, cells[i - 1]);
                if (wrapping && x == cellCountX - 1)
                {
					cell.SetNeighbour(HexDirection.E, cells[i - x]);
                }
			}
			if (z > 0)
			{
				if ((z & 1) == 0)
				{
					cell.SetNeighbour(HexDirection.SE, cells[i - cellCountX]);
					if (x > 0)
					{
						cell.SetNeighbour(HexDirection.SW, cells[i - cellCountX - 1]);
					}
					else if (wrapping)
                    {
						cell.SetNeighbour(HexDirection.SW, cells[i - 1]);
                    }
				}
				else
				{
					cell.SetNeighbour(HexDirection.SW, cells[i - cellCountX]);
					if (x < cellCountX - 1)
					{
						cell.SetNeighbour(HexDirection.SE, cells[i - cellCountX + 1]);
					}
					else if (wrapping)
					{
						cell.SetNeighbour(HexDirection.SE, cells[i - cellCountX * 2 + 1]);
					}
				}
			}
			Text label = Instantiate<Text>(cellLabelPrefab);
			label.rectTransform.anchoredPosition = new Vector2(position.x, position.z);
			cell.uiRect = label.rectTransform;

			cell.Elevation = 0;

			AddCellToChunk(x, z, cell);
		}

		private void AddCellToChunk(int x, int z, HexCell cell)
		{
			int chunkX = x / HexMetrics.chunkSizeX;
			int chunkZ = z / HexMetrics.chunkSizeZ;
			HexGridChunk chunk = chunks[chunkX + chunkZ * chunkCountX];

			int localX = x - chunkX * HexMetrics.chunkSizeX;
			int localZ = z - chunkZ * HexMetrics.chunkSizeZ;
			chunk.AddCell(localX + localZ * HexMetrics.chunkSizeX, cell);
		}

		public HexCell GetCell(HexCoordinates coordinates)
		{
			int z = coordinates.Z;
			if (z < 0 || z >= cellCountZ)
			{
				return null;
			}
			int x = coordinates.X + z / 2;
			if (x < 0 || x >= cellCountX)
			{
				return null;
			}
			return cells[x + z * cellCountX];
		}
		public HexCell GetCell(Vector3 position)
		{
			position = transform.InverseTransformPoint(position);
			HexCoordinates coordinates = HexCoordinates.FromPosition(position);			
			return GetCell(coordinates);
		}

		public HexCell GetCell(int xOffset, int zOffset)
        {
			return cells[xOffset + zOffset * cellCountX];
        }

		public HexCell GetCell(int cellIndex)
        {
			return cells[cellIndex];
        }

		public HexCell GetCell(Ray ray)
		{
			if (Physics.Raycast(ray, out RaycastHit hit))
			{
				return GetCell(hit.point);
			}
			return null;
		}

		public void ShowUI(bool visible)
		{
			for (int i = 0; i < chunks.Length; i++)
			{
				chunks[i].ShowUI(visible);
			}
		}

		public List<HexCell> GetPath()
        {
            if (!currentPathExists)
            {
				return null;
            }
			List<HexCell> path = ListPool<HexCell>.Get();
            for (HexCell c = currentPathTo; c != currentPathFrom; c=c.PathFrom)
            {
				path.Add(c);
            }
			path.Add(currentPathFrom);
			path.Reverse();
			return path;
        }

		public void FindPath(HexCell fromCell, HexCell toCell, HexUnit unit)
		{
			float start = Time.realtimeSinceStartup;
			ClearPath();
			currentPathFrom = fromCell;
			currentPathTo = toCell;
			currentPathExists = Search(fromCell, toCell, unit);
			ShowPath(unit.Speed);
			Debug.Log("Path Time" + (Time.realtimeSinceStartup - start) * 1000f + "ms");
		}


		private bool Search(HexCell fromCell, HexCell toCell, HexUnit unit)
		{
			int speed = unit.Speed;
			searchFrontierPhase += 2;
			if (searchFrontier == null)
			{
				searchFrontier = new HexCellPriorityQueue();
			}
			else
			{
				searchFrontier.Clear();
			}
			fromCell.SearchPhase = searchFrontierPhase;
			fromCell.Distance = 0;
			searchFrontier.Enqueue(fromCell);
			while (searchFrontier.Count > 0)
			{
				HexCell current = searchFrontier.Dequeue();
				current.SearchPhase += 1;
				if (current == toCell)
				{
					return true;
				}
				int currentTurn = (current.Distance - 1) / speed;
				for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
				{
					HexCell neighbour = current.GetNeighbour(d);
					if (neighbour == null || neighbour.SearchPhase > searchFrontierPhase)
					{
						continue;
					}
                    if (!unit.IsValidDestination(neighbour))
                    {
						continue;
                    }
					int moveCost = unit.GetMoveCost(current, neighbour, d);
                    if (moveCost < 0)
                    {
						continue;
                    }
					int distance = current.Distance + moveCost;
					int turn = (distance - 1) / speed;
					if (turn > currentTurn)
					{
						distance = turn * speed + moveCost;
					}
					if (neighbour.SearchPhase < searchFrontierPhase)
					{
						neighbour.SearchPhase = searchFrontierPhase;
						neighbour.Distance = distance;
						neighbour.PathFrom = current;
						neighbour.SearchHeuristic = neighbour.coordinates.DistanceTo(toCell.coordinates);
						searchFrontier.Enqueue(neighbour);
					}
					else if (distance < neighbour.Distance)
					{
						int oldPriority = neighbour.SearchPriority;
						neighbour.Distance = distance;
						neighbour.PathFrom = current;
						searchFrontier.Change(neighbour, oldPriority);
					}
				}
			}
			return false;
		}

		private List<HexCell> GetVisibleCells(HexCell fromCell, int range)
		{
			List<HexCell> visibleCells = ListPool<HexCell>.Get();

			searchFrontierPhase += 2;
			if (searchFrontier == null)
			{
				searchFrontier = new HexCellPriorityQueue();
			}
			else
			{
				searchFrontier.Clear();
			}
			range += fromCell.ViewElevation;
			fromCell.SearchPhase = searchFrontierPhase;
			fromCell.Distance = 0;
			searchFrontier.Enqueue(fromCell);
			HexCoordinates fromCoordinates = fromCell.coordinates;
			while (searchFrontier.Count > 0)
			{
				HexCell current = searchFrontier.Dequeue();
				current.SearchPhase += 1;
				visibleCells.Add(current);
				
				for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
				{
					HexCell neighbour = current.GetNeighbour(d);
					if (neighbour == null || neighbour.SearchPhase > searchFrontierPhase || !neighbour.Explorable)
					{
						continue;
					}
					
					int distance = current.Distance + 1;
					if (distance + neighbour.ViewElevation > range || distance > fromCoordinates.DistanceTo(neighbour.coordinates))
					{
						continue;
					}
					
					if (neighbour.SearchPhase < searchFrontierPhase)
					{
						neighbour.SearchPhase = searchFrontierPhase;
						neighbour.Distance = distance;
						neighbour.SearchHeuristic = 0;
						searchFrontier.Enqueue(neighbour);
					}
					else if (distance < neighbour.Distance)
					{
						int oldPriority = neighbour.SearchPriority;
						neighbour.Distance = distance;
						searchFrontier.Change(neighbour, oldPriority);
					}
				}
			}
			return visibleCells;
		}

		private void ShowPath(int speed)
		{
			if (currentPathExists)
			{
				HexCell current = currentPathTo;
				while (current != currentPathFrom)
				{
					int turn = (current.Distance - 1) / speed;
					current.SetLabel(turn.ToString());
					current.EnableHighlight(Color.white);
					current = current.PathFrom;
				}
			}
			currentPathFrom.EnableHighlight(Color.blue);
			currentPathTo.EnableHighlight(Color.red);
		}
		public void ClearPath()
		{
			if (currentPathExists)
			{
				HexCell current = currentPathTo;
				while (current != currentPathFrom)
				{
					current.SetLabel(null);
					current.DisableHighlight();
					current = current.PathFrom;
				}
				current.DisableHighlight();
				currentPathExists = false;
			}
			currentPathFrom = currentPathTo = null;
		}

		public void ClearPathAggressive()
        {
            for (int i = 0; i < cells.Length; i++)
            {

				HexCell current = cells[i];
				current.SetLabel(null);
				current.DisableHighlight();
			}
			currentPathExists = false;
			currentPathFrom = currentPathTo = null;
		}

		private void ClearUnits()
		{
			for (int i = 0; i < units.Count; i++)
			{
				units[i].Die();
			}
			units.Clear();
		}

		public void Save(BinaryWriter writer)
		{
			writer.Write(cellCountX);
			writer.Write(cellCountZ);
			writer.Write(wrapping);
			for (int i = 0; i < cells.Length; i++)
			{
				cells[i].Save(writer);
			}
			writer.Write(units.Count);
			for (int i = 0; i < units.Count; i++)
			{
				units[i].Save(writer);
			}
		}

		public void Load(BinaryReader reader, int header)
		{
			ClearPath();
			ClearUnits();
			StopAllCoroutines();
			int x = 20, z = 15;
			if (header >= 1)
			{
				x = reader.ReadInt32();
				z = reader.ReadInt32();
			}
			bool wrapping = header >= 5 && reader.ReadBoolean();
			if (x != cellCountX || z != cellCountZ || this.wrapping != wrapping)
			{
				if (!CreateMap(x, z, wrapping))
				{
					return;
				}
			}
			bool currentMode = cellShaderData.ImmediateMode;
			cellShaderData.ImmediateMode = true;
			for (int i = 0; i < cells.Length; i++)
			{
				cells[i].Load(reader, header);
			}

			for (int i = 0; i < chunks.Length; i++)
			{
				chunks[i].Refresh();
			}

            if (header >= 2)
			{
				int unitCount = reader.ReadInt32();
				for (int i = 0; i < unitCount; i++)
				{
					HexUnit.Load(reader, this);
				}
			}
			cellShaderData.ImmediateMode = currentMode;
		}

		public void IncreaseVisibility(HexCell fromCell, int range)
        {
			List<HexCell> cells = GetVisibleCells(fromCell, range);
            for (int i = 0; i < cells.Count; i++)
            {
				cells[i].IncreaseVisibility();
			}
			ListPool<HexCell>.Add(cells);
		}
		public void DecreaseVisibility(HexCell fromCell, int range)
		{
			List<HexCell> cells = GetVisibleCells(fromCell, range);
			for (int i = 0; i < cells.Count; i++)
			{
				cells[i].DecreaseVisibility();
			}
			ListPool<HexCell>.Add(cells);
		}
	}
}