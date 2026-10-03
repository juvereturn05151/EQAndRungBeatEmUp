using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AirPunchPlaytestValidation
{
    private const string Pending = "BeatEmUp.AirPunchPlaytestValidation";
    private const string Output = "Assets/EQ_Rung_BeatEmUp";
    private static readonly List<string> results = new List<string>();
    private static GameObject playerObject, enemyObject;
    private static ComboController player;
    private static EnemyHitReaction enemy;
    private static CombatClock clock;
    private static AttackData[] air;

    static AirPunchPlaytestValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Validate air punch playtest (Play Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException(label);
        results.Add("PASS: " + label);
    }
    private static void Poll()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false); results.Clear(); bool passed = false;
        try
        {
            foreach (var actor in UnityEngine.Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) actor.gameObject.SetActive(false);
            foreach (var existing in UnityEngine.Object.FindObjectsByType<CombatClock>(FindObjectsSortMode.None)) existing.enabled = false;
            air = Enumerable.Range(1, 3).Select(i => AssetDatabase.LoadAssetAtPath<AttackData>(Output + "/Attacks/AirPunch" + i + ".asset")).ToArray();
            Check(air.All(a => a), "All three existing air assets load");
            Data(); RecoveryAndLanding(); Cancels(); FullRoute(1, false); FullRoute(-1, false); FullRoute(1, true); Impacts(); Authoring();
            passed = true;
        }
        catch (Exception exception) { results.Add("FAIL: " + exception); Debug.LogException(exception); }
        finally
        {
            DestroyFixture(); Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/AirPunchPlaytestValidationResults.txt", results);
            Debug.Log("AIR PUNCH PLAYTEST VALIDATION " + (passed ? "PASSED" : "FAILED") + ": " + results.Count + " assertions");
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1); else EditorApplication.ExitPlaymode();
        }
    }
    private static void DestroyFixture()
    {
        if (playerObject) UnityEngine.Object.DestroyImmediate(playerObject);
        if (enemyObject) UnityEngine.Object.DestroyImmediate(enemyObject);
    }
    private static void Fixture(bool contact = false, int facing = 1)
    {
        DestroyFixture();
        playerObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Output + "/Prefabs/BlueShirtGuy.prefab"));
        enemyObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Output + "/Prefabs/BadGuy.prefab"));
        player = playerObject.GetComponent<ComboController>(); enemy = enemyObject.GetComponent<EnemyHitReaction>();
        enemyObject.GetComponent<EnemyCombat>().enabled = false;
        playerObject.transform.position = Vector3.zero; enemyObject.transform.position = new Vector3(facing * (contact ? .85f : 4), 0, 0);
        player.motor.Face(facing); player.motor.MoveInput = Vector2.zero; enemy.motor.MoveInput = Vector2.zero;
        enemy.health.maximumHealth = 500; enemy.health.Restore(); player.health.Restore();
        clock = UnityEngine.Object.FindFirstObjectByType<CombatClock>(); clock.enabled = false; clock.combatFPS = 60;
        Check(player.airCombo.SequenceEqual(air), "Real player prefab references the authored air combo");
    }
    private static void Step(int frames) { for (int i = 0; i < frames; i++) clock.StepFrame(); }
    private static void Until(Func<bool> condition, string label, bool slow = false)
    {
        for (int i = 0; !condition() && i < 120; i++)
            if (slow) clock.Advance(2f / 60); else Step(1);
        Check(condition(), label);
    }
    private static void Data()
    {
        int[] total = { 13, 14, 20 }, first = { 3, 3, 5 }, last = { 5, 5, 8 }, stop = { 3, 4, 6 }, stun = { 17, 19, 24 };
        float[] damage = { 7, 8, 13 };
        for (int p = 0; p < 3; p++)
        {
            var data = air[p];
            Check(data.TotalFrames == total[p] && data.FirstActiveFrame == first[p] && data.LastActiveFrame == last[p] && data.domain == AttackDomain.Air, data.name + " total, startup, active and recovery match the requested air timing");
            for (int i = 0; i < total[p]; i++)
            {
                var frame = data.frames[i]; bool active = i >= first[p] && i <= last[p];
                Check(frame.sprite && frame.hitboxes.Count == (active ? 1 : 0), data.name + " frame " + i + " sprite and active-only hitbox");
                Check(frame.canCancelIntoAttack == (p < 2 && i >= 5 && i <= (p == 0 ? 10 : 11)) && !frame.canCancelIntoLauncher && !frame.canCancelIntoJump, data.name + " frame " + i + " cancel permissions");
                Check(frame.gravityScale == (p < 2 ? .85f : 1) && frame.movementInputScale == .3f && !frame.suspendFalling && !frame.setHorizontalVelocity && !frame.setVerticalVelocity, data.name + " frame " + i + " preserves momentum and permits gravity");
                if (active)
                {
                    var hit = frame.hitboxes[0];
                    Check(hit.damage == damage[p] && hit.hitstopFrames == stop[p] && hit.hitstunFrames == stun[p] && hit.hitId == 0 && hit.repeatAfterFrames == 0 && hit.canHitAirborne && !hit.canHitGrounded && hit.hitType == (p == 2 ? HitType.AirFinisher : HitType.Normal) && hit.launchVelocity.y == (p == 2 ? -8 : 0), data.name + " frame " + i + " airborne impact and finisher settings");
                }
            }
        }
        Check(air[2].frames[5].hitboxes[0].size.y > air[1].frames[3].hitboxes[0].size.y, "Finisher uses a taller downward hitbox");
    }
    private static void RecoveryAndLanding()
    {
        for (int p = 0; p < 3; p++)
        {
            Fixture(); player.RequestJump(); player.attackPlayer.Play(air[p]); Step(air[p].TotalFrames - 1);
            Check(player.CurrentAttack == air[p] && player.attackPlayer.CurrentFrame == air[p].TotalFrames - 1 && !player.motor.IsGrounded, air[p].name + " whiff retains every recovery frame"); Step(1);
            Check(!player.CurrentAttack && player.State == CombatState.Jumping, air[p].name + " whiff finishes without automatic next attack");
            Until(() => player.motor.IsGrounded, air[p].name + " gravity eventually lands the player");
            Check(!player.CurrentAttack && player.State == CombatState.Idle, air[p].name + " landing resets the air state");
        }
        Fixture(); player.RequestJump(); player.RequestAttack(); Step(6); player.motor.SetVerticalVelocity(-20);
        Until(() => player.motor.IsGrounded, "Landing during recovery occurs safely");
        Check(!player.CurrentAttack && player.ComboIndex == 0 && player.State == CombatState.Idle && !player.motor.MovementLocked, "Landing clears attack override, route and movement lock");
        player.RequestAttack(); Check(player.CurrentAttack == player.groundCombo[0], "Attack after landing selects ground Punch1");
        Fixture(); player.motor.MoveInput = Vector2.right; player.RequestJump(); float x = playerObject.transform.position.x; float velocity = player.motor.VerticalVelocity; player.RequestAttack(); Step(1);
        Check(playerObject.transform.position.x > x && player.motor.VerticalVelocity < velocity && player.motor.VerticalVelocity > 0, "Forward jump retains horizontal motion while air attack gravity reduces upward velocity");
        player.motor.SetVerticalVelocity(-1); Step(1);
        Check(player.motor.VerticalVelocity < -1 && !player.motor.SuspendFalling, "Descending AirPunch1 continues falling instead of hovering");
    }
    private static void Cancels()
    {
        Fixture(); player.RequestJump(); player.RequestAttack(); Step(2); player.RequestAttack(); Step(2);
        Check(player.CurrentAttack == air[0] && player.BufferedInput == CombatInput.Attack, "Early air input uses the existing six-frame buffer"); Step(1);
        Check(player.CurrentAttack == air[1] && player.attackPlayer.CurrentFrame == 0, "AirPunch1 cancels directly into AirPunch2 on final active frame 5");
        Step(5); player.RequestAttack(); Check(player.CurrentAttack == air[2], "AirPunch2 cancels directly into AirPunch3 on frame 5");
        Step(20); player.RequestAttack(); Step(2);
        Check(!player.CurrentAttack && !player.motor.IsGrounded, "Finisher cannot restart AirPunch1 during the same jump");
        foreach (int p in new[] { 0, 1 })
        {
            Fixture(); player.RequestJump(); player.RequestAttack(); if (p == 1) { Step(5); player.RequestAttack(); }
            Step(p == 0 ? 10 : 11); player.RequestAttack();
            Check(player.CurrentAttack == air[p + 1], air[p].name + " cancel includes its final legal frame");
        }
    }
    private static void FullRoute(int facing, bool slow)
    {
        Fixture(true, facing); float initial = enemy.health.Current;
        player.RequestAttack(); Step(3); player.RequestAttack();
        Until(() => player.CurrentAttack == player.groundCombo[1], "Full route: Punch1 -> Punch2", slow);
        Step(2); player.RequestLauncher();
        Until(() => player.CurrentAttack == player.launcher, "Full route: Punch2 -> Launcher", slow);
        player.RequestJump();
        Until(() => !enemy.motor.IsGrounded, "Full route: launcher connects and makes enemy airborne", slow);
        Until(() => !player.motor.IsGrounded, "Full route: buffered manual Jump uses launcher jump cancel", slow);
        player.RequestAttack();
        Check(player.CurrentAttack == air[0], "Full route: Jump -> AirPunch1");
        float expected = initial - player.groundCombo[0].frames[4].hitboxes[0].damage - player.groundCombo[1].frames[4].hitboxes[0].damage - player.launcher.frames[player.launcher.FirstActiveFrame].hitboxes[0].damage;
        Until(() => enemy.health.Current < expected, "Full route: AirPunch1 hits the airborne opponent", slow); expected -= 7;
        Check(enemy.State == EnemyReaction.AirHit && enemy.JuggleHits == 1, "AirPunch1 maintains existing AirHit/juggle state");
        player.RequestAttack(); Until(() => player.CurrentAttack == air[1], "Full route: AirPunch1 -> AirPunch2", slow);
        Until(() => enemy.health.Current < expected, "Full route: AirPunch2 stays in range and connects", slow); expected -= 8;
        Check(enemy.State == EnemyReaction.AirHit && enemy.JuggleHits == 2, "AirPunch2 maintains the juggle without forcing a fall");
        player.RequestAttack(); Until(() => player.CurrentAttack == air[2], "Full route: AirPunch2 -> AirPunch3", slow);
        Until(() => enemy.health.Current < expected, "Full route: AirPunch3 connects", slow); expected -= 13;
        Check(enemy.health.Current == expected && enemy.State == EnemyReaction.Falling && !enemy.JuggleOpen && enemy.motor.VerticalVelocity <= -8 && player.attackPlayer.HitstopRemaining > 0, "Finisher deals 13, applies six-frame impact and drives enemy downward");
        Step(120);
        Check(player.motor.IsGrounded && !player.CurrentAttack && player.State == CombatState.Idle && enemy.motor.IsGrounded && enemy.CanAct, "Full route lands and recovers both actors (facing " + facing + ", slow rendering " + slow + ")");
    }
    private static void Impacts()
    {
        for (int p = 0; p < 3; p++)
        {
            Fixture(true); player.motor.Launch(4, 0); enemy.motor.Launch(4, 0); player.motor.Simulate(.1f); enemy.motor.Simulate(.1f);
            var data = air[p]; var hit = data.frames[data.FirstActiveFrame].hitboxes[0]; player.attackPlayer.Play(data);
            Step(data.FirstActiveFrame - 1); Check(enemy.health.Current == 500, data.name + " startup has no hitbox"); Step(1);
            Check(enemy.health.Current == 500 - hit.damage && enemy.RecoveryFrames == hit.hitstunFrames && player.attackPlayer.HitstopRemaining == hit.hitstopFrames, data.name + " applies correct first-active damage, hitstun and hitstop");
            float height = enemy.motor.Height; int frame = player.attackPlayer.CurrentFrame; Step(hit.hitstopFrames);
            Check(enemy.motor.Height == height && player.attackPlayer.CurrentFrame == frame && enemy.RecoveryFrames == hit.hitstunFrames, data.name + " hitstop preserves airborne height, timeline and hitstun");
            Step(40); Check(enemy.health.Current == 500 - hit.damage, data.name + " shared Hit ID prevents duplicate damage");
        }
        Fixture(); enemy.motor.Launch(1, 0); var finisher = AttackFrameAuthoring.CloneBox(air[2].frames[5].hitboxes[0]); finisher.launchVelocity.y = -11;
        enemy.Receive(finisher, 1); Check(enemy.motor.VerticalVelocity == -11, "Editing finisher Launch Velocity Y directly changes downward enemy speed");
        finisher.launchVelocity.y = -6; enemy.Receive(finisher, 1);
        Check(enemy.motor.VerticalVelocity == -6, "Finisher downward velocity still applies after juggle is already closed");
    }
    private static void Authoring()
    {
        foreach (var data in air)
        {
            string original = JsonUtility.ToJson(data); var copy = UnityEngine.Object.Instantiate(data);
            try
            {
                AttackFrameAuthoring.Add(copy); AttackFrameAuthoring.SetSprite(copy,0,0,data.frames[data.FirstActiveFrame].sprite);
                AttackFrameAuthoring.SetHitboxes(copy,0,0,true,new AttackHitboxData { damage=30, hitstopFrames=7, hitstunFrames=25, launchVelocity=new Vector2(0,-10) });
                AttackFrameAuthoring.SetMovement(copy,0,0,new Vector2(.03f,0)); AttackFrameAuthoring.SetCancels(copy,0,0,true,false,false);
                AttackFrameAuthoring.Range(copy,0,0,"Test air physics editing", f => { f.setVerticalVelocity=true; f.verticalVelocity=-2; f.gravityScale=.8f; });
                Check(copy.TotalFrames == data.TotalFrames+1 && copy.frames[0].hitboxes[0].launchVelocity.y == -10 && copy.frames[0].verticalVelocity == -2 && copy.frames[0].gravityScale == .8f && copy.frames[0].canCancelIntoAttack && JsonUtility.ToJson(data)==original, data.name + " remains editable with existing frame, sprite, hitbox, damage, movement, velocity, gravity and cancel tools");
                AttackDataEditorWindow.Open(data); var window = EditorWindow.GetWindow<AttackDataEditorWindow>(); Check(window.CurrentAttack == data, "Attack Data Editor opens " + data.name); window.Close();
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
    }
}
