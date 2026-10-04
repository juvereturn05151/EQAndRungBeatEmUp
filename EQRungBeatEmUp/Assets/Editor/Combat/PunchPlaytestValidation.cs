using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

// Tests the authored assets and existing runtime without rebuilding the demo.
[InitializeOnLoad]
public static class PunchPlaytestValidation
{
    private const string Pending = "BeatEmUp.PunchPlaytestValidation";
    private const string Output = "Assets/EQ_Rung_BeatEmUp";
    private static readonly List<string> results = new List<string>();
    private static AttackData[] punches;
    private static AttackData launcher;
    private static CombatClock clock;
    private static GameObject playerObject, enemyObject;
    private static ComboController player;
    private static EnemyHitReaction enemy;

    static PunchPlaytestValidation() { EditorApplication.update += Poll; }

    [MenuItem("Beat Em Up/Validate ground punch playtest (Play Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        results.Add("PASS: " + label);
    }

    private static void Poll()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false);
        results.Clear();
        bool passed = false;
        try
        {
            // Play Mode changes disappear on exit; the user's scene and assets are not saved.
            foreach (var actor in UnityEngine.Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) actor.gameObject.SetActive(false);
            foreach (var existing in UnityEngine.Object.FindObjectsByType<CombatClock>(FindObjectsSortMode.None)) existing.enabled = false;
            clock = UnityEngine.Object.FindFirstObjectByType<CombatClock>();
            if (!clock) clock = new GameObject("Punch validation clock").AddComponent<CombatClock>();
            clock.enabled = false;
            clock.combatFPS = 60;
            punches = Enumerable.Range(1, 3).Select(i => AssetDatabase.LoadAssetAtPath<AttackData>(Output + "/Attacks/Punch" + i + ".asset")).ToArray();
            launcher = AssetDatabase.LoadAssetAtPath<AttackData>(Output + "/Attacks/Launcher.asset");
            Check(punches.All(p => p) && launcher, "Existing Punch1, Punch2, Punch3 and Launcher assets load");
            ValidateData();
            ValidateRecovery();
            ValidateRoutes();
            ValidateImpacts();
            ValidateAuthoring();
            passed = true;
        }
        catch (Exception exception) { results.Add("FAIL: " + exception); Debug.LogException(exception); }
        finally
        {
            DestroyFixture();
            Directory.CreateDirectory("Documentation");
            File.WriteAllLines("Documentation/PunchPlaytestValidationResults.txt", results);
            Debug.Log("GROUND PUNCH PLAYTEST VALIDATION " + (passed ? "PASSED" : "FAILED") + ": " + results.Count + " assertions");
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }
    }

    private static void ValidateData()
    {
        int[] totals = { 26, 27, 38 }, first = { 7, 7, 11 }, last = { 12, 12, 18 };
        float[] damage = { 8, 9, 14 }, push = { 1, 1.4f, 5 }, movement = { .08f, .12f, .24f };
        int[] stun = { 18, 20, 24 }, stop = { 3, 4, 6 };
        for (int p = 0; p < 3; p++)
        {
            var attack = punches[p];
            Check(attack.attackName == "Punch" + (p + 1) && attack.domain == AttackDomain.Ground && !attack.isLauncher, attack.name + " uses the existing normal ground attack schema");
            Check(attack.TotalFrames == totals[p] && attack.FirstActiveFrame == first[p] && attack.LastActiveFrame == last[p], attack.name + " has the requested startup, active and complete recovery frames");
            Check(attack.frames.All(f => f.sprite), attack.name + " assigns existing artwork on every combat frame");
            for (int i = 0; i < attack.TotalFrames; i++)
            {
                var frame = attack.frames[i];
                bool active = i >= first[p] && i <= last[p];
                Check(frame.hitboxes.Count == (active ? 1 : 0), attack.name + " frame " + i + " hitbox timing");
                bool cancel = p < 2 && i >= 13 && i <= (p == 0 ? 20 : 22);
                Check(frame.canCancelIntoAttack == cancel && frame.canCancelIntoLauncher == (p == 1 && cancel) && !frame.canCancelIntoJump, attack.name + " frame " + i + " separate cancel permissions");
                if (active)
                {
                    var box = frame.hitboxes[0];
                    Check(box.damage == damage[p] && box.hitstunFrames == stun[p] && box.hitstopFrames == stop[p] && box.knockback == push[p] && box.launchVelocity == Vector2.zero && box.hitType == HitType.Normal && box.hitId == 0 && box.repeatAfterFrames == 0, attack.name + " frame " + i + " impact settings and shared hit history");
                }
            }
            Check(Mathf.Abs(attack.frames.Sum(f => f.movement.x) - movement[p]) < .0001f, attack.name + " has subtle authored forward movement");
        }
        Check(punches[0].frames[first[0]].hitboxes[0].size.x < punches[1].frames[first[1]].hitboxes[0].size.x && punches[1].frames[first[1]].hitboxes[0].size.x < punches[2].frames[first[2]].hitboxes[0].size.x, "Horizontal hitbox reach increases through the ground string");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Output + "/Prefabs/BlueShirtGuy.prefab");
        var combo = prefab.GetComponent<ComboController>();
        Check(combo.inputBufferFrames == 6 && combo.finisherRecoveryFrames == 0 && combo.groundCombo.SequenceEqual(punches) && combo.launcher == launcher, "Player prefab uses these assets, a six-frame buffer and authored recovery without extra finisher cooldown");
    }

    private static void DestroyFixture()
    {
        if (playerObject) UnityEngine.Object.DestroyImmediate(playerObject);
        if (enemyObject) UnityEngine.Object.DestroyImmediate(enemyObject);
    }

    private static void Fixture(bool contact = false, int facing = 1)
    {
        DestroyFixture();
        playerObject = Character("Punch validation player", out var motor, out var attack);
        player = playerObject.AddComponent<ComboController>();
        player.motor = motor; player.health = playerObject.AddComponent<CharacterHealth>();
        player.animationDriver = playerObject.GetComponent<CharacterAnimation>(); player.attackPlayer = attack;
        player.groundCombo = punches; player.launcher = launcher; player.inputBufferFrames = 6; player.finisherRecoveryFrames = 0;
        enemyObject = Character("Punch validation opponent", out var enemyMotor, out var enemyAttack);
        enemy = enemyObject.AddComponent<EnemyHitReaction>();
        enemy.motor = enemyMotor; enemy.health = enemyObject.AddComponent<CharacterHealth>(); enemy.health.maximumHealth = 500;
        enemy.animationDriver = enemyObject.GetComponent<CharacterAnimation>();
        var hurtbox = enemyObject.AddComponent<CombatHurtbox>(); hurtbox.motor = enemyMotor; hurtbox.health = enemy.health; hurtbox.enemy = enemy; hurtbox.team = 1;
        var collider = enemyObject.GetComponent<BoxCollider2D>(); collider.isTrigger = true; collider.offset = new Vector2(0, .55f); collider.size = new Vector2(1, 1.1f);
        enemyObject.transform.position = new Vector3(facing * (contact ? .85f : 4), 0, 0);
        playerObject.SetActive(true); enemyObject.SetActive(true); player.health.Restore(); enemy.health.Restore(); motor.Face(facing);
    }

    private static GameObject Character(string name, out CharacterMotor motor, out AttackPlayer attack)
    {
        var obj = new GameObject(name); obj.SetActive(false);
        attack = obj.AddComponent<AttackPlayer>(); motor = obj.GetComponent<CharacterMotor>();
        attack.motor = motor; attack.hitbox = obj.GetComponent<AttackHitbox>(); attack.hitbox.motor = motor;
        attack.animationDriver = obj.GetComponent<CharacterAnimation>(); motor.attackPlayer = attack;
        motor.sprite = obj.AddComponent<SpriteRenderer>();
        return obj;
    }

    private static void Step(int count) { for (int i = 0; i < count; i++) clock.StepFrame(); }

    private static void ValidateRecovery()
    {
        for (int i = 0; i < 3; i++)
        {
            Fixture(); player.attackPlayer.Play(punches[i]);
            Step(punches[i].TotalFrames - 1);
            Check(player.CurrentAttack == punches[i] && player.attackPlayer.CurrentFrame == punches[i].TotalFrames - 1 && player.motor.MovementLocked, punches[i].name + " whiff keeps its complete final recovery frame");
            Step(1);
            Check(!player.CurrentAttack && player.State == CombatState.Idle && !player.motor.MovementLocked && player.ComboIndex == 0, punches[i].name + " returns to neutral only after full recovery");
            player.RequestAttack();
            Check(player.CurrentAttack == punches[0], "Attack after " + punches[i].name + " recovery starts a fresh Punch1");
        }
        Fixture(); player.RequestAttack(); Step(21); player.RequestAttack();
        Check(player.CurrentAttack == punches[0], "Late Punch1 input cannot use a closed cancel window");
        Step(5);
        Check(!player.CurrentAttack && player.BufferedInput == CombatInput.None, "Late chain input expires at recovery completion without auto restarting Punch1");
        Fixture(); player.RequestAttack(); Step(13); player.RequestAttack(); Step(23); player.RequestAttack(); Step(4);
        Check(!player.CurrentAttack && player.BufferedInput == CombatInput.None, "Late Punch2 input cannot chain or auto restart after its cancel window closes");
    }

    private static void ValidateRoutes()
    {
        Fixture(); player.RequestAttack(); Step(13); player.RequestAttack();
        Check(player.CurrentAttack == punches[1] && player.attackPlayer.CurrentFrame == 0, "Punch1 cancels immediately into Punch2 on frame 13");
        Step(26); Check(player.CurrentAttack == punches[1], "Punch2 retains all recovery without continuation input"); Step(1);
        Check(!player.CurrentAttack && player.ComboIndex == 0, "Punch1 -> Punch2 -> neutral");
        Fixture(); player.RequestAttack(); Step(9); player.RequestAttack(); Step(3);
        Check(player.CurrentAttack == punches[0] && player.BufferedInput == CombatInput.Attack, "Early chain input waits in the existing buffer"); Step(1);
        Check(player.CurrentAttack == punches[1] && player.attackPlayer.CurrentFrame == 0, "Buffered Attack consumes at the first legal frame 13");
        Step(13); player.RequestAttack();
        Check(player.CurrentAttack == punches[2], "Punch2 cancels immediately into Punch3 on frame 13");
        Step(37); player.RequestAttack(); Check(player.CurrentAttack == punches[2], "Finisher cannot chain back into Punch1"); Step(1);
        Check(!player.CurrentAttack && player.ComboIndex == 0, "Punch1 -> Punch2 -> Punch3 -> neutral");
        Fixture(); player.RequestAttack(); Step(7); player.RequestAttack(); Step(6);
        Check(player.CurrentAttack == punches[1], "Six-frame buffer accepts input six logical frames before cancel");
        Fixture(); player.RequestAttack(); player.RequestAttack(); Step(13);
        Check(player.CurrentAttack == punches[0] && player.BufferedInput == CombatInput.None, "Input earlier than six frames before cancel expires");
        foreach (int frame in new[] { 13, 22 })
        {
            Fixture(); player.RequestAttack(); Step(13); player.RequestAttack(); Step(frame); player.RequestLauncher();
            Check(player.CurrentAttack == launcher, "Separate Launcher Cancel opens from Punch2 on frame " + frame);
        }
        Fixture(); player.RequestAttack(); Step(13); player.RequestAttack(); Step(23); player.RequestLauncher(); Step(4);
        Check(!player.CurrentAttack && player.BufferedInput == CombatInput.None, "Launcher cannot activate after Punch2's frame 22 cancel boundary");
        Fixture(); player.RequestAttack(); var visited = new List<AttackData> { player.CurrentAttack };
        for (int i = 0; i < 64; i++)
        {
            var previous = player.CurrentAttack; player.RequestAttack(); Step(1);
            if (player.CurrentAttack && player.CurrentAttack != previous) visited.Add(player.CurrentAttack);
        }
        Check(visited.SequenceEqual(punches) && !player.CurrentAttack, "Repeated Attack produces one intended string without restarting Punch1 during recovery");
        Fixture(); player.RequestAttack(); Step(20); player.RequestAttack();
        Check(player.CurrentAttack == punches[1], "Punch1 Attack Cancel includes its final frame 20");
        Step(22); player.RequestAttack();
        Check(player.CurrentAttack == punches[2], "Punch2 Attack Cancel includes its final frame 22");
        Fixture(); player.RequestAttack(); Step(13); player.RequestAttack(); Step(8); player.RequestLauncher(); Step(5);
        Check(player.CurrentAttack == launcher, "Early Launcher input buffers into Punch2's separate launcher window");
        Fixture();
        var air = ScriptableObject.CreateInstance<AttackData>(); air.domain = AttackDomain.Air;
        air.frames.Add(new AttackFrameData { sprite = punches[0].frames[0].sprite });
        air.frames.Add(new AttackFrameData { sprite = punches[0].frames[0].sprite });
        try
        {
            player.airCombo = new[] { air, air, air }; player.RequestJump(); player.RequestAttack(); Step(2);
            Check(!player.CurrentAttack && player.ComboIndex == 1, "Nonterminal air attack keeps the existing air route after completion");
            player.RequestAttack();
            Check(player.CurrentAttack == air && player.ComboIndex == 2, "Ground neutral reset preserves manual air-combo continuation");
        }
        finally { player.attackPlayer.Stop(); UnityEngine.Object.DestroyImmediate(air); }
    }

    private static void ValidateImpacts()
    {
        for (int i = 0; i < 3; i++)
        {
            foreach (int facing in new[] { 1, -1 })
            {
                Fixture(true, facing); var data = punches[i]; var hit = data.frames[data.FirstActiveFrame].hitboxes[0];
                player.attackPlayer.Play(data); Step(data.FirstActiveFrame - 1);
                Check(enemy.health.Current == 500, data.name + " startup cannot damage the opponent"); Step(1);
                Check(enemy.health.Current == 500 - hit.damage && enemy.RecoveryFrames == hit.hitstunFrames && enemy.State == (hit.wallBounce ? EnemyReaction.WallBounceEligible : EnemyReaction.GroundHit) && enemy.motor.IsGrounded, data.name + " applies damage and authored ground reaction on the first active frame");
                Check(player.attackPlayer.HitstopRemaining == hit.hitstopFrames && enemy.motor.attackPlayer.HitstopRemaining == hit.hitstopFrames, data.name + " freezes both actors for the authored hitstop");
                var position = enemyObject.transform.position; int frame = player.attackPlayer.CurrentFrame;
                Step(hit.hitstopFrames);
                Check(player.attackPlayer.CurrentFrame == frame && enemyObject.transform.position == position && enemy.RecoveryFrames == hit.hitstunFrames, data.name + " hitstop freezes timeline, motion and hitstun");
                Step(1);
                float push = (enemyObject.transform.position.x - position.x) * facing;
                Check(Mathf.Abs(push - hit.knockback / 60) < .0001f, data.name + " horizontal knockback mirrors facing and begins after hitstop");
                Step(60);
                Check(enemy.health.Current == 500 - hit.damage && enemy.motor.IsGrounded && enemy.CanAct, data.name + " hits once across its active frames and recovers without launching");
            }
        }
        Fixture(true); player.RequestAttack(); Step(7); player.RequestAttack(); Step(3);
        Check(player.BufferedInput == CombatInput.Attack && player.attackPlayer.CurrentFrame == 7, "Attack input remains buffered throughout hitstop"); Step(6);
        Check(player.CurrentAttack == punches[1], "Hitstop-buffered input consumes at Punch1 frame 13"); Step(7); Step(4); Step(6); player.RequestAttack(); Step(11); Step(6); Step(38);
        Check(enemy.health.Current == 469 && enemy.motor.IsGrounded, "Connected three-punch string deals 8 + 9 + 14 damage and never launches");
        Fixture(true); player.RequestAttack(); clock.Advance(.5f);
        Check(enemy.health.Current == 492 && !player.CurrentAttack, "A slow rendered frame still processes active frames, hitstop and recovery at logical 60 FPS");
    }

    private static void ValidateAuthoring()
    {
        var original = punches[0]; string before = JsonUtility.ToJson(original);
        var copy = UnityEngine.Object.Instantiate(original);
        try
        {
            AttackFrameAuthoring.SetSprite(copy, 0, 1, punches[2].frames[punches[2].FirstActiveFrame].sprite);
            AttackFrameAuthoring.SetHitboxes(copy, 0, 1, true, new AttackHitboxData { damage = 30, hitstopFrames = 7, hitstunFrames = 25 });
            AttackFrameAuthoring.SetMovement(copy, 0, 1, new Vector2(.02f, 0));
            AttackFrameAuthoring.SetCancels(copy, 0, 1, true, true, false);
            AttackFrameAuthoring.Add(copy);
            Check(copy.TotalFrames == original.TotalFrames + 1 && copy.frames[0].sprite == punches[2].frames[punches[2].FirstActiveFrame].sprite && copy.frames[0].movement.x == .02f && copy.frames[0].canCancelIntoAttack && copy.frames[0].canCancelIntoLauncher && copy.frames[0].hitboxes[0].damage == 30 && copy.frames[0].hitboxes[0].hitstopFrames == 7 && copy.frames[0].hitboxes[0].hitstunFrames == 25, "Existing editor authoring commands still edit sprites, hitboxes, frame count, motion, damage, hitstop, hitstun and cancel windows");
            Check(JsonUtility.ToJson(original) == before, "Authoring validation leaves the designer's real assets unchanged");
            AttackDataEditorWindow.Open(original);
            var window = EditorWindow.GetWindow<AttackDataEditorWindow>();
            Check(window.CurrentAttack == original, "Attack Data Editor opens the authored Punch1 asset");
            window.Close();
        }
        finally { UnityEngine.Object.DestroyImmediate(copy); }
    }
}
