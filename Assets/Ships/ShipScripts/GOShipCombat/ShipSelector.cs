using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShipSelector : MonoBehaviour
{
    public ShipMovePicker shipMover;
    public List<Ship> Ships = new List<Ship>();
    public List<Ship> Team1Ships = new List<Ship>();
    public List<Ship> Team2Ships = new List<Ship>();

    public List<Transform> Team1AsTargets = new List<Transform>();
    public List<Transform> Team2AsTargets = new List<Transform>();


    public Ship selectedShip;
    [SerializeField] private LayerMask selectionLayer;
    [SerializeField] private LayerMask shipHitLayer;

    private void Awake()
    {
        shipMover = GetComponent<ShipMovePicker>();
    }

    private void Start()
    {
        Ships = new List<Ship>(FindObjectsOfType<Ship>());
        for (int s = 0; s < Ships.Count; s++)
        {
            if(Ships[s].team == 1)
            {
                Team1Ships.Add(Ships[s]);
                Team1AsTargets.Add(Ships[s].transform);
                for (int t = 0; t < Ships[s].turrets.Length; t++)
                {
                    Team1AsTargets.Add(Ships[s].turrets[t].transform);
                }
            }
            else
            {
                Team2Ships.Add(Ships[s]);
                Team2AsTargets.Add(Ships[s].transform);
                for (int t = 0; t < Ships[s].turrets.Length; t++)
                {
                    Team2AsTargets.Add(Ships[s].turrets[t].transform);
                }
            }
        }
        InvokeRepeating(nameof(SetTargets), 5f, 15f);
    }

    private void Update()
    {
        if (Input.GetMouseButtonUp(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, selectionLayer))
            {
                selectedShip = hitInfo.collider.GetComponentInParent<Ship>();
                selectedShip.SelectionCubeEnable = true;
                selectedShip.SetCubeColour(selectedShip.team == 1 ? Color.green : Color.red);
                shipMover.Enable(selectedShip);
            }
        }
        if (Input.GetMouseButtonUp(1))
        {
            if (selectedShip)
            {
                selectedShip.SelectionCubeEnable = false;
                selectedShip = null;
                shipMover.HideAndStopEverything();
            }
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            if (selectedShip != null)
            {
                selectedShip.Travel(shipMover.TargetPosition, shipMover.TargetForward);
            }
        }
    }


    private void SetTargets()
    {
        Ships.ForEach(ship =>
        {
            for (int i = 0; i < ship.turrets.Length; i++)
            {
                //ship.turrets[i].gameObject.SetActive(false);
            }
            for (int i = 0; i < ship.turrets.Length; i++)
            {
                GetClosestValidTarget(ship.turrets[i], ship.team);
            }
            for (int i = 0; i < ship.turrets.Length; i++)
            {
                //ship.turrets[i].gameObject.SetActive(true);
            }
        });
    }

    private void GetClosestValidTarget(TurretMovementScript turret, int team)
    {
        Transform Target = null;
        if(team == 1)
        {
            Target = FindTarget(Team2AsTargets, turret);
        }
        else if (team == 2)
        {
            Target = FindTarget(Team1AsTargets, turret);
        }
        if (Target != null)
        {
            Debug.Log("Found valid Target");
        }
        else
        {
            Debug.Log("No Target");
        }
        turret.Target = Target;
    }

    private Transform FindTarget(List<Transform> targets, TurretMovementScript turret)
    {
        Transform closestTarget = null;
        float TargetDst = float.MaxValue;

        for (int i = 0; i < targets.Count; i++)
        {
            Transform target = targets[i];
            Ray ray = new Ray(turret.VerticalTransform.position, target.transform.position - turret.VerticalTransform.position);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, shipHitLayer))
            {
                TargetableObject obj = hit.collider.gameObject.GetComponent<TargetableObject>();
                if (obj.targetRoot == target)
                {
                    float dst = Vector3.Distance(target.transform.position, turret.VerticalTransform.position);
                    if (dst < TargetDst)
                    {
                        TargetDst = dst;
                        closestTarget = target;
                    }
                }
            }
        }
        return closestTarget;
    }
}
