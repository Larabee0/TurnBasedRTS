using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using System.Runtime.CompilerServices;

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

    public const int hashGridSize = 256;
    public const float hashGridScale = 0.25f;
    private static HexHash[] hashGrid;

    public static int wrapSize;

    public static bool Wrapping
    {
        get
        {
            return wrapSize > 0;
        }
    }

    public static void SetNoiseColours(Texture2D noiseTexture)
    {
        NativeArray<Color32> source = noiseTexture.GetPixelData<Color32>(0);
        noiseColours = new NativeArray<float4>(source.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var job = new TextureToFloat4
        {
            source = source,
            Destination = noiseColours
        };
        job.Schedule(noiseColours.Length,64).Complete();
    }

    public static void CleanUpNoiseColours()
    {
        if (noiseColours.IsCreated)
        {
            noiseColours.Dispose();
        }
    }

    public static float3 GetFirstSolidCorner(HexDirection direction)
    {
        return corners[(int)direction] * solidFactor;
    }

    public static float3 GetSecondSolidCorner(HexDirection direction)
    {
        return corners[(int)direction + 1] * solidFactor;
    }

    static readonly float3[] corners = {
            new float3(0f, 0f, outerRadius),
            new float3(innerRadius, 0f, 0.5f * outerRadius),
            new float3(innerRadius, 0f, -0.5f * outerRadius),
            new float3(0f, 0f, -outerRadius),
            new float3(-innerRadius, 0f, -0.5f * outerRadius),
            new float3(-innerRadius, 0f, 0.5f * outerRadius),
            new float3(0f, 0f, outerRadius)
        };
    public static float3 GetFirstCorner(HexDirection direction)
    {
        return corners[(int)direction];
    }

    public static float3 GetSecondCorner(HexDirection direction)
    {
        return corners[(int)direction + 1];
    }
    public static float3 GetBridge(HexDirection direction)
    {
        return (corners[(int)direction] + corners[(int)direction + 1]) * blendFactor;
    }

    public static float3 TerraceLerp(float3 a, float3 b, int step)
    {
        float h = step * horizontalTerraceStepSize;
        a.x += (b.x - a.x) * h;
        a.z += (b.z - a.z) * h;
        float v = ((step + 1) / 2) * verticalTerraceStepSize;
        a.y += (b.y - a.y) * v;
        return a;
    }

    public static float3x4 TerraceLerp(float3x4 a, float3x4 b, int step)
    {
        return new()
        {
            c0 = TerraceLerp(a.c0, b.c0, step),
            c1 = TerraceLerp(a.c1, b.c1, step),
            c2 = TerraceLerp(a.c2, b.c2, step),
            c3 = TerraceLerp(a.c3, b.c3, step),
        };
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

    public static float4 SampleNoise(NativeArray<float4> noise, float3 position, int wrapSize)
    {
        float4 sample = BillinearInterPolation(noise,position.x * noiseScale, position.z * noiseScale);
        if (wrapSize > 0 && position.x < innerDiameter * 1.5f)
        {
            float4 sample2 = BillinearInterPolation(noise, (position.x + wrapSize * innerDiameter) * noiseScale, position.z * noiseScale);
            sample = math.lerp(sample2, sample, position.x * (1f / innerDiameter) - 0.5f);
        }
        return sample;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 LerpForPerturb(float4 a, float4 b, float t)
    {
        t = math.clamp(t, 0f, 1f);
        return new float4
        {
            x = a.x + (b.x - a.x) * t,
            y = a.y + (b.y - a.y) * t,
            z = a.z + (b.z - a.z) * t,
            w = a.w + (b.w - a.w) * t,
        };
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

    public static float3 GetSolidEdgeMiddle(HexDirection direction)
    {
        return (corners[(int)direction] + corners[(int)direction + 1]) * (0.5f * solidFactor);
    }

    public static float3 Perturb(NativeArray<float4> noise, float3 position, int wrapSize)
    {
        float4 sample = SampleNoise(noise, position, wrapSize);
        position.x += (sample.x * 2f - 1f) * cellPerturbStrength;
        position.z += (sample.z * 2f - 1f) * cellPerturbStrength;
        return position;
    }

    public static float3 GetFirstWaterCorner(HexDirection direction)
    {
        return corners[(int)direction] * waterFactor;
    }

    public static float3 GetSecondWaterCorner(HexDirection direction)
    {
        return corners[(int)direction + 1] * waterFactor;
    }
    public static float3 GetWaterBridge(HexDirection direction)
    {
        return (corners[(int)direction] + corners[(int)direction + 1]) *
            waterBlendFactor;
    }

    public static void InitialiseHashGrid(uint seed)
    {
        hashGrid = new HexHash[hashGridSize * hashGridSize];
        Unity.Mathematics.Random random = new(seed);

        for (int i = 0; i < hashGrid.Length; i++)
        {
            hashGrid[i] = HexHash.Create(random);
        }
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
