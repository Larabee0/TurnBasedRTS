using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StaticVariableHandler : MonoBehaviour
{
    [SerializeField] private Texture2D noiseSource;
    // Start is called before the first frame update
    void Awake()
    {
        HexMetrics.SetNoiseColours(noiseSource);
    }
}
