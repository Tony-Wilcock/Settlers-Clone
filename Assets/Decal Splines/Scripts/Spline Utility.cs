
using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.UIElements;

namespace DecalSplines
{
#if UNITY_EDITOR
    public enum SceneViewEditMode
    {
        None, PlaceMode, FadePaint
    }

    [Serializable]
    public static class SplineUtility
    {
        public static void DrawCurvePoints(ISplineSegment segment)
        {         
            if (!segment.IsLast())
            {
                Transform parent = segment.transform.parent;
                Vector3 posWorld = parent.TransformPoint(segment.Position);
                Vector3 h1World = parent.TransformPoint(segment.h1);
                Vector3 h2World = parent.TransformPoint(segment.h2);
                Vector3 nextWorld = parent.TransformPoint(segment.next.Position);
                
                float pointSize = 0.01f;
                Handles.color = Color.red;
                Vector3[] points = Handles.MakeBezierPoints(posWorld, nextWorld, h1World,h2World,19);
                for (int i = 0; i < points.Length; i++)
                {
                    Handles.Button(points[i], Quaternion.identity, pointSize, pointSize, Handles.SphereHandleCap);
                    if (i + 1 < points.Length)
                        Handles.DrawLine(points[i], points[i + 1]);
                }
            }
        }

        public static void DrawGizmos(ISplineSegment segment, SceneViewEditMode editMode)
        {
            Undo.RecordObject(segment, $"Changed {segment.name}.");
            Undo.RecordObject(segment.transform, $"Changed {segment.name}.");

            DrawCurve(segment);
            DrawInsertHandle(segment,editMode);
            DrawCurveHandles(segment, editMode);
            DrawPositionHandle(segment, editMode);
            //DrawCurvePoints(segment);
        }

        private static void DrawCurveHandles(ISplineSegment segment,SceneViewEditMode editMode)
        {
            if (!segment.IsLast())
            {
                Transform parent = segment.transform.parent;
                Vector3 posWorld = parent.TransformPoint(segment.Position);
                Vector3 h1World = parent.TransformPoint(segment.h1);
                Vector3 h2World = parent.TransformPoint(segment.h2);
                Vector3 nextWorld = parent.TransformPoint(segment.next.Position);

                //Draw curve handles.
                float curveHandleSizeH1 = EditorStyleUtility.Styles.CurveHandleSize;
                if (EditorStyleUtility.Styles.ScreenSpaceHandles)
                    curveHandleSizeH1 *= HandleUtility.GetHandleSize(h1World);

                //Draw h1.
                if (segment.lockHandles)
                    Handles.color = EditorStyleUtility.Styles.CurveHandleColor;
                else
                    Handles.color = EditorStyleUtility.Styles.CurveHandleUnlockedColor;

                if (!EditorInput.CTRLDown)//Ctrl to toggle locked handles
                {
                    var fmh_78_68_638637312294779109 = Quaternion.identity; Vector3 newH1 = Handles.FreeMoveHandle(h1World, curveHandleSizeH1, Vector3.zero, Handles.SphereHandleCap);
                    if (newH1 != h1World)
                    {
                        if (segment.ParentDecalSpline.AutoSnap != EditorInput.ShiftDown)//Shift to free move
                        {
                            Camera sceneCam = SceneView.currentDrawingSceneView.camera;
                            Vector2 screenPixelCoord = sceneCam.WorldToScreenPoint(newH1);

                            Vector3 mousePos3D;
                            if (EditorInput.MousePosition3D(screenPixelCoord, segment.transform.parent, out mousePos3D))
                            newH1 = mousePos3D;
                        }
                        if (editMode == SceneViewEditMode.None)
                        {
                            segment.h1 = parent.InverseTransformPoint(newH1);
                            UpdateSegment(segment);
                            if (!segment.IsFirst())
                                UpdateSegment(segment.prev);
                        }
                    }
                }
                else
                {
                    if (Handles.Button(h1World, Quaternion.identity, curveHandleSizeH1, curveHandleSizeH1, Handles.SphereHandleCap))
                        if (editMode == SceneViewEditMode.None)
                            segment.lockHandles = !segment.lockHandles;
                }

                float curveHandleSizeH2 = EditorStyleUtility.Styles.CurveHandleSize;
                if (EditorStyleUtility.Styles.ScreenSpaceHandles)
                    curveHandleSizeH2 *= HandleUtility.GetHandleSize(h2World);

                //Draw h2.
                if (segment.next.lockHandles)
                    Handles.color = EditorStyleUtility.Styles.CurveHandleColor;
                else
                    Handles.color = EditorStyleUtility.Styles.CurveHandleUnlockedColor;

                if (!EditorInput.CTRLDown)//Ctrl to toggle locked handles
                {
                    var fmh_118_68_638637312294802912 = Quaternion.identity; Vector3 newH2 = Handles.FreeMoveHandle(h2World, curveHandleSizeH2, Vector3.zero, Handles.SphereHandleCap);
                    if (newH2 != h2World)
                    {
                        if (segment.ParentDecalSpline.AutoSnap != EditorInput.ShiftDown)//Shift to free move
                        {
                            Camera sceneCam = SceneView.currentDrawingSceneView.camera;
                            Vector2 screenPixelCoord = sceneCam.WorldToScreenPoint(newH2);

                            Vector3 mousePos3D;
                            if (EditorInput.MousePosition3D(screenPixelCoord, segment.transform.parent, out mousePos3D))
                                newH2 = mousePos3D;
                        }
                        if (editMode == SceneViewEditMode.None)
                        {
                            segment.h2 = parent.InverseTransformPoint(newH2);
                            UpdateSegment(segment);
                            if (!segment.IsLast())
                                UpdateSegment(segment.next);
                        }
                    }

                }
                else
                {
                    if (Handles.Button(h2World, Quaternion.identity, curveHandleSizeH1, curveHandleSizeH1, Handles.SphereHandleCap))
                    {
                        if (editMode == SceneViewEditMode.None)
                            segment.next.lockHandles = !segment.next.lockHandles;
                    }
                }

                //Draw handle lines
                Handles.color = Color.white;
                Handles.DrawLine(h1World, posWorld);
                Handles.DrawLine(h2World, nextWorld);
            }
        }
        private static void DrawCurve(ISplineSegment segment)
        {
            if (!segment.IsLast())
            {
                Transform parent = segment.transform.parent;
                Vector3 posWorld = parent.TransformPoint(segment.Position);
                Vector3 h1World = parent.TransformPoint(segment.h1);
                Vector3 h2World = parent.TransformPoint(segment.h2);
                Vector3 nextWorld = parent.TransformPoint(segment.next.Position);

                //Draw bezier from start to end.
                Handles.color = Color.white;
                Color pathColor = EditorStyleUtility.Styles.PathColorDecal;
                if (segment.style.GetType() == typeof(MeshSplineStyle))
                    Handles.color = EditorStyleUtility.Styles.PathColorMesh;
                if (segment.style.GetType() == typeof(NoneSplineStyle))
                    Handles.color = EditorStyleUtility.Styles.PathColorNone;

                float pathWidth = EditorStyleUtility.Styles.PathWidth;

                Handles.DrawBezier(posWorld, nextWorld, h1World, h2World, pathColor, null, pathWidth);
            }
        }

        private static void DrawInsertHandle(ISplineSegment segment, SceneViewEditMode editMode)
        {
            if (!segment.IsLast() && !segment.style.IsFixedLenght())
            {
                Transform parent = segment.transform.parent;
                Vector3 posWorld = parent.TransformPoint(segment.Position);
                Vector3 h1World = parent.TransformPoint(segment.h1);
                Vector3 h2World = parent.TransformPoint(segment.h2);
                Vector3 nextWorld = parent.TransformPoint(segment.next.Position);

                Vector3[] points = Handles.MakeBezierPoints(posWorld, nextWorld, h1World, h2World, 3);
                if (points != null)
                {
                    float instertHandleSize = EditorStyleUtility.Styles.InsertHandleSize;
                    if (EditorStyleUtility.Styles.ScreenSpaceHandles)
                        instertHandleSize *= HandleUtility.GetHandleSize(points[1]);

                    Handles.color = EditorStyleUtility.Styles.InsertHandleColor;
                    //if (Handles.Button(points[1], Quaternion.identity, instertHandleSize, instertHandleSize, Handles.SphereHandleCap))
                    if(CustomHandleButton(points[1], instertHandleSize))
                    {
                        if (editMode == SceneViewEditMode.None)
                        {
                            int count = segment.First.Count;
                            string name = "M" + count.ToString();
                            Vector3 pos = parent.InverseTransformPoint(points[1]);

                            segment.InsertNew(name, pos, segment.style);
                            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                            segment.ParentDecalSpline.UpdateDecalSpline();
                        }
                    }
                }
            }
        }

        private static void DrawPositionHandle(ISplineSegment segment, SceneViewEditMode editMode)
        {
            Transform parent = segment.transform.parent;
            Vector3 posWorld = parent.TransformPoint(segment.Position);
            //Draw position handle

            float posHandleSize = EditorStyleUtility.Styles.PositionHandleSize;
            if (EditorStyleUtility.Styles.ScreenSpaceHandles)
                posHandleSize *= HandleUtility.GetHandleSize(posWorld);
            Vector3 newPos = posWorld;

            if (!EditorInput.SpaceDown)//Space to hide handle
            {
                if (EditorInput.CTRLDown)//Ctrl remove segments
                {
                    Handles.color = EditorStyleUtility.Styles.PositionHandleRemoveColor;
                    if (Handles.Button(posWorld, Quaternion.identity, posHandleSize, posHandleSize, Handles.SphereHandleCap))
                    {
                        if (editMode == SceneViewEditMode.None)
                            segment.Remove(true);
                        return;
                    }
                }
                else
                {
                    Handles.color = EditorStyleUtility.Styles.PositionHandleColor;
                    var fmh_241_62_638637312294807268 = Quaternion.identity; newPos = Handles.FreeMoveHandle(posWorld, posHandleSize, Vector3.zero, Handles.SphereHandleCap);
                }
            }

            if (newPos != posWorld)
            {
                if (segment.ParentDecalSpline.AutoSnap != EditorInput.ShiftDown)//Shift to toggle autosnap
                {
                    Camera sceneCam = SceneView.currentDrawingSceneView.camera;
                    Vector2 screenPixelCoord = sceneCam.WorldToScreenPoint(newPos);

                    Vector3 mousePos3D;
                    if (EditorInput.MousePosition3D(screenPixelCoord, segment.transform.parent, out mousePos3D))
                        newPos = mousePos3D;
                }

                if (!segment.IsFirst())
                {
                    if (segment.prev.style.IsFixedLenght())
                    {
                        Vector3 prevWorld = parent.TransformPoint(segment.prev.Position);
                        Vector3 dir = (newPos - prevWorld).normalized;
                        newPos = prevWorld + dir * segment.prev.style.FixedLength;
                    }
                }

                if (editMode == SceneViewEditMode.None)
                {
                    segment.Position = parent.InverseTransformPoint(newPos);
                    int updatedChainLenght = segment.SetAllConnectedToFixedLength();
                    UpdateSegmentChain(segment, updatedChainLenght);
                    UpdateSegment(segment);
                    if (!segment.IsFirst())
                        UpdateSegment(segment.prev);
                }

            }
        }

        private static void UpdateSegment(ISplineSegment segment)
        {
            if (segment.ParentDecalSpline.LiveUpdate)
                segment.UpdateSegment();
        }

        private static void UpdateSegmentChain(ISplineSegment segment,int chainLength)
        {
            ISplineSegment currentSegment = segment;
            if (segment.ParentDecalSpline.LiveUpdate)
                for (int i = 0; i < chainLength; i++)
                {
                    currentSegment.UpdateSegment();
                    currentSegment = currentSegment.next;
                }
        }

        private static bool CustomHandleButton(Vector3 position, float size)
        {
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            Vector3 screenPos = Handles.matrix.MultiplyPoint(position);
            bool result = false;
            switch (Event.current.GetTypeForControl(controlID))
            {
                case EventType.Layout:
                    HandleUtility.AddControl(controlID, HandleUtility.DistanceToCircle(screenPos, size));
                    break;
                case EventType.MouseDown:
                    if (HandleUtility.nearestControl == controlID)
                    {
                        GUIUtility.hotControl = controlID;
                        result = true;
                        Event.current.Use();
                    }
                    break;
                case EventType.Repaint:
                    Handles.SphereHandleCap(controlID, position, Quaternion.identity, size, EventType.Repaint);
                    break;
            }
            return result;
        }
    }
#endif
}

