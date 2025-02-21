using UnityEditor;
using UnityEngine;

namespace DecalSplines
{
#if UNITY_EDITOR || DECALSPLINES_ALLOW_RUNTIME
    public class MeshSplineSegment : ISplineSegment
    {
        [SerializeField] private ModelManager modelManager;

        public static MeshSplineSegment Spawn(string name, MeshSplineStyle style, Vector3 position, Transform parent,DecalSpline parentSpline)
        {
            GameObject segmentObject = new GameObject();
            segmentObject.name = name;

#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(segmentObject, "Added spline segment.");
            Undo.SetTransformParent(segmentObject.transform, parent, "Set parent");
            MeshSplineSegment newSegment = Undo.AddComponent<MeshSplineSegment>(segmentObject);
#else
            segmentObject.transform.parent = parent;
            MeshSplineSegment newSegment = segmentObject.AddComponent<MeshSplineSegment>();
#endif

            newSegment.style = style;
            newSegment.Position = position;
            newSegment.transform.rotation = parent.rotation;
            newSegment.lockHandles = true;
            newSegment._parentDecalSpline = parentSpline;

            newSegment.prev = null;
            newSegment.next = null;


            newSegment.modelManager = ModelManager.Spawn(newSegment);

            return newSegment;
        }

        public new MeshSplineStyle style
        {
            get { return (MeshSplineStyle)_style; }
            set { _style = value; }
        }

        public override void UpdateSegment()
        {
            if(modelManager != null)
                modelManager.UpdateModels();
        }


        public override Vector3[] GetCurvePoints()
        {
            Vector3[] points = new Vector3[0];
            if (!IsLast())
            {
                float curveLength = GetCurveLength();
                int pointCount = (int)style.BoneCount;
                points = GetBezierPoints(pointCount);
            }
            return points;
        }
    }
#endif
}
