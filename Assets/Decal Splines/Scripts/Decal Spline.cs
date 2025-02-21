using UnityEditor;
using UnityEngine;

namespace DecalSplines
{
#if UNITY_EDITOR || DECALSPLINES_ALLOW_RUNTIME
    public class DecalSpline : MonoBehaviour
    {
        [SerializeField] private float projectionDepth = 5f;
        [SerializeField] private uint renderLayerMask = uint.MaxValue;
        [SerializeField] private bool autoSnap = true;
        [SerializeField] private bool liveUpdate = true;

        [SerializeField] private SplineTheme activeTheme;
        [SerializeField] private float fadeStrength = .25f;
        [SerializeField] private float fadePaintGiszmoSize = 2f;
        [SerializeField] private float fadeFactor = 1f;
        [SerializeField] private float widthScalar = 1f;

        public float ProjectionDepth {get{return projectionDepth;} }
        public uint RenderLayerMask { get { return renderLayerMask; } }
        public bool AutoSnap { get { return autoSnap; } }
        public bool LiveUpdate { get { return liveUpdate; } }
        public float FadeStrenght { get { return fadeStrength; } }
        public float FadePaintGiszmoSize { get { return fadePaintGiszmoSize; } }
        public float FadeFactor { get { return fadeFactor; } }
        public float WidthScalar { get { return widthScalar; } }

        private SegmentManager _segmentManager;
        private SegmentManager segmentManager
        {
            get
            {
                if (_segmentManager != null)
                    return _segmentManager;
                FindSegmentsManager();
                return _segmentManager;
            }
        }
        public SegmentManager SegmentManager { get { return segmentManager; } }

#if UNITY_EDITOR
        //Draw the Editor Gizmos
        public void DrawGizmos(SceneViewEditMode editMode)
        {
            segmentManager.DrawGizmos(editMode);
        }
#endif

        //Find the Segments Transform or Create one if it doesn't excist yet.
        private void FindSegmentsManager()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                //if the child's name is "Segments" then it is the segment container and _segments will be assigned.
                Transform child = transform.GetChild(i);
                if (child.name == "Segments")
                {
                    child.TryGetComponent<SegmentManager>(out _segmentManager);
                    return;
                }
            }
            //if no matching container was found an object named "Segments" will be created.
            GameObject segmentContainer = new GameObject();
            segmentContainer.name = "Segments";
            segmentContainer.transform.parent = transform;
            segmentContainer.transform.position = transform.position;
            _segmentManager = segmentContainer.AddComponent<SegmentManager>();

        }

        //Snaps the Decal Spline in place
        public void Snap()
        {
            segmentManager.Snap(transform.rotation);
        }

        //Update the Decal Spline
        public void UpdateDecalSpline()
        {
            segmentManager.UpdateDecalSpline();
        }

        //Deletes all segments
        public void ClearDecalSpline()
        {
            segmentManager.ClearSegments();
        }

        public void AddSegment(Vector3 position, ISplineStyle style)
        {
            segmentManager.AddSegment(position, style, this);  
        }

        public void ReplaceAllStyles(ISplineStyle newStyle)
        {
            segmentManager.ReplaceAllStyles(newStyle);
        }

        public void FadePaint(Vector3 position, float fadeStrength, float fadeSize)
        {
            segmentManager.FadePaint(position,fadeStrength,fadeSize);
        }
    }
#endif
}
