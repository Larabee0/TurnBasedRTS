using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct HexCoordinates
{
    [SerializeField]
    public int x, z;
    public int X
    {
        get
        {
            return x;
        }
    }

    public int Z
    {
        get
        {
            return z;
        }
    }

    public int Y
    {
        get
        {
            return -X - Z;
        }
    }

    public HexCoordinates(int x, int z, int wrapSize)
    {
        if (wrapSize > 0)
        {
            int oX = x + z / 2;
            if (oX < 0)
            {
                x += wrapSize;
            }
            else if (oX >= wrapSize)
            {
                x -= wrapSize;
            }
        }
        this.x = x;
        this.z = z;
    }

    public static HexCoordinates FromOffsetCoordinates(int x, int z, int wrapSize)
    {
        return new HexCoordinates(x - z / 2, z, wrapSize);
    }

    public static HexCoordinates FromPosition(Vector3 position, int wrapSize)
    {
        float x = position.x / HexMetrics.innerDiameter;
        float y = -x;
        float offset = position.z / (HexMetrics.outerRadius * 3f);
        x -= offset;
        y -= offset;
        int iX = Mathf.RoundToInt(x);
        int iY = Mathf.RoundToInt(y);
        int iZ = Mathf.RoundToInt(-x - y);
        if (iX + iY + iZ != 0)
        {
            float dX = Mathf.Abs(x - iX);
            float dY = Mathf.Abs(y - iY);
            float dZ = Mathf.Abs(-x - y - iZ);

            if (dX > dY && dX > dZ)
            {
                iX = -iY - iZ;
            }
            else if (dZ > dY)
            {
                iZ = -iX - iY;
            }
        }
        return new HexCoordinates(iX, iZ, wrapSize);
    }


}
