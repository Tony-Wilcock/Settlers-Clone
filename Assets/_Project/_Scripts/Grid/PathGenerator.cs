using DecalSplines;
using UnityEngine;


public class PathGenerator : MonoBehaviour
{
    [SerializeField] DecalSplineStyle style;
    [SerializeField] DecalSpline decalSpline;

    public void CreatePath(Vector3[] points)
    {
        if (!CanCreatePath(points))
        {
            Debug.LogError("PathGenerator.CreatePath: Cannot create path due to invalid configuration.");
            return;
        }

        DecalSpline newSpline = Instantiate(decalSpline);

        AddSegmentsToSpline(newSpline, points);

        newSpline.UpdateDecalSpline();
    }

    private bool CanCreatePath(Vector3[] points)
    {
        if (decalSpline == null)
        {
            Debug.LogError("PathGenerator: decalSplinePrefab is not assigned in the Inspector!");
            return false;
        }
        if (style == null)
        {
            Debug.LogError("PathGenerator: style is not assigned in the Inspector!");
            return false;
        }
        if (points == null || points.Length < 2)
        {
            Debug.LogError("PathGenerator: points array is null or has fewer than 2 points!");
            return false;
        }
        return true;
    }

    private void AddSegmentsToSpline(DecalSpline spline, Vector3[] points)
    {
        foreach (Vector3 point in points)
        {
            spline.AddSegment(point, style);
        }
    }
}
