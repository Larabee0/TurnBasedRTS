using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace GameObjectHexagons
{
    public class HexUnit : MonoBehaviour
    {
        const float travelSpeed = 4f;
        const float rotationSpeed = 180f;
        const int visionRange = 3;

        public static HexUnit unitPrefab;

        private float orientation;
        private HexCell location;
        private HexCell currentTravelLocation = null;
        private List<HexCell> pathToTravel;

        public HexGrid Grid { get; set; }

        public bool IsValidDestination(HexCell cell)
        {
            return cell.IsExplored && !cell.IsUnderwater && !cell.Unit;
        }

        public int Speed
        {
            get
            {
                return 24;
            }
        }

        public int VisionRange
        {
            get
            {
                return 3;
            }
        }
        public float Oreientation
        {
            get
            {
                return orientation;
            }
            set
            {
                orientation = value;
                transform.localRotation = Quaternion.Euler(0f, value, 0f);
            }
        }

        public HexCell Location
        {
            get
            {
                return location;
            }
            set
            {
                if (location)
                {
                    Grid.DecreaseVisibility(location, visionRange);
                    location.Unit = null;
                }
                location = value;
                value.Unit = this;
                Grid.IncreaseVisibility(value, visionRange);
                Grid.MakeChildOfColumn(transform, value.ColumnIndex);
                transform.localPosition = value.Position;
                
            }
        }

        private void OnEnable()
        {
            if (location)
            {
                transform.localPosition = location.Position;
                if (currentTravelLocation)
                {
                    Grid.IncreaseVisibility(location, visionRange);
                    Grid.DecreaseVisibility(currentTravelLocation, visionRange);
                    currentTravelLocation = null;
                }
            }
        }

        public void ValidateLocation()
        {
            transform.localPosition = location.Position;
        }

        public void Die()
        {
            if (location)
            {
                Grid.DecreaseVisibility(location, visionRange);
            }
            location.Unit = null;
            Destroy(gameObject);
        }

        public int GetMoveCost(HexCell fromCell, HexCell toCell, HexDirection direction)
        {
            HexEdgeType edgeType = fromCell.GetEdgeType(toCell);
            if(edgeType == HexEdgeType.Cliff)
            {
                return -1;
            }
            int moveCost;
            if (fromCell.HasRoadThroughEdge(direction))
            {
                moveCost = 1;
            }
            else if (fromCell.Walled != toCell.Walled)
            {
                return -1;
            }
            else
            {
                moveCost = edgeType == HexEdgeType.Flat ? 5 : 10;
                moveCost += toCell.UrbanLevel + toCell.Farmlevel + toCell.PlantLevel;
            }
            return moveCost;
        }

        private IEnumerator LookAt(Vector3 point)
        {
            if (HexMetrics.Wrapping)
            {
                float xDistance = point.x - transform.localPosition.x;
                if (xDistance < -HexMetrics.innerRadius * HexMetrics.wrapSize)
                {
                    point.x += HexMetrics.innerDiameter * HexMetrics.wrapSize;
                }
                else if (xDistance > HexMetrics.innerRadius * HexMetrics.wrapSize)
                {
                    point.x -= HexMetrics.innerDiameter * HexMetrics.wrapSize;
                }
            }
            point.y = transform.localPosition.y;
            Quaternion fromRotation = transform.localRotation;
            Quaternion toRotation = Quaternion.LookRotation(point - transform.localPosition);
            float angle = Quaternion.Angle(fromRotation, toRotation);
            if (angle > 0f)
            {
                float speed = rotationSpeed / angle;
                for (float t = Time.deltaTime * speed; t < 1f; t += Time.deltaTime * speed)
                {
                    transform.localRotation = Quaternion.Slerp(fromRotation, toRotation, t);
                    yield return null;
                }
                transform.LookAt(point);
                orientation = transform.localRotation.eulerAngles.y;
            }
        }

        public void Save(BinaryWriter writer)
        {
            location.coordinates.Save(writer);
            writer.Write(orientation);
        }

        public void Travel(List<HexCell> path)
        {
            //Location = path[path.Count - 1];
            location.Unit = null;
            location = path[path.Count - 1];            
            location.Unit = this;
            pathToTravel = path;
            StopAllCoroutines();
            StartCoroutine(TravelPath());
        }

        private IEnumerator TravelPath()
        {
            Vector3 a, b, c = pathToTravel[0].Position;            
            yield return LookAt(pathToTravel[1].Position);
            
            
            if (!currentTravelLocation)
            {
                currentTravelLocation = pathToTravel[0];
            }
            Grid.DecreaseVisibility(currentTravelLocation, VisionRange);
            int currentColumn = currentTravelLocation.ColumnIndex;

            float t = Time.deltaTime * travelSpeed;
            for (int i = 1; i < pathToTravel.Count; i++)
            {
                currentTravelLocation = pathToTravel[i];
                a = c;
                b = pathToTravel[i - 1].Position;

                int nextColumn = currentTravelLocation.ColumnIndex;
                if (currentColumn != nextColumn)
                {
                    if (nextColumn < currentColumn - 1)
                    {
                        a.x -= HexMetrics.innerDiameter * HexMetrics.wrapSize;
                        b.x -= HexMetrics.innerDiameter * HexMetrics.wrapSize;
                    }
                    else if (nextColumn > currentColumn + 1)
                    {
                        a.x += HexMetrics.innerDiameter * HexMetrics.wrapSize;
                        b.x += HexMetrics.innerDiameter * HexMetrics.wrapSize;
                    }
                    Grid.MakeChildOfColumn(transform, nextColumn);
                    currentColumn = nextColumn;
                }

                c = (b + currentTravelLocation.Position) * 0.5f;
                Grid.IncreaseVisibility(pathToTravel[i], visionRange);

                for (; t < 1f; t += Time.deltaTime * travelSpeed)
                {
                    transform.localPosition = Bezier.GetPoint(a, b, c, t);
                    Vector3 d = Bezier.GetDerivative(a, b, c, t);
                    d.y = 0f;
                    transform.localRotation = Quaternion.LookRotation(d);
                    yield return null;
                }
                Grid.DecreaseVisibility(pathToTravel[i], visionRange);
                t -= 1f;
            }
            currentTravelLocation = null;

            a = c;
            b = location.Position;
            c = b;
            Grid.IncreaseVisibility(location, visionRange);
            for (; t < 1f; t += Time.deltaTime * travelSpeed)
            {
                transform.localPosition = Bezier.GetPoint(a, b, c, t);
                Vector3 d = Bezier.GetDerivative(a, b, c, t);
                d.y = 0f;
                transform.localRotation = Quaternion.LookRotation(d);
                yield return null;
            }
            transform.localPosition = location.Position;
            orientation = transform.localRotation.eulerAngles.y;
            ListPool<HexCell>.Add(pathToTravel);
            pathToTravel = null;
        }

        public static void Load(BinaryReader reader,HexGrid grid)
        {
            HexCoordinates coordinates = HexCoordinates.Load(reader);
            float orientation = reader.ReadSingle();
            grid.AddUnit(Instantiate(unitPrefab), grid.GetCell(coordinates), orientation);
        }
    }
}

