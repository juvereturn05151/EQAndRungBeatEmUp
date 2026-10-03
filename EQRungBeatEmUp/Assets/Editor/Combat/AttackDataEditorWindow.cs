using System;
using BeatEmUp;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public sealed class AttackDataEditorWindow : EditorWindow
{
    [SerializeField] private AttackData attack;
    [SerializeField] private int currentFrame, rangeStart, rangeEnd, anchor, selectedBox;
    [SerializeField] private float timelineZoom = 28, previewZoom = 130, playbackSpeed = 1;
    [SerializeField] private Vector2 timelineScroll, inspectorScroll, bulkScroll;
    [SerializeField] private bool followSelection = true, mirror, onion, showMovementPath, loop = true;
    [SerializeField] private GameObject previewCharacter;
    [SerializeField] private Sprite bulkSprite;
    [SerializeField] private Vector2 bulkMovement;
    [SerializeField] private bool bulkAttackCancel, bulkLauncherCancel, bulkJumpCancel;
    private bool playing, timelineFocused, showIdentity;
    private double lastTick, playbackRemainder;
    private SerializedObject serializedAttack;
    private AttackTimelineGUI timeline;
    private AttackPreviewGUI preview;
    private int FPS { get { var clock = FindFirstObjectByType<CombatClock>(); return clock ? Mathf.Max(1, clock.combatFPS) : 60; } }
    public AttackData CurrentAttack => attack;
    public int CurrentFrame => currentFrame;
    [MenuItem("Tools/Combat/Attack Data Editor")]
    public static void Open() => Open(Selection.activeObject as AttackData);
    public static void Open(AttackData data)
    {
        var window = GetWindow<AttackDataEditorWindow>("Attack Data Editor");
        window.minSize = new Vector2(1100, 660);
        if (!window.docked) window.position = new Rect(100, 100, 1300, 800);
        if (data) window.SetAttack(data); window.Show(); window.Focus();
    }
    [OnOpenAsset(1)]
    public static bool OpenAsset(int id, int line)
    {
        var data = EditorUtility.InstanceIDToObject(id) as AttackData; if (!data) return false;
        Open(data); return true;
    }
    private void OnEnable()
    {
        minSize = new Vector2(1100, 660); timeline = new AttackTimelineGUI(); preview = new AttackPreviewGUI();
        if (!previewCharacter) previewCharacter = AssetDatabase.LoadAssetAtPath<GameObject>(CombatDemoBuilder.Output + "/Prefabs/BlueShirtGuy.prefab");
        if (!attack && Selection.activeObject is AttackData selected) attack = selected;
        if (attack) serializedAttack = new SerializedObject(attack);
        EditorApplication.update += Tick; Undo.undoRedoPerformed += OnUndo;
        AssemblyReloadEvents.beforeAssemblyReload += Save; EditorApplication.quitting += Save;
        ClampSelection();
    }
    private void OnDisable()
    {
        EditorApplication.update -= Tick; Undo.undoRedoPerformed -= OnUndo;
        AssemblyReloadEvents.beforeAssemblyReload -= Save; EditorApplication.quitting -= Save;
        preview?.Dispose(); preview = null;
    }
    private void OnSelectionChange() { if (followSelection && Selection.activeObject is AttackData selected) SetAttack(selected); }
    private void OnUndo() { playing = false; if (attack) serializedAttack = new SerializedObject(attack); ClampSelection(); Repaint(); }
    public void SetAttack(AttackData data)
    {
        if (data == attack && serializedAttack != null) return;
        playing = false; attack = data; currentFrame = rangeStart = rangeEnd = anchor = selectedBox = 0; timelineScroll = Vector2.zero;
        serializedAttack = data ? new SerializedObject(data) : null; preview?.ResetView(); Repaint();
    }
    public void SelectFrame(int index, bool extend = false)
    {
        playing = false; currentFrame = Mathf.Clamp(index, 0, Mathf.Max(0, attack ? attack.TotalFrames - 1 : 0));
        if (extend) { rangeStart = Mathf.Min(anchor, currentFrame); rangeEnd = Mathf.Max(anchor, currentFrame); }
        else { anchor = rangeStart = rangeEnd = currentFrame; }
        selectedBox = 0; Repaint();
    }
    private void ClampSelection()
    {
        int max = Mathf.Max(0, attack ? attack.TotalFrames - 1 : 0);
        currentFrame = Mathf.Clamp(currentFrame, 0, max); anchor = Mathf.Clamp(anchor, 0, max);
        rangeStart = Mathf.Clamp(rangeStart, 0, max); rangeEnd = Mathf.Clamp(rangeEnd, rangeStart, max);
        if (attack && attack.TotalFrames > 0) selectedBox = Mathf.Clamp(selectedBox, 0, Mathf.Max(0, attack.frames[currentFrame]?.hitboxes?.Count - 1 ?? 0));
    }
    private void Save() => AttackFrameAuthoring.Save(attack);
    private void Tick()
    {
        if (!playing || !attack || attack.TotalFrames == 0) return;
        double now = EditorApplication.timeSinceStartup; playbackRemainder += (now - lastTick) * FPS * playbackSpeed; lastTick = now;
        int steps = (int)playbackRemainder; if (steps <= 0) return; playbackRemainder -= steps;
        int next = currentFrame + steps;
        if (loop) currentFrame = next % attack.TotalFrames;
        else { currentFrame = Mathf.Min(next, attack.TotalFrames - 1); if (next >= attack.TotalFrames) playing = false; }
        selectedBox = 0; Repaint();
    }
    private void TogglePlay() { playing = !playing && attack && attack.TotalFrames > 0; lastTick = EditorApplication.timeSinceStartup; playbackRemainder = 0; }
    private void OnGUI()
    {
        if (timeline == null) timeline = new AttackTimelineGUI(); if (preview == null) preview = new AttackPreviewGUI();
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            var selected = (AttackData)EditorGUILayout.ObjectField(attack, typeof(AttackData), false, GUILayout.MinWidth(220));
            if (selected != attack) SetAttack(selected);
            followSelection = GUILayout.Toggle(followSelection, "Follow Project selection", EditorStyles.toolbarButton, GUILayout.Width(150));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("New Attack", EditorStyles.toolbarButton, GUILayout.Width(85))) NewAttack();
            using (new EditorGUI.DisabledScope(!attack)) if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(55))) Save();
        }
        if (!attack)
        {
            EditorGUILayout.HelpBox("Select or drag an AttackData asset into the field above, or create a new attack. This window edits the same frame data used by the game.", MessageType.Info); return;
        }
        ClampSelection(); Keyboard();
        EditorGUILayout.LabelField($"{attack.attackName}{(EditorUtility.IsDirty(attack) ? " *" : "")}   |   Total {attack.TotalFrames}f   |   First active {attack.FirstActiveFrame}   Last active {attack.LastActiveFrame}   |   Startup {(attack.FirstActiveFrame < 0 ? attack.TotalFrames : attack.FirstActiveFrame)}f   Active {attack.ActiveFrames}f   Recovery {(attack.LastActiveFrame < 0 ? 0 : attack.TotalFrames - attack.LastActiveFrame - 1)}f", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("|<", GUILayout.Width(35))) SelectFrame(0);
            if (GUILayout.Button("<", GUILayout.Width(35))) SelectFrame(currentFrame - 1);
            if (GUILayout.Button(playing ? "Pause" : "Play", GUILayout.Width(55))) TogglePlay();
            if (GUILayout.Button(">", GUILayout.Width(35))) SelectFrame(currentFrame + 1);
            if (GUILayout.Button(">|", GUILayout.Width(35))) SelectFrame(attack.TotalFrames - 1);
            loop = GUILayout.Toggle(loop, "Loop", GUILayout.Width(55));
            float[] speeds = { .25f, .5f, 1, 2 }; int speed = Mathf.Max(0, Array.IndexOf(speeds, playbackSpeed));
            playbackSpeed = speeds[EditorGUILayout.Popup(speed, new[] { "0.25x", "0.5x", "1x", "2x" }, GUILayout.Width(65))];
            GUILayout.Label($"Frame {currentFrame} / {Mathf.Max(0, attack.TotalFrames - 1)}   •   Total {attack.TotalFrames}f   •   FPS {FPS}");
            GUILayout.FlexibleSpace(); GUILayout.Label("Timeline zoom", GUILayout.Width(85)); timelineZoom = GUILayout.HorizontalSlider(timelineZoom, 10, 64, GUILayout.Width(95));
        }
        float top = 76, bottom = position.height - 33, leftWidth = 330, rightWidth = 340;
        var left = new Rect(5, top, leftWidth, Mathf.Max(200, bottom - top));
        var middle = new Rect(left.xMax + 5, top, Mathf.Max(200, position.width - leftWidth - rightWidth - 25), left.height);
        var right = new Rect(middle.xMax + 5, top, rightWidth, left.height);
        if (Event.current.type == EventType.MouseDown && !new Rect(middle.x, middle.y, middle.width, timeline.Height + 19).Contains(Event.current.mousePosition)) timelineFocused = false;
        DrawPreview(left); DrawTimelineAndBulk(middle); DrawInspector(right);
        GUI.Label(new Rect(8, position.height - 27, position.width - 16, 22), "Timeline: click/drag scrub • Shift-click/drag range • Alt-drag range to move • Ctrl+C / Ctrl+V / Delete when timeline focused • Preview: wheel zoom, middle drag pan", EditorStyles.miniLabel);
    }
    private void DrawPreview(Rect rect)
    {
        GUI.BeginGroup(rect);
        GUILayout.BeginArea(new Rect(0, 0, rect.width, 91));
        EditorGUILayout.LabelField("Character preview", EditorStyles.boldLabel);
        previewCharacter = (GameObject)EditorGUILayout.ObjectField("Hurtbox reference", previewCharacter, typeof(GameObject), true);
        using (new EditorGUILayout.HorizontalScope())
        {
            mirror = GUILayout.Toggle(mirror, mirror ? "Facing Left" : "Facing Right", EditorStyles.miniButton);
            onion = GUILayout.Toggle(onion, "Onion skin", EditorStyles.miniButton);
            showMovementPath = GUILayout.Toggle(showMovementPath, "Movement path", EditorStyles.miniButton);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Zoom", GUILayout.Width(35)); previewZoom = GUILayout.HorizontalSlider(previewZoom, 30, 500);
            if (GUILayout.Button("Reset", GUILayout.Width(50))) { previewZoom = 130; preview.ResetView(); }
        }
        GUILayout.EndArea();
        var canvas = new Rect(0, 95, rect.width, Mathf.Max(80, rect.height - 149));
        preview.Draw(canvas, attack, currentFrame, ref selectedBox, mirror, ref previewZoom, onion, showMovementPath, previewCharacter, () => { playing = false; timelineFocused = false; });
        if (attack.TotalFrames > 0 && attack.frames[currentFrame] != null)
        {
            var frame = attack.frames[currentFrame];
            GUI.Label(new Rect(2, canvas.yMax + 4, rect.width - 4, 42), $"Cancels: {(frame.canCancelIntoAttack ? "Attack " : "")}{(frame.canCancelIntoLauncher ? "Launcher " : "")}{(frame.canCancelIntoJump ? "Jump" : "")}\nMovement {frame.movement} | gravity {frame.gravityScale:F2}" + (frame.suspendFalling ? " | fall suspended" : ""), EditorStyles.wordWrappedMiniLabel);
        }
        GUI.EndGroup();
    }
    private void DrawTimelineAndBulk(Rect rect)
    {
        var grid = new Rect(rect.x, rect.y + 19, rect.width, timeline.Height);
        GUI.Label(new Rect(rect.x, rect.y, rect.width, 19), "Combat frame timeline", EditorStyles.boldLabel);
        timeline.Draw(grid, attack, currentFrame, rangeStart, rangeEnd, ref timelineScroll, ref timelineZoom,
            (i, extend) => SelectFrame(i, extend), MoveRange, AssignDroppedSprites, () => { timelineFocused = true; GUI.FocusControl(null); });
        GUILayout.BeginArea(new Rect(rect.x, grid.yMax + 5, rect.width, Mathf.Max(50, rect.yMax - grid.yMax - 5)));
        bulkScroll = EditorGUILayout.BeginScrollView(bulkScroll);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Range", GUILayout.Width(42));
            int first = EditorGUILayout.IntField(rangeStart, GUILayout.Width(45)), last = EditorGUILayout.IntField(rangeEnd, GUILayout.Width(45));
            if (first != rangeStart || last != rangeEnd) { playing = false; rangeStart = Mathf.Clamp(first, 0, Mathf.Max(0, attack.TotalFrames - 1)); rangeEnd = Mathf.Clamp(last, rangeStart, Mathf.Max(0, attack.TotalFrames - 1)); anchor = rangeStart; currentFrame = rangeStart; }
            GUILayout.Label($"({(attack.TotalFrames == 0 ? 0 : rangeEnd - rangeStart + 1)} selected)");
        }
        using (new EditorGUI.DisabledScope(playing))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add")) SelectFrame(AttackFrameAuthoring.Add(attack));
                if (GUILayout.Button("Insert")) SelectFrame(AttackFrameAuthoring.Insert(attack, currentFrame));
                using (new EditorGUI.DisabledScope(attack.TotalFrames == 0))
                {
                    if (GUILayout.Button("Duplicate")) DuplicateRange();
                    if (GUILayout.Button("Delete")) DeleteRange();
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(currentFrame == 0 || attack.TotalFrames == 0))
                    if (GUILayout.Button("Duplicate previous")) SelectFrame(AttackFrameAuthoring.DuplicatePrevious(attack, currentFrame));
                if (GUILayout.Button("Copy")) AttackFrameAuthoring.Copy(attack, rangeStart, rangeEnd);
                using (new EditorGUI.DisabledScope(!AttackFrameAuthoring.CanPaste)) if (GUILayout.Button("Paste")) PasteRange();
            }
            EditorGUILayout.Space(3); EditorGUILayout.LabelField("Apply to selected frames", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(attack.TotalFrames == 0))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bulkSprite = (Sprite)EditorGUILayout.ObjectField(bulkSprite, typeof(Sprite), false);
                    if (GUILayout.Button("Set sprite", GUILayout.Width(80))) AttackFrameAuthoring.SetSprite(attack, rangeStart, rangeEnd, bulkSprite);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Hold current sprite across range")) AttackFrameAuthoring.SetSprite(attack, rangeStart, rangeEnd, attack.frames[currentFrame]?.sprite);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Enable hitbox")) AttackFrameAuthoring.SetHitboxes(attack, rangeStart, rangeEnd, true, SelectedHitbox());
                    if (GUILayout.Button("Disable hitboxes")) AttackFrameAuthoring.SetHitboxes(attack, rangeStart, rangeEnd, false, null);
                }
                bulkMovement = EditorGUILayout.Vector2Field("Movement (forward / lane)", bulkMovement);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Set movement")) AttackFrameAuthoring.SetMovement(attack, rangeStart, rangeEnd, bulkMovement);
                    if (GUILayout.Button("Clear movement")) AttackFrameAuthoring.SetMovement(attack, rangeStart, rangeEnd, Vector2.zero);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    bulkAttackCancel = GUILayout.Toggle(bulkAttackCancel, "Attack"); bulkLauncherCancel = GUILayout.Toggle(bulkLauncherCancel, "Launcher"); bulkJumpCancel = GUILayout.Toggle(bulkJumpCancel, "Jump");
                }
                if (GUILayout.Button("Apply cancel permissions to range")) AttackFrameAuthoring.SetCancels(attack, rangeStart, rangeEnd, bulkAttackCancel, bulkLauncherCancel, bulkJumpCancel);
            }
        }
        EditorGUILayout.EndScrollView(); GUILayout.EndArea();
    }
    private AttackHitboxData SelectedHitbox()
    {
        var boxes = attack.TotalFrames > 0 ? attack.frames[currentFrame]?.hitboxes : null;
        return boxes != null && boxes.Count > 0 ? boxes[Mathf.Clamp(selectedBox, 0, boxes.Count - 1)] : null;
    }
    private void DrawInspector(Rect rect)
    {
        GUI.Box(rect, GUIContent.none); GUILayout.BeginArea(new Rect(rect.x + 6, rect.y + 4, rect.width - 12, rect.height - 8));
        inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);
        if (serializedAttack == null || serializedAttack.targetObject != attack) serializedAttack = new SerializedObject(attack);
        serializedAttack.Update();
        using (new EditorGUI.DisabledScope(playing))
        {
            showIdentity = EditorGUILayout.Foldout(showIdentity, "Attack properties", true);
            if (showIdentity)
                foreach (string property in new[] { "attackName", "domain", "isLauncher", "cooldownFrames", "artworkNotes" }) EditorGUILayout.PropertyField(serializedAttack.FindProperty(property));
            if (attack.TotalFrames == 0) EditorGUILayout.HelpBox("Add a frame to begin authoring.", MessageType.Info);
            else
            {
                EditorGUILayout.LabelField($"FRAME {currentFrame}", EditorStyles.boldLabel);
                var frame = serializedAttack.FindProperty("frames").GetArrayElementAtIndex(currentFrame);
                EditorGUILayout.PropertyField(frame.FindPropertyRelative("sprite"));
                EditorGUILayout.LabelField("Hitboxes", EditorStyles.boldLabel);
                var boxes = frame.FindPropertyRelative("hitboxes");
                if (boxes.arraySize > 0)
                {
                    string[] names = new string[boxes.arraySize]; for (int b = 0; b < names.Length; b++) names[b] = "Hitbox " + b;
                    selectedBox = EditorGUILayout.Popup("Selected hitbox", Mathf.Clamp(selectedBox, 0, names.Length - 1), names);
                    var box = boxes.GetArrayElementAtIndex(selectedBox);
                    foreach (string property in new[] { "offset", "size", "damage", "hitstunFrames", "hitstopFrames", "knockback", "launchVelocity", "hitType", "laneTolerance", "canHitGrounded", "canHitAirborne", "hitId", "repeatAfterFrames" }) EditorGUILayout.PropertyField(box.FindPropertyRelative(property));
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add hitbox"))
                    {
                        serializedAttack.ApplyModifiedProperties(); AttackFrameAuthoring.Change(attack, "Add frame hitbox", () => attack.frames[currentFrame].hitboxes.Add(new AttackHitboxData()));
                        selectedBox = attack.frames[currentFrame].hitboxes.Count - 1; serializedAttack.Update();
                    }
                    using (new EditorGUI.DisabledScope(boxes.arraySize == 0))
                        if (GUILayout.Button("Remove hitbox"))
                        {
                            serializedAttack.ApplyModifiedProperties(); AttackFrameAuthoring.Change(attack, "Remove frame hitbox", () => attack.frames[currentFrame].hitboxes.RemoveAt(selectedBox));
                            selectedBox = Mathf.Max(0, selectedBox - 1); serializedAttack.Update();
                        }
                }
                EditorGUILayout.Space(); EditorGUILayout.LabelField("Movement / velocity", EditorStyles.boldLabel);
                foreach (string property in new[] { "movement", "movementInputScale", "setHorizontalVelocity", "horizontalVelocity", "setVerticalVelocity", "verticalVelocity", "verticalVelocityModifier", "gravityScale", "suspendFalling" }) EditorGUILayout.PropertyField(frame.FindPropertyRelative(property));
                EditorGUILayout.Space(); EditorGUILayout.LabelField("Cancels / defense", EditorStyles.boldLabel);
                foreach (string property in new[] { "canCancelIntoAttack", "canCancelIntoLauncher", "canCancelIntoJump", "invulnerable", "superArmor", "events" }) EditorGUILayout.PropertyField(frame.FindPropertyRelative(property), true);
            }
            serializedAttack.ApplyModifiedProperties();
        }
        EditorGUILayout.EndScrollView(); GUILayout.EndArea();
    }
    private void MoveRange(int destination)
    {
        int count = rangeEnd - rangeStart + 1; int start = AttackFrameAuthoring.Move(attack, rangeStart, rangeEnd, destination);
        SelectFrame(start); rangeEnd = Mathf.Min(attack.TotalFrames - 1, start + count - 1);
    }
    private void DuplicateRange()
    {
        int count = rangeEnd - rangeStart + 1; int at = AttackFrameAuthoring.Duplicate(attack, rangeStart, rangeEnd);
        SelectFrame(at); rangeEnd = at + count - 1;
    }
    private void DeleteRange() => SelectFrame(AttackFrameAuthoring.Delete(attack, rangeStart, rangeEnd));
    private void PasteRange()
    {
        int at = Mathf.Clamp(currentFrame, 0, attack.TotalFrames); int count = AttackFrameAuthoring.Paste(attack, at);
        if (count > 0) { SelectFrame(at); rangeEnd = at + count - 1; }
    }
    private void AssignDroppedSprites(int at, Sprite[] sprites)
    {
        playing = false;
        if (sprites.Length == 1 && at >= rangeStart && at <= rangeEnd) AttackFrameAuthoring.SetSprite(attack, rangeStart, rangeEnd, sprites[0]);
        else AttackFrameAuthoring.Change(attack, "Drop sprites on combat timeline", () => { for (int i = 0; i < sprites.Length && at + i < attack.TotalFrames; i++) attack.frames[at + i].sprite = sprites[i]; });
        Repaint();
    }
    private void Keyboard()
    {
        var evt = Event.current;
        if (evt.type != EventType.KeyDown || !timelineFocused || EditorGUIUtility.editingTextField || playing) return;
        bool command = evt.control || evt.command;
        if (command && evt.keyCode == KeyCode.C) AttackFrameAuthoring.Copy(attack, rangeStart, rangeEnd);
        else if (command && evt.keyCode == KeyCode.V) PasteRange();
        else if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace) DeleteRange();
        else if (evt.keyCode == KeyCode.LeftArrow) SelectFrame(currentFrame - 1, evt.shift);
        else if (evt.keyCode == KeyCode.RightArrow) SelectFrame(currentFrame + 1, evt.shift);
        else if (evt.keyCode == KeyCode.Space) TogglePlay();
        else return;
        evt.Use();
    }
    private void NewAttack()
    {
        string path = EditorUtility.SaveFilePanelInProject("Create frame attack", "NewAttack", "asset", "Choose a location for the runtime AttackData.");
        if (string.IsNullOrEmpty(path)) return;
        var data = CreateInstance<AttackData>(); data.attackName = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(data, path); SetAttack(data); Selection.activeObject = data;
    }
}
