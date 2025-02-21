using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;


namespace DecalSplines
{
#if UNITY_EDITOR || DECALSPLINES_ALLOW_RUNTIME
    public class ISplineSegment : MonoBehaviour
    {
        [HideInInspector][SerializeField] protected DecalSpline _parentDecalSpline;
        [SerializeField] protected ISplineStyle _style;
        [SerializeField] protected Vector3 _h1;//Bezier handle1
        [SerializeField] protected Vector3 _h2;//Bezier handle2
        [SerializeField] public bool lockHandles;
        [SerializeField] public ISplineSegment prev;
        [SerializeField] public ISplineSegment next;

        public EventHandler OnRemoved;

        public Vector3 Position
        {
            get { return transform.localPosition; }
            set
            {
                Vector3 displacement = value - transform.localPosition;
                _h1 = _h1 + displacement;
                transform.localPosition = value;
                if (!IsFirst())
                {
                    prev._h2 += displacement;
                }
            }
        }

        public Vector3 h1
        {
            get { return _h1; }
            set { SetHandles(value, _h2); }
        }
        public Vector3 h2
        {
            get { return _h2; }
            set { SetHandles(_h1, value); }
        }

        public ISplineStyle style
        {
            get { return _style; }
            set 
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Undo.RecordObject(this, "Replaced Style");
#endif
                    ISplineSegment newSegment = Convert(this, value);
                    newSegment.SetAllConnectedToFixedLength();
#if UNITY_EDITOR 
                if (!Application.isPlaying)
                    EditorUtility.SetDirty(newSegment);    
#endif
            }
        }

        public DecalSpline ParentDecalSpline
        {
            get { return _parentDecalSpline; }
        }

        public virtual void UpdateSegment()
        { }

        public virtual void Destroy(bool undoable = false)
        {
#if UNITY_EDITOR
            if (undoable && !Application.isPlaying)
                Undo.DestroyObjectImmediate(gameObject);
            else
                DestroyImmediate(gameObject);
#else
            DestroyImmediate(gameObject);
#endif
        }

        public virtual Vector3[] GetCurvePoints()
        { return null; }

        public void Straighten(bool undoable = false)
        {
            if (!IsLast())
            {
#if UNITY_EDITOR
                if (undoable && !Application.isPlaying)
                {
                    Undo.RecordObject(this, "Set handles");
                    EditorUtility.SetDirty(this);
                }
#endif
                h1 = Vector3.Lerp(Position, next.Position, 0.1f);
                h2 = Vector3.Lerp(Position, next.Position, 0.9f);
            }
        }

        public void AutoSetHandles(bool undoable = false)
        {
            if (!IsFirst())
            {
                if (!IsLast())
                {
#if UNITY_EDITOR
                    if (undoable && !Application.isPlaying)
                    {
                        Undo.RecordObject(this, "Set handles");
                        Undo.RecordObject(prev, "Set handles");
                        EditorUtility.SetDirty(this);
                        EditorUtility.SetDirty(prev);
                    }
#endif
                    Vector3 tangent = next.Position - prev.Position;
                    tangent.Normalize();

                    float prevLenght = prev.Length;

                    _h1 = Position + tangent * Length * style.Curviness*0.05f;
                    prev.h2 = Position - tangent * prevLenght * style.Curviness * 0.05f;
                }
            }
        }

        private void SetHandles(Vector3 h1, Vector3 h2)
        {
            if (h1 != _h1)
            {
                _h1 = h1;
                if (lockHandles)
                {
                    if (!IsFirst())
                    {
                        float prevHandleLength = Vector3.Distance(prev.h2, Position);
                        Vector3 dir = Position - h1;
                        dir.Normalize();

                        prev._h2 = Position + dir * prevHandleLength;
                    }
                }
            }

            if (h2 != _h2)
            {
                _h2 = h2;
                if (!IsLast())
                {
                    if (next.lockHandles)
                    {
                        float nextHandleLength = Vector3.Distance(next.h1, next.Position);
                        Vector3 dir = next.Position - h2;
                        dir.Normalize();

                        next._h1 = next.Position + dir * nextHandleLength;
                    }
                }
            }
        }

#if UNITY_EDITOR
        public void DrawGizmos(SceneViewEditMode editMode)
        {
            SplineUtility.DrawGizmos(this,editMode);
        }
#endif

        public ISplineSegment Last
        {
            get
            {
                if (!IsLast())
                {
                    return next.Last;
                }

                return this;
            }
        }

        public ISplineSegment First
        {
            get
            {
                if (!IsFirst())
                {
                    return prev.First;
                }

                return this;
            }
        }

        public void Cut()
        {
            while (!IsLast())
                Last.Remove();

            Remove();
        }

        public void Insert(ISplineSegment segment, bool undoable = false)
        {
            if (segment != null)
            {
                if (!IsLast())
                {
                    ISplineSegment newSegmentLast = segment.Last;

#if UNITY_EDITOR
                    if (undoable && !Application.isPlaying)
                    {
                        Undo.RecordObject(next, "Inserted Segment");
                        Undo.RecordObject(newSegmentLast, "Inserted Segment");
                        EditorUtility.SetDirty(next);
                        EditorUtility.SetDirty(newSegmentLast);
                    }
#endif
                    next.prev = newSegmentLast;
                    newSegmentLast.next = next;
                }

#if UNITY_EDITOR
                if (undoable && !Application.isPlaying)
                {
                    Undo.RecordObject(this, "Inserted Segment");
                    Undo.RecordObject(segment, "Inserted Segment");
                    EditorUtility.SetDirty(this);
                    EditorUtility.SetDirty(segment);
                }
#endif
                next = segment;
                segment.prev = this;

                Straighten(undoable);
                AutoSetHandles(undoable);

            } 
        }

        public void InsertNew(string name ,Vector3 pos,ISplineStyle style)
        {
            ISplineSegment newSegment = null;
            if (style.GetType() == typeof(DecalSplineStyle))
                newSegment = DecalSplineSegment.Spawn(name, (DecalSplineStyle)style, pos, transform.parent,ParentDecalSpline);
            else if (style.GetType() == typeof(MeshSplineStyle))
                newSegment = MeshSplineSegment.Spawn(name, (MeshSplineStyle)style, pos, transform.parent, ParentDecalSpline);
            else if (style.GetType() == typeof(NoneSplineStyle))
                newSegment = NoneSplineSegment.Spawn(name, (NoneSplineStyle)style, pos, transform.parent, ParentDecalSpline);

            Vector3 originalH1 = h1;
            Vector3 originalH2 = h2;
            Insert(newSegment, true);
            newSegment.Straighten(true);
            newSegment.AutoSetHandles(true);
            h1 = originalH1;
            newSegment.h2 = originalH2;
        }

        public void Remove(bool undoable = false)
        {
            if (!IsFirst())
            {
#if UNITY_EDITOR
                if (undoable && !Application.isPlaying)
                {
                    Undo.RecordObject(prev, "prev");
                    EditorUtility.SetDirty(prev);
                }
#endif
                prev.next = next;
            }
            if (!IsLast())
            {
#if UNITY_EDITOR
                if (undoable && !Application.isPlaying)
                {
                    Undo.RecordObject(next, "next");
                    EditorUtility.SetDirty(next);
                }
#endif
                next.prev = prev;
            }

            OnRemoved?.Invoke(this, EventArgs.Empty);

            Destroy(undoable);
        }

        public void Replace(ISplineSegment segment, bool undoable = false)
        {
            if (segment != null)
            {
                if (!IsLast())
                {
                    ISplineSegment newSegmentLast = segment.Last;
#if UNITY_EDITOR
                    if (undoable && !Application.isPlaying)
                    {
                        Undo.RecordObject(next, "next");
                        Undo.RecordObject(newSegmentLast, "newSegmentLast");
                        EditorUtility.SetDirty(next);
                        EditorUtility.SetDirty(newSegmentLast);
                    }
#endif
                    next.prev = newSegmentLast;
                    newSegmentLast.next = next;

                }
#if UNITY_EDITOR
                if (undoable && !Application.isPlaying)
                {
                    Undo.RecordObject(segment, "segment");
                    Undo.RecordObject(this, "this");
                    EditorUtility.SetDirty(segment);
                }
#endif
                segment._h1 = _h1;
                segment._h2 = _h2;
                segment.lockHandles = lockHandles;
                next = segment;
                segment.prev = this;

            }
            Remove(undoable);
        }

        public void Append(ISplineSegment segment,bool undoable = false)
        {
            if (segment != null)
            {
                ISplineSegment last = Last;

                //place the segment fixedlenght away from the previous one if a fixedLength is set.
                if (segment.style.IsFixedLenght())
                {
                    Vector3 dir = (segment.Position - last.Position).normalized;
                    Vector3 newPos = last.Position + dir * segment.style.FixedLength;
                    segment.Position = newPos;
                }


                last.Insert(segment, undoable);
                //convert the connecting segment to match the inserted style
                last.style = segment.style;
            }
        }

        public bool IsLast() 
        {
            return next == null;
        }

        public bool IsFirst() { return prev == null; }

        public int Count
        {
            get
            {
                if (next != null)
                    return next.Count + 1;

                return 1;
            }
        }

        public float Length
        {
            get
            {
                if (!IsLast())
                    return Vector3.Distance(Position, next.Position);
                else
                    return 0;
            }
        }

        public float GetCurveLength()
        {
            float result = 0;
            if (!IsLast())
            {
                Vector3[] points = GetBezierPoints(1000);

                for (int i = 0; i < points.Length - 1; i++)
                {
                    result += Vector3.Distance(points[i], points[i + 1]);
                }
            }
            return result;
        }

        public bool IsEnd()
        {
            bool result = false;
            if (IsLast())
                result = true;
            else
            {
                if (next.GetType() != this.GetType())
                    result = true;
            }
            return result;
        }

        public Vector3[] GetBezierPoints(int division)
        {
            Vector3[] result = null;
            if (!IsLast() && division > 0)
            {
                result = new Vector3[division];
                float tStep = 1f / (division-1);
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = GetBezierPoint(tStep * i);
                }

            }
            return result;
        }

        public Vector3 GetBezierPoint(float t)
        {
            if (!IsLast())
            {
                Vector3 p0 = Vector3.Lerp(Position, h1, t);
                Vector3 p1 = Vector3.Lerp(h1, h2, t);
                Vector3 quadp0 = Vector3.Lerp(p0, p1, t);

                Vector3 p2 = Vector3.Lerp(h1, h2, t);
                Vector3 p3 = Vector3.Lerp(h2, next.Position, t);
                Vector3 quadp1 = Vector3.Lerp(p2, p3, t);

                return Vector3.Lerp(quadp0, quadp1, t);
            }
            return Vector3.zero;
        }

        public int SetAllConnectedToFixedLength()
        {
            int chainLenght = 0;
            ISplineSegment nextSegment = next;
            while (nextSegment != null)
            {
                if (nextSegment.prev.style.IsFixedLenght())
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        Undo.RecordObject(nextSegment, "Set Lenght of Segment");
                        Undo.RecordObject(nextSegment.transform, "Set Lenght of Segment");
                        EditorUtility.SetDirty(nextSegment);
                    }
#endif
                    Vector3 segPos = nextSegment.Position;
                    Vector3 prevPos = nextSegment.prev.Position;

                    Vector3 dir = (segPos - prevPos).normalized;
                    nextSegment.Position = prevPos + dir * nextSegment.prev.style.FixedLength;
                    Straighten();
                    AutoSetHandles();

                    nextSegment = nextSegment.next;
                    chainLenght++;

                }
                else
                {
                    break;
                }
            }
            return chainLenght;
        }

        //Converts the segment to the convertToStyle type if type doesn't match the exsisting style.
        public static ISplineSegment Convert(ISplineSegment segment, ISplineStyle convertToStyle)
        {
            ISplineSegment newSegment = null;
            if (convertToStyle.GetType() == typeof(DecalSplineStyle) && segment.GetType() != typeof(DecalSplineSegment))
            {
                newSegment = DecalSplineSegment.Spawn(segment.name, (DecalSplineStyle)convertToStyle, segment.Position, segment.transform.parent, segment.ParentDecalSpline);
            }
            else if (convertToStyle.GetType() == typeof(MeshSplineStyle) && (segment.GetType() != typeof(MeshSplineSegment) || convertToStyle != segment.style))
            {
                newSegment = MeshSplineSegment.Spawn(segment.name, (MeshSplineStyle)convertToStyle, segment.Position, segment.transform.parent, segment.ParentDecalSpline);
            }
            else if (convertToStyle.GetType() == typeof(NoneSplineStyle) && segment.GetType() != typeof(NoneSplineSegment))
            {
                newSegment = NoneSplineSegment.Spawn(segment.name, (NoneSplineStyle)convertToStyle, segment.Position, segment.transform.parent, segment.ParentDecalSpline);
            }

            if (newSegment != null)
            {
                newSegment.lockHandles = segment.lockHandles;

                //Convert by replacing the old segment by the one one.
                segment.Replace(newSegment,true);
                return newSegment;
            }

            //Just update the style if no real convertion is required.
            segment._style = convertToStyle;
            return segment;
        }
        
    }
#endif
}
