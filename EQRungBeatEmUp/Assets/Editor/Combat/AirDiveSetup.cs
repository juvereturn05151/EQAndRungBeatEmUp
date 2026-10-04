using System;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AirDiveSetup
{
    public const string AssetPath = "Assets/EQ_Rung_BeatEmUp/Attacks/AirHeadbuttDive.asset";
    public const string ArtPath = "Assets/ArtAssets/Characters/BlueShirtGuy/AirDive/";
    private static Sprite Pose(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + "BlueShirtGuy_AirDive_" + name + ".png");
    [MenuItem("Beat Em Up/Combat/Set up airborne headbutt dive")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var attack = AssetDatabase.LoadAssetAtPath<AttackData>(AssetPath);
        if (!attack)
        {
            attack = ScriptableObject.CreateInstance<AttackData>(); attack.attackName = "Air Headbutt Dive";
            attack.domain = AttackDomain.Air; attack.requiresAirborne = true; attack.landingFrame = 14; attack.airborneHoldFrame = 13;
            attack.artworkNotes = "Head-first special. 0-4 startup, 5-6 travel, 7-13 active (13 holds until landing), 14-16 impact, 17-23 recovery. Velocity5: forward6/down8; compact head hitbox. Ground contact branches directly to14. Events: DiveWindup, DiveWhoosh, DiveLanding. Edit all timings/physics/hits here.";
            for (int i = 0; i < 24; i++)
            {
                string pose = i < 2 ? "AirReady" : i < 5 ? "Windup" : i < 7 ? "DiveStart" : i < 14 ? "ActiveDive" : i < 17 ? "Impact" : "LandingRecovery";
                var frame = new AttackFrameData { sprite = Pose(pose), movementInputScale = 0, gravityScale = i < 5 ? 0 : i < 14 ? .35f : 1,
                    setHorizontalVelocity = true, horizontalVelocity = i >= 5 && i < 14 ? 6 : 0,
                    setVerticalVelocity = i == 0 || i == 5, verticalVelocity = i == 5 ? -8 : 0 };
                if (!frame.sprite) throw new Exception("Missing dive pose " + pose);
                if (i >= 7 && i <= 13) frame.hitboxes.Add(new AttackHitboxData { hitId = 0, offset = new Vector2(.48f, .2f), size = new Vector2(.42f, .42f), laneTolerance = .4f,
                    damage = 14, hitstopFrames = 6, hitstunFrames = 22, knockback = 1.2f, hitType = HitType.AirFinisher, launchVelocity = new Vector2(0, -5), canHitGrounded = true, canHitAirborne = true });
                if (i == 0) frame.events.Add("DiveWindup"); if (i == 5) frame.events.Add("DiveWhoosh"); if (i == 14) frame.events.Add("DiveLanding");
                attack.frames.Add(frame);
            }
            AssetDatabase.CreateAsset(attack, AssetPath);
        }
        string prefabPath = "Assets/EQ_Rung_BeatEmUp/Prefabs/BlueShirtGuy.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try { root.GetComponent<ComboController>().airDive = attack; PrefabUtility.SaveAsPrefabAsset(root, prefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        foreach (string path in new[] { CombatDemoBuilder.ScenePath, "Assets/EQ_Rung_BeatEmUp/Scenes/HauntedHouse.unity" })
        {
            if (!System.IO.File.Exists(path)) continue;
            var scene = EditorSceneManager.OpenScene(path);
            foreach (var player in UnityEngine.Object.FindObjectsByType<ComboController>(FindObjectsSortMode.None)) { player.airDive = attack; EditorUtility.SetDirty(player); }
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets(); Debug.Log("AIR HEADBUTT DIVE SETUP COMPLETE: six key poses / nominal24 frames / Launcher in air");
    }
}
