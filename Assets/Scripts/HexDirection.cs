/// <summary>
/// Hexagonal direction, pointy side up, which is north.
/// </summary>
public enum HexDirection : sbyte
{
	/// <summary>Northeast.</summary>
	NE,
	/// <summary>East.</summary>
	E,
	/// <summary>Southeast.</summary>
	SE,
	/// <summary>Southwest.</summary>
	SW,
	/// <summary>West.</summary>
	W,
	/// <summary>Northwest.</summary>
	NW
}

public enum HemiSphereMode : byte
{
    Both,
    North,
    South
}

public enum MeshType : byte
{
    Terrain,
    Rivers,
    Water,
    WaterShore,
    Estuaries,
    Roads,
    Walls
}
