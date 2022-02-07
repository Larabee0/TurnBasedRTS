using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipMovePicker : MonoBehaviour
{
    public Vector3 mousePosition;
    private Vector3 targetPositionInternal;
    private Vector3 targetFowardInternal;
    public bool UseCustomFoward;
    private Vector3 customFoward;
    float maxDistance = 100f;

    public Vector3 TargetPosition { get { return ArrowTransform.position; } }
    public Vector3 TargetForward { get { return ArrowTransform.forward; } }

    [SerializeField] private LayerMask gridLayermask;

    Mesh lineMesh;
    MeshRenderer lineRenderer;
    bool EnableLine { set { lineRenderer.enabled = value; } }


    Mesh arrowMesh;
    MeshRenderer arrowRenderer;
    bool EnableArrow { get { return arrowRenderer.enabled; } set { arrowRenderer.enabled = value; } }

    public Transform selectedShip = null;
    private Transform ArrowTransform;

    public Material cursorMat;
    private Transform CursorArrow = null;
    MeshRenderer CursorArrowRenderer;
    bool EnableCursor { set { CursorArrowRenderer.enabled = value; } }

    public bool UpdateLineAndArrow = false;

    private void Awake()
    {
        lineRenderer = transform.GetChild(0).GetComponent<MeshRenderer>();
        transform.GetChild(0).GetComponent<MeshFilter>().mesh = lineMesh = new Mesh() { subMeshCount = 1};
        ArrowTransform = transform.GetChild(1);
        arrowRenderer = transform.GetChild(1).GetComponent<MeshRenderer>();
        transform.GetChild(1).GetComponent<MeshFilter>().mesh = arrowMesh = new Mesh() { subMeshCount = 1 };
    }

    private void Start()
    {
        CreateArrow();
        CursorArrow = Instantiate(ArrowTransform, transform);
        CursorArrowRenderer = CursorArrow.GetComponent<MeshRenderer>();
        CursorArrowRenderer.material = cursorMat;
        HideAndStopEverything();
    }

    void Update()
    {
        CursorArrow.position = mousePosition;
        CursorArrow.forward = (selectedShip == null) ? Vector3.forward : mousePosition - selectedShip.position == Vector3.zero ? selectedShip.forward : mousePosition - selectedShip.position;
        EnableCursor = true;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        float distanceToScreen = maxDistance / 10f;
        if (Physics.Raycast(ray, out RaycastHit hitInfo, maxDistance, gridLayermask))
        {
            distanceToScreen = hitInfo.distance;
        }

        mousePosition = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, distanceToScreen));
        mousePosition.y = selectedShip == null ? 0 : selectedShip.transform.position.y;

        if (Input.GetMouseButton(0))
        {
            SetLineAndArrowEnable(true);
            if (EnableArrow && Input.GetKey(KeyCode.LeftShift))
            {
                UseCustomFoward = true;
                Vector3 fwd = mousePosition - targetPositionInternal;
                if(fwd != Vector3.zero)
                {
                    CursorArrow.forward = customFoward = fwd;
                }
                
            }
            else
            {
                EnableCursor = false;
                UseCustomFoward = false;
                targetPositionInternal = mousePosition;
            }
        }
        if (UpdateLineAndArrow)
        {
            DrawLine();
            PlaceArrow();
        }

    }

    public void HideAndStopEverything()
    {
        UseCustomFoward = EnableArrow = EnableCursor = EnableLine = enabled = false;
        selectedShip = null;
    }

    public void Enable(Ship ship)
    {
        selectedShip = ship.transform;
        targetPositionInternal = ship.transform.position;
        customFoward = targetFowardInternal = ship.transform.forward;
        UseCustomFoward= UpdateLineAndArrow = true;
        enabled = true;
        SetLineAndArrowEnable(true);
    }

    public void SetLineAndArrowEnable(bool enabled = false)
    {
        EnableLine = EnableArrow = enabled;
    }

    public void StartLineArrowUpdate()
    {
        SetLineAndArrowEnable(true);
        UpdateLineAndArrow = true;
    }

    public void StopLineArrowUpdate()
    {
        SetLineAndArrowEnable();
        UpdateLineAndArrow = false;
    }

    public void CreateArrow()
    {
        arrowMesh.Clear();
        Vector3[] vertices = new Vector3[5];
        vertices[0] = new Vector3(0, 0, 0.7f);
        vertices[1] = new Vector3(+0.25f, +0.25f, -0.3f); // top right
        vertices[2] = new Vector3(-0.25f, +0.25f, -0.3f); // Top Left
        vertices[3] = new Vector3(+0.25f, -0.25f, -0.3f); // bottom right
        vertices[4] = new Vector3(-0.25f, -0.25f, -0.3f); // bottom left

        int[] indices = new int[16];
        indices[0] = 0;
        indices[1] = 1;
        indices[2] = 0;
        indices[3] = 2;
        indices[4] = 0;
        indices[5] = 3;
        indices[6] = 0;
        indices[7] = 4;

        indices[8] = 4;
        indices[9] = 2;
        indices[10] = 2;
        indices[11] = 1;
        indices[12] = 1;
        indices[13] = 3;
        indices[14] = 3;
        indices[15] = 4;

        arrowMesh.SetVertices(vertices);
        arrowMesh.SetIndices(indices, MeshTopology.Lines, 0);
    }

    public void DrawLine()
    {
        lineMesh.Clear();
        Vector3[] vertices = new Vector3[2];
        vertices[0] = transform.InverseTransformPoint((selectedShip == null) ? Vector3.zero : selectedShip.position);
        vertices[1] = transform.InverseTransformPoint(targetPositionInternal);
        int[] indices = new int[2];
        indices[0] = 0;
        indices[1] = 1;
        lineMesh.SetVertices(vertices);
        lineMesh.SetIndices(indices, MeshTopology.Lines, 0);
    }

    public void PlaceArrow()
    {
        Vector3 origin = (selectedShip == null) ? Vector3.zero : selectedShip.position;
        Vector3 target = targetPositionInternal;

        targetFowardInternal = target - origin;
        ArrowTransform.position = target;

        Vector3 fwd = UseCustomFoward ? customFoward : targetFowardInternal;
        if(fwd == Vector3.zero)
        {
            if(selectedShip!= null)
            {
                fwd = selectedShip.forward;
                ArrowTransform.forward = fwd;
            }
        }
        else
        {
            ArrowTransform.forward = fwd;
        }
        
    }
}
