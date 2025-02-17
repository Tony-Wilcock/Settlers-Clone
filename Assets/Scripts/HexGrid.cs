using TMPro;
using UnityEngine;

public class HexGrid : MonoBehaviour
{
    public int width = 6;
    public int height = 6;

    public HexCell cellPrefab;
    public TMP_Text cellLabelPrefab;  // Prefab for the coordinate labels

    private HexCell[] cells;
    private Canvas gridCanvas;  // Canvas for the UI labels
    private HexMesh hexMesh;

    public Color defaultColor = Color.white; // Default cell color


    void Awake()
    {
        gridCanvas = GetComponentInChildren<Canvas>();
        hexMesh = GetComponentInChildren<HexMesh>();
        cells = new HexCell[height * width];

        for (int z = 0, i = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                CreateCell(x, z, i++);
            }
        }
    }

    void Start()
    {
        hexMesh.Triangulate(cells);
    }

    void OnEnable()
    {
        if (!hexMesh)
        {
            hexMesh = GetComponentInChildren<HexMesh>();
        }
        Refresh();
    }

    public void Refresh()
    {
        hexMesh.Triangulate(cells);
    }

    void CreateCell(int x, int z, int i)
    {
        Vector3 position;
        //Crucial positioning calculation, accounts for hex offset.
        position.x = (x + z * 0.5f - z / 2) * HexMetrics.HexWidth();  // Use HexMetrics
        position.y = 0f;
        position.z = z * HexMetrics.HexHeight();

        HexCell cell = cells[i] = Instantiate<HexCell>(cellPrefab);
        cell.transform.SetParent(transform, false);
        cell.transform.localPosition = position;
        cell.coordinates = HexCoordinates.FromOffsetCoordinates(x, z);
        cell.color = defaultColor;  // Set the default color

        TMP_Text label = Instantiate<TMP_Text>(cellLabelPrefab);
        label.rectTransform.SetParent(gridCanvas.transform, false);
        label.rectTransform.anchoredPosition = new Vector2(position.x, position.z);
        label.text = cell.coordinates.ToStringOnSeparateLines();

        cell.name = "HexCell " + cell.coordinates.ToString(); //Good for debugging
    }

    public HexCell GetCell(Vector3 position)
    {
        //Convert a world position to a hex cell

        float hexWidth = HexMetrics.HexWidth(); // Get the correct width
        float hexHeight = HexMetrics.HexHeight(); // Get the correct height
        HexCoordinates coordinates = HexCoordinates.FromPosition(position, hexWidth, hexHeight);
        int index = coordinates.X + coordinates.Z * width + coordinates.Z / 2; // Convert back to array index

        // IMPORTANT: Check bounds before accessing the array!
        if (index >= 0 && index < cells.Length)
        {
            return cells[index];
        }
        else
        {
            Debug.LogWarning("Clicked position is outside the grid bounds.");
            return null; // Or handle the out-of-bounds case appropriately
        }
    }

    public void ColorCell(Vector3 position, Color color)
    {
        HexCell cell = GetCell(position);
        if (cell != null)
        {
            cell.color = color;
            hexMesh.Triangulate(cells); // Efficiently regenerate only the affected part of the mesh

        }

    }
}