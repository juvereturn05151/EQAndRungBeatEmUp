using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerSkillData))]
public sealed class PlayerSkillDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); var skill = (PlayerSkillData)target;
        if (!skill.cast) return;
        int[] releases = skill.cast.frames.Select((f,i)=>new {f,i}).Where(v=>v.f != null && v.f.events.Contains(skill.releaseEvent)).Select(v=>v.i).ToArray();
        if (releases.Length != 1) EditorGUILayout.HelpBox("Author exactly one '"+skill.releaseEvent+"' event in the cast's frame data. The runtime releases once per cast.",MessageType.Warning);
        else EditorGUILayout.HelpBox("Release: frame "+releases[0]+" (zero-based). Cast: "+skill.cast.TotalFrames+" frames. Startup: "+releases[0]+"; recovery after release: "+(skill.cast.TotalFrames-releases[0]-1)+". Open the cast in the existing Frame Attack Editor to change poses and release timing.",MessageType.Info);
        if (GUILayout.Button("Select cast frame data")) Selection.activeObject = skill.cast;
        if (skill.projectilePrefab && GUILayout.Button("Select projectile tuning")) Selection.activeObject = skill.projectilePrefab.gameObject;
        if(skill.delivery==PlayerSkillDelivery.Area)
        {
            var box=skill.cast.frames.SelectMany(f=>f.hitboxes).FirstOrDefault(h=>h.groundArea);
            if(box!=null)
            {
                EditorGUILayout.Space(); EditorGUILayout.LabelField("Shared ground-area hitbox tuning",EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                float radius=EditorGUILayout.FloatField("Skill radius (X)",box.size.x*.5f);
                float depth=EditorGUILayout.FloatField("Skill depth radius (Y)",box.size.y*.5f);
                float damage=EditorGUILayout.FloatField("Damage",box.damage);
                int stun=EditorGUILayout.IntField("Hitstun frames",box.hitstunFrames), stop=EditorGUILayout.IntField("Hitstop frames",box.hitstopFrames);
                float knockback=EditorGUILayout.FloatField("Outward knockback",box.knockback);
                if(EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(skill.cast,"Tune area skill");
                    foreach(var hit in skill.cast.frames.SelectMany(f=>f.hitboxes).Where(h=>h.groundArea))
                    {
                        hit.size=new Vector2(Mathf.Max(.01f,radius)*2,Mathf.Max(.01f,depth)*2); hit.laneTolerance=Mathf.Max(.01f,depth);
                        hit.damage=Mathf.Max(0,damage); hit.hitstunFrames=Mathf.Max(0,stun); hit.hitstopFrames=Mathf.Max(0,stop); hit.knockback=Mathf.Max(0,knockback);
                    }
                    EditorUtility.SetDirty(skill.cast);
                }
                EditorGUILayout.LabelField("Active frames",skill.cast.ActiveFrames.ToString());
                EditorGUILayout.LabelField("Recovery frames",(skill.cast.TotalFrames-skill.cast.LastActiveFrame-1).ToString());
                EditorGUILayout.HelpBox("Hitboxes use the cast AttackData's normal damage/defense/history path. Radius is projected onto the walking plane; depth prevents hits in invalid lanes. Edit active/recovery duration in the Frame Attack Editor.",MessageType.Info);
            }
        }
    }
}

[CustomEditor(typeof(PlayerMeter))]
public sealed class PlayerMeterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); var meter = (PlayerMeter)target;
        if (Application.isPlaying)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Current resource",meter.CurrentMeter.ToString("0.##")+" / "+meter.MaxMeter.ToString("0.##")+" bars");
            var rect=EditorGUILayout.GetControlRect(false,20); EditorGUI.ProgressBar(rect,meter.Normalized,"Skill meter");
        }
        EditorGUILayout.HelpBox("Amounts are in bars. Accepted enemy damage grants hit gain; successful parry grants parry gain. Whiffs, blocks and rejected damage do not grant meter. Starting Meter is restored with the existing new-run / health restore event.",MessageType.Info);
    }
}
