using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DecalSplines
{
    //Class used to extend the unity editor in order to be able to place spline segments etc..
    [CustomEditor(typeof(DecalSpline))]
    public class DecalSplineEditor : Editor
    {
        private SceneViewEditMode editMode = SceneViewEditMode.None;
        private SerializedProperty activeTheme;
        private SerializedProperty projectionDepth;
        private SerializedProperty autoSnap;
        private SerializedProperty liveUpdate;
        private SerializedProperty fadeStrength;
        private SerializedProperty fadePaintGiszmoSize;
        private SerializedProperty fadeFactor;
        private SerializedProperty widthScalar;

        private SplineThemeEditor themeEditor;
        private bool prevLeftMouseDown;
        private Color guiColor;

        private void OnEnable()
        {
            activeTheme = serializedObject.FindProperty("activeTheme");
            projectionDepth = serializedObject.FindProperty("projectionDepth");
            autoSnap = serializedObject.FindProperty("autoSnap");
            liveUpdate = serializedObject.FindProperty("liveUpdate");
            fadeStrength = serializedObject.FindProperty("fadeStrength");
            fadePaintGiszmoSize = serializedObject.FindProperty("fadePaintGiszmoSize");
            fadeFactor = serializedObject.FindProperty("fadeFactor");
            widthScalar = serializedObject.FindProperty("widthScalar");

            guiColor = GUI.backgroundColor;
#if UNITY_EDITOR
            Undo.undoRedoPerformed -= HandleUndo;
            Undo.undoRedoPerformed += HandleUndo;
#endif
        }
#if UNITY_EDITOR
        private void HandleUndo()
        {
            DecalSpline decalSpline = (DecalSpline)target;
            decalSpline.UpdateDecalSpline();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= HandleUndo;
        }
#endif

        //Draw the property window
        public override void OnInspectorGUI()
        {
            Event e = Event.current;
            ProcessEvent(e);

            DecalSpline decalSpline = (DecalSpline)target;

            serializedObject.Update();
            GUI.backgroundColor = guiColor;
            GUILayoutOption buttonWidth = GUILayout.Width(90f);
            
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = editMode == SceneViewEditMode.PlaceMode ? Color.green:guiColor;
            //The button to go into place mode
            GUIContent placeButton = new GUIContent("Place", "Toggles Place Mode.");
            //disable button if no theme is active.
            if (activeTheme.objectReferenceValue == null)
            {
                GUI.enabled = false;
                placeButton.tooltip = "An active theme needs to be selected before using Place Mode.";
            }
            if (GUILayout.Button(placeButton, buttonWidth))
            {
                if (activeTheme.objectReferenceValue != null)
                    editMode = editMode == SceneViewEditMode.PlaceMode? SceneViewEditMode.None: SceneViewEditMode.PlaceMode;
            }
            GUI.backgroundColor = guiColor;
            GUI.enabled = true;

            GUIContent maskLabel = new GUIContent("Render Mask", "The Rendering layer mask this Decal Spline is drawn on.");
            GUILayout.Label(maskLabel);
            uint renderLayerMask = serializedObject.FindProperty("renderLayerMask").uintValue;

            EditorGUI.BeginChangeCheck();
            renderLayerMask = RenderLayerManager.DrawInspectorLayerGUI(renderLayerMask);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.FindProperty("renderLayerMask").uintValue = renderLayerMask;
                serializedObject.ApplyModifiedProperties();
                decalSpline.UpdateDecalSpline();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            //The button to go into fade paint mode
            GUIContent fadeButton = new GUIContent("Fade Paint", "Toggles Fade Paint Mode.");
            GUI.backgroundColor = editMode == SceneViewEditMode.FadePaint ? Color.green : guiColor;
            if (GUILayout.Button(fadeButton, buttonWidth))
            {
                editMode = editMode == SceneViewEditMode.FadePaint ? SceneViewEditMode.None : SceneViewEditMode.FadePaint;
            }
            GUI.backgroundColor = guiColor;

            GUIContent strengthLabel = new GUIContent("Strenght :" + fadeStrength.floatValue.ToString("0.00"), "Sets the fade strenght for Fade Paint Mode.");
            GUILayout.Label(strengthLabel, GUILayout.Width(90f));
            fadeStrength.floatValue = GUILayout.HorizontalSlider((fadeStrength).floatValue, 0.01f, 1f);

            GUIContent sizeLabel = new GUIContent("Size : " + fadePaintGiszmoSize.floatValue.ToString("0.00"), "Sets size of the circle in Fade Paint Mode.");
            GUILayout.Label(sizeLabel, GUILayout.Width(70f));
            fadePaintGiszmoSize.floatValue = GUILayout.HorizontalSlider((fadePaintGiszmoSize).floatValue, 0.01f, 50f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            //The button to snap down the spline
            GUIContent snapButton = new GUIContent("Snap", "Snaps the spline to underlaying meshes.");
            if (GUILayout.Button(snapButton, buttonWidth))
            {
                decalSpline.Snap();
                decalSpline.UpdateDecalSpline();
            }
            EditorGUILayout.PropertyField(autoSnap);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            //Button that updates the Decal Spline.
            GUIContent updateButton = new GUIContent("Update", "Updates the Decal Spline manually, used to refresh the Decal Spline");
            if (GUILayout.Button(updateButton, buttonWidth))
            {
                decalSpline.UpdateDecalSpline();
            }
            EditorGUILayout.PropertyField(liveUpdate);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            //Button that clears the Decal Spline
            GUIContent clearButton = new GUIContent("Clear All", "Clears the entire Decal Spline, this can't be undone.");
            if (GUILayout.Button(clearButton, buttonWidth))
            {
                //Comfirmation Window to prevent accidental clearing
                if (EditorUtility.DisplayDialog("Clear Decal Spline", "Confirm that you want to permanetly clear the selected Decal spline.", "Confirm"))
                {
                    decalSpline.ClearDecalSpline();
                    serializedObject.Update();
                }
            }
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(projectionDepth);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                decalSpline.UpdateDecalSpline();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            //Button that replaces all styles on the Decal Spline
            GUIContent replaceButton = new GUIContent("Replace All", "Replaces all spline segments with the currently selected style.");
            if (GUILayout.Button(replaceButton, buttonWidth))
            {
                SplineTheme theme = (SplineTheme)(activeTheme.objectReferenceValue);
                decalSpline.ReplaceAllStyles(theme.ActiveStyle);
                serializedObject.Update();
            }

            //Fade Factor UI
            GUIContent fadeFactorLabel = new GUIContent("Opacity : " + fadeFactor.floatValue.ToString("0.00"), "Controls to opacity of the Decal Spline.");
            GUILayout.Label(fadeFactorLabel, GUILayout.Width(90f));
            fadeFactor.floatValue = GUILayout.HorizontalSlider((fadeFactor).floatValue, 0.00f, 1.00f);

            //Width Scalar UI
            GUIContent widthScalarLabel = new GUIContent("Scale : " + widthScalar.floatValue.ToString("0.0"), "Scales the DecalSpline");
            GUILayout.Label(widthScalarLabel, GUILayout.Width(80f));
            widthScalar.floatValue = GUILayout.HorizontalSlider((widthScalar).floatValue, 0.1f, 5f);

            EditorGUILayout.EndHorizontal();

            //The style selection box as nested editor
            EditorGUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Theme Settings:");
            GUILayout.EndHorizontal();

            //The active theme selection
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(activeTheme);
            if (EditorGUI.EndChangeCheck())
            {
                themeEditor = null;
            }
            if (activeTheme.objectReferenceValue != null)
            {
                ThemeEditor();
            }

          
            serializedObject.ApplyModifiedProperties();
        }

        private void ThemeEditor()
        {
            if (themeEditor == null)
            {
                themeEditor = (SplineThemeEditor)CreateEditor(activeTheme.objectReferenceValue as SplineTheme);
                themeEditor.OnActiveStyleChanged += ThemeActiveStyleChanged;
            }

            themeEditor.OnInspectorGUI();
        }


        public void OnSceneGUI()
        {
            DecalSpline decalSpline = (DecalSpline)target;

            Event e = Event.current;
            ProcessEvent(e);

            switch (editMode)
            {
                case SceneViewEditMode.PlaceMode:
                    HandlePlaceMode(e, decalSpline);
                    break;
                case SceneViewEditMode.FadePaint:
                    HandleFadePaint(e, decalSpline);
                    break;
            }

            CheckTransfromChanged(decalSpline);
            DrawProjectionGizmo(decalSpline);
            decalSpline.DrawGizmos(editMode);

            if (prevLeftMouseDown && !EditorInput.LeftMouseDown)
                decalSpline.UpdateDecalSpline();

            prevLeftMouseDown = EditorInput.LeftMouseDown;

            //keep the editor refreshing as long as Decal Spline is selected.
            SceneView.RepaintAll();
        }

        private void ProcessEvent(Event e)
        {
            if (e.type == EventType.MouseDown && e.button == 0)
                EditorInput.LeftMouseDown = true;
            if (e.type == EventType.MouseUp && e.button == 0)
                EditorInput.LeftMouseDown = false;

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space)
                EditorInput.SpaceDown = true;
            if (e.type == EventType.KeyUp && e.keyCode == KeyCode.Space)
                EditorInput.SpaceDown = false;

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.LeftControl)
                EditorInput.CTRLDown = true;
            if (e.type == EventType.KeyUp && e.keyCode == KeyCode.LeftControl)
                EditorInput.CTRLDown = false;

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.LeftShift)
                EditorInput.ShiftDown = true;
            if (e.type == EventType.KeyUp && e.keyCode == KeyCode.LeftShift)
                EditorInput.ShiftDown = false;
        }

        private void HandlePlaceMode(Event e, DecalSpline decalSpline)
        {
            //Escape to exit place mode.
            if (e.isKey && e.keyCode == KeyCode.Escape)
            {
                editMode = SceneViewEditMode.None;
                Repaint();
            }

            //Check the mouse3DPos and draw the placement gizmo.
            Vector3 mousePos3D;
            if (EditorInput.MousePosition3D(HandleUtility.GUIPointToScreenPixelCoordinate(e.mousePosition), decalSpline.transform, out mousePos3D))
            {
                SplineTheme theme = (SplineTheme)(activeTheme.objectReferenceValue);
                if (theme != null)
                {
                    ISplineStyle style = theme.ActiveStyle;
                    float gizmoSize = 0.1f;
                    if (style != null)
                        gizmoSize = style.Width * 0.5f;
                    Handles.color = EditorStyleUtility.Styles.PlaceGizmoColor;
                    Handles.DrawWireDisc(mousePos3D, decalSpline.transform.rotation * Vector3.up, gizmoSize);//draw placement gizmo.

                    //Place a spline segment if left mouse button clicked .
                    if (e.type == EventType.MouseUp && e.button == 0)
                    {
                        decalSpline.AddSegment(mousePos3D, style);
                    }
                }
                else editMode = SceneViewEditMode.None;
            }

            //Prevent deselection.
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(GetHashCode(), FocusType.Passive));
        }

        private void HandleFadePaint(Event e, DecalSpline decalSpline)
        {
            //Escape to exit fade paint mode.
            if (e.isKey && e.keyCode == KeyCode.Escape)
            {
                editMode = SceneViewEditMode.None;
                Repaint();
            }

            //Check the mouse3DPos and draw the fade paint gizmo.
            Vector3 mousePos3D;
            if (EditorInput.MousePosition3D(HandleUtility.GUIPointToScreenPixelCoordinate(e.mousePosition), decalSpline.transform, out mousePos3D))
            {

                float gizmoSize = fadePaintGiszmoSize.floatValue;

                Handles.color = EditorStyleUtility.Styles.FadePaintGizmoColor;
                Handles.DrawWireDisc(mousePos3D, decalSpline.transform.rotation * Vector3.up, gizmoSize);//draw fade paint gizmo.

                //If the mouse button is pressed
                //Fade the splineSegment inside the gizmo circle. 
                if (e.type == EventType.MouseUp && e.button == 0)
                {
                    float strenght = EditorInput.CTRLDown ? fadeStrength.floatValue : -fadeStrength.floatValue;
                    decalSpline.FadePaint(mousePos3D,strenght,fadePaintGiszmoSize.floatValue);
                }
            }

            //Prevent deselection.
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(GetHashCode(), FocusType.Passive));
        }

        private void DrawProjectionGizmo(DecalSpline decalSpline)
        {
            Transform targetTransform = decalSpline.transform;
            Handles.ArrowHandleCap(0, targetTransform.position, targetTransform.rotation* Quaternion.LookRotation(Vector3.down),.5f , EventType.Repaint);
            Handles.RectangleHandleCap(0, targetTransform.position, targetTransform.rotation * Quaternion.LookRotation(Vector3.down),1f  , EventType.Repaint);
        }

        private Vector3 prevPos;
        private Quaternion prevRot;
        private void CheckTransfromChanged(DecalSpline decalSpline)
        {
            if (!EditorInput.LeftMouseDown)
            {
                if (autoSnap.boolValue != EditorInput.ShiftDown)
                    if(prevPos != Vector3.zero)
                        if (decalSpline.transform.position != prevPos || decalSpline.transform.rotation != prevRot)
                            decalSpline.Snap();
                prevPos = decalSpline.transform.position;
                prevRot = decalSpline.transform.rotation;
            }
        }

        private void ThemeActiveStyleChanged(object sender, EventArgs e)
        {
            //Activate place mode if the style was changed for user friendly behavior.
            if (activeTheme.objectReferenceValue != null)
                editMode = SceneViewEditMode.PlaceMode;
        }

        //Menu item in the Hierarchy window.
        [MenuItem("GameObject/Uhm..Uhm.. Games/Decal Spline")]
        public static void CreateObject(MenuCommand menuCommand)
        {
            GameObject decalSpline = ObjectFactory.CreateGameObject("Decal Spline", typeof(DecalSpline));

            SceneView lastView = SceneView.lastActiveSceneView;
            decalSpline.transform.position = lastView ? lastView.pivot : Vector3.zero;

            StageUtility.PlaceGameObjectInCurrentStage(decalSpline);
            GameObjectUtility.EnsureUniqueNameForSibling(decalSpline);

            Undo.RegisterCreatedObjectUndo(decalSpline, $"Create Object: {decalSpline.name}");
            Selection.activeGameObject = decalSpline;


            EditorGUIUtility.SetIconForObject(decalSpline, EditorGUIUtility.FindTexture("sv_label_5"));

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        //Menu item in the Create Toolbar in the Project window.
        [MenuItem("Assets/Create/Decal Splines/Material")]
        public static void CreateMaterial()
        {
            //Search for the template material.
            string fileName = "Decal Spline Template Material";
            string searchFilter = $"\"{fileName}\"";
            string[] guids = AssetDatabase.FindAssets(searchFilter);

            if (guids.Length > 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                if (assetPath.Length > 0)
                {
                    //find the template material
                    Material template = (Material)AssetDatabase.LoadAssetAtPath(assetPath, typeof(Material));

                    if (template != null)
                    {
                        //Copy the template material.
                        Material newMarterial = new Material(template);

                        //Create asset in selected folder.
                        string directory = AssetDatabase.GetAssetPath(Selection.activeObject);
                        if (System.IO.Path.HasExtension(directory))
                            directory = System.IO.Path.GetDirectoryName(directory);

                        string assetName = "New Decal Spline Material.mat";
                        string path = System.IO.Path.Combine(directory, assetName);

                        AssetDatabase.CreateAsset(newMarterial, path);
                        Selection.activeObject = newMarterial;
                    }
                }
            }
        }
    }
}

