using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace GameObjectHexagons
{
	public class HexCell : MonoBehaviour
    {
		public HexGridChunk chunk;
		public HexCoordinates coordinates;

		public RectTransform uiRect;

		public HexCell[] neighbours;

		public bool Explorable { get; set; }

		public int Index { get; set; }

		public int ColumnIndex { get; set; }

		public int SearchHeuristic { get; set; }

		public int SearchPhase { get; set; }
		public HexCell PathFrom { get; set; }

		public HexCell NextWithSamePriority { get; set; }

		public HexUnit Unit { get; set; }

		public HexCellShaderData ShaderData { get; set; }

		[SerializeField] private bool[] roads;
		private bool explored;

		private bool walled;
		private bool hasIncomingRiver;
		private bool hasOutgoingRiver;
		private HexDirection incomingRiver;
		private HexDirection outgoingRiver;

		private int terrianTypeIndex;

		private int elevation = int.MinValue;
		private int waterLevel;

		private int urbanLevel;
		private int farmLevel;
		private int plantLevel;

		private int specialIndex;

		private int distance;

		private int visibility;

		public int Elevation
		{
			get
			{
				return elevation;
			}
            set
            {
				if (elevation == value)
				{
					return;
				}
				int originalViewElevation = ViewElevation;
				elevation = value;
                if (ViewElevation != originalViewElevation)
                {
					ShaderData.ViewElevationChanged();
                }
				RefreshPosition();
				ValidateRivers();

				for (int i = 0; i < roads.Length; i++)
				{
					if (roads[i] && GetElevationDifference((HexDirection)i) > 1)
					{
						SetRoad(i, false);
					}
				}

				Refresh();
			}
		}

		public int WaterLevel
		{
			get
			{
				return waterLevel;
			}
			set
			{
				if (waterLevel == value)
				{
					return;
				}
				int originalViewElevation = ViewElevation;
				waterLevel = value;
				if (ViewElevation != originalViewElevation)
				{
					ShaderData.ViewElevationChanged();
				}
				ValidateRivers();
				Refresh();
			}
		}

		public int UrbanLevel
		{
			get
			{
				return urbanLevel;
			}
			set
			{
				if (urbanLevel != value)
				{
					urbanLevel = value;
					RefreshSelfOnly();
				}
			}
		}

		public int Farmlevel
		{
			get
			{
				return farmLevel;
			}
			set
			{
				if (farmLevel != value)
				{
					farmLevel = value;
					RefreshSelfOnly();
				}
			}
		}

		public int PlantLevel
		{
			get
			{
				return plantLevel;
			}
			set
			{
				if (plantLevel != value)
				{
					plantLevel = value;
					RefreshSelfOnly();
				}
			}
		}

		public int SpecialIndex
		{
			get
			{
				return specialIndex;
			}
			set
			{
				if (specialIndex != value && !HasRiver)
				{
					specialIndex = value;
					RemoveRoads();
					RefreshSelfOnly();
				}
			}
		}

		public int SearchPriority
		{
			get
			{
				return distance + SearchHeuristic;
			}
		}

		public int ViewElevation
        {
            get
            {
				return elevation >= waterLevel ? elevation : waterLevel;
            }
        }

		public bool IsVisible
        {
            get
            {
				return visibility > 0 && Explorable;
            }
        }

		public bool IsUnderwater
		{
			get
			{
				return waterLevel > elevation;
			}
		}

		public bool HasRiver
		{
			get
			{
				return hasIncomingRiver || hasOutgoingRiver;
			}
		}

		public bool IsExplored
		{
			get
			{
				return explored && Explorable;
			}
			private set
			{
				explored = value;
			}
		}

		public bool HasRiverBeginOrEnd
		{
			get
			{
				return hasIncomingRiver != hasOutgoingRiver;
			}
		}

		public bool HasIncomingRiver
		{
			get
			{
				return hasIncomingRiver;
			}
		}

		public bool HasOutgoingRiver
		{
			get
			{
				return hasOutgoingRiver;
			}
		}

		public bool HasRoads
		{
			get
			{
				for (int i = 0; i < roads.Length; i++)
				{
					if (roads[i])
					{
						return true;
					}
				}
				return false;
			}
		}

		public bool IsSpeical
		{
			get
			{
				return specialIndex > 0;
			}
		}

		public bool Walled
		{
			get
			{
				return walled;
			}
			set
			{
				if (walled != value)
				{
					walled = value;
					Refresh();
				}
			}
		}

		public int TerrianTypeIndex
        {
            get
            {
				return terrianTypeIndex;
            }
            set
            {
				if(terrianTypeIndex != value)
                {
					terrianTypeIndex = value;
					ShaderData.RefreshTerrian(this);
					//Refresh();
                }
            }
        }

		public int Distance
        {
            get
            {
				return distance;
            }
            set
            {
				distance = value;
				//UpdateDistanceLabel();
            }
        }

		public float StreamBedY
		{
			get
			{
				return (elevation + HexMetrics.streamBedElevationOffset) * HexMetrics.elevationStep;
			}
		}

		public float RiverSurfaceY
		{
			get
			{
				return (elevation + HexMetrics.waterElevationOffset) * HexMetrics.elevationStep;
			}
		}

		public float WaterSurfaceY
        {
            get
            {
				return (waterLevel + HexMetrics.waterElevationOffset) * HexMetrics.elevationStep;
            }
        }

		public HexDirection IncomingRiver
		{
			get
			{
				return incomingRiver;
			}
		}

		public HexDirection OutgoingRiver
		{
			get
			{
				return outgoingRiver;
			}
		}

		public HexDirection RiverBeginOrEndDirection
		{
			get
			{
				return hasIncomingRiver ? incomingRiver : outgoingRiver;
			}
		}

		public Vector3 Position
		{
			get
			{
				return transform.localPosition;
			}
		}

		public bool HasRoadThroughEdge(HexDirection direction)
		{
			return roads[(int)direction];
		}

		public bool HasRiverThroughEdge(HexDirection direction)
		{
			return hasIncomingRiver && incomingRiver == direction || hasOutgoingRiver && outgoingRiver == direction;
		}

		public int GetElevationDifference(HexDirection direction)
		{
			int difference = elevation - GetNeighbour(direction).elevation;
			return difference >= 0 ? difference : -difference;
		}

		public HexEdgeType GetEdgeType(HexDirection direction)
		{
			return HexMetrics.GetEdgeType(elevation, neighbours[(int)direction].elevation);
		}

		public HexEdgeType GetEdgeType(HexCell otherCell)
		{
			return HexMetrics.GetEdgeType(elevation, otherCell.elevation);
		}

		public HexCell GetNeighbour(HexDirection direction)
		{
			return neighbours[(int)direction];
		}

		public void SetNeighbour(HexDirection direction, HexCell cell)
		{
			neighbours[(int)direction] = cell;
			cell.neighbours[(int)direction.Opposite()] = this;
		}

		public void RemoveOutgoingRiver()
		{
			if (!hasOutgoingRiver)
			{
				return;
			}
			hasOutgoingRiver = false;
			RefreshSelfOnly();
			HexCell neighbor = GetNeighbour(outgoingRiver);
			neighbor.hasIncomingRiver = false;
			neighbor.RefreshSelfOnly();
		}

		public void RemoveIncomingRiver()
		{
			if (!hasIncomingRiver)
			{
				return;
			}
			hasIncomingRiver = false;
			RefreshSelfOnly();

			HexCell neighbor = GetNeighbour(incomingRiver);
			neighbor.hasOutgoingRiver = false;
			neighbor.RefreshSelfOnly();
		}

		public void RemoveRiver()
		{
			RemoveOutgoingRiver();
			RemoveIncomingRiver();
		}

		public void SetOutgoingRiver(HexDirection direction)
		{
			if (hasOutgoingRiver && outgoingRiver == direction)
			{
				return;
			}
			HexCell neighbour = GetNeighbour(direction);
			if (!IsValidRiverDestination(neighbour))
			{
				return;
			}
			RemoveOutgoingRiver();
			if (hasIncomingRiver && incomingRiver == direction)
			{
				RemoveIncomingRiver();
			}
			hasOutgoingRiver = true;
			outgoingRiver = direction;
			specialIndex = 0;

			neighbour.RemoveIncomingRiver();
			neighbour.hasIncomingRiver = true;
			neighbour.incomingRiver = direction.Opposite();
			neighbour.specialIndex = 0;

			SetRoad((int)direction, false);
		}

		public void RemoveRoads()
		{
			for (int i = 0; i < neighbours.Length; i++)
			{
				if (roads[i])
				{
					SetRoad(i, false);
				}
			}
		}

		public void AddRoad(HexDirection direction)
		{
			if (!roads[(int)direction] && !HasRiverThroughEdge(direction) && !IsSpeical && !GetNeighbour(direction).IsSpeical && GetElevationDifference(direction) <= 1)
			{
				SetRoad((int)direction, true);
			}
		}

		public void Save(BinaryWriter writer)
        {
			writer.Write((byte)terrianTypeIndex);
			writer.Write((byte)(elevation + 127));
			writer.Write((byte)waterLevel);
			writer.Write((byte)urbanLevel);
			writer.Write((byte)farmLevel);
			writer.Write((byte)plantLevel);
			writer.Write((byte)specialIndex);
			writer.Write(walled);

            if (hasIncomingRiver)
            {
				writer.Write((byte)(incomingRiver + 128));
            }
            else
            {
				writer.Write((byte)0);
            }

			if (hasOutgoingRiver)
			{
				writer.Write((byte)(outgoingRiver + 128));
			}
			else
			{
				writer.Write((byte)0);
			}

			int roadFlags = 0;
			for (int i = 0; i < roads.Length; i++)
			{
                if (roads[i])
                {
					roadFlags |= 1 << i;
                }
			}
			writer.Write((byte)roadFlags);
			writer.Write(IsExplored);
		}

		public void Load(BinaryReader reader, int header)
        {
			terrianTypeIndex = reader.ReadByte();
			ShaderData.RefreshTerrian(this);
			elevation = reader.ReadByte();
            if (header >= 4)
            {
				elevation -= 127;
            }
			RefreshPosition();
			waterLevel = reader.ReadByte();
			urbanLevel = reader.ReadByte();
			farmLevel = reader.ReadByte();
			plantLevel = reader.ReadByte();
			specialIndex = reader.ReadByte();
			walled = reader.ReadBoolean();

			byte riverData = reader.ReadByte();
            if (riverData >= 128)
            {
				hasIncomingRiver = true;
				incomingRiver = (HexDirection)(riverData - 128);
            }
            else
            {
				hasIncomingRiver = false;
            }

			riverData = reader.ReadByte();
			if (riverData >= 128)
			{
				hasOutgoingRiver = true;
				outgoingRiver = (HexDirection)(riverData - 128);
			}
			else
			{
				hasOutgoingRiver = false;
			}

			int roadFlags = reader.ReadByte();
			for (int i = 0; i < roads.Length; i++)
			{
				roads[i] = (roadFlags & (1 << i)) != 0;
			}
			IsExplored = header >= 3 ? reader.ReadBoolean() : false;
			ShaderData.RefreshVisibility(this);
		}

		public void DisableHighlight()
        {
			uiRect.GetChild(0).GetComponent<Image>().enabled = false;
        }

		public void EnableHighlight()
        {
			uiRect.GetChild(0).GetComponent<Image>().enabled = true;
		}

		public void EnableHighlight(Color colour)
        {
			Image highlight = uiRect.GetChild(0).GetComponent<Image>();
			highlight.color = colour;
			highlight.enabled = true;
		}

		public void SetLabel(string text)
		{
			uiRect.GetComponent<Text>().text = text;
		}

		public void IncreaseVisibility()
        {
			visibility += 1;

			if (visibility == 1)
            {
				IsExplored = true;
				ShaderData.RefreshVisibility(this);
            }
        }

		public void DecreaseVisibility()
        {
			visibility -= 1;
			if (visibility == 0)
			{
				ShaderData.RefreshVisibility(this);
			}
		}

		public void ResetVisibility()
		{
            if (visibility > 0)
            {
				visibility = 0;
				ShaderData.RefreshVisibility(this);
            }
		}

		public void SetMapData(float data)
        {
			ShaderData.SetMapData(this, data);
        }

		private bool IsValidRiverDestination(HexCell neighbour)
		{
			return neighbour && (elevation >= neighbour.elevation || waterLevel == neighbour.elevation);
		}

		private void SetRoad(int index, bool state)
		{
			roads[index] = state;
			neighbours[index].roads[(int)((HexDirection)index).Opposite()] = state;
			neighbours[index].RefreshSelfOnly();
			RefreshSelfOnly();
		}

		private void ValidateRivers()
		{
			if (hasOutgoingRiver && !IsValidRiverDestination(GetNeighbour(outgoingRiver)))
			{
				RemoveOutgoingRiver();
			}
			if (hasIncomingRiver && !GetNeighbour(incomingRiver).IsValidRiverDestination(this))
			{
				RemoveIncomingRiver();
			}
		}

		private void Refresh()
		{
			if (chunk)
			{
				chunk.Refresh();
				for (int i = 0; i < neighbours.Length; i++)
				{
					HexCell neighbor = neighbours[i];
					if (neighbor != null && neighbor.chunk != chunk)
					{
						neighbor.chunk.Refresh();
					}
				}
                if (Unit)
                {
					Unit.ValidateLocation();
                }
			}
		}

		private void RefreshSelfOnly()
		{
			chunk.Refresh();
			if (Unit)
			{
				Unit.ValidateLocation();
			}
		}

		private void RefreshPosition()
		{

			Vector3 position = transform.localPosition;
			position.y = elevation * HexMetrics.elevationStep;
			position.y += (HexMetrics.SampleNoise(position).y * 2f - 1f) * HexMetrics.elevationPerturbStrength;
			transform.localPosition = position;

			Vector3 uiPosition = uiRect.localPosition;
			uiPosition.z = -position.y;
			uiRect.localPosition = uiPosition;
		}
    }
}
