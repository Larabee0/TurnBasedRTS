using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Jobs;
using Unity.Burst;
using System.Runtime.CompilerServices;

namespace DOTSHexagonsV2
{
	public struct HexGridVertex : IBufferElementData
    {
		public float3 Value;
	}
	public struct HexGridTriangles : IBufferElementData
	{
		public uint Value;
	}

	public struct HexGridIndices : IBufferElementData
    {
		public float3 Value;
    }
	public struct HexGridWeights : IBufferElementData
	{
		public float4 Value;
	}

	public struct HexGridUV2 : IBufferElementData
	{
		public float2 Value;
	}
	public struct HexGridUV4 : IBufferElementData
	{
		public float4 Value;
	}


	public struct HexGridComponent : IComponentData
    {
        public int currentCentreColumnIndex;
        public int cellCountX;
        public int cellCountZ;
        public int cellCount;
        public int chunkCountX;
        public int chunkCountZ;
        public int chunkCount;
        public uint seed;
        public bool wrapping;
        public int wrapSize;
        public Entity gridEntity;
    }

	public struct HexRenderer : IComponentData
    {
		public int ChunkIndex;
		public RendererID rendererID;
	}

	public struct RepaintScheduled : IComponentData { }
	public struct RepaintNow : IComponentData { }

	public enum RendererID
    {
		Terrian,
		River,
		Water,
		WaterShore,
		Estuaries,
		Roads,
		Walls
	}

	public struct HexGridUnInitialised : IComponentData { }

    public struct HexGridDataInitialised : IComponentData
    {
        public int chunkIndex;
        public Entity gridEntity;
    }
    public struct HexGridInvokeEvent : IComponentData { }
    public struct HexGridCreated : IComponentData { }
    public struct GridWithColumns : IComponentData { }
    public struct GridWithChunks : IComponentData { }

	public struct HexGridVisualsInitialised : IComponentData { }

	public struct HexGridChunkInitialisationComponent : IComponentData
    {
        public int chunkIndex;
        public Entity gridEntity;
    }
    public struct HexGridChunkComponent : IComponentData
    {
        public int chunkIndex;

        public Entity entityTerrian;
        public Entity entityRiver;
        public Entity entityWater;
        public Entity entityWaterShore;
        public Entity entityEstuaries;
        public Entity entityRoads;
        public Entity entityWalls;

        public float3 Position;
        public Entity gridEntity;
        public Entity FeatureContainer;
    }

    public struct HexGridChunkBuffer : IBufferElementData
    {
        public Entity ChunkEntity;
		public int ChunkIndex;
    }


	public struct HexColumn : IComponentData
	{
		public int columnIndex;
	}

	public struct HexGridParent : IComponentData
    {
		public Entity Value;
    }

	public struct HexGridPreviousParent : IComponentData
	{
		public Entity Value;
	}

	public struct HexGridChild : IBufferElementData
    {
		public Entity Value;
    }


	public struct HexGridCellBuffer : IBufferElementData
    {
        public int cellIndex;
        public int featureCount;
        public int towerCount;
        public bool hasSpecialFeature;
        public bool HasFeature { get { return featureCount > 0; } }
        public bool HasAnyFeature { get { return featureCount > 0 || towerCount > 0 || hasSpecialFeature; } }
        public bool HasSpecialFeature { get { return hasSpecialFeature; } }
        public bool HasTowers { get { return towerCount > 0; } }
    }

    public struct RefreshChunk : IComponentData { }

	public struct HexCell : IBufferElementData, System.IEquatable<HexCell>
	{
		public static readonly HexCell Null = CreateEmpty(int.MinValue, int.MinValue, int.MinValue);

		public HexCoordinates coordinates;

		public Entity grid;

		public int x;
		public int z;

		public int NeighbourNE;
		public int NeighbourE;
		public int NeighbourSE;
		public int NeighbourSW;
		public int NeighbourW;
		public int NeighbourNW;

		public bool Explorable;

		public int Index;
		public int ChunkIndex;
		public int ColumnIndex;

		public bool RoadsNE;
		public bool RoadsE;
		public bool RoadsSE;
		public bool RoadsSW;
		public bool RoadsW;
		public bool RoadsNW;

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
					// refresh
				}
			}
		}

		private bool walled;
		public bool hasIncomingRiver;
		public bool hasOutgoingRiver;
		public HexDirection incomingRiver;
		public HexDirection outgoingRiver;

		public int terrianTypeIndex;

		public int Elevation
		{
			get { return elevation; }
			set
			{
				elevation = value;
				RefreshPosition();
			}
		}

		private void RefreshPosition()
		{
			Vector3 position = Position;
			position.y = elevation * HexFunctions.elevationStep;
			position.y += (HexFunctions.SampleNoise(HexFunctions.noiseColours, position, wrapSize).y * 2f - 1f) * HexFunctions.elevationPerturbStrength;
			Position = position;
		}


		public void RefreshPosition(NativeArray<float4> noiseColours)
		{
			Vector3 position = Position;
			position.y = elevation * HexFunctions.elevationStep;
			position.y += (HexFunctions.SampleNoise(noiseColours, position, wrapSize).y * 2f - 1f) * HexFunctions.elevationPerturbStrength;
			Position = position;
		}

		public int elevation;
		public int wrapSize;

		public int WaterLevel
		{
			get
			{
				return waterLevel;
			}
			set
			{
				switch (waterLevel == value)
				{
					case true:
						return;
					case false:
						waterLevel = value;
						break;
				}
			}
		}
		public int waterLevel;

		public int urbanLevel;
		public int farmLevel;
		public int plantLevel;
		private int specialIndex;

		public int SpecialIndex
		{
			get
			{
				return specialIndex;
			}
			set
			{
				specialIndex = value;
				RemoveAllRoads();
			}
		}

		private int visibility;

		private bool explored;
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

		public int ViewElevation
		{
			get
			{
				return Elevation >= WaterLevel ? Elevation : WaterLevel;
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
		public bool IsSpeical
		{
			get
			{
				return SpecialIndex > 0;
			}
		}
		public bool HasRiver
		{
			get
			{
				return hasIncomingRiver || hasOutgoingRiver;
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
				return RoadsNE switch
				{
					true => true,
					false => RoadsE switch
					{
						true => true,
						false => RoadsSE switch
						{
							true => true,
							false => RoadsSW switch
							{
								true => true,
								false => RoadsW switch
								{
									true => true,
									false => RoadsNW switch
									{
										true => true,
										false => false,
									},
								},
							},
						},
					},
				};
			}
		}
		public float StreamBedY
		{
			get
			{
				return (elevation + HexFunctions.streamBedElevationOffset) * HexFunctions.elevationStep;
			}
		}

		public float RiverSurfaceY
		{
			get
			{
				return (elevation + HexFunctions.waterElevationOffset) * HexFunctions.elevationStep;
			}
		}

		public float WaterSurfaceY
		{
			get
			{
				return (waterLevel + HexFunctions.waterElevationOffset) * HexFunctions.elevationStep;
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

		public float3 Position;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexCell SetNeighbour(HexCell cell, HexDirection direction, int neighbourIndex)
		{
			switch (direction)
			{
				case HexDirection.NE:
					cell.NeighbourNE = neighbourIndex;
					break;
				case HexDirection.E:
					cell.NeighbourE = neighbourIndex;
					break;
				case HexDirection.SE:
					cell.NeighbourSE = neighbourIndex;
					break;
				case HexDirection.SW:
					cell.NeighbourSW = neighbourIndex;
					break;
				case HexDirection.W:
					cell.NeighbourW = neighbourIndex;
					break;
				case HexDirection.NW:
					cell.NeighbourNW = neighbourIndex;
					break;
			}
			return cell;
		}


		public bool HasRiverThroughEdge(HexDirection direction)
		{
			return hasIncomingRiver && incomingRiver == direction || hasOutgoingRiver && outgoingRiver == direction;
		}

		public void AddRoad(HexDirection direction, HexCell neighbour)
		{
			if (!HasRoadThroughEdge(this, direction) && !HasRiverThroughEdge(this, direction) && !IsSpeical && !neighbour.IsSpeical && GetElevationDifference(this, neighbour) <= 1)
			{
				SetRoad(direction, true);
			}
		}

		public void SetRoad(HexDirection direction, bool state)
		{
			switch (direction)
			{
				case HexDirection.NE:
					this.RoadsNE = state;
					break;
				case HexDirection.E:
					this.RoadsE = state;
					break;
				case HexDirection.SE:
					this.RoadsSE = state;
					break;
				case HexDirection.SW:
					this.RoadsSW = state;
					break;
				case HexDirection.W:
					this.RoadsW = state;
					break;
				case HexDirection.NW:
					this.RoadsNW = state;
					break;
			}
		}

		public bool GetRoad(HexDirection direction)
		{
			return direction switch
			{
				HexDirection.NE => this.RoadsNE,
				HexDirection.E => this.RoadsE,
				HexDirection.SE => this.RoadsSE,
				HexDirection.SW => this.RoadsSW,
				HexDirection.W => this.RoadsW,
				HexDirection.NW => this.RoadsNW,
				_ => HasRoads,
			};
		}

		public void RemoveAllRoads()
		{
			RoadsNE = false;
			RoadsE = false;
			RoadsSE = false;
			RoadsSW = false;
			RoadsW = false;
			RoadsNW = false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasRoadThroughEdge(HexCell cell, HexDirection direction)
		{
			return direction switch
			{
				HexDirection.NE => cell.RoadsNE,
				HexDirection.E => cell.RoadsE,
				HexDirection.SE => cell.RoadsSE,
				HexDirection.SW => cell.RoadsSW,
				HexDirection.W => cell.RoadsW,
				HexDirection.NW => cell.RoadsNW,
				_ => false,
			};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool HasRiverThroughEdge(HexCell cell, HexDirection direction)
		{
			return cell.hasIncomingRiver && cell.incomingRiver == direction || cell.hasOutgoingRiver && cell.outgoingRiver == direction;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GetElevationDifference(HexCell cell, HexCell neighbourCell)
		{
			int difference = cell.Elevation - neighbourCell.Elevation;
			return difference >= 0 ? difference : -difference;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexEdgeType GetEdgeType(HexCell cell, HexCell neighbourCell)
		{
			return HexFunctions.GetEdgeType(cell.Elevation, neighbourCell.Elevation);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int GetNeighbourIndex(HexCell cell, HexDirection direction)
		{
			return direction switch
			{
				HexDirection.NE => cell.NeighbourNE,
				HexDirection.E => cell.NeighbourE,
				HexDirection.SE => cell.NeighbourSE,
				HexDirection.SW => cell.NeighbourSW,
				HexDirection.W => cell.NeighbourW,
				HexDirection.NW => cell.NeighbourNW,
				_ => int.MinValue,
			};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexCell GetNeighbour(HexCell cell, NativeArray<HexCell> cells, HexDirection direction)
		{
			int neighbourIndex = direction switch
			{
				HexDirection.NE => cell.NeighbourNE,
				HexDirection.E => cell.NeighbourE,
				HexDirection.SE => cell.NeighbourSE,
				HexDirection.SW => cell.NeighbourSW,
				HexDirection.W => cell.NeighbourW,
				HexDirection.NW => cell.NeighbourNW,
				_ => int.MinValue,
			};

			if (neighbourIndex == int.MinValue)
			{
				return HexCell.Null;
			}
			return cells[neighbourIndex];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexCell CreateWithNoNeighbours(int index, int x, int z, int wrapSize = 0)
		{
			HexCell cell = new HexCell();
			cell.wrapSize = wrapSize;
			cell.Index = index;
			cell.coordinates = HexCoordinates.FromOffsetCoordinates(x, z, wrapSize);
			cell.NeighbourE = int.MinValue;
			cell.NeighbourNE = int.MinValue;
			cell.NeighbourNW = int.MinValue;
			cell.NeighbourSE = int.MinValue;
			cell.NeighbourSW = int.MinValue;
			cell.NeighbourW = int.MinValue;
			cell.x = x;
			cell.z = z;
			return cell;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexCell CreateEmpty(int index, int x, int z)
		{
			HexCell cell = new HexCell();
			cell.Index = index;
			cell.coordinates = new HexCoordinates();
			cell.NeighbourE = int.MinValue;
			cell.NeighbourNE = int.MinValue;
			cell.NeighbourNW = int.MinValue;
			cell.NeighbourSE = int.MinValue;
			cell.NeighbourSW = int.MinValue;
			cell.NeighbourW = int.MinValue;
			cell.x = x;
			cell.z = z;
			return cell;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexCell SetOutgoingRiver(NativeArray<HexCell> cells, HexCell cell, HexDirection direction)
		{
			switch (cell.hasOutgoingRiver && cell.OutgoingRiver == direction)
			{
				case false:
					int neighbourIndex = GetNeighbourIndex(cell, direction);
					switch (neighbourIndex != int.MinValue)
					{
						case true:
							HexCell neighbour = cells[neighbourIndex];
							switch (IsValidRiverDestination(cell, neighbour))
							{
								case true:
									cell = RemoveOutgoingRiver(cells, cell);
									switch (cell.hasIncomingRiver && cell.incomingRiver == direction)
									{
										case true:
											cell = RemoveIncomingRiver(cells, cell);
											break;
									}
									cell.hasOutgoingRiver = true;
									cell.outgoingRiver = direction;
									cell.SpecialIndex = 0;
									cells[cell.Index] = cell;
									neighbour = RemoveIncomingRiver(cells, neighbour);
									neighbour.hasIncomingRiver = true;
									neighbour.incomingRiver = direction.Opposite();
									neighbour.SpecialIndex = 0;
									cells[neighbourIndex] = neighbour;
									break;
							}
							break;
					}
					break;
			}
			return cell;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexCell RemoveOutgoingRiver(NativeArray<HexCell> cells, HexCell cell)
		{
			switch (cell.hasOutgoingRiver)
			{
				case true:
					cell.hasOutgoingRiver = false;
					HexCell neighbor = cells[GetNeighbourIndex(cell, cell.outgoingRiver)];
					neighbor.hasIncomingRiver = false;
					cells[cell.Index] = cell;
					cells[neighbor.Index] = neighbor;
					break;
			}
			return cell;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexCell RemoveIncomingRiver(NativeArray<HexCell> cells, HexCell cell)
		{
			switch (cell.hasIncomingRiver)
			{
				case true:
					cell.hasOutgoingRiver = false;
					HexCell neighbor = cells[GetNeighbourIndex(cell, cell.outgoingRiver)];
					neighbor.hasOutgoingRiver = false;
					cells[cell.Index] = cell;
					cells[neighbor.Index] = neighbor;
					break;
			}
			return cell;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsValidRiverDestination(HexCell cell, HexCell neighbour)
		{
			return cell.Elevation >= neighbour.Elevation || cell.WaterLevel == neighbour.Elevation;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexCell ValidateRivers(NativeArray<HexCell> cells, HexCell cell)
		{

			if (cell.hasOutgoingRiver && !IsValidRiverDestination(cell, cells[GetNeighbourIndex(cell, cell.outgoingRiver)]))
			{
				cell = RemoveOutgoingRiver(cells, cell);
			}
			if (cell.hasIncomingRiver && !IsValidRiverDestination(cells[GetNeighbourIndex(cell, cell.outgoingRiver)], cell))
			{
				cell = RemoveIncomingRiver(cells, cell);
			}
			cells[cell.Index] = cell;
			return cell;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(HexCell other)
		{
			return this.Index == other.Index;
		}

		public static HexCell SetRoad(NativeArray<HexCell> cells, HexCell cell, HexDirection direction, bool state = true)
		{
			HexCell neighbour = GetNeighbour(cell, cells, direction);
			if (!cell.HasRiverThroughEdge(direction) && !cell.IsSpeical && !neighbour.IsSpeical && GetElevationDifference(cell, neighbour) <= 1)
			{
				cell.SetRoad(direction, state);
				neighbour.SetRoad(direction.Opposite(), state);
			}
			cells[cell.Index] = cell;
			cells[neighbour.Index] = neighbour;
			return cell;
		}
	}


	public struct CellContainer : IBufferElementData
	{
		public Entity container;
		public int cellIndex;
	}

	public struct FeatureContainer : IComponentData
	{
		public Entity GridEntity;
	}

	public struct FeatureDataContainer : IComponentData
	{
		public int cellIndex;
		public Entity containerEntity;
		public Entity GridEntity;
	}

	public struct CellFeature : IBufferElementData
	{
		public int cellIndex;
		public int featureLevelIndex;
		public int featureSubIndex;
		public FeatureCollection featureType;
		public float3 position;
		public float3 direction;
		public Entity feature;
		public bool UpdateCellFeatures;
		public bool UpdateFeaturePosition;
		public int ID
		{
			get
			{
				if (feature == Entity.Null)
				{
					return cellIndex ^ featureSubIndex ^ (int)featureType;
				}
				return (cellIndex ^ featureSubIndex ^ (int)featureType) ^ feature.Index;
			}
		}
	}

	public struct Feature : IBufferElementData
	{
		public int cellIndex;
		public int featureLevelIndex;
		public int featureSubIndex;
		public FeatureCollection featureType;
		public float3 position;
		public float3 direction;
		public Entity feature;
		public int ID
		{
			get
			{
				if (feature == Entity.Null)
				{
					return cellIndex ^ featureSubIndex ^ (int)featureType;
				}
				return (cellIndex ^ featureSubIndex ^ (int)featureType) ^ feature.Index;
			}
		}
	}
	public struct PossibleFeaturePosition : IBufferElementData
	{
		public int cellIndex;
		public float3 position;
		public float3 direction;
		public FeatureCollection ReservedFor;
	}

	public struct RefreshCellFeatures : IComponentData { }

	public enum FeatureCollection
	{
		None,
		WallTower,
		Bridge,
		Special,
		Urban,
		Farm,
		Plant
	}

}