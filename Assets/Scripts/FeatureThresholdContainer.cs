using System;

[Obsolete("Use a float3 instead")]
public struct FeatureThresholdContainer
{
    public float a;
    public float b;
    public float c;
    
    public FeatureThresholdContainer(float a, float b, float c)
    {
        this.a = a;
        this.b = b;
        this.c = c;
    }

    public float GetLevel(int input)
    {
        return input switch
        {
            0 => a,
            1 => b,
            2 => c,
            _ =>0f
        };
    }
}
