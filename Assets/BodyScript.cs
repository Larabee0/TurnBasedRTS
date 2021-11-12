using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BodyScript : MonoBehaviour
{
    public MeshRenderer meshRenderer;
    public Material material;
    public SphereCollider colliderSphere;
    public Vector3 scale;    
    public float ScaleMin = 1;    
    public float ScaleMax = 99;
    public HighLevelBodyType HighLevelType = HighLevelBodyType.Determine;
    public LowLevelBodyType LowLevelType = LowLevelBodyType.Determine;
    // Start is called before the first frame update
    private void Start()
    {
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        colliderSphere = GetComponentInChildren<SphereCollider>();
        meshRenderer.transform.localScale= scale;
        meshRenderer.material = material;
    }
}
