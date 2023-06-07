using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Constant metrics and utility methods for the hex map.
/// </summary>
public static class HexMetrics
{

	/// <summary>
	/// Ratio of outer to inner radius of a hex cell.
	/// </summary>
	public const float outerToInner = 0.866025404f;

	/// <summary>
	/// Ratio of inner to outer radius of a hex cell.
	/// </summary>
	public const float innerToOuter = 1f / outerToInner;

	/// <summary>
	/// Outer radius of a hex cell.
	/// </summary>
	public const float outerRadius = 10f;

	/// <summary>
	/// Inner radius of a hex cell.
	/// </summary>
	public const float innerRadius = outerRadius * outerToInner;

	/// <summary>
	/// Inner diameter of a hex cell.
	/// </summary>
	public const float innerDiameter = innerRadius * 2f;

	/// <summary>
	/// Factor of the solid uniform region inside a hex cell.
	/// </summary>
	public const float solidFactor = 0.8f;

	/// <summary>
	/// Factor of the blending region inside a hex cell.
	/// </summary>
	public const float blendFactor = 1f - solidFactor;

	/// <summary>
	/// Factor of the solid uniform water region inside a hex cell.
	/// </summary>
	public const float waterFactor = 0.6f;

	/// <summary>
	/// Factor of the water blending region inside a hex cell.
	/// </summary>
	public const float waterBlendFactor = 1f - waterFactor;

	/// <summary>
	/// Height of a single elevation step.
	/// </summary>
	public const float elevationStep = 3f;

	/// <summary>
	/// Amount of terrace levels per slope.
	/// </summary>
	public const int terracesPerSlope = 2;

	/// <summary>
	/// Amount of terraces steps per slope needed for <see cref="terracesPerSlope"/>.
	/// </summary>
	public const int terraceSteps = terracesPerSlope * 2 + 1;

	/// <summary>
	/// Amount of horizontal terrace steps per slope.
	/// </summary>
	public const float horizontalTerraceStepSize = 1f / terraceSteps;

	/// <summary>
	/// Amount of vertical terrace steps per slope.
	/// </summary>
	public const float verticalTerraceStepSize = 1f / (terracesPerSlope + 1);

	/// <summary>
	/// Strength of cell position terturbation.
	/// </summary>
	public const float cellPerturbStrength = 4f;

	/// <summary>
	/// Strength of vertical elevation perturbation.
	/// </summary>
	public const float elevationPerturbStrength = 1.5f;

	/// <summary>
	/// Offset for stream bed elevation.
	/// </summary>
	public const float streamBedElevationOffset = -1.75f;

	/// <summary>
	/// Offset for water elevation.
	/// </summary>
	public const float waterElevationOffset = -0.5f;

	/// <summary>
	/// Height of walls.
	/// </summary>
	public const float wallHeight = 4f;

	/// <summary>
	/// Vertical wall offset, negative to prevent them from floating above the surface.
	/// </summary>
	public const float wallYOffset = -1f;

	/// <summary>
	/// Wall thickness.
	/// </summary>
	public const float wallThickness = 0.75f;

	/// <summary>
	/// Wall elevation offset, matching <see cref="verticalTerraceStepSize"/>.
	/// </summary>
	public const float wallElevationOffset = verticalTerraceStepSize;

	/// <summary>
	/// Probability threshold for wall towers.
	/// </summary>
	public const float wallTowerThreshold = 0.5f;

	/// <summary>
	/// Length at which the bridge model is designed.
	/// </summary>
	public const float bridgeDesignLength = 7f;

	/// <summary>
	/// World scale of the noise.
	/// </summary>
	public const float noiseScale = 0.003f;

	/// <summary>
	/// Hex grid chunk size in the X dimension.
	/// </summary>
	public const int chunkSizeX = 4;

	/// <summary>
	/// Hex grid chunk size in the Z dimension.
	/// </summary>
	public const int chunkSizeZ = 4;

    public static NativeArray<float4> noiseColours;

    /// <summary>
    /// Size of the hash grid.
    /// </summary>
    public const int hashGridSize = 256;

	/// <summary>
	/// World scale of the hash grid.
	/// </summary>
	public const float hashGridScale = 0.25f;

	public static HexHash[] hashGrid;

	static readonly float3[] corners = {
		new float3(0f, 0f, outerRadius),
		new float3(innerRadius, 0f, 0.5f * outerRadius),
		new float3(innerRadius, 0f, -0.5f * outerRadius),
		new float3(0f, 0f, -outerRadius),
		new float3(-innerRadius, 0f, -0.5f * outerRadius),
		new float3(-innerRadius, 0f, 0.5f * outerRadius),
		new float3(0f, 0f, outerRadius)
	};

	static readonly float3[] featureThresholds = {
		new float3 (0.0f, 0.0f, 0.4f),
		new float3 (0.0f, 0.4f, 0.6f),
		new float3 (0.4f, 0.6f, 0.8f)
	};

    public static Material Terrain;
    public static Material Rivers;
    public static Material Water;
    public static Material WaterShore;
    public static Material Estuaries;
    public static Material Roads;
    public static Material Walls;

	public static Material GetRendererMaterial(MeshType meshType)
	{
        return meshType switch
        {
            MeshType.Terrain => Terrain,
            MeshType.Rivers => Rivers,
            MeshType.Water => Water,
            MeshType.WaterShore => WaterShore,
            MeshType.Estuaries => Estuaries,
            MeshType.Roads => Roads,
            MeshType.Walls => Walls,
            _ => null,
        };
    }

    /// <summary>
    /// Texture used for sampling noise.
    /// </summary>
    public static Texture2D noiseSource;
	
	public static void SetNoiseColours(Texture2D noiseTexture)
    {
        NativeArray<Color32> source = noiseTexture.GetPixelData<Color32>(0);
        noiseColours = new NativeArray<float4>(source.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var job = new TextureToFloat4
        {
            source = source,
            Destination = noiseColours
        };
        job.Schedule(noiseColours.Length, 64).Complete();
    }

	/// <summary>
	/// Sample the noise texture.
	/// </summary>
	/// <param name="position">Sample position.</param>
	/// <returns>Four-component noise sample.</returns>
	public static float4 SampleNoise(float3 position)
	{
        float4 sample = (Vector4)noiseSource.GetPixelBilinear(position.x * noiseScale, position.z * noiseScale);

		if (Wrapping && position.x < innerDiameter * 1.5f)
		{
            float4 sample2 = (Vector4)noiseSource.GetPixelBilinear((position.x + wrapSize * innerDiameter) * noiseScale, position.z * noiseScale);
			sample = math.lerp(sample2, sample, position.x * (1f / innerDiameter) - 0.5f);
		}

		return sample;
	}

	/// <summary>
	/// Wrap size of the map, matching its X size if east-west wrapping is enabled.
	/// </summary>
	public static int wrapSize;

	/// <summary>
	/// Whether east-west map wrapping is enabled.
	/// </summary>
	public static bool Wrapping => wrapSize > 0;

	/// <summary>
	/// Initialize the hash grid.
	/// </summary>
	/// <param name="seed">Seed to use for initialization.</param>
	public static void InitializeHashGrid(uint seed)
	{
		hashGrid = new HexHash[hashGridSize * hashGridSize];
        Unity.Mathematics.Random random = new(seed);
        for (int i = 0; i < hashGrid.Length; i++)
		{
			hashGrid[i] = HexHash.Create(ref random);
		}
	}

	/// <summary>
	/// Sample the hash grid.
	/// </summary>
	/// <param name="position">Sample position</param>
	/// <returns>Sampled <see cref="HexHash"/>.</returns>
	public static HexHash SampleHashGrid(float3 position)
	{
		int x = (int)(position.x * hashGridScale) % hashGridSize;
		if (x < 0)
		{
			x += hashGridSize;
		}
		int z = (int)(position.z * hashGridScale) % hashGridSize;
		if (z < 0)
		{
			z += hashGridSize;
		}
		return hashGrid[x + z * hashGridSize];
	}

    public static HexHash SampleHashGrid(NativeArray<HexHash> hashGrid, float3 position)
    {
        int x = (int)(position.x * hashGridScale) % hashGridSize;
        if (x < 0)
        {
            x += hashGridSize;
        }
        int z = (int)(position.z * hashGridScale) % hashGridSize;
        if (z < 0)
        {
            z += hashGridSize;
        }
        return hashGrid[x + z * hashGridSize];
    }

	/// <summary>
	/// Get the feature threshold levels.
	/// </summary>
	/// <param name="level">Feature level.</param>
	/// <returns>Array containing the thresholds.</returns>
	public static float3 GetFeatureThresholds(int level) =>featureThresholds[level];

	/// <summary>
	/// Get the first outer cell corner for a direction.
	/// </summary>
	/// <param name="direction">The desired direction.</param>
	/// <returns>The corner on the counter-clockwise side.</returns>
	public static float3 GetFirstCorner(HexDirection direction) => corners[(int)direction];

	/// <summary>
	/// Get the second outer cell corner for a direction.
	/// </summary>
	/// <param name="direction">The desired direction.</param>
	/// <returns>The corner on the clockwise side.</returns>
	public static float3 GetSecondCorner(HexDirection direction) => corners[(int)direction + 1];

	/// <summary>
	/// Get the first inner solid cell corner for a direction.
	/// </summary>
	/// <param name="direction">The desired direction.</param>
	/// <returns>The corner on the counter-clockwise side.</returns>
	public static float3 GetFirstSolidCorner(HexDirection direction) => corners[(int)direction] * solidFactor;

	/// <summary>
	/// Get the second inner solid cell corner for a direction.
	/// </summary>
	/// <param name="direction">The desired direction.</param>
	/// <returns>The corner on the clockwise side.</returns>
	public static float3 GetSecondSolidCorner(HexDirection direction) => corners[(int)direction + 1] * solidFactor;

	/// <summary>
	/// Get the middle of the inner solid cell edge for a direction.
	/// </summary>
	/// <param name="direction">The desired direction.</param>
	/// <returns>The position in between the two inner solid cell corners.</returns>
	public static float3 GetSolidEdgeMiddle(HexDirection direction) => (corners[(int)direction] + corners[(int)direction + 1]) * (0.5f * solidFactor);

	/// <summary>
	/// Get the first inner water cell corner for a direction.
	/// </summary>
	/// <param name="direction">The desired direction.</param>
	/// <returns>The corner on the counter-clockwise side.</returns>
	public static float3 GetFirstWaterCorner(HexDirection direction) => corners[(int)direction] * waterFactor;

	/// <summary>
	/// Get the second inner water cell corner for a direction.
	/// </summary>
	/// <param name="direction">The desired direction.</param>
	/// <returns>The corner on the clockwise side.</returns>
	public static float3 GetSecondWaterCorner(HexDirection direction) => corners[(int)direction + 1] * waterFactor;

	/// <summary>
	/// Get the vector needed to bridge to the next cell for a given direction.
	/// </summary>
	/// <param name="direction">The desired direction.</param>
	/// <returns>The bridge vector.</returns>
	public static float3 GetBridge(HexDirection direction) => (corners[(int)direction] + corners[(int)direction + 1]) * blendFactor;

	/// <summary>
	/// Get the vector needed to bridge to the next water cell for a given direction.
	/// </summary>
	/// <param name="direction">The desired direction.</param>
	/// <returns>The bridge vector.</returns>
	public static float3 GetWaterBridge(HexDirection direction) => (corners[(int)direction] + corners[(int)direction + 1]) * waterBlendFactor;

	/// <summary>
	/// Interpolate a position along a terraced edge.
	/// </summary>
	/// <param name="a">Start position.</param>
	/// <param name="b">End position.</param>
	/// <param name="step">Terrace interpolation step.</param>
	/// <returns>The position found by applying terrace interpolation.</returns>
	public static float3 TerraceLerp(float3 a, float3 b, int step)
	{
		float h = step * horizontalTerraceStepSize;
		a.x += (b.x - a.x) * h;
		a.z += (b.z - a.z) * h;
		float v = (step + 1) / 2 * verticalTerraceStepSize;
		a.y += (b.y - a.y) * v;
		return a;
	}

    public static float4 TerraceLerp(float4 a, float4 b, int step)
    {
        float h = step * horizontalTerraceStepSize;
        return HexExtensions.Lerp(a, b, h);
    }


    public static EdgeVertices TerraceLerp(EdgeVertices a, EdgeVertices b, int step)
    {
        return new()
        {
            c0 = TerraceLerp(a.c0, b.c0, step),
            c1 = TerraceLerp(a.c1, b.c1, step),
            c2 = TerraceLerp(a.c2, b.c2, step),
            c3 = TerraceLerp(a.c3, b.c3, step),
			c4 = TerraceLerp(a.c4, b.c4, step),
        };
    }

    /// <summary>
    /// Interpolate a color along a terraced edge.
    /// </summary>
    /// <param name="a">Start color.</param>
    /// <param name="b">End color.</param>
    /// <param name="step">Terrace interpolation step.</param>
    /// <returns>The color found by applying terrace interpolation.</returns>
    public static Color TerraceLerp(Color a, Color b, int step)
	{
		float h = step * horizontalTerraceStepSize;
		return Color.Lerp(a, b, h);
	}

	/// <summary>
	/// Interpolate a position along a wall.
	/// </summary>
	/// <param name="near">Near position.</param>
	/// <param name="far">Far position.</param>
	/// <returns>The middle position with appropriate Y coordinate.</returns>
	public static float3 WallLerp(float3 near, float3 far)
	{
		near.x += (far.x - near.x) * 0.5f;
		near.z += (far.z - near.z) * 0.5f;
		float v = near.y < far.y ? wallElevationOffset : (1f - wallElevationOffset);
		near.y += (far.y - near.y) * v + wallYOffset;
		return near;
	}

	/// <summary>
	/// Apply wall thickness.
	/// </summary>
	/// <param name="near">Near position.</param>
	/// <param name="far">Far position.</param>
	/// <returns>Position taking wall thickness into account.</returns>
	public static float3 WallThicknessOffset(float3 near, float3 far)
	{
        float3 offset;
		offset.x = far.x - near.x;
		offset.y = 0f;
		offset.z = far.z - near.z;
		return math.normalize(offset) * (wallThickness * 0.5f);
	}

	/// <summary>
	/// Determine the <see cref="HexEdgeType"/> based on two elevations.
	/// </summary>
	/// <param name="elevation1">First elevation.</param>
	/// <param name="elevation2">Second elevation.</param>
	/// <returns>Matching <see cref="HexEdgeType"/>.</returns>
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

	/// <summary>
	/// Perturn a position.
	/// </summary>
	/// <param name="position">A position.</param>
	/// <returns>The positions with noise applied to its XZ components.</returns>
	public static float3 Perturb(float3 position)
	{
		float4 sample = SampleNoise(position);
		position.x += (sample.x * 2f - 1f) * cellPerturbStrength;
		position.z += (sample.z * 2f - 1f) * cellPerturbStrength;
		return position;
	}

    public static float3 Perturb(NativeArray<float4> noise, float3 position, int wrapSize)
    {
        float4 sample = SampleNoise(noise, position, wrapSize);
        position.x += (sample.x * 2f - 1f) * cellPerturbStrength;
        position.z += (sample.z * 2f - 1f) * cellPerturbStrength;
        return position;
    }

    public static float4 SampleNoise(NativeArray<float4> noise, float3 position, int wrapSize)
    {
        float4 sample = BillinearInterPolation(noise, position.x * noiseScale, position.z * noiseScale);
        if (wrapSize > 0 && position.x < innerDiameter * 1.5f)
        {
            float4 sample2 = BillinearInterPolation(noise, (position.x + wrapSize * innerDiameter) * noiseScale, position.z * noiseScale);
            sample = math.lerp(sample2, sample, position.x * (1f / innerDiameter) - 0.5f);
        }
        return sample;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        float2 px = new((float)(1 - pX), (float)pX);
        float2 py = new((float)(1 - pY), (float)pY);

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
                    int2 coordinates = new()
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetIndexFromXY(int2 xy)
    {
        xy = math.abs(xy);
        int pixelRowLength = 512;
        int adder = math.clamp(xy.y, 0, 262143) * pixelRowLength;

        return math.clamp(xy.x + adder, 0, 262143);
    }

    [BurstCompile]
    public struct TextureToFloat4 : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<Color32> source;
        [WriteOnly]
        public NativeArray<float4> Destination;
        public void Execute(int i)
        {
            Destination[i] = (Vector4)(Color)source[i];
        }
    }
}