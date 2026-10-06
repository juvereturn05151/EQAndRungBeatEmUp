using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(EnemyAIProfile))]
public sealed class EnemyAIProfileEditor : Editor
{
    ReorderableList attacks;
    float previewDistance=2, previewLane;
    void OnEnable()
    {
        attacks=new ReorderableList(serializedObject,serializedObject.FindProperty("attacks"),true,true,true,true);
        attacks.drawHeaderCallback=r=>EditorGUI.LabelField(r,"Attack choices — drag to reorder; weights select randomly");
        attacks.elementHeightCallback=i=>EditorGUI.GetPropertyHeight(attacks.serializedProperty.GetArrayElementAtIndex(i),true)+6;
        attacks.drawElementCallback=(r,i,a,f)=>{ r.y+=2; r.height-=4; EditorGUI.PropertyField(r,attacks.serializedProperty.GetArrayElementAtIndex(i),new GUIContent("Choice "+(i+1)),true); };
        attacks.onAddCallback=list=>{
            int index=list.serializedProperty.arraySize; list.serializedProperty.InsertArrayElementAtIndex(index);
            var choice=list.serializedProperty.GetArrayElementAtIndex(index);
            choice.FindPropertyRelative("id").stringValue="Attack"+(index+1); choice.FindPropertyRelative("attack").objectReferenceValue=null;
            choice.FindPropertyRelative("weight").floatValue=1; choice.FindPropertyRelative("maximumRange").floatValue=1;
            choice.FindPropertyRelative("minimumRange").floatValue=0; choice.FindPropertyRelative("laneTolerance").floatValue=.55f;
            choice.FindPropertyRelative("cooldown").floatValue=1; choice.FindPropertyRelative("projectile").boolValue=false;
            choice.FindPropertyRelative("prerequisites").ClearArray(); choice.isExpanded=true;
        };
    }
    static void StringPicker(SerializedProperty property,string label,string[] options)
    {
        var values=new[]{property.stringValue}.Concat(options).Distinct().ToArray();
        int index=System.Array.IndexOf(values,property.stringValue);
        property.stringValue=values[EditorGUILayout.Popup(label,Mathf.Max(0,index),values)];
    }
    public override void OnInspectorGUI()
    {
        serializedObject.Update(); var profile=(EnemyAIProfile)target;
        EditorGUILayout.HelpBox("States are evaluated in list order within each transition list. First matching transition wins; all its conditions must pass. Times are seconds on the 60 FPS combat clock. Hitstop pauses AI timers.",MessageType.Info);
        if(GUILayout.Button("Open Enemy AI Editor")) EnemyAIEditorWindow.Open(profile);
        StringPicker(serializedObject.FindProperty("defaultState"),"Default state",profile.states.Where(s=>s!=null && s.role==EnemyAIStateRole.Normal).Select(s=>s.id).ToArray());
        foreach(var field in new[]{"aggroRange","loseTargetRange","targetRule","meleeRange","useGroundPlaneRange","projectileRange","preferredDistance","laneTolerance","reactionDelay","recoveryTime","commitToAttack"}) EditorGUILayout.PropertyField(serializedObject.FindProperty(field));
        EditorGUILayout.Space(); EditorGUILayout.LabelField("States / actions / transitions",EditorStyles.boldLabel);
        var states=serializedObject.FindProperty("states");
        for(int i=0;i<states.arraySize;i++)
        {
            var state=states.GetArrayElementAtIndex(i); var id=state.FindPropertyRelative("id");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal(); state.isExpanded=EditorGUILayout.Foldout(state.isExpanded,id.stringValue+" → "+state.FindPropertyRelative("action").enumDisplayNames[state.FindPropertyRelative("action").enumValueIndex],true);
            if(GUILayout.Button("↑",GUILayout.Width(25)) && i>0) { states.MoveArrayElement(i,i-1); serializedObject.ApplyModifiedProperties(); GUIUtility.ExitGUI(); }
            if(GUILayout.Button("↓",GUILayout.Width(25)) && i<states.arraySize-1) { states.MoveArrayElement(i,i+1); serializedObject.ApplyModifiedProperties(); GUIUtility.ExitGUI(); }
            if(GUILayout.Button("Remove",GUILayout.Width(65))) { states.DeleteArrayElementAtIndex(i); serializedObject.ApplyModifiedProperties(); GUIUtility.ExitGUI(); }
            EditorGUILayout.EndHorizontal();
            if(state.isExpanded)
            {
                EditorGUILayout.PropertyField(id,new GUIContent("Unique state ID")); EditorGUILayout.PropertyField(state.FindPropertyRelative("role"));
                bool protectedRole=state.FindPropertyRelative("role").enumValueIndex!=0;
                if(protectedRole) EditorGUILayout.HelpBox("Combat owns this interrupt state. It stays active until the existing hit reaction recovers; its movement/attack actions and authored transitions are not executed.",MessageType.Info);
                else
                {
                    foreach(var field in new[]{"action","duration","movementScale","faceTarget"}) EditorGUILayout.PropertyField(state.FindPropertyRelative(field));
                    var action=(EnemyAIAction)state.FindPropertyRelative("action").enumValueIndex;
                    if(action==EnemyAIAction.UseAttack || action==EnemyAIAction.ProjectileAttack) StringPicker(state.FindPropertyRelative("attackChoice"),"Attack choice",profile.attacks.Where(a=>a!=null).Select(a=>a.id).ToArray());
                    var transitions=state.FindPropertyRelative("transitions");
                    EditorGUILayout.LabelField("Transitions — checked top to bottom",EditorStyles.boldLabel);
                    for(int t=0;t<transitions.arraySize;t++)
                    {
                        var transition=transitions.GetArrayElementAtIndex(t); EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.BeginHorizontal(); EditorGUILayout.LabelField("IF all conditions pass →");
                        if(GUILayout.Button("↑",GUILayout.Width(25)) && t>0) { transitions.MoveArrayElement(t,t-1); serializedObject.ApplyModifiedProperties(); GUIUtility.ExitGUI(); }
                        if(GUILayout.Button("↓",GUILayout.Width(25)) && t<transitions.arraySize-1) { transitions.MoveArrayElement(t,t+1); serializedObject.ApplyModifiedProperties(); GUIUtility.ExitGUI(); }
                        if(GUILayout.Button("×",GUILayout.Width(25))) { transitions.DeleteArrayElementAtIndex(t); serializedObject.ApplyModifiedProperties(); GUIUtility.ExitGUI(); } EditorGUILayout.EndHorizontal();
                        StringPicker(transition.FindPropertyRelative("targetState"),"Go to",profile.states.Where(s=>s!=null && s.role==EnemyAIStateRole.Normal).Select(s=>s.id).ToArray());
                        EditorGUILayout.PropertyField(transition.FindPropertyRelative("conditions"),true); EditorGUILayout.EndVertical();
                    }
                    if(GUILayout.Button("Add transition")) { int t=transitions.arraySize; transitions.InsertArrayElementAtIndex(t); var transition=transitions.GetArrayElementAtIndex(t); transition.FindPropertyRelative("targetState").stringValue=profile.defaultState; transition.FindPropertyRelative("conditions").ClearArray(); }
                }
            }
            EditorGUILayout.EndVertical();
        }
        if(GUILayout.Button("Add state"))
        {
            int i=states.arraySize; states.InsertArrayElementAtIndex(i); var s=states.GetArrayElementAtIndex(i);
            s.FindPropertyRelative("id").stringValue="State"+(i+1); s.FindPropertyRelative("role").enumValueIndex=0; s.FindPropertyRelative("action").enumValueIndex=0;
            s.FindPropertyRelative("duration").floatValue=.25f; s.FindPropertyRelative("movementScale").floatValue=1; s.FindPropertyRelative("faceTarget").boolValue=true;
            s.FindPropertyRelative("attackChoice").stringValue=""; s.FindPropertyRelative("transitions").ClearArray(); s.isExpanded=true;
        }
        EditorGUILayout.Space(); attacks.DoLayoutList(); serializedObject.ApplyModifiedProperties();
        EditorGUILayout.LabelField("Validation",EditorStyles.boldLabel);
        var warnings=profile.Validate().ToArray(); if(warnings.Length==0) EditorGUILayout.HelpBox("Profile references are valid.",MessageType.Info);
        foreach(var warning in warnings) EditorGUILayout.HelpBox(warning,MessageType.Warning);
        if(GUILayout.Button("Validate profile")) Debug.Log(warnings.Length==0 ? profile.name+": valid" : string.Join("\n",warnings),profile);
        EditorGUILayout.Space(); EditorGUILayout.LabelField("Attack range preview (no spawning)",EditorStyles.boldLabel);
        previewDistance=EditorGUILayout.FloatField("X distance",previewDistance); previewLane=EditorGUILayout.FloatField("Lane difference",previewLane);
        EditorGUILayout.HelpBox("This previews range and lane only. Live cooldowns, target health and additional prerequisites are shown on the running enemy.",MessageType.None);
        foreach(var choice in profile.attacks.Where(a=>a!=null)) EditorGUILayout.LabelField(choice.id,(profile.useGroundPlaneRange ? new Vector2(previewDistance,previewLane).magnitude : Mathf.Abs(previewDistance))>=choice.minimumRange && (profile.useGroundPlaneRange ? new Vector2(previewDistance,previewLane).magnitude : Mathf.Abs(previewDistance))<=choice.maximumRange && Mathf.Abs(previewLane)<choice.laneTolerance ? "In range; weight "+choice.weight : "Out of range");
    }
}

[CustomEditor(typeof(EnemyCombat))]
public sealed class EnemyCombatAIEditor : Editor
{
    int forced;
    public override void OnInspectorGUI()
    {
        var brain=(EnemyCombat)target;
        serializedObject.Update();
        if(brain.aiProfile) DrawPropertiesExcluding(serializedObject,"attack","attackRange","minimumAttackRange","laneRange","attackCooldownFrames"); else DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();
        if(!brain.aiProfile) EditorGUILayout.HelpBox("No AI profile: original enemy behavior remains active. Assign a profile to edit states and decisions.",MessageType.Info);
        if(GUILayout.Button("Open Enemy AI Editor")) EnemyAIEditorWindow.Open(brain.aiProfile,brain);
        if(brain.aiProfile && brain.aiProfile.attacks.Any(a=>a!=null && a.projectile) && !brain.GetComponent<EnemyProjectileAttack>()) EditorGUILayout.HelpBox("Projectile choices require EnemyProjectileAttack and a projectile prefab.",MessageType.Warning);
        SupportTuning(brain);
        if(Application.isPlaying) Live(brain,ref forced);
    }
    public static void SupportTuning(EnemyCombat brain)
    {
        var support=brain ? brain.GetComponent<PrefectSupport>() : null;if(!support)return;
        EditorGUILayout.LabelField("Prefect support tuning",EditorStyles.boldLabel);
        var so=new SerializedObject(support);so.Update();
        foreach(var field in new[]{"rusherPrefab","rushersPerCall","retreatDistance","emergencyPushDistance","minimumCallDistance","spawnPlayerClearance","spawnEnemyClearance"})EditorGUILayout.PropertyField(so.FindProperty(field));
        so.ApplyModifiedProperties();
        if(brain.motor){var motor=new SerializedObject(brain.motor);motor.Update();EditorGUILayout.PropertyField(motor.FindProperty("moveSpeed"),new GUIContent("Retreat Speed"));motor.ApplyModifiedProperties();}
        if(brain.aiProfile){var profile=new SerializedObject(brain.aiProfile);profile.Update();EditorGUILayout.PropertyField(profile.FindProperty("preferredDistance"));EditorGUILayout.PropertyField(profile.FindProperty("attacks"),new GUIContent("Call / push cooldown choices"),true);profile.ApplyModifiedProperties();}
        if(support.pushAttack && support.pushAttack.FirstActiveFrame>=0 && support.pushAttack.frames[support.pushAttack.FirstActiveFrame].hitboxes.Count>0){var attack=new SerializedObject(support.pushAttack);attack.Update();bool changed=false;var first=attack.FindProperty("frames").GetArrayElementAtIndex(support.pushAttack.FirstActiveFrame).FindPropertyRelative("hitboxes").GetArrayElementAtIndex(0);float push=first.FindPropertyRelative("knockback").floatValue;
            EditorGUI.BeginChangeCheck();push=EditorGUILayout.FloatField("Push Knockback",push);changed=EditorGUI.EndChangeCheck();if(changed){var frames=attack.FindProperty("frames");for(int i=0;i<frames.arraySize;i++){var boxes=frames.GetArrayElementAtIndex(i).FindPropertyRelative("hitboxes");for(int j=0;j<boxes.arraySize;j++)boxes.GetArrayElementAtIndex(j).FindPropertyRelative("knockback").floatValue=Mathf.Max(0,push);}attack.ApplyModifiedProperties();}}
    }
    public static void Live(EnemyCombat brain,ref int forced)
    {
        if(!brain) return;
        EditorGUILayout.Space(); EditorGUILayout.LabelField("Live AI (runtime only)",EditorStyles.boldLabel);
        EditorGUILayout.LabelField("State",brain.AI?.StateName ?? "Legacy behavior");
        EditorGUILayout.LabelField("Attack tokens",brain.coordinator ? brain.coordinator.Describe(brain) : "No encounter coordinator (standalone AI)");
        var running=brain.attackPlayer.CurrentAttack;
        if(running && running.feedback != null && running.feedback.areaWarning)
            EditorGUILayout.LabelField("Attack phase",running.Phase(brain.attackPlayer.CurrentFrame)+" (frame "+brain.attackPlayer.CurrentFrame+")");
        EditorGUILayout.ObjectField("Target",brain.target,typeof(Transform),true);
        EditorGUILayout.ObjectField("Chosen attack",brain.AI?.Selected?.attack ?? brain.attackPlayer.CurrentAttack,typeof(AttackData),false);
        EditorGUILayout.LabelField("Combat reaction",brain.reaction.State.ToString());
        EditorGUILayout.LabelField("Parry stun",brain.reaction.StunRemaining+" / "+brain.reaction.StunDuration+"f | Can be stunned: "+brain.reaction.CanBeParryStunned);
        var armor=brain.GetComponent<HitCountArmor>();
        if(armor) EditorGUILayout.LabelField("Armor",armor.ArmorRemaining+" / "+armor.maxArmorHits+" | Active: "+armor.ArmorActive+" | Broken: "+armor.ArmorBroken+" | Restore: "+armor.RecoveryRemaining+"f");
        var support=brain.GetComponent<PrefectSupport>();
        if(support){EditorGUILayout.LabelField("Prefect ranges", "Preferred "+support.PreferredDistance+" / Retreat "+support.retreatDistance+" / Emergency "+support.emergencyPushDistance);EditorGUILayout.LabelField("Backup",support.CallReady+" | cooldown "+support.CallCooldown.ToString("F2")+" | last spawned "+support.LastCallSpawned);EditorGUILayout.LabelField("Push",support.PushReady+" | cooldown "+support.PushCooldown.ToString("F2"));EditorGUILayout.LabelField("Encounter / Rushers / slots",support.ActiveEnemyCount+" / "+support.ActiveRusherCount+" / "+support.EnemySlots);EditorGUILayout.LabelField("Retreat direction",support.SelectedRetreatDirection.ToString("F2"));}
        var grab=brain.GetComponent<CombatGrabController>();
        if(grab)
        {
            EditorGUILayout.LabelField("Grab phase / frame",(running==grab.successfulGrab ? grab.leapEnabled ? "Hold / face strikes / release" : "Hold / slam" : grab.LeapActive ? grab.GrabActive ? "Descending / grab active" : "Leaping" : grab.LungeActive ? grab.GrabActive ? "Lunge / grab active" : "Launch / lunge" : running==grab.grabAttack && brain.attackPlayer.CurrentFrame<running.FirstActiveFrame ? "Telegraph" : "Recovery / neutral")+" / "+brain.attackPlayer.CurrentFrame);
            EditorGUILayout.ObjectField("Current grab target",grab.CurrentGrabbedTarget,typeof(ComboController),true);
            EditorGUILayout.ObjectField("Grab anchor",grab.grabAnchor,typeof(Transform),true);
            EditorGUILayout.LabelField("Hold / miss recovery",grab.HoldFrames+"f / "+grab.GrabMissRecoveryFrames+"f");
            EditorGUILayout.LabelField("Committed target / direction",grab.TargetPositionAtCommit.ToString("F2")+" / "+grab.LockedGrabDirection.ToString("F2"));
            EditorGUILayout.ObjectField("Locked player",grab.LockedTarget,typeof(Transform),true);
            if(grab.leapEnabled) EditorGUILayout.LabelField("Leap / face strikes",grab.LeapActive+" | destination "+grab.LeapDestination.ToString("F2")+" | strikes "+grab.FaceStrikesApplied+" / "+grab.faceAttackCount);
            EditorGUILayout.LabelField("Lunge",grab.LungeActive+" | "+grab.LungeDistanceRemaining.ToString("F2")+" remaining | Wall stop: "+grab.StoppedByWall);
        }
        if(brain.AI!=null)
        {
            EditorGUILayout.LabelField("State / recovery seconds",brain.AI.StateSeconds.ToString("F2")+" / "+brain.AI.RecoverySeconds.ToString("F2"));
            EditorGUILayout.LabelField("Last transition",brain.AI.LastTransition);
            foreach(var choice in brain.aiProfile.attacks.Where(a=>a!=null)) EditorGUILayout.LabelField(choice.id+" cooldown",brain.AI.Cooldown(choice.id).ToString("F2"));
            EditorGUILayout.LabelField("Eligible choices",string.Join(", ",brain.AI.AvailableAttacks().Select(a=>a.id)));
            var ids=brain.aiProfile.states.Where(s=>s!=null && s.role==EnemyAIStateRole.Normal).Select(s=>s.id).ToArray();
            if(ids.Length>0) { forced=Mathf.Clamp(forced,0,ids.Length-1); forced=EditorGUILayout.Popup("Force state",forced,ids); if(GUILayout.Button("Force state (safe neutral only)")) brain.AI.ForceState(ids[forced]); }
        }
        if(GUILayout.Button("Refresh AI (reset timers)")) brain.RefreshAI();
        if(GUILayout.Button("Print current AI state")) Debug.Log(brain.name+": "+brain.AI?.StateName+"; target "+brain.target+"; attack "+brain.attackPlayer.CurrentAttack,brain);
    }
    void OnSceneGUI()
    {
        var brain=(EnemyCombat)target; if(!brain.aiProfile) return;
        Handles.color=new Color(1,.65f,.1f,.7f); Handles.DrawWireDisc(brain.transform.position,Vector3.forward,brain.aiProfile.aggroRange);
        var running=brain.attackPlayer.CurrentAttack;
        string phase=running && running.feedback != null && running.feedback.areaWarning ? "\n"+running.Phase(brain.attackPlayer.CurrentFrame) : "";
        string cooldown=brain.AI!=null && brain.AI.Selected!=null ? "\nCooldown: "+brain.AI.Cooldown(brain.AI.Selected.id).ToString("F2")+"s" : "";
        Handles.Label(brain.transform.position+Vector3.up*1.1f,brain.name+"\n"+(brain.AI?.StateName ?? brain.aiProfile.defaultState)+phase+cooldown);
        if(brain.target) { Handles.color=Color.cyan; Handles.DrawLine(brain.transform.position,brain.target.position); }
    }
    public override bool RequiresConstantRepaint() => Application.isPlaying;
}

public sealed class EnemyAIEditorWindow : EditorWindow
{
    EnemyAIProfile profile;
    EnemyCombat brain;
    Editor inspector;
    Vector2 scroll;
    int forced;
    [MenuItem("Beat Em Up/Enemies/Enemy AI Editor")]
    public static void ShowWindow() => Open(Selection.activeObject as EnemyAIProfile,Selection.activeGameObject ? Selection.activeGameObject.GetComponent<EnemyCombat>() : null);
    public static void Open(EnemyAIProfile profile,EnemyCombat brain=null)
    {
        var window=GetWindow<EnemyAIEditorWindow>("Enemy AI Editor"); window.profile=profile; window.brain=brain; window.minSize=new Vector2(480,450); window.Show();
    }
    void OnGUI()
    {
        EditorGUILayout.LabelField("Enemy AI Editor",EditorStyles.boldLabel);
        brain=(EnemyCombat)EditorGUILayout.ObjectField("Enemy (optional live debug)",brain,typeof(EnemyCombat),true);
        if(brain && GUILayout.Button("Use this enemy's profile")) profile=brain.aiProfile;
        profile=(EnemyAIProfile)EditorGUILayout.ObjectField("AI profile",profile,typeof(EnemyAIProfile),false);
        if(GUILayout.Button("Create new AI profile"))
        {
            string path=EditorUtility.SaveFilePanelInProject("Create AI profile","NewEnemyAI","asset","Choose a profile asset path");
            if(!string.IsNullOrEmpty(path)) { profile=CreateInstance<EnemyAIProfile>(); profile.states.Add(new EnemyAIState()); AssetDatabase.CreateAsset(profile,path); }
        }
        if(!profile) { EditorGUILayout.HelpBox("Select an Enemy AI Profile asset, or an enemy with a profile. New profiles also appear under Create → Beat Em Up → Enemy AI Profile.",MessageType.Info); return; }
        if(brain && !Application.isPlaying && GUILayout.Button("Assign profile to selected enemy")) { Undo.RecordObject(brain,"Assign Enemy AI Profile"); brain.aiProfile=profile; PrefabUtility.RecordPrefabInstancePropertyModifications(brain); EditorUtility.SetDirty(brain); }
        scroll=EditorGUILayout.BeginScrollView(scroll);
        if(brain)EnemyCombatAIEditor.SupportTuning(brain);
        if(Application.isPlaying && brain) EnemyCombatAIEditor.Live(brain,ref forced);
        Editor.CreateCachedEditor(profile,null,ref inspector); inspector.OnInspectorGUI(); EditorGUILayout.EndScrollView();
    }
    void OnInspectorUpdate() { if(Application.isPlaying) Repaint(); }
    void OnDisable() { if(inspector) DestroyImmediate(inspector); }
}
