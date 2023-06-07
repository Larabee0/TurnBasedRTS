using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Set of five vertex positions describing a cell edge.
/// </summary>
public struct EdgeVertices
{

	public float3 c0, c1, c2, c3, c4;

	/// <summary>
	/// Create a straight edge with equidistant vertices between two corner positions.
	/// </summary>
	/// <param name="corner1">Frist corner.</param>
	/// <param name="corner2">Second corner.</param>
	public EdgeVertices(Vector3 corner1, Vector3 corner2)
	{
		c0 = corner1;
		c1 = math.lerp(corner1, corner2, 0.25f);
		c2 = math.lerp(corner1, corner2, 0.5f);
		c3 = math.lerp(corner1, corner2, 0.75f);
		c4 = corner2;
	}

	/// <summary>
	/// Create a straight edge between two corner positions, with configurable outer step.
	/// </summary>
	/// <param name="corner1">First corner.</param>
	/// <param name="corner2">Second corner.</param>
	/// <param name="outerStep">First step away from corners, as fraction of edge.</param>
	public EdgeVertices(Vector3 corner1, Vector3 corner2, float outerStep)
	{
		c0 = corner1;
		c1 = math.lerp(corner1, corner2, outerStep);
		c2 = math.lerp(corner1, corner2, 0.5f);
		c3 = math.lerp(corner1, corner2, 1f - outerStep);
		c4 = corner2;
	}

	/// <summary>
	/// Create edge vertices for a specific terrace step.
	/// </summary>
	/// <param name="a">Edge on first side of the terrace.</param>
	/// <param name="b">Edge on second side of the terrace.</param>
	/// <param name="step">Terrace interpolation step, 0-<see cref="HexMetrics.terraceSteps"/> inclusive.</param>
	/// <returns>Edge vertices interpolated along terrace.</returns>
	public static EdgeVertices TerraceLerp(EdgeVertices a, EdgeVertices b, int step)
	{
		EdgeVertices result;
		result.c0 = HexMetrics.TerraceLerp(a.c0, b.c0, step);
		result.c1 = HexMetrics.TerraceLerp(a.c1, b.c1, step);
		result.c2 = HexMetrics.TerraceLerp(a.c2, b.c2, step);
		result.c3 = HexMetrics.TerraceLerp(a.c3, b.c3, step);
		result.c4 = HexMetrics.TerraceLerp(a.c4, b.c4, step);
		return result;
	}
}