using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

public sealed class BossEditorWindow : EditorWindow
{
    BossEncounterData data;
    LevelDefinition level;
    Vector2 scroll;
    int selectedTotem;
    StageSegmentDefinition Stage => level ? level.stages.FirstOrDefault(s => s.stageId == "Stage07_WhiteGhostBossChamber") : null;
    TotemBossController Boss => Object.FindObjectsByType<TotemBossController>(FindObjectsSortMode.None).FirstOrDefault(b => b.data == data);
    [MenuItem("Tools/Combat/Boss Editor")]
    public static void Open() => GetWindow<BossEditorWindow>("Boss Editor");
    void OnEnable()
    {
        data = AssetDatabase.LoadAssetAtPath<BossEncounterData>(BossEncounterSetup.DataPath);
        level = AssetDatabase.LoadAssetAtPath<LevelDefinition>(BossEncounterSetup.Root + "ThaiHauntedHouse.asset");
        SceneView.duringSceneGui += SceneGUI; Undo.undoRedoPerformed += Changed;
    }
    void OnDisable()
    {
        SceneView.duringSceneGui -= SceneGUI; Undo.undoRedoPerformed -= Changed;
        EncounterScenePreview.Clear();
    }
    void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }
    void Changed() { SyncRuntimeTotems(); Repaint(); SceneView.RepaintAll(); if (EncounterScenePreview.IsActive && Stage != null) Preview(); }
    void SyncRuntimeTotems()
    {
        var boss = Boss;
        if (!Application.isPlaying || !boss || Stage == null) return;
        var placements = Stage.destructibles.Where(p => p.kind == PropKind.CursedTotem).ToArray();
        for (int i = 0; i < placements.Length && i < boss.Totems.Count; i++)
        {
            var prop = boss.Totems[i].Prop; if (!prop) continue;
            prop.totemBreakRadius = placements[i].totemBreakRadius; prop.SetMaximumHealth(placements[i].health);
            prop.transform.position = placements[i].position;
            if (prop.visual) prop.visual.sortingOrder = Mathf.RoundToInt(-prop.transform.position.y * 100);
        }
    }
    void Preview() { if (Stage != null) EncounterScenePreview.Show(level, Stage, Stage.encounters.SelectMany(e => e.waves)); }
    void OnGUI()
    {
        data = (BossEncounterData)EditorGUILayout.ObjectField("Boss configuration", data, typeof(BossEncounterData), false);
        level = (LevelDefinition)EditorGUILayout.ObjectField("Arena level", level, typeof(LevelDefinition), false);
        if (!data) { if (GUILayout.Button("Configure First Boss Encounter")) { BossEncounterSetup.Build(); data = AssetDatabase.LoadAssetAtPath<BossEncounterData>(BossEncounterSetup.DataPath); } return; }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("Positions use the walking-plane world XY. Drag Scene View handles. Changes to configuration assets in Play Mode persist; use Undo to revert tuning.", MessageType.Info);
        if (!Application.isPlaying)
        {
            if (GUILayout.Button("Preview boss arena in Scene View")) { Preview(); SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(0,.5f), new Vector3(8,4,1)), false); }
            if (GUILayout.Button("Clear arena preview")) EncounterScenePreview.Clear();
        }
        EditorGUILayout.LabelField($"Vulnerability: {data.vulnerabilityFrames}f / {data.vulnerabilityFrames / 60f:0.00}s at 60 FPS", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("PHASE 1", EditorStyles.boldLabel); Phase("phase1");
        EditorGUILayout.LabelField("PHASE 2", EditorStyles.boldLabel); Phase("phase2");
        if (GUILayout.Button("Add Warp Point")) { Undo.RecordObject(data, "Add warp point"); data.warpPoints.Add(new BossWarpPoint { label = "Warp " + (data.warpPoints.Count + 1), position = Vector2.zero }); EditorUtility.SetDirty(data); }
        using (new EditorGUI.DisabledScope(data.warpPoints.Count == 0))
            if (GUILayout.Button("Remove Last Warp Point")) { Undo.RecordObject(data, "Remove warp point"); data.warpPoints.RemoveAt(data.warpPoints.Count - 1); EditorUtility.SetDirty(data); }
        EditorGUILayout.LabelField("TOTEMS", EditorStyles.boldLabel);
        TotemFields();
        if (GUILayout.Button("Edit Book projectile (speed, damage, hitstun, deflection, lifetime)")) Selection.activeObject = data.bookProjectile;
        if (GUILayout.Button("Edit Curse Wave (speed, width/depth, stun, deflection)")) Selection.activeObject = data.curseWaveProjectile;
        if (GUILayout.Button("Save boss and level assets")) AssetDatabase.SaveAssets();
        EditorGUILayout.Space(); EditorGUILayout.LabelField("Warp / Summon / Overlay / Feedback settings", EditorStyles.boldLabel);
        var serialized = new SerializedObject(data); serialized.Update();
        foreach (string field in new[] { "warpPoints", "minionSpawnPoints", "warpOutFrames", "warpInFrames", "arrivalFrames", "warpCooldownFrames", "canRepeatWarpPoint", "warpOffset", "moveChance", "moveDistance", "moveSpeed", "phase2HealthThreshold", "phaseTransitionFrames", "bookProjectile", "curseWaveProjectile", "bookSpawnOffset", "curseSpawnOffset", "vulnerabilityFrames", "additionalWave", "totemMode", "breakWaveRadius", "breakWaveFrames", "totemRespawnFrames", "totemRespawnHP", "rusherPrefab", "grapplerPrefab", "throwerPrefab", "rusherCount", "grapplerCount", "throwerCount", "maxBossMinions", "playerSpawnClearance", "enemySpawnClearance", "strongGhostPriority", "despawnMinionsOnDeath", "warpOutFeedback", "warpInFeedback", "phaseFeedback", "shieldHitFeedback", "vulnerableFeedback", "totemBreakFeedback", "shieldColor", "vulnerableColor" })
            EditorGUILayout.PropertyField(serialized.FindProperty(field), true);
        if (serialized.ApplyModifiedProperties()) Changed();
        DebugControls(Boss);
        EditorGUILayout.EndScrollView();
    }
    void Phase(string field)
    {
        var serialized = new SerializedObject(data); serialized.Update(); var list = serialized.FindProperty(field);
        for (int i = 0; i < list.arraySize; i++)
        {
            var row = list.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(row.FindPropertyRelative("action"));
            EditorGUILayout.PropertyField(row.FindPropertyRelative("enabled"));
            EditorGUILayout.PropertyField(row.FindPropertyRelative("weight"));
            EditorGUILayout.PropertyField(row.FindPropertyRelative("attack"));
            var attack = row.FindPropertyRelative("attack").objectReferenceValue as AttackData;
            if (attack && GUILayout.Button("Open frame timeline / hitboxes")) { Selection.activeObject = attack; EditorApplication.ExecuteMenuItem("Tools/Combat/Attack Data Editor"); }
            EditorGUILayout.EndVertical();
        }
        if (serialized.ApplyModifiedProperties()) Changed();
    }
    void TotemFields()
    {
        if (Stage == null) return;
        var serialized = new SerializedObject(level); serialized.Update();
        int index = level.stages.IndexOf(Stage);
        var props = serialized.FindProperty("stages").GetArrayElementAtIndex(index).FindPropertyRelative("destructibles");
        int count = 0;
        for (int i = 0; i < props.arraySize; i++)
        {
            var prop = props.GetArrayElementAtIndex(i); if (prop.FindPropertyRelative("kind").enumValueIndex != (int)PropKind.CursedTotem) continue;
            EditorGUILayout.LabelField("Totem " + (++count), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("position")); EditorGUILayout.PropertyField(prop.FindPropertyRelative("health"));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("totemBreakRadius"), new GUIContent("Radius override (0 = default)"));
            if (GUILayout.Button("Select Totem " + count)) { selectedTotem = count - 1; if (Application.isPlaying && Boss && Boss.Totems.Count > selectedTotem) Selection.activeGameObject = Boss.Totems[selectedTotem].gameObject; }
        }
        if (serialized.ApplyModifiedProperties()) Changed();
    }
    static void DebugControls(TotemBossController boss)
    {
        EditorGUILayout.Space(); EditorGUILayout.LabelField("PLAY MODE TESTING", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || !boss))
        {
            if (GUILayout.Button("Force Warp")) boss.ForceWarp();
            if (GUILayout.Button("Force Phase 2")) boss.ForcePhase2();
            foreach (BossAction action in System.Enum.GetValues(typeof(BossAction)))
                if (GUILayout.Button("Force " + action) && !boss.ForceAction(action)) Debug.LogWarning("Action unavailable: phase, reaction, weight, asset, target or enemy cap.", boss);
            if (GUILayout.Button("Make Boss Vulnerable (debug)")) boss.DebugVulnerable();
            if (GUILayout.Button("Make Boss Invulnerable")) boss.DebugInvulnerable();
            if (GUILayout.Button("Set Boss HP to 49%")) { boss.DebugVulnerable(); var hp = boss.GetComponent<CharacterHealth>(); hp.Damage(Mathf.Max(0, hp.Current - hp.EffectiveMaximum * .49f)); }
        }
        if (!Application.isPlaying || !boss) return;
        var health = boss.GetComponent<CharacterHealth>();
        EditorGUILayout.HelpBox($"Phase {(boss.Phase2 ? 2 : 1)} / {boss.State}\nHP {health.Current:0}/{health.EffectiveMaximum:0}\n{(boss.Invulnerable ? "Invulnerable" : "Vulnerable")} / {boss.VulnerabilityRemaining}f\nWarp {boss.LastWarpIndex + 1} / total {boss.WarpsPerformed}\nAction {boss.Selected?.action.ToString() ?? "None"} / previous {boss.PreviousAction?.action.ToString() ?? "None"}\nTotems alive {boss.Totems.Count(t => t && !t.Prop.IsBroken)} / broken {boss.Totems.Count(t => t && t.Prop.IsBroken)}\nBoss minions {boss.ActiveMinions}", MessageType.None);
        foreach (var totem in boss.Totems) if (totem && GUILayout.Button("Simulate break: " + totem.name + " @ " + totem.transform.position)) totem.DebugBreak(boss.GetComponent<EnemyCombat>().target.GetComponent<CharacterMotor>());
        if (boss.flow)
        {
            var living = boss.flow.LivingEnemies.Where(h => h != health).ToArray();
            foreach (var prefab in new[] { boss.data.rusherPrefab, boss.data.grapplerPrefab, boss.data.throwerPrefab })
                if (prefab) EditorGUILayout.LabelField(prefab.name, living.Count(h => h.GetComponent<EnemyCombat>()?.aiProfile == prefab.GetComponent<EnemyCombat>()?.aiProfile).ToString());
        }
    }
    void SceneGUI(SceneView view)
    {
        if (!data) return;
        for (int i = 0; i < data.warpPoints.Count; i++)
        {
            var point = data.warpPoints[i]; if (point == null) continue;
            Handles.color = point.enabled ? Color.cyan : Color.gray; Handles.Label(point.position, point.label + (point.enabled ? "" : " (disabled)"));
            EditorGUI.BeginChangeCheck(); var position = Handles.PositionHandle(point.position, Quaternion.identity);
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(data, "Move boss warp point"); point.position = position; EditorUtility.SetDirty(data); Repaint(); }
        }
        if (Stage != null)
        {
            var totems = Stage.destructibles.Where(p => p.kind == PropKind.CursedTotem).ToList();
            for (int i = 0; i < totems.Count; i++)
            {
                var prop = totems[i]; Vector3 position = prop.position;
                var boss = Boss; var runtime = Application.isPlaying && boss && boss.Totems.Count > i ? boss.Totems[i] : null;
                if (runtime) position = runtime.transform.position;
                Handles.color = i == selectedTotem ? Color.yellow : new Color(.8f,.4f,1);
                float waveRadius = prop.totemBreakRadius > 0 ? prop.totemBreakRadius : data.breakWaveRadius;
                Handles.DrawWireDisc(position, Vector3.forward, waveRadius);
                Handles.Label(position + Vector3.up * .12f, $"Totem {i + 1} / HP {(runtime ? runtime.Prop.Current : prop.health):0} / break radius {waveRadius:0.00}");
                if (Handles.Button(position, Quaternion.identity, .06f, .09f, Handles.DotHandleCap)) { selectedTotem = i; if (runtime) Selection.activeGameObject = runtime.gameObject; Repaint(); }
                EditorGUI.BeginChangeCheck(); var next = Handles.PositionHandle(position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(level, "Move boss Totem"); prop.position = next; EditorUtility.SetDirty(level);
                    if (runtime) { Undo.RecordObject(runtime.transform, "Move runtime Totem"); runtime.transform.position = next; }
                    Changed();
                }
                EditorGUI.BeginChangeCheck(); float radius = Handles.ScaleSlider(waveRadius, position, Vector3.right, Quaternion.identity, HandleUtility.GetHandleSize(position), .05f);
                if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(level, "Resize Totem wave radius"); prop.totemBreakRadius = Mathf.Max(.01f, radius); if (runtime) runtime.Prop.totemBreakRadius = prop.totemBreakRadius; EditorUtility.SetDirty(level); Repaint(); }
            }
        }
        Handles.color = Color.green;
        for (int i = 0; i < data.minionSpawnPoints.Count; i++)
        {
            var position = data.minionSpawnPoints[i]; Handles.Label(position, "Minion spawn " + (i + 1));
            EditorGUI.BeginChangeCheck(); var next = Handles.PositionHandle(position, Quaternion.identity);
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(data, "Move minion spawn"); data.minionSpawnPoints[i] = next; EditorUtility.SetDirty(data); Repaint(); }
        }
        Vector3 origin = Boss ? Boss.transform.position : Vector3.zero;
        Handles.color = Color.white; Handles.DrawWireDisc(origin, Vector3.forward, data.moveDistance); Handles.Label(origin, "Boss small move range");
        Handles.color = Color.magenta; Handles.DrawWireCube(origin + (Vector3)data.bookSpawnOffset, Vector3.one * .1f); Handles.Label(origin + (Vector3)data.bookSpawnOffset, "Book spawn");
        if (data.curseWaveProjectile) { Handles.color = Color.red; Handles.DrawWireCube(origin + (Vector3)data.curseSpawnOffset, data.curseWaveProjectile.waveSize); Handles.Label(origin + (Vector3)data.curseSpawnOffset, "Curse Wave width / lane depth"); }
        var swipe = data.phase1.FirstOrDefault(c => c.action == BossAction.Swipe)?.attack;
        if (swipe) foreach (var hit in swipe.frames.SelectMany(f => f.hitboxes).Take(1)) { Handles.color = Color.yellow; Handles.DrawWireCube(origin + (Vector3)hit.offset, hit.size); Handles.Label(origin + (Vector3)hit.offset, "Swipe hitbox (edit Attack Data)"); }
    }
}

[CustomEditor(typeof(TotemBossController))]
public sealed class TotemBossInspector : Editor
{
    public override void OnInspectorGUI() { DrawDefaultInspector(); if (GUILayout.Button("Open visual Boss Editor")) BossEditorWindow.Open(); }
}
