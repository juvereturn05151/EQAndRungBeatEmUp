using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PunchAnimationValidation
{
    const string Root = "Assets/EQ_Rung_BeatEmUp/";
    const string Pending = "BeatEmUp.PunchAnimationValidation";
    static readonly List<string> results = new List<string>();
    static GameObject po, eo;
    static ComboController player;
    static EnemyHitReaction enemy;
    static CombatClock clock;
    static PunchAnimationValidation() => EditorApplication.update += Poll;
    static void Check(bool pass, string label)
    {
        if (!pass) throw new Exception(label);
        results.Add("PASS: " + label);
    }
    [MenuItem("Beat Em Up/Combat/Synchronize repaired punch clips")]
    public static void Synchronize()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        for (int n = 1; n <= 3; n++)
        {
            var attack = AssetDatabase.LoadAssetAtPath<AttackData>(Root + "Attacks/Punch" + n + ".asset");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/Punch " + n + ".anim");
            if (!attack || !clip || attack.frames.Any(f => !f.sprite)) throw new Exception("Missing punch asset or sprite " + n);
            var keys = new List<ObjectReferenceKeyframe>(); Sprite previous = null;
            for (int i = 0; i < attack.TotalFrames; i++)
                if (attack.frames[i].sprite != previous)
                {
                    previous = attack.frames[i].sprite;
                    keys.Add(new ObjectReferenceKeyframe { time = i / 60f, value = previous });
                }
            keys.Add(new ObjectReferenceKeyframe { time = attack.TotalFrames / 60f, value = previous });
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            EditorUtility.SetDirty(clip);
        }
        AssetDatabase.SaveAssets();
        CharacterSelectSetup.Build();
        File.WriteAllText("Documentation/PunchAnimationSetupResults.txt", "PASS: Repaired punch sprites imported, existing clips synchronized with combat timelines, multiplayer catalog refreshed.");
    }
    [MenuItem("Beat Em Up/Validate repaired punch animation (Play Mode)")]
    public static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists("Temp/PunchAnimation.setup-request"))
        {
            File.Delete("Temp/PunchAnimation.setup-request");
            try { Synchronize(); }
            catch (Exception e) { File.WriteAllText("Documentation/PunchAnimationSetupResults.txt", "FAIL: " + e); Debug.LogException(e); }
            return;
        }
        if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists("Temp/PunchAnimation.validate-request"))
        { File.Delete("Temp/PunchAnimation.validate-request"); Run(); return; }
        if (!EditorApplication.isPlaying || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false); results.Clear();
        try
        {
            foreach (var actor in Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) actor.gameObject.SetActive(false);
            clock = Object.FindFirstObjectByType<CombatClock>();
            if (!clock) clock = new GameObject("Punch validation clock").AddComponent<CombatClock>();
            clock.enabled = false;
            ArtAndClips();
            foreach (int facing in new[] { -1, 1 })
            {
                for (int n = 0; n < 3; n++) Playback(n, facing);
                Combo(facing);
            }
            Debug.Log("PUNCH ANIMATION VALIDATION PASSED: " + results.Count);
        }
        catch (Exception e) { results.Add("FAIL: " + e); Debug.LogException(e); }
        finally
        {
            Clear(); File.WriteAllLines("Documentation/PunchAnimationValidationResults.txt", results);
            EditorApplication.ExitPlaymode();
        }
    }
    static Texture2D Read(string path)
    {
        var texture = new Texture2D(2, 2);
        if (!texture.LoadImage(File.ReadAllBytes(path))) throw new Exception("Invalid sprite PNG " + path);
        return texture;
    }
    static void ArtAndClips()
    {
        var idle = Read(Root + "Sprites/BlueShirtGuy_Idle2_01.png");
        try
        {
            var idlePixels = idle.GetPixels32();
            for (int n = 1; n <= 3; n++)
            {
                for (int pose = 1; pose <= 12; pose++)
                {
                    string path = Root + "Sprites/BlueShirtGuy_Attack" + n + "_" + pose.ToString("00") + ".png";
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    Check(sprite && sprite.rect.size == new Vector2(128, 128) && sprite.pixelsPerUnit == 100 &&
                        sprite.pivot == new Vector2(64, 8) && importer && importer.filterMode == FilterMode.Point,
                        "Punch " + n + " pose " + pose + ": original canvas, pixel scale, pivot and point filtering");
                    var texture = Read(path);
                    try
                    {
                        var pixels = texture.GetPixels32();
                        Check(pixels.Take(20 * 128).SequenceEqual(idlePixels.Take(20 * 128)),
                            "Punch " + n + " pose " + pose + ": fixed Idle shoes and ground contact");
                        if (pose == 12 || n == 1 && pose == 1)
                            Check(pixels.SequenceEqual(idlePixels), "Punch " + n + " endpoint " + pose + ": exact Idle, no reset pop");
                    }
                    finally { Object.DestroyImmediate(texture); }
                }
                var attack = AssetDatabase.LoadAssetAtPath<AttackData>(Root + "Attacks/Punch" + n + ".asset");
                int total = n == 1 ? 26 : n == 2 ? 27 : 38, first = n < 3 ? 7 : 11, last = n < 3 ? 12 : 18;
                Check(attack.TotalFrames == total && attack.FirstActiveFrame == first && attack.LastActiveFrame == last,
                    "Punch " + n + ": original duration and hit window");
                var impact = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Sprites/BlueShirtGuy_Attack" + n + "_07.png");
                Check(attack.frames.Skip(first).Take(last - first + 1).All(f => f.sprite == impact),
                    "Punch " + n + ": strongest impact pose throughout original hit window");
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/Punch " + n + ".anim");
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"));
                Check(keys != null && Mathf.Abs(clip.length - total / 60f) < .001f && AnimationUtility.GetCurveBindings(clip).Length == 0,
                    "Punch " + n + ": clip length matches combat, no transform animation");
                for (int f = 0; f < total; f++)
                {
                    var expected = keys.Last(k => k.time <= f / 60f + .00001f).value as Sprite;
                    Check(expected == attack.frames[f].sprite, "Punch " + n + ": clip/combat pose agree at frame " + f);
                }
            }
        }
        finally { Object.DestroyImmediate(idle); }
    }
    static void Clear()
    {
        if (po) Object.DestroyImmediate(po);
        if (eo) Object.DestroyImmediate(eo);
    }
    static void Fixture(int facing)
    {
        Clear();
        po = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/BlueShirtGuy.prefab"));
        eo = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/BadGuy.prefab"));
        player = po.GetComponent<ComboController>(); enemy = eo.GetComponent<EnemyHitReaction>();
        po.GetComponent<PlayerCombatInput>().enabled = false; eo.GetComponent<EnemyCombat>().enabled = false;
        player.health.SafeStageProtection = enemy.health.SafeStageProtection = false;
        enemy.health.maximumHealth = 500; enemy.health.Restore();
        player.motor.arenaMin = enemy.motor.arenaMin = Vector2.one * -20;
        player.motor.arenaMax = enemy.motor.arenaMax = Vector2.one * 20;
        player.motor.ResetForStage(Vector2.zero); player.motor.Face(facing); enemy.motor.ResetForStage(new Vector2(8 * facing, 0));
    }
    static void Step(int count = 1) { for (int i = 0; i < count; i++) clock.StepFrame(); }
    static void Playback(int index, int facing)
    {
        Fixture(facing); var attack = player.groundCombo[index];
        Check(player.attackPlayer.Play(attack), "Punch " + (index + 1) + ": existing player starts attack, facing " + facing);
        bool grounded = true, correct = true;
        for (int i = 0; i < attack.TotalFrames; i++)
        {
            correct &= player.motor.sprite.sprite == player.attackPlayer.Frame.sprite && player.motor.sprite.flipX == (facing < 0);
            grounded &= player.motor.IsGrounded && player.motor.Height == 0;
            Step();
        }
        Check(correct && grounded && !player.CurrentAttack, "Punch " + (index + 1) + ": actual sprite playback, grounding and finish, facing " + facing);
    }
    static void Combo(int facing)
    {
        Fixture(facing); enemy.motor.ResetForStage(new Vector2(.8f * facing, 0));
        float expected = enemy.health.Current - player.groundCombo.Sum(a => a.frames[a.FirstActiveFrame].hitboxes[0].damage);
        player.RequestAttack(); Step(3); player.RequestAttack();
        Until(() => player.CurrentAttack == player.groundCombo[1]); Step(3); player.RequestAttack();
        Until(() => player.CurrentAttack == player.groundCombo[2]); Step(90);
        Check(enemy.health.Current == expected && !player.CurrentAttack && player.motor.IsGrounded,
            "Existing three-punch buffered combo preserves damage and recovery, facing " + facing);
    }
    static void Until(Func<bool> condition)
    {
        for (int i = 0; i < 180 && !condition(); i++) Step();
        if (!condition()) throw new Exception("Buffered punch route timed out");
    }
}
