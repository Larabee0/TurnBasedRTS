using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class HexNode : MonoBehaviour
{
    public HexNode previousNode;
    public HexNode[] neighbouringNodes;
    public int x;
    public int y;
    public int2 xy;

    public MeshRenderer thisMeshRenderer;
    public Collider thisCollider;
    public List<Material> materials;

    // Start is called before the first frame update
    void Start()
    {
        if (thisMeshRenderer == null)
        {
            thisMeshRenderer = GetComponent<MeshRenderer>();
        }
        if (thisCollider == null)
        {
            thisCollider = GetComponent<UnityEngine.Collider>();
        }
        ResetMaterial();
    }
    public void SetNeighbours(HexNode[] hexNodes, int2 thisNode)
    {
        neighbouringNodes = hexNodes;
        x = thisNode.x;
        y = thisNode.y;
        xy = thisNode;
    }

    public void ResetMaterial()
    {

        thisMeshRenderer.material = materials[0];
    }
    public void HighlightCell()
    {
        thisMeshRenderer.material = materials[1];
    }

}
