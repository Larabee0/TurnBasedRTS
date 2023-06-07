using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
public struct HexHash
{
    public float a;
    public float b;
    public float c;
    public float d;
    public float e;
    public static HexHash Create(Random Random)
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
