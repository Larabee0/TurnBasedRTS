using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace GameObjectHexagons
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
		public static HexDirection Opposite(this HexDirection direction)
		{
			return (int)direction < 3 ? (direction + 3) : (direction - 3);
		}
		public static HexDirection Previous(this HexDirection direction)
		{
			return direction == HexDirection.NE ? HexDirection.NW : (direction - 1);
		}

		public static HexDirection Next(this HexDirection direction)
		{
			return direction == HexDirection.NW ? HexDirection.NE : (direction + 1);
		}

		public static HexDirection Previous2(this HexDirection direction)
		{
			direction -= 2;
			return direction >= HexDirection.NE ? direction : (direction + 6);
		}

		public static HexDirection Next2(this HexDirection direction)
		{
			direction += 2;
			return direction <= HexDirection.NW ? direction : (direction - 6);
		}
	}

	public static class HexMetrics
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

		public const int chunkSizeX = 5, chunkSizeZ = 5;

		public static Texture2D noiseSource;

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

		public static Vector3 GetFirstSolidCorner(HexDirection direction)
		{
			return corners[(int)direction] * solidFactor;
		}

		public static Vector3 GetSecondSolidCorner(HexDirection direction)
		{
			return corners[(int)direction + 1] * solidFactor;
		}

		static readonly Vector3[] corners = {
			new Vector3(0f, 0f, outerRadius),
			new Vector3(innerRadius, 0f, 0.5f * outerRadius),
			new Vector3(innerRadius, 0f, -0.5f * outerRadius),
			new Vector3(0f, 0f, -outerRadius),
			new Vector3(-innerRadius, 0f, -0.5f * outerRadius),
			new Vector3(-innerRadius, 0f, 0.5f * outerRadius),
			new Vector3(0f, 0f, outerRadius)
		};
		public static Vector3 GetFirstCorner(HexDirection direction)
		{
			return corners[(int)direction];
		}

		public static Vector3 GetSecondCorner(HexDirection direction)
		{
			return corners[(int)direction + 1];
		}
		public static Vector3 GetBridge(HexDirection direction)
		{
			return (corners[(int)direction] + corners[(int)direction + 1]) * blendFactor;
		}

		public static Vector3 TerraceLerp(Vector3 a, Vector3 b, int step)
		{
			float h = step * horizontalTerraceStepSize;
			a.x += (b.x - a.x) * h;
			a.z += (b.z - a.z) * h;
			float v = ((step + 1) / 2) * verticalTerraceStepSize;
			a.y += (b.y - a.y) * v;
			return a;
		}

		public static Color TerraceLerp(Color a, Color b, int step)
		{
			float h = step * horizontalTerraceStepSize;
			return Color.Lerp(a, b, h);
		}

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

		public static Vector4 SampleNoise(Vector3 position)
		{
			Vector4 sample = noiseSource.GetPixelBilinear(position.x * noiseScale, position.z * noiseScale);
			if (Wrapping && position.x < innerDiameter * 1.5f)
			{
				Vector4 sample2 = noiseSource.GetPixelBilinear((position.x + wrapSize * innerDiameter) * noiseScale, position.z * noiseScale);
				sample = Vector4.Lerp(sample2, sample, position.x * (1f / innerDiameter) - 0.5f);
			}
			return sample;
		}

		public static Vector3 GetSolidEdgeMiddle(HexDirection direction)
		{
			return
				(corners[(int)direction] + corners[(int)direction + 1]) *
				(0.5f * solidFactor);
		}

		public static Vector3 Perturb(Vector3 position)
		{
			Vector4 sample = SampleNoise(position);
			position.x += (sample.x * 2f - 1f) * cellPerturbStrength;
			position.z += (sample.z * 2f - 1f) * cellPerturbStrength;
			return position;
		}

		public static Vector3 GetFirstWaterCorner(HexDirection direction)
		{
			return corners[(int)direction] * waterFactor;
		}

		public static Vector3 GetSecondWaterCorner(HexDirection direction)
		{
			return corners[(int)direction + 1] * waterFactor;
		}
		public static Vector3 GetWaterBridge(HexDirection direction)
		{
			return (corners[(int)direction] + corners[(int)direction + 1]) *
				waterBlendFactor;
		}


		public const int hashGridSize = 256;
		public const float hashGridScale = 0.25f;
		static HexHash[] hashGrid;
		public static void InitializeHashGrid(int seed)
		{
			hashGrid = new HexHash[hashGridSize * hashGridSize];
			Random.State currentState = Random.state;
			Random.InitState(seed);

			for (int i = 0; i < hashGrid.Length; i++)
			{
				hashGrid[i] = HexHash.Create();
			}
			Random.state = currentState;
		}

		public static HexHash SampleHashGrid(Vector3 position)
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

		static FeatureThresholdContainer[] featureThresholds =
		{
			new FeatureThresholdContainer{ a = 0.0f, b = 0.0f, c = 0.4f },
			new FeatureThresholdContainer{ a = 0.0f, b = 0.4f, c = 0.6f },
			new FeatureThresholdContainer{ a = 0.4f, b = 0.6f, c = 0.8f }
		};

		public static FeatureThresholdContainer GetFeatureThresholds(int level)
		{
			return featureThresholds[level];

		}

		public static Vector3 WallThicknessOffset(Vector3 near, Vector3 far)
		{
			return new Vector3(far.x - near.x, 0, far.z - near.z).normalized * (wallThickness * 0.5f);
		}

		public static Vector3 WallLerp(Vector3 near, Vector3 far)
		{
			near.x += (far.x - near.x) * 0.5f;
			near.z += (far.z - near.z) * 0.5f;
			float v = near.y < far.y ? wallElevationOffset : (1f - wallElevationOffset);
			near.y += (far.y - near.y) * v + wallYOffset;
			return near;
		}

		public static int wrapSize;

		public static bool Wrapping
		{
			get
			{
				return wrapSize > 0;
			}
		}
	}

	public static class Bezier
	{
		public static Vector3 GetPoint(Vector3 a, Vector3 b, Vector3 c, float t)
		{
			float r = 1f - t;
			return r * r * a + 2f * r * t * b + t * t * c;
		}

		public static Vector3 GetDerivative(Vector3 a, Vector3 b, Vector3 c, float t)
		{
			return 2f * ((1f - t) * (b - a) + t * (c - b));
		}
	}

	public struct HexHash
	{
		public float a;
		public float b;
		public float c;
		public float d;
		public float e;
		public static HexHash Create()
		{
			HexHash hash;
			hash.a = Random.value * 0.999f;
			hash.b = Random.value * 0.999f;
			hash.c = Random.value * 0.999f;
			hash.d = Random.value * 0.999f;
			hash.e = Random.value * 0.999f;
			return hash;
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
		public FeatureThresholdContainer(float A, float B, float C)
		{
			a = A;
			b = B;
			c = C;
		}
	}

	public static class ListPool<T>
	{
		static Stack<List<T>> stack = new Stack<List<T>>();
		public static List<T> Get()
		{
			if (stack.Count > 0)
			{
				return stack.Pop();
			}
			return new List<T>();
		}
		public static void Add(List<T> list)
		{
			list.Clear();
			stack.Push(list);
		}
	}

	[System.Serializable]
	public struct HexFeatureCollection
	{
		public Transform[] prefabs;
		public Transform Pick(float choice)
		{
			return prefabs[(int)(choice * prefabs.Length)];
		}
	}

	[System.Serializable]
	public struct HexCoordinates
	{
		[SerializeField]
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

		public HexCoordinates(int x, int z)
		{
			if (HexMetrics.Wrapping)
			{
				int oX = x + z / 2;
				if (oX < 0)
				{
					x += HexMetrics.wrapSize;
				}
				else if (oX >= HexMetrics.wrapSize)
				{
					x -= HexMetrics.wrapSize;
				}
			}
			this.x = x;
			this.z = z;
		}

		public static HexCoordinates FromOffsetCoordinates(int x, int z)
		{
			return new HexCoordinates(x - z / 2, z);
		}

		public static HexCoordinates FromPosition(Vector3 position)
		{
			float x = position.x / HexMetrics.innerDiameter;
			float y = -x;
			float offset = position.z / (HexMetrics.outerRadius * 3f);
			x -= offset;
			y -= offset;
			int iX = Mathf.RoundToInt(x);
			int iY = Mathf.RoundToInt(y);
			int iZ = Mathf.RoundToInt(-x - y);
			if (iX + iY + iZ != 0)
			{
				float dX = Mathf.Abs(x - iX);
				float dY = Mathf.Abs(y - iY);
				float dZ = Mathf.Abs(-x - y - iZ);

				if (dX > dY && dX > dZ)
				{
					iX = -iY - iZ;
				}
				else if (dZ > dY)
				{
					iZ = -iX - iY;
				}
			}
			return new HexCoordinates(iX, iZ);
		}

		public static HexCoordinates Load(BinaryReader reader)
		{
			HexCoordinates c;
			c.x = reader.ReadInt32();
			c.z = reader.ReadInt32();
			return c;
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

		public int DistanceTo(HexCoordinates other)
		{
			int xy = (x < other.x ? other.x - x : x - other.x) + (Y < other.Y ? other.Y - Y : Y - other.Y);
			if (HexMetrics.Wrapping)
			{
				other.x += HexMetrics.wrapSize;
				int xyWrapped = (x < other.x ? other.x - x : x - other.x) + (Y < other.Y ? other.Y - Y : Y - other.Y);
				if (xyWrapped < xy)
				{
					xy = xyWrapped;
				}
				else
				{
					other.x -= 2 * HexMetrics.wrapSize;
					xyWrapped = (x < other.x ? other.x - x : x - other.x) + (Y < other.Y ? other.Y - Y : Y - other.Y);
					if (xyWrapped < xy)
					{
						xy = xyWrapped;
					}
				}
			}
			return (xy + (z < other.z ? other.z - z : z - other.z)) / 2;
		}
		public void Save(BinaryWriter writer)
		{
			writer.Write(x);
			writer.Write(z);
		}
	}

	public struct EdgeVertices
	{
		public Vector3 v1;
		public Vector3 v2;
		public Vector3 v3;
		public Vector3 v4;
		public Vector3 v5;

		public EdgeVertices(Vector3 corner1, Vector3 corner2)
		{
			v1 = corner1;
			v2 = Vector3.Lerp(corner1, corner2, 0.25f);
			v3 = Vector3.Lerp(corner1, corner2, 0.5f);
			v4 = Vector3.Lerp(corner1, corner2, 0.75f);
			v5 = corner2;
		}
		public EdgeVertices(Vector3 corner1, Vector3 corner2, float outerStep)
		{
			v1 = corner1;
			v2 = Vector3.Lerp(corner1, corner2, outerStep);
			v3 = Vector3.Lerp(corner1, corner2, 0.5f);
			v4 = Vector3.Lerp(corner1, corner2, 1f - outerStep);
			v5 = corner2;
		}

		public static EdgeVertices TerraceLerp(
		EdgeVertices a, EdgeVertices b, int step)
		{
			EdgeVertices result;
			result.v1 = HexMetrics.TerraceLerp(a.v1, b.v1, step);
			result.v2 = HexMetrics.TerraceLerp(a.v2, b.v2, step);
			result.v3 = HexMetrics.TerraceLerp(a.v3, b.v3, step);
			result.v4 = HexMetrics.TerraceLerp(a.v4, b.v4, step);
			result.v5 = HexMetrics.TerraceLerp(a.v5, b.v5, step);
			return result;
		}
	}

	public class HexCellPriorityQueue
	{
		List<HexCell> list = new List<HexCell>();

		int count = 0;
		int minimum = int.MaxValue;

		public int Count
		{
			get
			{
				return count;
			}
		}

		public void Enqueue(HexCell cell)
		{
			count += 1;
			int priority = cell.SearchPriority;
			if (priority < minimum)
			{
				minimum = priority;
			}
			while (priority >= list.Count)
			{
				list.Add(null);
			}
			cell.NextWithSamePriority = list[priority];
			list[priority] = cell;
		}

		public HexCell Dequeue()
		{
			count -= 1;
			for (; minimum < list.Count; minimum++)
			{
				HexCell cell = list[minimum];
				if (cell != null)
				{
					list[minimum] = cell.NextWithSamePriority;
					return cell;
				}
			}
			return null;
		}
		public void Change(HexCell cell, int oldPriority)
		{
			HexCell current = list[oldPriority];
			HexCell next = current.NextWithSamePriority;
			if (current == cell)
			{
				list[oldPriority] = next;
			}
			else
			{
				while (next != cell)
				{
					current = next;
					next = current.NextWithSamePriority;
				}
				current.NextWithSamePriority = cell.NextWithSamePriority;
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
	}

}
