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
		public static implicit operator float3(HexGridVertex v) { return v.Value; }
		public static implicit operator HexGridVertex(float3 v) { return new HexGridVertex { Value = v }; }
		public static implicit operator Vector3(HexGridVertex v) { return v.Value; }
		public static implicit operator HexGridVertex(Vector3 v) { return new HexGridVertex { Value = v }; }
		public float3 Value;
	}

	public struct HexGridTriangles : IBufferElementData
	{
		public static implicit operator uint(HexGridTriangles v) { return v.Value; }
		public static implicit operator HexGridTriangles(uint v) { return new HexGridTriangles { Value = v }; }
		public uint Value;
	}

	public struct HexGridIndices : IBufferElementData
	{
		public static implicit operator float3(HexGridIndices v) { return v.Value; }
		public static implicit operator HexGridIndices(float3 v) { return new HexGridIndices { Value = v }; }
		public static implicit operator Vector3(HexGridIndices v) { return v.Value; }
		public static implicit operator HexGridIndices(Vector3 v) { return new HexGridIndices { Value = v }; }
		public float3 Value;
	}

	public struct HexGridWeights : IBufferElementData
	{
		public static implicit operator float4(HexGridWeights v) { return v.Value; }
		public static implicit operator HexGridWeights(float4 v) { return new HexGridWeights { Value = v }; }
		public static implicit operator Vector4(HexGridWeights v) { return v.Value; }
		public static implicit operator HexGridWeights(Vector4 v) { return new HexGridWeights { Value = v }; }
		public static implicit operator Color(HexGridWeights v) { return (Vector4)v.Value; }
		public static implicit operator HexGridWeights(Color v) { return new HexGridWeights { Value = (Vector4)v }; }

		public float4 Value;
	}

	public struct HexGridUV2 : IBufferElementData
	{
		public static implicit operator float2(HexGridUV2 v) { return v.Value; }
		public static implicit operator HexGridUV2(float2 v) { return new HexGridUV2 { Value = v }; }
		public static implicit operator Vector2(HexGridUV2 v) { return v.Value; }
		public static implicit operator HexGridUV2(Vector2 v) { return new HexGridUV2 { Value = v }; }
		public float2 Value;
	}

	public struct HexGridUV4 : IBufferElementData
	{
		public static implicit operator float4(HexGridUV4 v) { return v.Value; }
		public static implicit operator HexGridUV4(float4 v) { return new HexGridUV4 { Value = v }; }
		public static implicit operator Vector4(HexGridUV4 v) { return v.Value; }
		public static implicit operator HexGridUV4(Vector4 v) { return new HexGridUV4 { Value = v }; }
		public float4 Value;
	}


	public struct CentreMap : IComponentData
	{
		public static implicit operator float(CentreMap v) { return v.Value; }
		public static implicit operator CentreMap(float v) { return new CentreMap { Value = v }; }
		public float Value;
	}

	public struct ColumnOffset : IComponentData
	{
		public static implicit operator float3(ColumnOffset v) { return v.Value; }
		public static implicit operator ColumnOffset(float3 v) { return new ColumnOffset { Value = v }; }
		public static implicit operator Vector3(ColumnOffset v) { return v.Value; }
		public static implicit operator ColumnOffset(Vector3 v) { return new ColumnOffset { Value = v }; }

		public float3 Value;
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
		public int MeshArrayIndex;
	}

	public struct HexMeshIndex : IComponentData
	{
		public static implicit operator int(HexMeshIndex v) { return v.Value; }
		public static implicit operator HexMeshIndex(int v) { return new HexMeshIndex { Value = v }; }
		public int Value;
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
		public static implicit operator int(HexColumn v) { return v.Value; }
		public static implicit operator HexColumn(int v) { return new HexColumn { Value = v }; }
		public int Value;
	}

	public struct HexGridParent : IComponentData
	{
		public static implicit operator Entity(HexGridParent v) { return v.Value; }
		public static implicit operator HexGridParent(Entity v) { return new HexGridParent { Value = v }; }
		public Entity Value;

		public static bool operator ==(HexGridParent lhs, HexGridParent rhs)
		{
			if (lhs.Value == rhs.Value)
			{
				return true;
			}

			return false;
		}

		public static bool operator !=(HexGridParent lhs, HexGridParent rhs)
		{
			return !(lhs == rhs);
		}

		public static bool operator ==(HexGridParent lhs, HexGridPreviousParent rhs)
		{
			if (lhs.Value == rhs.Value)
			{
				return true;
			}

			return false;
		}

		public static bool operator !=(HexGridParent lhs, HexGridPreviousParent rhs)
		{
			return !(lhs == rhs);
		}
		public static bool operator ==(HexGridParent lhs, Entity rhs)
		{
			if (lhs.Value == rhs)
			{
				return true;
			}

			return false;
		}

		public static bool operator !=(HexGridParent lhs, Entity rhs)
		{
			return !(lhs == rhs);
		}

		public override bool Equals(object compare)
		{
			return this == (Entity)compare;
		}

		public override int GetHashCode()
		{
			return Value.GetHashCode();
		}

		public override string ToString()
		{
			return Value.ToString();
		}
	}

	public struct HexGridPreviousParent : IComponentData
	{
		public static implicit operator Entity(HexGridPreviousParent v) { return v.Value; }
		public static implicit operator HexGridPreviousParent(Entity v) { return new HexGridPreviousParent { Value = v }; }
		public Entity Value;

		public static bool operator ==(HexGridPreviousParent lhs, HexGridPreviousParent rhs)
		{
			if (lhs.Value == rhs.Value)
			{
				return true;
			}

			return false;
		}

		public static bool operator !=(HexGridPreviousParent lhs, HexGridPreviousParent rhs)
		{
			return !(lhs == rhs);
		}

		public static bool operator ==(HexGridPreviousParent lhs, HexGridParent rhs)
		{
			if (lhs.Value == rhs.Value)
			{
				return true;
			}

			return false;
		}

		public static bool operator !=(HexGridPreviousParent lhs, HexGridParent rhs)
		{
			return !(lhs == rhs);
		}

		public static bool operator ==(HexGridPreviousParent lhs, Entity rhs)
		{
			if (lhs.Value == rhs)
			{
				return true;
			}

			return false;
		}

		public static bool operator !=(HexGridPreviousParent lhs, Entity rhs)
		{
			return !(lhs == rhs);
		}

		public override bool Equals(object compare)
		{
			return this == (Entity)compare;
		}

		public override int GetHashCode()
		{
			return Value.GetHashCode();
		}

		public override string ToString()
		{
			return Value.ToString();
		}
	}

	public struct HexGridChild : IBufferElementData
	{
		public static implicit operator Entity(HexGridChild v) { return v.Value; }
		public static implicit operator HexGridChild(Entity v) { return new HexGridChild { Value = v }; }
		public Entity Value;


		public static bool operator ==(HexGridChild lhs, HexGridChild rhs)
		{
			if (lhs.Value == rhs.Value)
			{
				return true;
			}

			return false;
		}

		public static bool operator !=(HexGridChild lhs, HexGridChild rhs)
		{
			return !(lhs == rhs);
		}

		public static bool operator ==(HexGridChild lhs, Entity rhs)
		{
			if (lhs.Value == rhs)
			{
				return true;
			}

			return false;
		}

		public static bool operator !=(HexGridChild lhs, Entity rhs)
		{
			return !(lhs == rhs);
		}

		public override bool Equals(object compare)
		{
			return this == (Entity)compare;
		}

		public override int GetHashCode()
		{
			return Value.GetHashCode();
		}

		public override string ToString()
		{
			return Value.ToString();
		}
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
			float3 position = Position;
			position.y = elevation * HexFunctions.elevationStep;
			position.y += (HexFunctions.SampleNoise(HexFunctions.noiseColours, position, wrapSize).y * 2f - 1f) * HexFunctions.elevationPerturbStrength;
			Position = position;
		}


		public void RefreshPosition(NativeArray<float4> noiseColours)
		{
			float3 position = Position;
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
		public static HexCell GetNeighbour(int cellIndex, NativeArray<HexCell> cells, HexDirection direction)
		{
			HexCell cell = cells[cellIndex];
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
		public static HexCell GetNeighbour(HexCell cell, DynamicBuffer<HexCell> cells, HexDirection direction)
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
		public static HexCell GetNeighbour(int cellIndex, DynamicBuffer<HexCell> cells, HexDirection direction)
		{
			HexCell cell = cells[cellIndex];
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
					HexCell neighbour = GetNeighbour(cell, cells, direction);
					switch ((bool)neighbour)
					{
						case true:
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
									cells[neighbour.Index] = neighbour;
									break;
							}
							break;
					}
					break;
			}
			return cell;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexCell SetOutgoingRiver(DynamicBuffer<HexCell> cells, HexCell cell, HexDirection direction)
		{
			switch (cell.hasOutgoingRiver && cell.OutgoingRiver == direction)
			{
				case false:
					HexCell neighbour = GetNeighbour(cell,cells, direction);
					switch ((bool)neighbour)
					{
						case true:
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
									cells[neighbour.Index] = neighbour;
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
					HexCell neighbor = GetNeighbour(cell, cells, cell.outgoingRiver);
					neighbor.hasIncomingRiver = false;
					cells[cell.Index] = cell;
					cells[neighbor.Index] = neighbor;
					break;
			}
			return cell;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexCell RemoveOutgoingRiver(DynamicBuffer<HexCell> cells, HexCell cell)
		{
			switch (cell.hasOutgoingRiver)
			{
				case true:
					cell.hasOutgoingRiver = false;
					HexCell neighbor = GetNeighbour(cell, cells, cell.outgoingRiver);
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
					HexCell neighbor = GetNeighbour(cell, cells, cell.outgoingRiver);
					neighbor.hasOutgoingRiver = false;
					cells[cell.Index] = cell;
					cells[neighbor.Index] = neighbor;
					break;
			}
			return cell;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexCell RemoveIncomingRiver(DynamicBuffer<HexCell> cells, HexCell cell)
		{
			switch (cell.hasIncomingRiver)
			{
				case true:
					cell.hasOutgoingRiver = false;
					HexCell neighbor = GetNeighbour(cell, cells, cell.outgoingRiver);
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

			if (cell.hasOutgoingRiver && !IsValidRiverDestination(cell,GetNeighbour(cell, cells, cell.outgoingRiver)))
			{
				cell = RemoveOutgoingRiver(cells, cell);
			}
			if (cell.hasIncomingRiver && !IsValidRiverDestination(GetNeighbour(cell, cells, cell.outgoingRiver), cell))
			{
				cell = RemoveIncomingRiver(cells, cell);
			}
			cells[cell.Index] = cell;
			return cell;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexCell ValidateRivers(DynamicBuffer<HexCell> cells, HexCell cell)
		{

			if (cell.hasOutgoingRiver && !IsValidRiverDestination(cell, GetNeighbour(cell, cells, cell.outgoingRiver)))
			{
				cell = RemoveOutgoingRiver(cells, cell);
			}
			if (cell.hasIncomingRiver && !IsValidRiverDestination(GetNeighbour(cell, cells, cell.outgoingRiver), cell))
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

		//public static implicit operator null(HexCell v) { return HexCell.Null; }

		public static implicit operator bool (HexCell c)=> c.Index != int.MinValue;

		public static bool operator ==(HexCell lhs, HexCell rhs)=> lhs.Index == rhs.Index;

		public static bool operator !=(HexCell lhs, HexCell rhs)=> !(lhs == rhs);
		
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
		public static HexCell SetRoad(DynamicBuffer<HexCell> cells, HexCell cell, HexDirection direction, bool state = true)
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


		public static bool IsValidDestination(HexCell cell)
		{
			//return cell.IsExplored && !cell.IsUnderwater;
			return !cell.IsUnderwater;
		}


		public static int GetMoveCost(HexCell fromCell, HexCell toCell, HexDirection direction)
		{
			HexEdgeType edgeType = GetEdgeType(fromCell,toCell);
			if (edgeType == HexEdgeType.Cliff)
			{
				return -1;
			}
			int moveCost;
			if (HasRoadThroughEdge(fromCell, direction))
			{
				moveCost = 1;
			}
			else if (fromCell.Walled != toCell.Walled)
			{
				return -1;
			}
			else
			{
				moveCost = edgeType == HexEdgeType.Flat ? 5 : 10;
				moveCost += toCell.urbanLevel + toCell.farmLevel + toCell.plantLevel;
			}
			return moveCost;
		}


		public override int GetHashCode()
        {
            return -2134847229 + Index.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
    }


	public struct FeatureGridInfo : IComponentData
	{
		public Entity GridEntity;
	}

	public struct CellContainer : IBufferElementData
	{
		public Entity container;
		public int cellIndex;
	}

	public struct FeatureContainer : IComponentData
	{
		public Entity GridEntity;
		public Entity ChunkEntity;
	}

	public struct FeatureGridEntities : IComponentData
	{
		public Entity containerEntity;
		public Entity GridEntity;
		public Entity ChunkEntity;
	}
	[System.Serializable]
	public struct CellFeature : IBufferElementData, System.IEquatable<CellFeature>, System.IComparable<CellFeature>
	{
		public int cellIndex;
		public int featureLevelIndex;
		public int featureSubIndex;
		public FeatureType featureType;
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

        public int CompareTo(CellFeature other)
        {
            return cellIndex.CompareTo(other.cellIndex);
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public bool Equals(CellFeature other)
        {
            return other == this;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public bool IsSameFeatureInSameCell(CellFeature other)
        {
			return cellIndex == other.cellIndex && featureType == other.featureType
				&& featureLevelIndex == other.featureLevelIndex && featureSubIndex == other.featureSubIndex;
		}


		public static bool operator ==(CellFeature lhs, CellFeature rhs)
		{
			return lhs.cellIndex == rhs.cellIndex && lhs.featureType == rhs.featureType
				&& lhs.featureLevelIndex == rhs.featureLevelIndex && lhs.featureSubIndex == rhs.featureSubIndex 
				&& lhs.position.Equals(rhs.position) && lhs.direction.Equals(rhs.direction);
		}

		public static bool operator !=(CellFeature lhs, CellFeature rhs)
		{
			return !(lhs == rhs);
		}

	}

	public struct Feature : IBufferElementData
	{
		public int cellIndex;
		public int featureLevelIndex;
		public int featureSubIndex;
		public FeatureType featureType;
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
		public FeatureType ReservedFor;
	}

	public struct RefreshCellFeatures : IComponentData { }

	public struct ProcessFeatures : IComponentData { }

	public enum FeatureType
	{
		None,
		WallTower,
		Bridge,
		Special,
		Urban,
		Farm,
		Plant
	}

	public struct HexCellShaderDataComponent : IComponentData
	{
		public Entity grid;
		public bool ImmidateMode;
	}
	public struct ActiveGridEntity : IComponentData { }
	public struct MakeActiveGridEntity : IComponentData { }
	public struct SetFeatureVisability : IComponentData { }
	public struct InitialiseHexCellShader : IComponentData { public int x, z; public Entity grid; }
	public struct HexCellShaderRunUpdateLoop : IComponentData { }
	public struct HexCellShaderRefreshAll : IComponentData { }
	public struct NeedsVisibilityReset : IComponentData { }
	public struct HexCellTextureDataBuffer : IBufferElementData
	{
		public static implicit operator Color32(HexCellTextureDataBuffer v) { return v.Value; }
		public static implicit operator HexCellTextureDataBuffer(Color32 v) { return new HexCellTextureDataBuffer { Value = v }; }
		public Color32 Value;
	}
	public struct HexCellTransitioningCells : IBufferElementData
	{
		public static implicit operator int(HexCellTransitioningCells v) { return v.Value; }
		public static implicit operator HexCellTransitioningCells(int v) { return new HexCellTransitioningCells { Value = v }; }
		public int Value;
	}


	public struct HexUnitComp : IComponentData
    {
		public Entity GridEntity;
		public float travelSpeed;
		public int Speed;
		public float rotationSpeed;
		public int visionRange;

		public float orientation;
		public Entity Self;
	}
	public struct HexUnitLocation : IComponentData
	{
		public static implicit operator HexCell(HexUnitLocation v) { return v.Cell; }
		public static implicit operator HexUnitLocation(HexCell v) { return new HexUnitLocation { Cell = v }; }
		public HexCell Cell;
    }
	public struct HexUnitCurrentTravelLocation : IComponentData
	{
		public static implicit operator HexCell(HexUnitCurrentTravelLocation v) { return v.Cell; }
		public static implicit operator HexUnitCurrentTravelLocation(HexCell v) { return new HexUnitCurrentTravelLocation { Cell = v }; }
		public HexCell Cell;
	}
	public struct HexPath : IBufferElementData
	{
		public static implicit operator HexCell(HexPath v) { return v.Cell; }
		public static implicit operator HexPath(HexCell v) { return new HexPath { Cell = v }; }
		public HexCell Cell;
	}

	public struct HexUnitPathTo : IComponentData
	{
		public static implicit operator HexCell(HexUnitPathTo v) { return v.Cell; }
		public static implicit operator HexUnitPathTo(HexCell v) { return new HexUnitPathTo { Cell = v }; }
		public HexCell Cell;
	}

	public struct HexFromCell : IComponentData
    {
		public static implicit operator HexCell(HexFromCell v) { return v.Cell; }
		public static implicit operator HexFromCell(HexCell v) { return new HexFromCell { Cell = v }; }
		public HexCell Cell;
	}
	public struct HexToCell : IComponentData
	{
		public static implicit operator HexCell(HexToCell v) { return v.Cell; }
		public static implicit operator HexToCell(HexCell v) { return new HexToCell { Cell = v }; }
		public HexCell Cell;
	}
	public struct FindPath : IComponentData
	{
		public static implicit operator PathFindingOptions(FindPath v) { return v.Options; }
		public static implicit operator FindPath(PathFindingOptions v) { return new FindPath { Options = v }; }
		public PathFindingOptions Options;
	}

	public struct PathFindingOptions
	{
		public static implicit operator PathFindingOptions(HexUnitComp v) 
		{
			return new PathFindingOptions
			{
				GridEntity = v.GridEntity,
				travelSpeed = v.travelSpeed,
				Speed = v.Speed,
				rotationSpeed = v.rotationSpeed,
				visionRange = v.visionRange,
				orientation = v.orientation,
			};
		}
		//public static implicit operator HexUnitComp(PathFindingOptions v)
		//{ 
		//	return new HexUnitComp 
		//	{ 
		//		Options = v 
		//	}; 
		//}
		public Entity GridEntity;
		public float travelSpeed;
		public int Speed;
		public float rotationSpeed;
		public int visionRange;

		public float orientation;

		public int searchFrontierPhase;
	}

	public struct FoundPath : IComponentData { }
	public struct NotFoundPath : IComponentData { }
}