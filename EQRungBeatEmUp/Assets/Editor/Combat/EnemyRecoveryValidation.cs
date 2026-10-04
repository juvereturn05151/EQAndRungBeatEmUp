using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class EnemyRecoveryValidation
{
    private const string Pending = "BeatEmUp.EnemyRecoveryValidation";
    private const string Root = "Assets/EQ_Rung_BeatEmUp";
    private static readonly List<string> results = new List<string>();
    private static GameObject playerObject, enemyObject;
    private static ComboController player;
    private static EnemyHitReaction enemy;
    private static EnemyCombat ai;
    private static CombatClock clock;
    static EnemyRecoveryValidation() { EditorApplication.update += Poll; }

    [MenuItem("Beat Em Up/Validate enemy recovery (Play Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    private static void Check(bool pass, string label)
    {
        if (!pass) throw new InvalidOperationException(label);
        results.Add("PASS: " + label);
    }
    private static void Poll()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false); results.Clear();
        try
        {
            foreach (var actor in UnityEngine.Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) actor.gameObject.SetActive(false);
            foreach (var oldClock in UnityEngine.Object.FindObjectsByType<CombatClock>(FindObjectsSortMode.None)) oldClock.enabled = false;
            Assets(); NaturalFall(); FullCombo(1); FullCombo(-1); HitstopAndGroundedDamage(); DeathOverrides(); IndependentEnemies(); SlowRender(); OrdinaryGroundHit();
            Directory.CreateDirectory("Documentation");
            File.WriteAllLines("Documentation/EnemyRecoveryValidationResults.txt", results);
            Debug.Log("ENEMY RECOVERY VALIDATION PASSED: " + results.Count + " checks");
            if (Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.ExitPlaymode();
        }
        catch (Exception e)
        {
            Directory.CreateDirectory("Documentation"); results.Add("FAIL: " + e);
            File.WriteAllLines("Documentation/EnemyRecoveryValidationResults.txt", results);
            Debug.LogException(e);
            if (Application.isBatchMode) EditorApplication.Exit(1); else EditorApplication.ExitPlaymode();
        }
    }
    private static void Fixture(bool activeAI = false)
    {
        if (playerObject) UnityEngine.Object.DestroyImmediate(playerObject);
        if (enemyObject) UnityEngine.Object.DestroyImmediate(enemyObject);
        playerObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/BlueShirtGuy.prefab"));
        enemyObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/BadGuy.prefab"));
        player = playerObject.GetComponent<ComboController>(); enemy = enemyObject.GetComponent<EnemyHitReaction>(); ai = enemyObject.GetComponent<EnemyCombat>();
        playerObject.transform.position = Vector3.zero; enemyObject.transform.position = new Vector3(.85f, 0, 0);
        player.motor.Face(1); player.motor.MoveInput = Vector2.zero;
        player.health.Restore(); enemy.health.Restore(); ai.passiveTrainingDummy = !activeAI; ai.target = playerObject.transform;
        clock = UnityEngine.Object.FindFirstObjectByType<CombatClock>(); clock.enabled = false; clock.combatFPS = 60;
        foreach (var animator in enemyObject.GetComponentsInChildren<Animator>()) { animator.Rebind(); animator.Update(0); }
        Physics2D.SyncTransforms();
    }
    private static void Step(int frames = 1) { for (int i = 0; i < frames; i++) { Physics2D.SyncTransforms(); clock.StepFrame(); } }
    private static void Until(Func<bool> ready, string label, int max = 300)
    {
        for (int i = 0; !ready() && i < max; i++) Step();
        Check(ready(), label);
    }
    private static void Launch()
    {
        Check(enemy.GetComponentInChildren<CombatHurtbox>().Receive(player.launcher.frames[player.launcher.FirstActiveFrame].hitboxes[0], 1), "Existing launcher damage path accepts grounded enemy");
        Check(enemy.State == EnemyReaction.Launched && !enemy.CanAct, "Launch enters existing Launched state and blocks AI");
    }
    private static void Land()
    {
        Until(() => enemy.motor.IsGrounded, "Existing motor reaches floor after launch");
        Check(enemy.State == EnemyReaction.Knockdown && enemy.PhaseFramesRemaining == enemy.KnockdownFrames, "Landed event immediately enters full Knockdown phase");
        Check(enemy.motor.Height == 0 && enemy.motor.VerticalVelocity == 0 && enemy.motor.visual.localPosition.y == 0, "Floor contact clamps height, velocity and visual to ground");
    }
    private static void Assets()
    {
        Fixture();
        Check(enemy.knockdownClip && enemy.getUpClip && enemy.airborneSprite && enemy.downedSprite, "Prefab has all existing recovery references");
        Check(enemy.knockdownRecoveryDelayFrames == 45 && enemy.GetUpFrames == 48, "Configured downed delay is 45 frames and existing GetUp is 48 frames");
        Check(!enemy.knockdownClip.isLooping && !enemy.getUpClip.isLooping, "Existing recovery clips do not loop");
        var controller = (AnimatorController)enemy.animationDriver.animator.runtimeAnimatorController;
        foreach (string name in new[] { "Idle", "Walk", "GroundHit", "Launched", "AirHit", "Falling", "Landing", "Defeated", "Knockdown", "Downed", "GetUp" })
            Check(controller.layers[0].stateMachine.states.Any(s => s.state.name == name), "Existing controller contains " + name);
        foreach (var clip in new[] { enemy.knockdownClip, enemy.getUpClip })
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                    Check(key.value && AssetDatabase.GetAssetPath(key.value).StartsWith("Assets/ArtAssets/Characters/NPCs/ThaiBadBoy/Animations/"), clip.name + " references only existing Thai bad guy sprites");
        Check(AssetDatabase.GetAssetPath(enemy.airborneSprite).EndsWith("ThaiBadBoy_Knockdown_03.png") && AssetDatabase.GetAssetPath(enemy.downedSprite).EndsWith("ThaiBadBoy_Knockdown_06.png"), "Airborne and downed poses reuse existing Knockdown sprites");
    }
    private static void NaturalFall()
    {
        Fixture(true); Launch();
        Until(() => enemy.motor.VerticalVelocity < 0, "Launched enemy begins falling naturally");
        Check(!enemy.motor.IsGrounded && enemy.State != EnemyReaction.Normal && !enemy.CanAct && !ai.attackPlayer.CurrentAttack, "Negative velocity never returns enemy to Idle/AI");
        Check(enemy.motor.sprite.sprite == enemy.airborneSprite && !enemy.animationDriver.animator.enabled, "Airborne holds the existing airborne Knockdown pose");
        Land();
        var position = enemyObject.transform.position;
        Step(enemy.KnockdownFrames - 1);
        Check(enemy.State == EnemyReaction.Knockdown && !enemy.CanAct && !ai.attackPlayer.CurrentAttack && enemyObject.transform.position == position, "Full Knockdown duration blocks movement and attacks");
        Step();
        Check(enemy.State == EnemyReaction.Downed && enemy.PhaseFramesRemaining == 45 && enemy.motor.sprite.sprite == enemy.downedSprite, "Knockdown completes into exact final lying pose with 45-frame hold");
        Step(44);
        Check(enemy.State == EnemyReaction.Downed && enemy.PhaseFramesRemaining == 1 && enemy.motor.sprite.sprite == enemy.downedSprite && enemyObject.transform.position == position && !ai.attackPlayer.CurrentAttack, "Downed holds pose/ground/position through frame 44");
        Step();
        Check(enemy.State == EnemyReaction.GetUp && enemy.PhaseFramesRemaining == 48 && !enemy.CanAct, "Frame 45 starts existing GetUp");
        var getUpKeys = AnimationUtility.GetObjectReferenceCurve(enemy.getUpClip, AnimationUtility.GetObjectReferenceCurveBindings(enemy.getUpClip)[0]);
        Step(20);
        Check(enemy.motor.sprite.sprite == getUpKeys[2].value && enemy.State == EnemyReaction.GetUp && !ai.attackPlayer.CurrentAttack && enemyObject.transform.position == position, "GetUp plays kneeling pose at authored timing without early AI");
        Step(27);
        Check(enemy.State == EnemyReaction.GetUp && !enemy.CanAct, "GetUp retains its final recovery frame");
        Step();
        Check(enemy.State == EnemyReaction.Normal && enemy.CanAct && enemy.animationDriver.animator.enabled && enemy.animationDriver.animator.speed == 1, "Completed GetUp restores Normal and normal Animator playback");
        Step();
        Check(ai.attackPlayer.CurrentAttack == ai.attack, "Active AI resumes its existing attack after recovery");
        Debug.Log("RECOVERY TIMING: Knockdown=" + enemy.KnockdownFrames + " Downed=45 GetUp=" + enemy.GetUpFrames);
    }
    private static void FullCombo(int facing)
    {
        Fixture(); player.motor.Face(facing); enemyObject.transform.position = new Vector3(.85f * facing, 0, 0);
        player.RequestAttack(); Until(() => player.attackPlayer.CurrentFrame == 7, "Punch1 cancel timing"); player.RequestAttack();
        Until(() => player.attackPlayer.CurrentFrame == 7, "Punch2 cancel timing"); player.RequestLauncher();
        Until(() => !enemy.motor.IsGrounded, "Full combo launcher hits"); player.RequestJump();
        Until(() => !player.motor.IsGrounded, "Manual launcher jump cancel succeeds"); player.RequestAttack();
        float hp = enemy.health.Current; Until(() => enemy.health.Current < hp, "AirPunch1 connects");
        Until(() => !player.attackPlayer.IsFrozen && player.attackPlayer.CurrentFrame >= 5, "AirPunch1 cancel window"); player.RequestAttack();
        hp = enemy.health.Current; Until(() => enemy.health.Current < hp, "AirPunch2 connects");
        Until(() => !player.attackPlayer.IsFrozen && player.attackPlayer.CurrentFrame >= 5, "AirPunch2 cancel window"); player.RequestAttack();
        hp = enemy.health.Current; Until(() => enemy.health.Current < hp, "AirPunch3 connects");
        Check(enemy.GroundBounceEligible && enemy.motor.VerticalVelocity <= -8, "AirPunch3 arms ground bounce and drives downward slam");
        Until(() => enemy.GroundBouncesUsed == 1, "Full combo consumes one ground bounce at floor contact");
        Check(!enemy.motor.IsGrounded && enemy.motor.VerticalVelocity > 0, "Ground bounce adds a short airborne reaction");
        Until(() => enemy.motor.IsGrounded, "Full combo lands after bounce");
        Check(enemy.State == EnemyReaction.Knockdown && enemy.IsRecovering && !enemy.CanAct, "Bounce landing enters Knockdown and blocks AI during recovery");
        ai.passiveTrainingDummy = false; playerObject.transform.position = new Vector3(-3 * facing, 0, 0);
        Until(() => enemy.CanAct, "Full combo completes bounce landing recovery");
        Check(enemy.State == EnemyReaction.Normal && enemy.JuggleHits == 0 && enemy.GroundBouncesUsed == 0, "Neutral resets juggle and ground bounce budgets");
        Step(); Check(enemy.motor.MoveInput != Vector2.zero, "Recovered enemy resumes chase");
        enemy.health.Restore(); Launch(); Check(enemy.JuggleOpen, "Recovered enemy can be launched/juggled again");
    }
    private static void HitstopAndGroundedDamage()
    {
        Fixture(true); Launch(); Land(); Step(enemy.KnockdownFrames);
        int remaining = enemy.PhaseFramesRemaining; var position = enemyObject.transform.position; var pose = enemy.motor.sprite.sprite;
        ai.attackPlayer.Freeze(6); Step(6);
        Check(enemy.State == EnemyReaction.Downed && enemy.PhaseFramesRemaining == remaining && enemy.motor.sprite.sprite == pose, "Hitstop freezes downed timer and held pose");
        float hp = enemy.health.Current;
        var hit = player.groundCombo[0].frames[player.groundCombo[0].FirstActiveFrame].hitboxes[0];
        enemy.GetComponentInChildren<CombatHurtbox>().Receive(hit, 1);
        Check(enemy.health.Current == hp - hit.damage && enemy.State == EnemyReaction.Downed && enemy.PhaseFramesRemaining == remaining && enemyObject.transform.position == position, "Grounded damage cannot wake, shove or restart a downed enemy");
        Step(45); Check(enemy.State == EnemyReaction.GetUp, "Downed timer continues after hitstop");
        remaining = enemy.PhaseFramesRemaining; ai.attackPlayer.Freeze(4); Step(4);
        Check(enemy.PhaseFramesRemaining == remaining && !ai.attackPlayer.CurrentAttack, "Hitstop also freezes GetUp progression");
        enemy.GetComponentInChildren<CombatHurtbox>().Receive(hit, 1);
        Check(enemy.State == EnemyReaction.GetUp && enemy.PhaseFramesRemaining == remaining, "Grounded hit cannot cancel GetUp into normal AI");
        Fixture(); enemy.knockdownRecoveryDelayFrames = 0; Launch(); Land(); Step(enemy.KnockdownFrames);
        Check(enemy.State == EnemyReaction.GetUp, "Configurable zero downed delay transitions directly to GetUp");
        Fixture(); enemy.knockdownRecoveryDelayFrames = 12; Launch(); Land(); Step(enemy.KnockdownFrames); Step(11);
        Check(enemy.State == EnemyReaction.Downed && enemy.PhaseFramesRemaining == 1, "Custom downed delay uses each enemy's configuration");
        Step(); Check(enemy.State == EnemyReaction.GetUp, "Custom delay completes at frame 12");
    }
    private static void DeathOverrides()
    {
        foreach (var state in new[] { EnemyReaction.Launched, EnemyReaction.Knockdown, EnemyReaction.Downed, EnemyReaction.GetUp })
        {
            Fixture(true); Launch();
            if (state != EnemyReaction.Launched) Land();
            if (state == EnemyReaction.Downed || state == EnemyReaction.GetUp) Step(enemy.KnockdownFrames);
            if (state == EnemyReaction.GetUp) Step(45);
            Check(enemy.State == state, "Death test reaches " + state);
            ai.attackPlayer.Freeze(5); enemy.health.Damage(enemy.health.Current);
            Check(enemy.State == EnemyReaction.Defeated && !enemy.CanAct && enemy.PhaseFramesRemaining == 0, "Direct damage immediately overrides " + state + " even during hitstop");
            Step(250);
            Check(enemy.State == EnemyReaction.Defeated && enemy.motor.IsGrounded && !ai.attackPlayer.CurrentAttack, "Dead enemy never stands up from " + state);
            enemy.health.Restore(); Check(enemy.State == EnemyReaction.Normal && enemy.CanAct, "Explicit training respawn clears stale recovery/death state");
        }
        Fixture(); Launch(); Land(); Step(enemy.KnockdownFrames);
        var lethal = new AttackHitboxData { damage = 9999, canHitGrounded = true, canHitAirborne = true };
        enemy.GetComponentInChildren<CombatHurtbox>().Receive(lethal, 1);
        Step(200); Check(enemy.State == EnemyReaction.Defeated, "Lethal normal hurtbox damage during Downed also prevents recovery");
    }
    private static void IndependentEnemies()
    {
        Fixture(); Launch(); Land(); Step(enemy.KnockdownFrames);
        var otherObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/BadGuy.prefab"));
        var other = otherObject.GetComponent<EnemyHitReaction>(); otherObject.transform.position = new Vector3(4, 1, 0);
        other.knockdownRecoveryDelayFrames = 70;
        other.GetComponentInChildren<CombatHurtbox>().Receive(player.launcher.frames[player.launcher.FirstActiveFrame].hitboxes[0], 1);
        var thirdObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/BadGuy.prefab"));
        var third = thirdObject.GetComponent<EnemyHitReaction>(); thirdObject.transform.position = new Vector3(-4, 1, 0);
        third.knockdownRecoveryDelayFrames = 20;
        third.GetComponentInChildren<CombatHurtbox>().Receive(player.launcher.frames[player.launcher.FirstActiveFrame].hitboxes[0], 1);
        Step(10);
        Check(enemy.State == EnemyReaction.Downed && enemy.PhaseFramesRemaining == 35 && !other.motor.IsGrounded, "Enemy A downed timer continues independently while B is airborne");
        enemy.health.Damage(enemy.health.Current);
        Until(() => third.CanAct, "Simultaneously launched enemy C completes its own shorter recovery", 400);
        Check(other.IsRecovering && !other.CanAct && enemy.State == EnemyReaction.Defeated, "Enemy C's shorter delay never releases B early or resurrects A");
        Until(() => other.CanAct, "Enemy B completes its own longer recovery", 100);
        Check(enemy.State == EnemyReaction.Defeated && other.State == EnemyReaction.Normal && third.State == EnemyReaction.Normal, "Three enemies keep independent recovery/death state");
        UnityEngine.Object.DestroyImmediate(otherObject);
        UnityEngine.Object.DestroyImmediate(thirdObject);
    }
    private static void SlowRender()
    {
        Fixture(true); Launch(); Land();
        int total = enemy.KnockdownFrames + enemy.knockdownRecoveryDelayFrames + enemy.GetUpFrames;
        clock.Advance((total - 1 + .25f) * CombatClock.FrameSeconds);
        Check(enemy.State == EnemyReaction.GetUp && enemy.PhaseFramesRemaining == 1 && !ai.attackPlayer.CurrentAttack, "Slow rendered frame advances every recovery tick without early AI");
        clock.Advance(CombatClock.FrameSeconds);
        Check(enemy.State == EnemyReaction.Normal && enemy.CanAct, "Slow rendered clock finishes at the same recovery tick");
    }
    private static void OrdinaryGroundHit()
    {
        Fixture(); var hit = player.groundCombo[0].frames[player.groundCombo[0].FirstActiveFrame].hitboxes[0];
        enemy.GetComponentInChildren<CombatHurtbox>().Receive(hit, 1); Step(hit.hitstunFrames);
        Check(enemy.State == EnemyReaction.Normal && !enemy.IsRecovering, "Ordinary ground hit preserves existing stagger recovery without knockdown");
    }
}
