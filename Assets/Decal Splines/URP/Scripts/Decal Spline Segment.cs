using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DecalSplines
{
#if UNITY_EDITOR || DECALSPLINES_ALLOW_RUNTIME
    public class DecalSplineSegment : ISplineSegment
    {
        [SerializeField] private ProjectorManager projectorManager;
        [SerializeField] private float transparency = 1;

        public float Transparency
        {
            get { return transparency; }
            set 
            {
                if (value < 0)
                    transparency = 0;
                else if (value > 1)
                    transparency = 1;
                else
                    transparency = value;
            }
        }
 
        public static DecalSplineSegment Spawn(string name, DecalSplineStyle style, Vector3 position, Transform parentTransform, DecalSpline parentSpline)
        {
            GameObject segmentObject = new GameObject();
            segmentObject.name = name;
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(segmentObject, "Added spline segment.");
            Undo.SetTransformParent(segmentObject.transform, parentTransform,"Set parent for new segment");
            DecalSplineSegment newSegment = Undo.AddComponent<DecalSplineSegment>(segmentObject);
#else
            segmentObject.transform.parent = parentTransform;
            DecalSplineSegment newSegment = segmentObject.AddComponent<DecalSplineSegment>();
#endif

            newSegment.style = style;
            newSegment.Position = position;
            newSegment.transform.rotation = parentTransform.rotation;
            newSegment._h1 = position;
            newSegment._h2 = position;
            newSegment.lockHandles = true;
            newSegment._parentDecalSpline = parentSpline;

            newSegment.prev = null;
            newSegment.next = null;

            newSegment.projectorManager = ProjectorManager.Spawn(newSegment);

#if UNITY_EDITOR
            Undo.RegisterCompleteObjectUndo(newSegment, "Configure spline segement");
#endif

            return newSegment;

        }

        public override void Destroy(bool undoable = false)
        {
#if UNITY_EDITOR
            Undo.RegisterCompleteObjectUndo(this, "Removed Segment");
#endif
            projectorManager.Destroy(undoable);
            base.Destroy(undoable);
        }

        public new DecalSplineStyle style
        {
            get { return (DecalSplineStyle)_style; }
            set { _style = value; }
        }

        public override void UpdateSegment()
        {
            if (projectorManager != null)
                projectorManager.UpdateProjectors();
        }

        public override Vector3[] GetCurvePoints()
        {
            Vector3[] points = new Vector3[0];
            if (!IsLast())
            {
                float resolution = style.Resolution;
                float curveLength = GetCurveLength();
                int pointCount = (int)(curveLength * resolution) + 2;
                points = GetBezierPoints(pointCount);
            }
            return points;
        }

        public DecalProjector GetFirstProjector()
        {
            return projectorManager.GetFirstProjector();
        }

        public DecalProjector GetLastProjector()
        {
            return projectorManager.GetLastProjector();
        }

        public void DisconnectLastProjector()
        {
            projectorManager.DisconnectLastProjector();
        }

        public void DisconnectFirstProjector()
        {
            projectorManager.DisconnectFirstProjector();
        }
    }
#endif
}