using System;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;

namespace DecalSplines
{
#if UNITY_EDITOR || DECALSPLINES_ALLOW_RUNTIME
    public class SegmentManager : MonoBehaviour
    {
        //[HideInInspector]
        [SerializeField] private ISplineSegment firstSegment;
        public ISplineSegment FirstSegment { get { return firstSegment; } }

#if UNITY_EDITOR
        public SegmentManager()
        {
                Undo.undoRedoPerformed += HandleUndoRedo;
        }

        //Re add the romoved callback due to it being removed by the undo.
        private void HandleUndoRedo()
        {
            if (firstSegment != null)
            {
                firstSegment.OnRemoved -= OnFirstRemoved;
                firstSegment.OnRemoved += OnFirstRemoved;
            }

        }

#endif

        //Update the Decal Spline
        public void UpdateDecalSpline()
        {
            ISplineSegment segment = firstSegment;
            while (segment != null)
            {
                segment.UpdateSegment();
                segment = segment.next;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
#endif
        }

        public void OnFirstRemoved(object sender, EventArgs e)
        {
            if ((ISplineSegment)sender == firstSegment)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Undo.RecordObject(this, "Set new first segment");
#endif

                firstSegment.OnRemoved -= OnFirstRemoved;
                firstSegment = firstSegment.next;

                if (firstSegment != null)
                {
                    firstSegment.OnRemoved += OnFirstRemoved;
                }
            }
            else
                ((ISplineSegment)sender).OnRemoved -= OnFirstRemoved;
        }


        //Deletes all segments
        public void ClearSegments()
        {
            if (firstSegment != null)
            {
                firstSegment.Cut();

#if UNITY_EDITOR
                if (!Application.isPlaying)
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
#endif
            }
        }

        public void AddSegment(Vector3 position, ISplineStyle style, DecalSpline parentDecalSpline)
        {
            if (style != null)
            {
                int count = 0;
                if (firstSegment != null)
                    count = firstSegment.Count;

                string name = "M" + count.ToString();
                Vector3 pos = transform.InverseTransformPoint(position);

                ISplineSegment newSegment = null;
                if (style.GetType() == typeof(DecalSplineStyle))
                    newSegment = DecalSplineSegment.Spawn(name, (DecalSplineStyle)style, pos, transform, parentDecalSpline);
                else if (style.GetType() == typeof(MeshSplineStyle))
                    newSegment = MeshSplineSegment.Spawn(name, (MeshSplineStyle)style, pos, transform, parentDecalSpline);
                else if (style.GetType() == typeof(NoneSplineStyle))
                    newSegment = NoneSplineSegment.Spawn(name, (NoneSplineStyle)style, pos, transform, parentDecalSpline);

                newSegment.lockHandles = !style.FreeHandles;

                if (firstSegment != null)
                    firstSegment.Append(newSegment,true);
                else
                {
                    firstSegment = newSegment;
                    firstSegment.OnRemoved += OnFirstRemoved;
                }

#if UNITY_EDITOR
                if (!Application.isPlaying)
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
#endif   
            }
        }

        //Thanks Dries.
        public void ReplaceAllStyles(ISplineStyle newStyle)
        {
            if (newStyle != null)
            {
                ISplineSegment segment = firstSegment;
                while (segment != null)
                {
                    segment.style = newStyle;
                    segment = segment.next;
                }
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
#endif   
            }
        }

        public void FadePaint(Vector3 position, float fadeStrength, float fadeSize)
        {
            if (firstSegment != null)
            {
                ISplineSegment segment = firstSegment;
                Vector3 localPos = transform.InverseTransformPoint(position);
                while (segment != null)
                {
                    if (segment.GetType() == typeof(DecalSplineSegment))
                    {
                        float distanceToGizmo = Vector3.Distance(segment.Position, localPos);
                        if (distanceToGizmo <= fadeSize)
                        {
                            float fadeFactor = distanceToGizmo / fadeSize;
                            float fadeAmount = math.lerp(fadeStrength, 0, fadeFactor);
                            ((DecalSplineSegment)segment).Transparency += fadeAmount;
                        }
                    }
                    segment = segment.next;
                }
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
#endif
            }
        }

        public void Snap(Quaternion rotation)
        {
            ISplineSegment segment = firstSegment;
            while (segment != null)
            {
                //Snap PositionHandle
                Vector3 pos = transform.TransformPoint(segment.Position);
                Vector3 newPos = GetSnapPosition(pos, rotation,segment);
                
                if (!segment.IsFirst())
                {
                    if (segment.prev.style.IsFixedLenght())
                    {
                        Vector3 prevWorld = transform.TransformPoint(segment.prev.Position);
                        Vector3 dir = (newPos - prevWorld).normalized;
                        newPos = prevWorld + dir * segment.prev.style.FixedLength;
                    }
                }

                segment.Position = transform.InverseTransformPoint(newPos);

                //Snap H1 and H2
                Vector3 newH1 = transform.TransformPoint(segment.h1);
                segment.h1 = transform.InverseTransformPoint(newH1);
                Vector3 newH2 = transform.TransformPoint(segment.h2);
                segment.h2 = transform.InverseTransformPoint(newH2);

                segment = segment.next;
            }
        }

        private Vector3 GetSnapPosition(Vector3 pos, Quaternion rotation, ISplineSegment segment)
        {
            Vector3 up = rotation * Vector3.up;
            Vector3 down = rotation * Vector3.down;


            Vector3 snapPos = pos;
            float closestDistance = float.MaxValue;

            //Check the down ray first.
            Ray rayDown = new Ray(pos + up * 0.001f, down);
            RaycastHit[] raycastHits = Physics.RaycastAll(rayDown, 2000f);
            if (raycastHits != null && raycastHits.Length > 0)
            {
                foreach (RaycastHit hit in raycastHits)
                {
                    if (hit.distance <= closestDistance)
                    {
                        if (!hit.transform.IsChildOf(segment.transform.parent))
                        {
                            closestDistance = hit.distance;
                            snapPos = hit.point;
                        }
                    }
                }
            }

            //Check the up ray.
            Ray rayUp = new Ray(pos + down * 0.001f, up);
            raycastHits = Physics.RaycastAll(rayUp, 2000f);
            if (raycastHits != null && raycastHits.Length > 0)
            {
                foreach (RaycastHit hit in raycastHits)
                {
                    if (hit.distance <= closestDistance)
                    {
                        if (!hit.transform.IsChildOf(segment.transform.parent))
                        {
                            closestDistance = hit.distance;
                            snapPos = hit.point;
                        }
                    }
                }
            }

            return snapPos;
        }

#if UNITY_EDITOR
        //Draw the Editor Gizmos
        public void DrawGizmos(SceneViewEditMode editMode)
        {
            if (firstSegment != null)
            {
                ISplineSegment segment = firstSegment;
                while (segment != null)
                {
                    segment.DrawGizmos(editMode);
                    segment = segment.next;
                }
            }
        }
#endif
    }
#endif
}