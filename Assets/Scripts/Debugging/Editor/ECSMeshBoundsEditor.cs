using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ECSMeshBounds))]
public class ECSMeshBoundsEditor : Editor
{
    ECSMeshBounds ecsMeshBounds;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("Get From Entity World"))
        {
            ecsMeshBounds.GatherMeshFromECSWorld();
        }
        if (GUILayout.Button("Convert to Entity"))
        {
            ecsMeshBounds.ConverToEntityEditor();
        }
    }
    void OnEnable()
    {
        ecsMeshBounds = (ECSMeshBounds)target;
        Tools.hidden = true;
    }

    void OnDisable()
    {
        Tools.hidden = false;
    }
}
