using System;
using BeatEmUp;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(AttackData))]
public sealed class AttackDataEditor : Editor
{
    private int selectedFrame;
    private bool scenePreview, mirror, playing;
    private bool showMovement, showDefense;
    private double lastPreviewTime;
    private GameObject preview;
    private SpriteRenderer previewSprite;
    private CharacterMotor referenceCharacter;
    private readonly BoxBoundsHandle boxHandle = new BoxBoundsHandle();
    private AttackData Data => (AttackData)target;
    private void OnEnable()
    {
        SceneView.duringSceneGui += DrawScene; EditorApplication.update += PreviewTick;
        Camera.onPreCull += PreviewCamera; Camera.onPostRender += HidePreview;
    }
    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawScene; EditorApplication.update -= PreviewTick;
        Camera.onPreCull -= PreviewCamera; Camera.onPostRender -= HidePreview;
        if (preview) DestroyImmediate(preview);
    }
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Open Attack Data Editor timeline")) AttackDataEditorWindow.Open(Data);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("attackName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("domain"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isLauncher"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cooldownFrames"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("requiresAirborne"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("landingFrame"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("airborneHoldFrame"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("artworkNotes"));
        var clock = FindFirstObjectByType<CombatClock>();
        EditorGUILayout.LabelField("Combat FPS: " + (clock ? clock.combatFPS.ToString() : "60 (automatic clock)"));
        int first = Data.FirstActiveFrame, last = Data.LastActiveFrame;
        EditorGUILayout.HelpBox($"Total {Data.TotalFrames}f | First active {first} | Last active {last}\nStartup {(first < 0 ? Data.TotalFrames : first)}f | Active {Data.ActiveFrames}f | Remaining recovery {(last < 0 ? 0 : Data.TotalFrames - last - 1)}f", MessageType.Info);
        var frames = serializedObject.FindProperty("frames");
        selectedFrame = Mathf.Clamp(selectedFrame, 0, Mathf.Max(0, frames.arraySize - 1));
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Previous")) selectedFrame = Mathf.Max(0, selectedFrame - 1);
            if (GUILayout.Button("Next")) selectedFrame = Mathf.Min(frames.arraySize - 1, selectedFrame + 1);
            if (GUILayout.Button(playing ? "Stop preview" : "Preview Attack")) { playing = !playing; lastPreviewTime = EditorApplication.timeSinceStartup; }
        }
        if (frames.arraySize > 0) selectedFrame = EditorGUILayout.IntSlider("Combat frame (zero based)", selectedFrame, 0, frames.arraySize - 1);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Frame")) EditFrames("Add");
            if (GUILayout.Button("Insert")) EditFrames("Insert");
            using (new EditorGUI.DisabledScope(frames.arraySize == 0))
            {
                if (GUILayout.Button("Duplicate")) EditFrames("Duplicate");
                if (GUILayout.Button("Delete")) EditFrames("Delete");
            }
        }
        using (new EditorGUI.DisabledScope(selectedFrame < 1 || frames.arraySize == 0))
            if (GUILayout.Button("Duplicate Previous Frame (insert copy here)")) EditFrames("Previous");
        serializedObject.ApplyModifiedProperties(); serializedObject.Update(); frames = serializedObject.FindProperty("frames");
        if (frames.arraySize > 0)
        {
            selectedFrame = Mathf.Clamp(selectedFrame, 0, frames.arraySize - 1);
            var frame = frames.GetArrayElementAtIndex(selectedFrame);
            EditorGUILayout.LabelField($"Frame {selectedFrame}", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(frame.FindPropertyRelative("sprite"));
            EditorGUILayout.PropertyField(frame.FindPropertyRelative("hitboxes"), true);
            showMovement = EditorGUILayout.Foldout(showMovement, "Movement / velocity", true);
            if (showMovement)
                foreach (string property in new[] { "movement", "setHorizontalVelocity", "horizontalVelocity", "setVerticalVelocity", "verticalVelocity", "verticalVelocityModifier", "gravityScale", "suspendFalling" })
                    EditorGUILayout.PropertyField(frame.FindPropertyRelative(property));
            EditorGUILayout.LabelField("Cancel permissions", EditorStyles.boldLabel);
            foreach (string property in new[] { "canCancelIntoAttack", "canCancelIntoLauncher", "canCancelIntoJump" })
                EditorGUILayout.PropertyField(frame.FindPropertyRelative(property));
            showDefense = EditorGUILayout.Foldout(showDefense, "Defense / optional events", true);
            if (showDefense)
                foreach (string property in new[] { "invulnerable", "superArmor", "events" })
                    EditorGUILayout.PropertyField(frame.FindPropertyRelative(property), true);
            if (Data.frames[selectedFrame].sprite == null) EditorGUILayout.HelpBox("This frame has no sprite. Assign artwork before using it.", MessageType.Warning);
        }
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.Space();
        scenePreview = EditorGUILayout.Toggle("Scene view preview", scenePreview);
        referenceCharacter = (CharacterMotor)EditorGUILayout.ObjectField("Preview relative to character", referenceCharacter, typeof(CharacterMotor), true);
        mirror = EditorGUILayout.Toggle("Preview facing left", mirror);
        EditorGUILayout.HelpBox("Enable Scene view preview. Yellow boxes are attack hitboxes; cyan is the existing hurtbox. Drag the yellow center and edge handles. Changes save to this asset and support Undo. Preview plays visuals only.", MessageType.None);
        if (GUILayout.Button("Focus preview in Scene view"))
        {
            scenePreview = true; UpdatePreview();
            var view = SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView : EditorWindow.GetWindow<SceneView>();
            view.in2DMode = true; view.Focus(); view.Frame(new Bounds(Origin + Vector3.up * .6f, Vector3.one * 2), false);
        }
        UpdatePreview(); SceneView.RepaintAll();
    }
    private void EditFrames(string operation)
    {
        serializedObject.ApplyModifiedProperties(); Undo.RecordObject(Data, operation + " combat frame");
        AttackFrameData Copy(int index) => JsonUtility.FromJson<AttackFrameData>(JsonUtility.ToJson(Data.frames[index]));
        if (operation == "Add") { Data.frames.Add(new AttackFrameData()); selectedFrame = Data.frames.Count - 1; }
        else if (operation == "Insert") Data.frames.Insert(selectedFrame, new AttackFrameData());
        else if (operation == "Duplicate") { Data.frames.Insert(selectedFrame + 1, Copy(selectedFrame)); selectedFrame++; }
        else if (operation == "Previous") Data.frames.Insert(selectedFrame, Copy(selectedFrame - 1));
        else if (operation == "Delete") Data.frames.RemoveAt(selectedFrame);
        EditorUtility.SetDirty(Data); serializedObject.Update();
    }
    private Vector3 Origin => referenceCharacter ? referenceCharacter.transform.position + Vector3.up * referenceCharacter.Height : Vector3.zero;
    private void UpdatePreview()
    {
        if (!scenePreview || Data.frames.Count == 0 || EditorApplication.isPlaying)
        {
            if (preview) preview.SetActive(false); return;
        }
        if (!preview)
        {
            preview = new GameObject("Attack frame preview (unsaved)") { hideFlags = HideFlags.HideAndDontSave };
            previewSprite = preview.AddComponent<SpriteRenderer>(); previewSprite.sortingOrder = 30000;
            previewSprite.enabled = false;
        }
        selectedFrame = Mathf.Clamp(selectedFrame, 0, Data.frames.Count - 1);
        preview.SetActive(true); preview.transform.position = Origin;
        previewSprite.sprite = Data.frames[selectedFrame].sprite; previewSprite.flipX = mirror;
    }
    private void PreviewCamera(Camera camera)
    {
        if (previewSprite) previewSprite.enabled = scenePreview && !EditorApplication.isPlaying && camera.cameraType == CameraType.SceneView;
    }
    private void HidePreview(Camera camera) { if (previewSprite) previewSprite.enabled = false; }
    private void PreviewTick()
    {
        if (!playing || Data.frames.Count == 0) return;
        var clock = FindFirstObjectByType<CombatClock>(); int fps = clock ? Mathf.Max(1, clock.combatFPS) : 60;
        double now = EditorApplication.timeSinceStartup; int steps = (int)((now - lastPreviewTime) * fps);
        if (steps == 0) return;
        lastPreviewTime += (double)steps / fps; selectedFrame = (selectedFrame + steps) % Data.frames.Count;
        UpdatePreview(); Repaint(); SceneView.RepaintAll();
    }
    private void DrawScene(SceneView view)
    {
        if (!scenePreview || Data.frames.Count == 0) return;
        UpdatePreview(); var frame = Data.frames[selectedFrame]; var origin = Origin;
        Vector2 hurtOffset = new Vector2(0, .55f), hurtSize = new Vector2(.55f, 1.05f);
        if (referenceCharacter)
        {
            var collider = referenceCharacter.GetComponentInChildren<CombatHurtbox>()?.GetComponent<BoxCollider2D>();
            if (collider) { hurtOffset = collider.offset; hurtSize = collider.size; }
        }
        Handles.color = Color.cyan; Handles.DrawWireCube(origin + (Vector3)hurtOffset, hurtSize);
        Handles.Label(origin + Vector3.up * 1.6f, $"{Data.attackName} | Frame {selectedFrame}/{Data.frames.Count - 1} | yellow hitbox / cyan hurtbox");
        for (int i = 0; i < frame.hitboxes.Count; i++)
        {
            var box = frame.hitboxes[i]; if (box == null) continue;
            int facing = mirror ? -1 : 1;
            Vector3 center = origin + new Vector3(box.offset.x * facing, box.offset.y, 0);
            Handles.color = Color.yellow;
            EditorGUI.BeginChangeCheck();
            var moved = Handles.PositionHandle(center, Quaternion.identity);
            boxHandle.center = moved; boxHandle.size = new Vector3(box.size.x, box.size.y, .01f);
            boxHandle.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y;
            boxHandle.handleColor = Color.yellow; boxHandle.wireframeColor = Color.yellow; boxHandle.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(Data, "Edit frame hitbox");
                box.offset = new Vector2((moved.x - origin.x) * facing, moved.y - origin.y);
                box.size = new Vector2(Mathf.Max(.01f, boxHandle.size.x), Mathf.Max(.01f, boxHandle.size.y));
                // Edge handles may move the center while resizing.
                box.offset = new Vector2((boxHandle.center.x - origin.x) * facing, boxHandle.center.y - origin.y);
                EditorUtility.SetDirty(Data); Repaint();
            }
            Handles.Label(center, $"Hitbox {i} / ID {box.hitId} / depth +/- {box.laneTolerance:F2}");
        }
    }
}
