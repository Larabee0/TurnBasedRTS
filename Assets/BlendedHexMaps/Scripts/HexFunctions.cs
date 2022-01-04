using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using Unity.Jobs;

namespace DOTSHexagonsV2
{
	public enum HexDirection
	{
		NE,
		E,
		SE,
		SW,
		W,
		NW
	}

	public enum HexEdgeType
	{
		Flat,
		Slope,
		Cliff
	}

	public enum HemiSphereMode
	{
		Both,
		North,
		South
	}

	enum OptionalToggle
	{
		Ignore,
		Yes,
		No
	}

	public static class HexDirectionExtensions
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexDirection Opposite(this HexDirection direction)
		{
			return (int)direction < 3 ? (direction + 3) : (direction - 3);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexDirection Previous(this HexDirection direction)
		{
			return direction == HexDirection.NE ? HexDirection.NW : (direction - 1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexDirection Next(this HexDirection direction)
		{
			return direction == HexDirection.NW ? HexDirection.NE : (direction + 1);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexDirection Previous2(this HexDirection direction)
		{
			direction -= 2;
			return direction >= HexDirection.NE ? direction : (direction + 6);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexDirection Next2(this HexDirection direction)
		{
			direction += 2;
			return direction <= HexDirection.NW ? direction : (direction - 6);
		}
	}

	public static class HexFunctions
	{
		public const float bridgeDesignLength = 7f;

		public const float wallYOffset = -1f;

		public const float wallTowerThreshold = 0.5f;

		public const float wallElevationOffset = verticalTerraceStepSize;

		public const float wallThickness = 0.75f;

		public const float wallHeight = 4f;

		public const float waterBlendFactor = 1f - waterFactor;

		public const float waterFactor = 0.6f;

		public const float waterElevationOffset = -0.5f;

		public const float streamBedElevationOffset = -1.75f;

		public const int chunkSizeX = 4, chunkSizeZ = 4;

		public static Texture2D noiseSource;

		public static Texture2D cellTexture;

		public static NativeArray<float4> noiseColours;

		public const float cellPerturbStrength = 4f;

		public const float elevationPerturbStrength = 1.5f;

		public const float noiseScale = 0.003f;

		public const float elevationStep = 3f;

		public const int terracesPerSlope = 2;

		public const int terraceSteps = terracesPerSlope * 2 + 1;

		public const float horizontalTerraceStepSize = 1f / terraceSteps;

		public const float verticalTerraceStepSize = 1f / (terracesPerSlope + 1);

		public const float outerToInner = 0.866025404f;

		public const float innerToOuter = 1f / outerToInner;

		public const float outerRadius = 10f;

		public const float innerRadius = outerRadius * outerToInner;

		public const float innerDiameter = innerRadius * 2f;

		public const float solidFactor = 0.8f;

		public const float blendFactor = 1f - solidFactor;

		public static void SetNoiseColours()
		{
			Color[] coloursTemp = noiseSource.GetPixels(0);
			NativeArray<float4> noiseColours = new NativeArray<float4>(coloursTemp.Length, Allocator.Temp);
			for (int i = 0; i < coloursTemp.Length; i++)
			{
				Color color = coloursTemp[i];
				noiseColours[i] = new float4(color.r, color.g, color.b, color.a);
			}
			HexFunctions.noiseColours = new NativeArray<float4>(noiseColours, Allocator.Persistent);
			noiseColours.Dispose();
		}

		public static void CleanUpNoiseColours()
		{
			try
			{
				noiseColours.Dispose();
			}
			catch { }
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 GetFirstSolidCorner(HexDirection direction)
		{
			return corners[(int)direction] * solidFactor;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 GetSecondSolidCorner(HexDirection direction)
		{
			return corners[(int)direction + 1] * solidFactor;
		}

		private static readonly float3[] corners = {
			new float3(0f, 0f, outerRadius),
			new float3(innerRadius, 0f, 0.5f * outerRadius),
			new float3(innerRadius, 0f, -0.5f * outerRadius),
			new float3(0f, 0f, -outerRadius),
			new float3(-innerRadius, 0f, -0.5f * outerRadius),
			new float3(-innerRadius, 0f, 0.5f * outerRadius),
			new float3(0f, 0f, outerRadius)
		};

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 GetFirstCorner(HexDirection direction)
		{
			return corners[(int)direction];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 GetSecondCorner(HexDirection direction)
		{
			return corners[(int)direction + 1];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 GetBridge(HexDirection direction)
		{
			return (corners[(int)direction] + corners[(int)direction + 1]) * blendFactor;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 TerraceLerp(float3 a, float3 b, int step)
		{
			float h = step * horizontalTerraceStepSize;
			a.x += (b.x - a.x) * h;
			a.z += (b.z - a.z) * h;
			float v = ((step + 1) / 2) * verticalTerraceStepSize;
			a.y += (b.y - a.y) * v;
			return a;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float4 TerraceLerp(float4 a, float4 b, int step)
		{
			float h = step * horizontalTerraceStepSize;
			return math.lerp(a, b, h);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexEdgeType GetEdgeType(int elevation1, int elevation2)
		{
			if (elevation1 == elevation2)
			{
				return HexEdgeType.Flat;
			}
			int delta = elevation2 - elevation1;
			if (delta == 1 || delta == -1)
			{
				return HexEdgeType.Slope;
			}
			return HexEdgeType.Cliff;
		}

		public static float4 SampleNoise(NativeArray<float4> noise, float3 position, int wrapSize)
		{
			float4 sample = BillinearInterPolation(noise, position.x * noiseScale, position.z * noiseScale);
			if ((wrapSize > 0) && (position.x < innerDiameter * 1.5f))
			{
				float4 sample2 = BillinearInterPolation(noise, (position.x + wrapSize * innerDiameter) * noiseScale, position.z * noiseScale);
				sample = LerpForPerturb(sample2, sample, position.x * (1f / innerDiameter) - 0.5f);
			}
			return sample;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float4 LerpForPerturb(float4 a, float4 b, float t)
		{
			t = Clamp01(t);
			return new float4
			{
				x = a.x + (b.x - a.x) * t,
				y = a.y + (b.y - a.y) * t,
				z = a.z + (b.z - a.z) * t,
				w = a.w + (b.w - a.w) * t,
			};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Clamp01(float value)
		{
			switch (value < 0f)
			{
				case true:
					return 0f;
			};
			switch (value > 1f)
			{
				case true:
					return 1f;
			};
			return value;
		}

		private static float4 BillinearInterPolation(NativeArray<float4> noiseColours, float u, float v)
		{
			u = u > 0 ? u % 1 : 1 + (u % 1);
			v = v > 0 ? v % 1 : 1 + (v % 1);
			float pixelXCoordinate = u * 512f - 0.5f;
			float pixelYCoordinate = (1f - v) * 512f - 0.5f;

			pixelXCoordinate = pixelXCoordinate < 0 ? 512 - pixelXCoordinate : pixelXCoordinate;
			pixelYCoordinate = pixelYCoordinate < 0 ? 512 - pixelYCoordinate : pixelYCoordinate;

			int x = (int)math.floor(pixelXCoordinate);
			int y = (int)math.floor(pixelYCoordinate);

			float pX = pixelXCoordinate - x;
			float pY = pixelYCoordinate - y;

			float2 px = new float2((float)(1 - pX), (float)pX);
			float2 py = new float2((float)(1 - pY), (float)pY);

			float red = 0;
			float green = 0;
			float blue = 0;
			float alpha = 0;

			for (int i = 0; i < 2; i++)
			{
				for (int j = 0; j < 2; j++)
				{
					float p = px[i] * py[j];
					if (p != 0)
					{
						int2 coordinates = new int2
						{
							x = (x + i) % 512,
							y = (y + j) % 512
						};
						float4 Out = noiseColours[GetIndexFromXY(coordinates)];
						red += Out.x * p;
						green += Out.y * p;
						blue += Out.z * p;
						alpha += Out.w * p;
					}
				}
			}
			return new float4(red, green, blue, alpha);
		}
		private static int GetIndexFromXY(int2 xy)
		{
			xy = math.abs(xy);
			int pixelRowLength = 512;
			int adder = math.clamp(xy.y, 0, 262143) * pixelRowLength;

			return math.clamp(xy.x + adder, 0, 262143);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 GetSolidEdgeMiddle(HexDirection direction)
		{
			return (corners[(int)direction] + corners[(int)direction + 1]) * (0.5f * solidFactor);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 Perturb(NativeArray<float4> noise, float3 position, int wrapSize)
		{

			float4 sample = SampleNoise(noise, position, wrapSize);
			position.x += (sample.x * 2f - 1f) * cellPerturbStrength;
			position.z += (sample.z * 2f - 1f) * cellPerturbStrength;
			return position;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 GetFirstWaterCorner(HexDirection direction)
		{
			return corners[(int)direction] * waterFactor;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 GetSecondWaterCorner(HexDirection direction)
		{
			return corners[(int)direction + 1] * waterFactor;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 GetWaterBridge(HexDirection direction)
		{
			return (corners[(int)direction] + corners[(int)direction + 1]) * waterBlendFactor;
		}

		public const int hashGridSize = 256;
		public const float hashGridScale = 0.25f;
		public static NativeArray<HexHash> InitializeHashGrid(uint seed)
		{
			NativeArray<HexHash> hashGrid = new NativeArray<HexHash>(hashGridSize * hashGridSize, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
			Unity.Mathematics.Random randomNumberGenerator = new Unity.Mathematics.Random(seed);
			HexHash.Create(hashGrid, randomNumberGenerator);

			return hashGrid;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexHash SampleHashGrid(DynamicBuffer<HexHash> hashGrid, float3 position)
		{
			position *= hashGridScale;
			int x = (int)position.x % hashGridSize;
			if (x < 0)
			{
				x += hashGridSize;
			}
			int z = (int)position.z % hashGridSize;
			if (z < 0)
			{
				z += hashGridSize;
			}
			return hashGrid[x + z * hashGridSize];
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static HexHash SampleHashGrid(NativeArray<HexHash> hashGrid, float3 position)
		{
			position *= hashGridScale;
			int x = (int)position.x % hashGridSize;
			if (x < 0)
			{
				x += hashGridSize;
			}
			int z = (int)position.z % hashGridSize;
			if (z < 0)
			{
				z += hashGridSize;
			}
			return hashGrid[x + z * hashGridSize];
		}

		private static readonly FeatureThresholdContainer[] featureThresholds =
		{
			new FeatureThresholdContainer{ a = 0.0f, b = 0.0f, c = 0.4f },
			new FeatureThresholdContainer{ a = 0.0f, b = 0.4f, c = 0.6f },
			new FeatureThresholdContainer{ a = 0.4f, b = 0.6f, c = 0.8f }
		};

		public static FeatureThresholdContainer GetFeatureThresholds(int level)
		{
			return featureThresholds[level];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 WallThicknessOffset(float3 near, float3 far)
		{
			return math.normalize(new float3(far.x - near.x, 0, far.z - near.z)) * (wallThickness * 0.5f);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float3 WallLerp(float3 near, float3 far)
		{
			near.x += (far.x - near.x) * 0.5f;
			near.z += (far.z - near.z) * 0.5f;
			float v = near.y < far.y ? wallElevationOffset : (1f - wallElevationOffset);
			near.y += (far.y - near.y) * v + wallYOffset;
			return near;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static quaternion FromToRotation(float3 aFrom, float3 aTo)
		{
			float3 axis = math.cross(aFrom, aTo);
			float angle = ExtraTurretMathsFunctions.Angle(aFrom, aTo);
			return quaternion.AxisAngle(math.normalize(axis), angle);
		}

		
	}

	public static class Bezier
	{
		public static Vector3 GetPoint(float3 a, float3 b, float3 c, float t)
		{
			float r = 1f - t;
			return r * r * a + 2f * r * t * b + t * t * c;
		}

		public static Vector3 GetDerivative(float3 a, float3 b, float3 c, float t)
		{
			return 2f * ((1f - t) * (b - a) + t * (c - b));
		}
	}

	public struct HexHash : IBufferElementData
	{
		public float a;
		public float b;
		public float c;
		public float d;
		public float e;
		public static void Create(NativeArray<HexHash> hashGrid, Unity.Mathematics.Random randomNumberGenerator)
		{
			for (int i = 0; i < hashGrid.Length; i++)
			{
				hashGrid[i] = new HexHash
				{
					a = randomNumberGenerator.NextFloat() * 0.999f,
					b = randomNumberGenerator.NextFloat() * 0.999f,
					c = randomNumberGenerator.NextFloat() * 0.999f,
					d = randomNumberGenerator.NextFloat() * 0.999f,
					e = randomNumberGenerator.NextFloat() * 0.999f
				};
			}
		}
	}

	public struct FeatureThresholdContainer
	{
		public float a;
		public float b;
		public float c;
		public float GetLevel(int input)
		{
			return input switch
			{
				0 => a,
				1 => b,
				2 => c,
				_ => a,
			};
		}
	}


	public struct HexCellPriorityQueue : INativeDisposable
	{
		public static HexCellPriorityQueue Null { get; set; }
		private NativeList<int> list;
		public NativeArray<HexCellQueueElement> elements;
		private int count;
		private int minimum;
		public int searchPhase;
		private Allocator allocatedWith;
		public Allocator AllocatedWith { get { return allocatedWith; } }

		public HexCellPriorityQueue(NativeArray<HexCellQueueElement> cellsIn, Allocator allocator = Allocator.Temp)
		{
			elements = cellsIn;
			list = new NativeList<int>(cellsIn.Length, allocator);
			count = 0;
			minimum = int.MaxValue;
			searchPhase = 0;
			allocatedWith = allocator;
		}

		public void Reallocate(Allocator As)
		{
			NativeArray<int> temp = list.ToArray(Allocator.Temp);
			NativeArray<HexCellQueueElement> elementsTemp = new NativeArray<HexCellQueueElement>(elements, Allocator.Temp);
			list.Dispose();
			elements.Dispose();
			list = new NativeList<int>(temp.Length, As);
			list.CopyFrom(temp);
			elements = new NativeArray<HexCellQueueElement>(elementsTemp, As);
			temp.Dispose();
			elementsTemp.Dispose();
			allocatedWith = As;
		}

		public void SetElements(NativeArray<HexCellQueueElement> cellsIn, Allocator allocator = Allocator.Temp)
		{
			try
			{
				elements.Dispose();
			}
			catch { }
			try
			{
				list.Dispose();
			}
			catch { }
			elements = cellsIn;
			list = new NativeList<int>(cellsIn.Length, allocator);
		}

		public int Count
		{
			get
			{
				return count;
			}
		}

		public void Enqueue(HexCellQueueElement cell)
		{
			count += 1;
			int priority = cell.SearchPriority;
			if (priority < minimum)
			{
				minimum = priority;
			}
			if (priority > list.Capacity)
			{
				list.Capacity = priority + 1;
			}
			while (priority >= list.Length)
			{
				list.Add(int.MinValue);
			}

			cell.NextWithSamePriority = list[priority];
			elements[cell.cellIndex] = cell;
			list[priority] = cell.cellIndex;
		}

		public int DequeueIndex()
		{
			count -= 1;
			for (; minimum < list.Length; minimum++)
			{
				int potentialCell = list[minimum];
				if (potentialCell != int.MinValue)
				{
					list[minimum] = elements[potentialCell].NextWithSamePriority;
					return potentialCell;
				}
			}
			return int.MinValue;
		}

		public HexCellQueueElement DequeueElement()
		{
			count -= 1;
			for (; minimum < list.Length; minimum++)
			{
				int potentialCell = list[minimum];
				if (potentialCell != int.MinValue)
				{
					list[minimum] = elements[potentialCell].NextWithSamePriority;
					return elements[potentialCell];
				}
			}
			return HexCellQueueElement.Null;
		}

		public void Change(HexCellQueueElement cell, int oldPriority)
		{
			elements[cell.cellIndex] = cell;
			int current = list[oldPriority];
			int next = elements[current].NextWithSamePriority;

			if (current == cell.cellIndex)
			{
				list[oldPriority] = next;
			}
			else
			{
				while (next != cell.cellIndex)
				{
					current = next;
					next = elements[current].NextWithSamePriority;
				}
				HexCellQueueElement currentElement = elements[current];
				currentElement.NextWithSamePriority = cell.NextWithSamePriority;
				elements[current] = currentElement;
				Enqueue(cell);
				count -= 1;
			}
		}

		public void Clear()
		{
			list.Clear();
			count = 0;
			minimum = int.MaxValue;
		}

		public JobHandle Dispose(JobHandle inputDeps)
		{
			return elements.Dispose(list.Dispose(inputDeps));
		}

		public void Dispose()
		{
			elements.Dispose();
			list.Dispose();
		}
	}

	public struct HexCellQueueElement : IBufferElementData
	{
		public static readonly HexCellQueueElement Null = new HexCellQueueElement { cellIndex = int.MinValue, NextWithSamePriority = int.MinValue, SearchPhase = int.MinValue, PathFrom = int.MinValue };
		public int cellIndex;
		public int SearchPriority { get { return Distance + SearchHeuristic; } }
		public int NextWithSamePriority;
		public int SearchPhase;
		public int Distance;
		public int SearchHeuristic;
		public int PathFrom;

		public static bool operator ==(HexCellQueueElement lhs, HexCellQueueElement rhs)
        {
			return lhs.cellIndex == rhs.cellIndex;
        }

		public static bool operator !=(HexCellQueueElement lhs, HexCellQueueElement rhs)
		{
			return !(lhs == rhs);
		}


		public bool Equals(HexCellQueueElement other)
		{
			return other == this;
		}

		public override bool Equals(object obj)
		{
			return base.Equals(obj);
		}

        public override int GetHashCode()
        {
			return cellIndex.GetHashCode();
        }
    }

	public struct HexCoordinates
	{
		private int x, z;
		public int X
		{
			get
			{
				return x;
			}
		}

		public int Z
		{
			get
			{
				return z;
			}
		}

		public HexCoordinates(int x, int z, int wrapSize = 0)
		{
			if (wrapSize > 0)
			{
				int oX = x + z / 2;
				if (oX < 0)
				{
					x += wrapSize;
				}
				else if (oX >= wrapSize)
				{
					x -= wrapSize;
				}
			}
			this.x = x;
			this.z = z;
		}

		public static HexCoordinates FromOffsetCoordinates(int x, int z, int wrapSize = 0)
		{
			return new HexCoordinates(x - z / 2, z, wrapSize);
		}
		public static HexCoordinates FromPosition(float3 position, int wrapSize = 0)
		{
			float x = position.x / HexFunctions.innerDiameter;
			float y = -x;
			float offset = position.z / (HexFunctions.outerRadius * 3f);
			x -= offset;
			y -= offset;
			int iX = (int)math.round(x);
			int iY = (int)math.round(y);
			int iZ = (int)math.round(-x - y);
			if (iX + iY + iZ != 0)
			{
				float dX = math.abs(x - iX);
				float dY = math.abs(y - iY);
				float dZ = math.abs(-x - y - iZ);

				if (dX > dY && dX > dZ)
				{
					iX = -iY - iZ;
				}
				else if (dZ > dY)
				{
					iZ = -iX - iY;
				}
			}
			return new HexCoordinates(iX, iZ, wrapSize);
		}

		public int Y
		{
			get
			{
				return -X - Z;
			}
		}
		public override string ToString()
		{
			return "(" + X.ToString() + ", " + Y.ToString() + ", " + Z.ToString() + ")";
		}

		public string ToStringOnSeparateLines()
		{
			return X.ToString() + "\n" + Y.ToString() + "\n" + Z.ToString();
		}

		public int DistanceTo(HexCoordinates other, int wrapSize = 0)
		{
			int xy = (x < other.x ? other.x - x : x - other.x) + (Y < other.Y ? other.Y - Y : Y - other.Y);
			if (wrapSize > 0)
			{
				other.x += wrapSize;
				int xyWrapped = (x < other.x ? other.x - x : x - other.x) + (Y < other.Y ? other.Y - Y : Y - other.Y);
				if (xyWrapped < xy)
				{
					xy = xyWrapped;
				}
				else
				{
					other.x -= 2 * wrapSize;
					xyWrapped = (x < other.x ? other.x - x : x - other.x) + (Y < other.Y ? other.Y - Y : Y - other.Y);
					if (xyWrapped < xy)
					{
						xy = xyWrapped;
					}
				}
			}
			return (xy + (z < other.z ? other.z - z : z - other.z)) / 2;
		}
	}

	public struct EdgeVertices
	{
		public float3 v1;
		public float3 v2;
		public float3 v3;
		public float3 v4;
		public float3 v5;

		public EdgeVertices(float3 corner1, float3 corner2)
		{
			v1 = corner1;
			v2 = math.lerp(corner1, corner2, 0.25f);
			v3 = math.lerp(corner1, corner2, 0.5f);
			v4 = math.lerp(corner1, corner2, 0.75f);
			v5 = corner2;
		}

		public EdgeVertices(Vector3 corner1, Vector3 corner2, float outerStep)
		{
			v1 = corner1;
			v2 = math.lerp(corner1, corner2, outerStep);
			v3 = math.lerp(corner1, corner2, 0.5f);
			v4 = math.lerp(corner1, corner2, 1f - outerStep);
			v5 = corner2;
		}

		public static EdgeVertices TerraceLerp(EdgeVertices a, EdgeVertices b, int step)
		{
			EdgeVertices result;
			result.v1 = HexFunctions.TerraceLerp(a.v1, b.v1, step);
			result.v2 = HexFunctions.TerraceLerp(a.v2, b.v2, step);
			result.v3 = HexFunctions.TerraceLerp(a.v3, b.v3, step);
			result.v4 = HexFunctions.TerraceLerp(a.v4, b.v4, step);
			result.v5 = HexFunctions.TerraceLerp(a.v5, b.v5, step);
			return result;
		}
	}
}