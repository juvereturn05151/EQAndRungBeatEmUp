using BeatEmUp;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CombatProjectile)), CanEditMultipleObjects]
public sealed class CombatProjectileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var wave = serializedObject.FindProperty("groundWave"); EditorGUILayout.PropertyField(wave, new GUIContent("Ground Wave (Rectangular)"));
        bool ground = wave.boolValue;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("speed"), new GUIContent(ground ? "Wave Speed" : "Speed"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lifetimeFrames"), new GUIContent(ground ? "Wave Lifetime Frames" : "Lifetime Frames"));
        if (ground)
        {
            var size = serializedObject.FindProperty("waveSize");
            EditorGUILayout.PropertyField(size.FindPropertyRelative("x"), new GUIContent("Wave Width", "Full width along horizontal travel X."));
            EditorGUILayout.PropertyField(size.FindPropertyRelative("y"), new GUIContent("Wave Depth", "Full walking-lane Y depth; half this amount on each side. Independent of jump height."));
            EditorGUILayout.HelpBox("Swept ground rectangle; no circular collision. Depth is the full walking-lane band. Crescents resize to these values. Maximum Travel Distance is measured from the spawn center; 0 uses lifetime alone.",MessageType.Info);
        }
        else EditorGUILayout.PropertyField(serializedObject.FindProperty("collisionRadius"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maximumTravelDistance"));
        DrawPropertiesExcluding(serializedObject,"m_Script","groundWave","waveSize","speed","lifetimeFrames","collisionRadius","maximumTravelDistance");
        serializedObject.ApplyModifiedProperties();
        if (Application.isPlaying && target is CombatProjectile projectile)
        {
            EditorGUILayout.LabelField("Owner / faction", (projectile.Owner ? projectile.Owner.name : "None") + " / " + projectile.Faction);
            EditorGUILayout.LabelField("Velocity", projectile.Velocity.ToString());
            EditorGUILayout.LabelField("Parry / deflect", projectile.hit.canBeParried + " / " + projectile.CanDeflect);
            EditorGUILayout.LabelField("Deflections / age", projectile.DeflectionCount + " / " + projectile.maxDeflections + " | " + projectile.Age + " / " + projectile.lifetimeFrames + "f");
        }
    }
    public override bool RequiresConstantRepaint() => Application.isPlaying;
}
