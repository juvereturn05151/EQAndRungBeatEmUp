using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Refreshes the existing demo without rebuilding scenes or replacing user artwork.
public static class CombatDemoBuilder
{
    public const string Output = "Assets/EQ_Rung_BeatEmUp";
    public const string ScenePath = Output + "/Scenes/ComboDemo.unity";
    [MenuItem("Beat Em Up/Edit Punch1 frame data")]
    public static void EditPunch()
    {
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<AttackData>(Output + "/Attacks/Punch1.asset");
        EditorGUIUtility.PingObject(Selection.activeObject);
    }
    [MenuItem("Beat Em Up/Refresh frame combat setup")]
    public static void Build()
    {
        foreach (string name in new[] { "BlueShirtGuy", "BadGuy" })
        {
            string path = Output + "/Prefabs/" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var player = root.GetComponent<AttackPlayer>();
                if (!player) player = root.AddComponent<AttackPlayer>();
                player.motor = root.GetComponent<CharacterMotor>(); player.hitbox = root.GetComponent<AttackHitbox>();
                player.animationDriver = root.GetComponent<CharacterAnimation>();
                player.motor.attackPlayer = player; player.hitbox.owner = player;
                var combo = root.GetComponent<ComboController>(); if (combo) combo.attackPlayer = player;
                var enemy = root.GetComponent<EnemyCombat>(); if (enemy) enemy.attackPlayer = player;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string name in new[] { "BlueShirtGuy", "BadGuy" })
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Output + "/Animations/" + name + ".controller");
            if (!controller) continue;
            foreach (var layer in controller.layers)
                foreach (var child in layer.stateMachine.states.ToArray())
                    if (new[] { "Punch1", "Punch2", "Punch3", "Launcher", "AirPunch1", "AirPunch2", "AirPunch3", "Attack" }.Contains(child.state.name))
                        layer.stateMachine.RemoveState(child.state);
            EditorUtility.SetDirty(controller);
        }
        AssetDatabase.SaveAssets(); Debug.Log("Frame combat references refreshed; only locomotion/reaction Animator states remain.");
    }
}
