using Unity.Mathematics;

/// <summary>
/// Five-component hash value.
/// </summary>
public struct HexHash
{

	public float a, b, c, d, e;

	/// <summary>
	/// Create a hex hash.
	/// </summary>
	/// <returns>Hash value based on <see cref="Random"/>.</returns>
	public static HexHash Create(ref Random Random)
	{
		HexHash hash;
		hash.a = Random.NextFloat() * 0.999f;
		hash.b = Random.NextFloat() * 0.999f;
		hash.c = Random.NextFloat() * 0.999f;
		hash.d = Random.NextFloat() * 0.999f;
		hash.e = Random.NextFloat() * 0.999f;
		return hash;
	}
}