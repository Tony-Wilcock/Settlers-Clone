using UnityEngine;

[System.Serializable]
public struct HexCoordinates
{
    [SerializeField]
    private int x, z;

    public int X { get { return x; } }
    public int Z { get { return z; } }
    public int Y { get { return -X - Z; } } // Calculate axial Y coordinate


    public HexCoordinates(int x, int z)
    {
        this.x = x;
        this.z = z;
    }

    public static HexCoordinates FromOffsetCoordinates(int x, int z)
    {
        return new HexCoordinates(x - z / 2, z);  //Crucial conversion!
    }

    public override string ToString()
    {
        return "(" + X.ToString() + ", " + Y.ToString() + ", " + Z.ToString() + ")";
    }

    public string ToStringOnSeparateLines()
    {
        return X.ToString() + "\n" + Y.ToString() + "\n" + Z.ToString();
    }

    public static HexCoordinates FromPosition(Vector3 position, float hexWidth, float hexHeight)
    {
        // Inverse transformation from world position to hex coordinates
        float q = (2f / 3f * position.x) / hexWidth;
        float r = (-1f / 3f * position.x + Mathf.Sqrt(3f) / 3f * position.z) / hexWidth; // Using hexWidth since it defines the overall scale

        return AxialToCube(q, r).RoundToCubeCoordinates();
    }

    // Helper methods for coordinate conversion and rounding

    private static HexCoordinates AxialToCube(float q, float r)
    {
        //Axial to Cube
        int x = Mathf.RoundToInt(q);
        int z = Mathf.RoundToInt(r);
        return new HexCoordinates(x, z);
    }

    private HexCoordinates RoundToCubeCoordinates()
    {
        //Cube Coordinate Rounding
        int rx = X;
        int ry = Y;
        int rz = Z;

        float xDiff = Mathf.Abs(rx - X);
        float yDiff = Mathf.Abs(ry - Y);
        float zDiff = Mathf.Abs(rz - Z);


        if (xDiff > yDiff && xDiff > zDiff)
        {
            rx = -ry - rz;
        }
        else if (yDiff > zDiff)
        {
            ry = -rx - rz;
        }
        else
        {
            rz = -rx - ry;
        }
        return new HexCoordinates(rx, rz); // Return in x,z format, as y is derived.
    }

}