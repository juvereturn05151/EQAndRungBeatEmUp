using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AirAttackPolishSetup
{
    const string Stamp = "Air timing polish: 15% longer";
    static AirAttackPolishSetup() { EditorApplication.update += Poll; }
    static int Boundary(int frame) => (frame * 115 + 99) / 100;
    [MenuItem("Beat Em Up/Combat/Apply slower air attacks and finisher impact")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var impact = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit D 3D (Yellow).prefab");
        if (!impact) throw new Exception("Missing air finisher impact prefab");
        var paths = new[] { "Assets/EQ_Rung_BeatEmUp/Attacks/AirPunch1.asset", "Assets/EQ_Rung_BeatEmUp/Attacks/AirPunch2.asset", "Assets/EQ_Rung_BeatEmUp/Attacks/AirPunch3.asset", "Assets/EQ_Rung_BeatEmUp/Attacks/AirHeadbuttDive.asset" }
            .Concat(new[] { "AirPunch1", "AirPunch2", "AirPunch3", "AirDive" }.Select(n => "Assets/EQ_Rung_BeatEmUp/Characters/Character2/Character2_" + n + ".asset"));
        foreach (var path in paths)
        {
            var attack = AssetDatabase.LoadAssetAtPath<AttackData>(path);
            if (!attack) throw new Exception("Missing air attack: " + path);
            Undo.RecordObject(attack, "Slow air attack and strengthen finisher feedback");
            if (!(attack.artworkNotes ?? "").Contains(Stamp))
            {
                int previousTotal = attack.TotalFrames;
                var frames = new List<AttackFrameData>();
                for (int i = 0; i < previousTotal; i++)
                {
                    frames.Add(attack.frames[i]);
                    for (int repeat = 1; repeat < Boundary(i + 1) - Boundary(i); repeat++)
                    {
                        var hold = JsonUtility.FromJson<AttackFrameData>(JsonUtility.ToJson(attack.frames[i]));
                        // Holds retain hit/cancel/physics settings, without repeating entry impulses or cues.
                        hold.events.Clear(); hold.movement = Vector2.zero;
                        hold.setVerticalVelocity = false; hold.verticalVelocityModifier = 0;
                        frames.Add(hold);
                    }
                }
                if (attack.landingFrame >= 0) attack.landingFrame = Boundary(attack.landingFrame);
                if (attack.airborneHoldFrame >= 0) attack.airborneHoldFrame = Boundary(attack.airborneHoldFrame + 1) - 1;
                attack.frames = frames;
                attack.artworkNotes = Stamp + ": " + previousTotal + " -> " + attack.TotalFrames + " combat frames. Frame-entry cues occur once; damage, hitstop, gravity, impulses and hit IDs retained. Dive landing/hold indices remapped. Timings editable in Frame Attack Editor.";
            }
            if (path.EndsWith("AirPunch3.asset"))
            {
                attack.feedback.impactPrefab = impact;
                attack.feedback.impactScale = .24f; attack.feedback.impactLifetime = .6f;
                attack.feedback.impactRotateWithFacing = false;
            }
            EditorUtility.SetDirty(attack);
        }
        AssetDatabase.SaveAssets(); MultiplayerSetup.Build();
        Debug.Log("AIR ATTACK POLISH: all eight player air attacks slowed; both combo finishers use a larger confirmed-hit burst.");
    }
    static void Poll()
    {
        const string request = "Temp/AirAttackPolish.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request); Build(); AirPunchPlaytestValidation.Run();
    }
}
