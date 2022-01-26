using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurretMovementScript : MonoBehaviour
{
    public Transform HorizontalTransform;
    public Transform VerticalTransform;

    public Transform Target;

    private float TraverseAngle = 0f;
    private float elevation = 0f;
    private float turretAimThreshold = 2f;
    private float timeSinceFire = 0f;
    private float fireThreshold = 10f;
    [SerializeField] private float traverseSpeed = 5f;
    [SerializeField] private float elevationSpeed = 5f;
    [SerializeField] private float MaxDepression = -5;
    [SerializeField] private float MaxElevation = 5f;
    Color DebugColour;
    private void Awake()
    {
        DebugColour = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
        fireThreshold += Random.Range(-1f, 1f);
    }
    public void Update()
    {
        if(Target == null)
        {
            return;
        }
        Vector3 baseUp = transform.up;
        Vector3 TargetPos = Target.position;

        float targetTraverse = Vector3.SignedAngle(transform.forward, Vector3.ProjectOnPlane(TargetPos - HorizontalTransform.position, baseUp), baseUp);

        TraverseAngle = Mathf.MoveTowardsAngle(TraverseAngle, targetTraverse, traverseSpeed * Time.deltaTime);

        if (Mathf.Abs(TraverseAngle) > Mathf.Epsilon)
        {
            HorizontalTransform.localEulerAngles = Vector3.up * TraverseAngle;
        }

        Vector3 localTargetPos = HorizontalTransform.InverseTransformDirection(TargetPos - VerticalTransform.position);
        float targetElevation = Vector3.Angle(Vector3.ProjectOnPlane(localTargetPos, Vector3.up), localTargetPos);
        targetElevation *= Mathf.Sign(localTargetPos.y);

        targetElevation = Mathf.Clamp(targetElevation, -MaxDepression, MaxElevation);
        elevation = Mathf.MoveTowards(elevation, targetElevation, elevationSpeed * Time.deltaTime);

        if (Mathf.Abs(elevation) > Mathf.Epsilon)
        {
            VerticalTransform.localEulerAngles = Vector3.right * -elevation;
        }
        if(Vector3.Angle(TargetPos - VerticalTransform.position,VerticalTransform.forward) < turretAimThreshold)
        {
            timeSinceFire += Time.deltaTime;
            if(timeSinceFire > fireThreshold)
            {
                Debug.DrawLine(HorizontalTransform.position, Target.transform.position, DebugColour, 0.75f);
                timeSinceFire = 0f;
            }
        }
    }
}
