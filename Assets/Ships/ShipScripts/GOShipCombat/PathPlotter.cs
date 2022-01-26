using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PathCreation;
public class PathPlotter : MonoBehaviour
{
    public PathCreator creator;
    public ShipSelector shipSelector;
    public ShipMovePicker shipMover;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyUp(KeyCode.P))
        {
            PlotPath();
        }
    }

    private void PlotPath()
    {
        creator.bezierPath.SetPoint(0, transform.InverseTransformPoint(shipSelector.selectedShip.transform.position));
        creator.bezierPath.SetPoint(1, shipMover.TargetPosition);
    }
}
