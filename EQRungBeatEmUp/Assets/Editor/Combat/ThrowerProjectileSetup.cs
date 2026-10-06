using System;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

public static class ThrowerProjectileSetup
{
    public const string Root = "Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/";
    public const string ProjectilePath = Root + "Prefabs/NotebookProjectile.prefab";
    public const string ThrowerPath = Root + "Prefabs/Thrower.prefab";
    public const string AttackPath = Root + "Attacks/Thrower_Throw_Notebook.asset";
    [MenuItem("Beat Em Up/Enemies/Configure Thrower notebook projectile")]
    public static void Build()
    {
        if (Application.isPlaying) return;
        var sprites = Enumerable.Range(1, 6).Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/ArtAssets/Characters/Enemies/Effects/Animations/Notebook/Effects_Notebook_" + i.ToString("00") + ".png")).ToArray();
        if (sprites.Any(s => !s)) throw new Exception("Notebook flight sprites are missing");
        var go = new GameObject("NotebookProjectile");
        GameObject prefab;
        try
        {
            var projectile = go.AddComponent<CombatProjectile>();
            projectile.groundPlaneFlight=true; projectile.hit.canHitGrounded=true; projectile.hit.canHitAirborne=false;
            var child = new GameObject("Visual"); child.transform.SetParent(go.transform, false);
            child.transform.localPosition = -sprites[0].bounds.center;
            projectile.visual = child.AddComponent<SpriteRenderer>(); projectile.visual.sprite = sprites[0];
            projectile.flightSprites = sprites;
            prefab = PrefabUtility.SaveAsPrefabAsset(go, ProjectilePath);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
        var thrower = PrefabUtility.LoadPrefabContents(ThrowerPath);
        try
        {
            var brain = thrower.GetComponent<EnemyCombat>();
            brain.attackRange = 6; brain.minimumAttackRange = 1.25f; brain.laneRange = .55f;
            var ranged = thrower.GetComponent<EnemyProjectileAttack>();
            if (!ranged) ranged = thrower.AddComponent<EnemyProjectileAttack>();
            ranged.projectilePrefab = prefab.GetComponent<CombatProjectile>();
            PrefabUtility.SaveAsPrefabAsset(thrower, ThrowerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(thrower); }
        var attack = AssetDatabase.LoadAssetAtPath<AttackData>(AttackPath);
        int release = attack.frames.FindIndex(f => f.sprite && f.sprite.name.EndsWith("_04", StringComparison.Ordinal));
        if (release < 0) throw new Exception("Throw release drawing is missing");
        foreach (var frame in attack.frames) { frame.hitboxes.Clear(); frame.events.RemoveAll(e => e == "ThrowProjectile" || e == "Swing"); }
        attack.frames[release].events.Add("Swing"); attack.frames[release].events.Add("ThrowProjectile");
        attack.feedback = new AttackFeedbackData {
            swingSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Deadly Kombat Free version/punch_long_whoosh_21.wav"), swingVolume = .28f
        };
        attack.artworkNotes = "Notebook ranged throw: existing 40-frame six-pose animation; release on drawing 04 (frame 18). No melee hitboxes. Frame event spawns one projectile through EnemyProjectileAttack.";
        EditorUtility.SetDirty(attack); AssetDatabase.SaveAssets();
        Debug.Log("Thrower configured: release frame " + release + ", 1.25–6 range, swept notebook projectile");
    }
}
