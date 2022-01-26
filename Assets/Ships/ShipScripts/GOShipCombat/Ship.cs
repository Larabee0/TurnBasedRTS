using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ship : MonoBehaviour
{
    public TurretMovementScript[] turrets;
    public MeshRenderer selectionCube;

    [SerializeField] private ShipMotionStats motionStats;


    public bool SelectionCubeEnable { set { selectionCube.enabled = value; } }

    public int team = 1;
    private Vector3 FinalPoint;
    private Vector3 FinalDirection;
    private float startDst;
    private bool angleIncorrect;
    private bool atDestination;
    private void Awake()
    {
        selectionCube = transform.GetChild(0).GetComponent<MeshRenderer>();
        turrets = GetComponentsInChildren<TurretMovementScript>();
        SelectionCubeEnable = false;
        enabled = false;
    }

    public void SetCubeColour(Color inColour)
    {
        float cachedAlpha = selectionCube.material.color.a;
        inColour.a = cachedAlpha;
        selectionCube.material.color = inColour;
    }

    private void Update()
    {
        atDestination = Vector3.Distance(transform.position, FinalPoint) < Mathf.Epsilon;
        if (angleIncorrect && !atDestination)
        {
            Vector3 toFwd = FinalPoint - transform.position;
            Quaternion toRotation;
            if (toFwd == Vector3.zero)
            {
                toRotation = transform.localRotation;
            }
            else
            {
                toRotation = Quaternion.LookRotation(toFwd);
            }
            if (Quaternion.Angle(transform.rotation, toRotation) > 0)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, motionStats.TurningSpeed * Time.deltaTime);
                transform.position += Mathf.Max((1.5f - Mathf.InverseLerp(startDst, 0, Vector3.Distance(transform.position, FinalPoint))) / DistanceCompoensation, 0f) * motionStats.MovementSpeed * Time.deltaTime * transform.forward;
            }
            else
            {
                angleIncorrect = false;
            }
        }
        else if (!atDestination)
        {
            angleIncorrect = false;
            transform.position = Vector3.MoveTowards(transform.position, FinalPoint, motionStats.MovementSpeed * Time.deltaTime);
        }

        if (atDestination)
        {
            Quaternion toRotation = Quaternion.LookRotation(FinalDirection);
            float angle = Quaternion.Angle(transform.rotation, toRotation);
            if (angle > 0)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, motionStats.TurningSpeed * Time.deltaTime);
            }
            else
            {
                enabled = false;
            }
        }
    }

    float DistanceCompoensation;

    public void Travel(Vector3 Point, Vector3 Direction)
    {
        FinalPoint = Point;
        FinalDirection = Direction;
        startDst = Vector3.Distance(transform.position, FinalPoint);

        DistanceCompoensation = Mathf.Lerp(1f, 10f, Mathf.InverseLerp(10f, 0f, startDst));

        angleIncorrect = enabled = true;
    }
}

[System.Serializable]
public struct ShipMotionStats
{
    public float MovementSpeed;
    public float TurningSpeed;
    public TurnOn turnWhen;
}

public enum TurnOn
{
    StartMove,
    DuringMove
}
public static class Bezier
{
    public static Vector3 GetPoint(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float r = 1f - t;
        return r * r * a + 2f * r * t * b + t * t * c;
    }

    public static Vector3 GetDerivative(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        return 2f * ((1f - t) * (b - a) + t * (c - b));
    }
}
