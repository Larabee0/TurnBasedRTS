using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshGridTypeThing : MonoBehaviour
{
    private MeshFilter meshFilter;

    [Range(10,1000)]
    [SerializeField] private int width = 10;
    [Range(10, 1000)]
    [SerializeField] private int length = 10;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
    }

    private void Start()
    {
        int zStart = -(length/2);
        int xStart = -(width/2);


        Vector3[] vertices = new Vector3[width * length];
        for (int l = 0, v = 0, z = zStart; l < length; l++, z++)
        {
            for (int w = 0,x = xStart; w < width; w++,x++, v++)
            {
                vertices[v] = new Vector3(x, 0f, z);
            }
        }

        int[] indices = new int[vertices.Length * 2];

        int rowIndex = 0; // z
        int colIndex = 0; // x
        int colCount = width; // x
        for (int i = 0; i < indices.Length; i+=4)
        {
            indices[i] = rowIndex * colCount + colIndex;
            indices[i+1] = (rowIndex + 1) * colCount + colIndex;
            rowIndex++;
            rowIndex = (rowIndex == length-1) ? 0 : rowIndex;
            colIndex = rowIndex == 0 ? colIndex + 1 : colIndex;
        }

        rowIndex = 0;
        colIndex = 0;

        for (int i = 2; i < indices.Length; i += 4)
        {
            indices[i] = rowIndex * colCount + colIndex;
            indices[i + 1] = rowIndex * colCount + (colIndex + 1);
            colIndex++;
            colIndex = (colIndex == width-1) ? 0 : colIndex;
            rowIndex = colIndex == 0 ? rowIndex + 1 : rowIndex;
        }

        meshFilter.mesh = new Mesh() { subMeshCount = 1 };
        meshFilter.mesh.SetVertices(vertices);
        meshFilter.mesh.SetIndices(indices, MeshTopology.Lines, 0);
        meshFilter.mesh.RecalculateBounds();
    }
}
