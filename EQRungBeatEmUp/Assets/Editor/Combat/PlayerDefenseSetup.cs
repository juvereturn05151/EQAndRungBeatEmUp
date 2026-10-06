using System;
using System.IO;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PlayerDefenseSetup
{
    private const string Root = "Assets/EQ_Rung_BeatEmUp";
    static PlayerDefenseSetup() { EditorApplication.update += PollParrySetup; }
    static void PollParrySetup()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Temp/ParrySetup.request")) return;
        try { File.Delete("Temp/ParrySetup.request"); } catch (IOException) { return; }
        ConfigureParry();
    }
    [MenuItem("Beat Em Up/Defense/Configure parry defaults")]
    public static void ConfigureParry()
    {
        if (EditorApplication.isPlaying) return;
        var data = AssetDatabase.LoadAssetAtPath<PlayerDefenseData>(Root + "/PlayerDefense.asset");
        if (!data) throw new InvalidOperationException("Existing PlayerDefense asset missing");
        Undo.RecordObject(data, "Configure parry defaults");
        data.parryWindowFrames = 8; data.parryHitstopFrames = 6; data.parryAttackerStunFrames = 90;
        data.parryRecoveryFrames = 8; data.parryRearmDelayFrames = 6;
        data.parryFeedback = new AttackFeedbackData {
            impactSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Deadly Kombat Free version/block_large_71.wav"), impactVolume = .7f,
            impactPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Sword Trails/Plain/CFXR4 Sword Hit PLAIN (Cross).prefab"),
            impactScale = .2f, impactLifetime = .45f
        };
        EditorUtility.SetDirty(data); AssetDatabase.SaveAssets(); MultiplayerSetup.Build();
        Debug.Log("PARRY DEFAULTS CONFIGURED: 8f active, 6f hitstop, 90f enemy stun, 8f player recovery, 6f extra re-arm delay.");
    }
    public static void Build()
    {
        try
        {
            const string path = Root + "/PlayerDefense.asset";
            var data = AssetDatabase.LoadAssetAtPath<PlayerDefenseData>(path);
            if (!data) { data = ScriptableObject.CreateInstance<PlayerDefenseData>(); AssetDatabase.CreateAsset(data, path); }
            data.dodge = Holds(new[] { Existing("Attack1", 9), Existing("Attack1", 10), Existing("Attack1", 10), Existing("Attack1", 11), Existing("Idle", 1) }, new[] { 3, 3, 4, 4, 6 });
            data.guard = Holds(new[] { Existing("Attack1", 2), Existing("Attack1", 4) }, new[] { 2, 3 });
            data.blockPose = Existing("Attack1", 5);
            data.parry = Holds(new[] { Existing("Attack1", 2), New("Parry", 1), New("Parry", 2), Existing("Attack1", 7) }, new[] { 1, 2, 3, 2 });
            data.knockdown = Holds(new[] { New("KnockDown", 1), New("KnockDown", 2), New("KnockDown", 3), New("KnockDown", 4) }, new[] { 4, 5, 4, 5 });
            data.die = Holds(new[] { New("KnockDown", 1), New("KnockDown", 2), New("KnockDown", 3), New("KnockDown", 4) }, new[] { 3, 5, 4, 6 });
            data.getUp = Holds(new[] { New("KnockDown", 4), New("KnockDown", 3), New("KnockDown", 2), New("KnockDown", 1), Existing("Idle", 1) }, new[] { 6, 6, 6, 5, 1 });
            EditorUtility.SetDirty(data);
            var prefab = PrefabUtility.LoadPrefabContents(Root + "/Prefabs/BlueShirtGuy.prefab");
            try { prefab.GetComponent<ComboController>().defenseData = data; PrefabUtility.SaveAsPrefabAsset(prefab, Root + "/Prefabs/BlueShirtGuy.prefab"); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
            Debug.Log("PLAYER DEFENSE DATA CONFIGURED");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(1); }
    }
    // Gameplay copies have the established foot pivot. Source-art copies use
    // centered pivots and must not be mixed into grounded state timelines.
    private static Sprite Existing(string animation, int index) => Load($"{Root}/Sprites/BlueShirtGuy_{(animation == "Idle" ? "Idle2" : animation)}_{index:00}.png");
    private static Sprite New(string animation, int index) => Load($"Assets/ArtAssets/Characters/BlueShirtGuy/Animations/{animation}/BlueShirtGuy_{animation}_{index:00}.png");
    private static Sprite Load(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (!sprite) throw new Exception("Missing sprite: " + path);
        return sprite;
    }
    private static CharacterPoseHold[] Holds(Sprite[] sprites, int[] frames)
    {
        var holds = new CharacterPoseHold[sprites.Length];
        for (int i = 0; i < holds.Length; i++) holds[i] = new CharacterPoseHold { sprite = sprites[i], frames = frames[i] };
        return holds;
    }
}
