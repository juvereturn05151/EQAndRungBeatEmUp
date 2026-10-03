using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HitBlinkValidation
{
    private const string Pending = "BeatEmUp.HitBlinkValidation";
    private static readonly List<string> results = new List<string>();
    private static IEnumerator tests;
    private static CustomYieldInstruction waiting;
    private static GameObject root;
    private static CharacterHealth health;
    private static ComboController combo;
    private static HitBlinkEffect blink;
    private static SpriteRenderer body, extra, initiallyHidden;
    private static CombatHurtbox hurtbox;
    private static Animator animator;
    private static CombatClock clock;
    private static float previousTimeScale;

    static HitBlinkValidation() { EditorApplication.update += Poll; }

    [MenuItem("Beat Em Up/Validate player hit blink (Play Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Poll()
    {
        if (tests == null)
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            SessionState.SetBool(Pending, false); results.Clear(); previousTimeScale = Time.timeScale;
            tests = Tests();
        }
        try
        {
            if (waiting != null && waiting.keepWaiting) return;
            waiting = null;
            if (!tests.MoveNext()) { Finish(true); return; }
            waiting = tests.Current as CustomYieldInstruction;
        }
        catch (Exception exception) { results.Add("FAIL: " + exception); Debug.LogException(exception); Finish(false); }
    }

    private static void Finish(bool passed)
    {
        Time.timeScale = previousTimeScale;
        if (root) UnityEngine.Object.DestroyImmediate(root);
        tests = null; waiting = null;
        Directory.CreateDirectory("Documentation");
        File.WriteAllLines("Documentation/HitBlinkValidationResults.txt", results);
        Debug.Log("HIT BLINK VALIDATION " + (passed ? "PASSED" : "FAILED") + ": " + results.Count + " assertions");
        if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1); else EditorApplication.ExitPlaymode();
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        results.Add("PASS: " + label);
    }

    private sealed class Until : CustomYieldInstruction
    {
        private readonly Func<bool> condition;
        private readonly float deadline;
        public Until(Func<bool> condition) { this.condition = condition; deadline = Time.realtimeSinceStartup + 2; }
        public override bool keepWaiting
        {
            get
            {
                if (condition()) return false;
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Blink transition was not observed");
                return true;
            }
        }
    }

    private static bool IsBlinking() => (bool)typeof(HitBlinkEffect).GetField("blinking", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(blink);
    private static AttackData Attack(bool invulnerable = false, bool armor = false)
    {
        var data = ScriptableObject.CreateInstance<AttackData>();
        var first = AssetDatabase.LoadAssetAtPath<AttackData>("Assets/EQ_Rung_BeatEmUp/Attacks/Punch1.asset");
        for (int i = 0; i < 30; i++) data.frames.Add(new AttackFrameData { sprite = first.frames[i % first.TotalFrames].sprite, invulnerable = invulnerable, superArmor = armor });
        return data;
    }

    private static void Step(int frames) { for (int i = 0; i < frames; i++) clock.StepFrame(); }

    private static IEnumerator Tests()
    {
        foreach (var motor in UnityEngine.Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) motor.gameObject.SetActive(false);
        foreach (var existing in UnityEngine.Object.FindObjectsByType<CombatClock>(FindObjectsSortMode.None)) existing.enabled = false;
        clock = UnityEngine.Object.FindFirstObjectByType<CombatClock>();
        if (!clock) clock = new GameObject("Blink validation clock").AddComponent<CombatClock>();
        clock.enabled = false;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Prefabs/BlueShirtGuy.prefab");
        var configured = new SerializedObject(prefab.GetComponent<HitBlinkEffect>());
        Check(configured.FindProperty("hitBlinkDuration").floatValue == .35f && configured.FindProperty("hitBlinkInterval").floatValue == .06f && configured.FindProperty("blinkOnHit").boolValue, "Player prefab exposes enabled blink with duration 0.35 and interval 0.06 seconds");
        var renderers = configured.FindProperty("blinkRenderers");
        Check(renderers.arraySize == 1 && renderers.GetArrayElementAtIndex(0).objectReferenceValue == prefab.GetComponent<CharacterMotor>().sprite, "Only the existing Visual/body renderer is configured to blink");

        root = new GameObject("Blink validation player"); root.SetActive(false);
        var attack = root.AddComponent<AttackPlayer>(); var motorPlayer = root.GetComponent<CharacterMotor>(); var driver = root.GetComponent<CharacterAnimation>();
        health = root.AddComponent<CharacterHealth>(); combo = root.AddComponent<ComboController>(); blink = root.AddComponent<HitBlinkEffect>();
        var visual = new GameObject("Visual"); visual.transform.SetParent(root.transform); body = visual.AddComponent<SpriteRenderer>(); animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = prefab.GetComponent<CharacterAnimation>().animator.runtimeAnimatorController;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; driver.animator = animator;
        motorPlayer.visual = visual.transform; motorPlayer.sprite = body; motorPlayer.attackPlayer = attack;
        attack.motor = motorPlayer; attack.hitbox = root.GetComponent<AttackHitbox>(); attack.hitbox.motor = motorPlayer; attack.animationDriver = driver;
        combo.motor = motorPlayer; combo.health = health; combo.animationDriver = driver; combo.attackPlayer = attack;
        hurtbox = visual.AddComponent<CombatHurtbox>(); hurtbox.motor = motorPlayer; hurtbox.health = health; hurtbox.player = combo;
        extra = new GameObject("Unrelated effect").AddComponent<SpriteRenderer>(); extra.transform.SetParent(root.transform);
        initiallyHidden = new GameObject("Optional hidden weapon").AddComponent<SpriteRenderer>(); initiallyHidden.transform.SetParent(root.transform); initiallyHidden.enabled = false;
        var setup = new SerializedObject(blink); var targets = setup.FindProperty("blinkRenderers"); targets.arraySize = 2;
        targets.GetArrayElementAtIndex(0).objectReferenceValue = body; targets.GetArrayElementAtIndex(1).objectReferenceValue = initiallyHidden; setup.ApplyModifiedPropertiesWithoutUndo();
        root.SetActive(true); health.Restore(); animator.Rebind(); animator.Update(0);
        var hit = new AttackHitboxData { damage = 10, hitstunFrames = 18, hitstopFrames = 5, knockback = 1 };
        var data = Attack(); attack.Play(data);
        Check(hurtbox.Receive(hit, -1), "Existing CombatHurtbox accepts a player hit during an attack"); animator.Update(0);
        Check(health.Current == 190 && !attack.CurrentAttack && combo.State == CombatState.Hitstun && animator.GetCurrentAnimatorStateInfo(0).IsName("GroundHit"), "Damage still interrupts AttackData and selects the existing GroundHit reaction");
        Check(body.enabled && IsBlinking(), "Accepted damage starts visible with one active blink timer");
        yield return new Until(() => !body.enabled);
        Check(root.activeInHierarchy && combo.enabled && motorPlayer.enabled && hurtbox.enabled && hurtbox.GetComponent<BoxCollider2D>().enabled && extra.enabled && !initiallyHidden.enabled, "Blink hides only selected visuals; gameplay, colliders and unrelated effects remain enabled");
        float normalized = animator.GetCurrentAnimatorStateInfo(0).normalizedTime; animator.Update(.1f);
        Check(animator.GetCurrentAnimatorStateInfo(0).normalizedTime > normalized && !body.enabled, "Reaction animation advances while its renderer is hidden");
        Check(hurtbox.Receive(hit, -1) && health.Current == 180 && body.enabled, "Repeated damage is accepted during blink and restarts from visible");
        yield return new Until(() => !body.enabled);
        Check(hurtbox.Receive(hit, -1) && health.Current == 170 && body.enabled, "Another repeated hit refreshes the same timer cleanly");
        yield return new Until(() => !IsBlinking());
        Check(body.enabled && extra.enabled && !initiallyHidden.enabled, "Blink finishes with original renderer states restored");

        attack.Freeze(5); health.Damage(10); Step(5);
        Check(combo.State == CombatState.Hitstun && attack.HitstopRemaining == 0, "Existing logical hitstop still expires normally");
        Time.timeScale = 0;
        yield return new Until(() => !body.enabled);
        yield return new Until(() => !IsBlinking());
        Check(body.enabled && Time.timeScale == 0, "Unscaled blink flickers and completes even with timeScale zero"); Time.timeScale = previousTimeScale;
        Step(19); Check(combo.State == CombatState.Idle, "Original hitstun duration still recovers through the combat clock");
        attack.Play(data); health.Damage(10); yield return new Until(() => !body.enabled);
        var before = body.sprite; Step(4);
        Check(attack.CurrentFrame == 4 && body.sprite != before && !body.enabled, "AttackData sprite progression works independently of hidden visibility");
        yield return new Until(() => !IsBlinking()); attack.Stop();

        var invulnerable = Attack(true); attack.Play(invulnerable); float current = health.Current;
        Check(!hurtbox.Receive(hit, -1) && health.Current == current && !IsBlinking(), "Existing frame invulnerability rejects damage without blinking"); attack.Stop();
        var armor = Attack(false, true); attack.Play(armor);
        Check(hurtbox.Receive(hit, -1) && attack.CurrentAttack == armor && IsBlinking(), "Super armor still takes damage and blinks without interrupting its attack"); attack.Stop(); blink.StopBlink();
        health.Damage(0); Check(!IsBlinking(), "Zero damage does not trigger blink");

        health.Damage(1); yield return new Until(() => !body.enabled); health.Damage(health.Current);
        Check(health.IsDead && body.enabled && !IsBlinking(), "Death during the invisible phase restores visibility immediately");
        Step(1); Check(combo.State == CombatState.Hitstun && motorPlayer.MovementLocked, "Existing player death state and movement lock are preserved");
        health.Restore(); Check(body.enabled && !IsBlinking(), "Health restoration clears old blink timing");
        health.Damage(1); yield return new Until(() => !body.enabled); health.Restore();
        Check(body.enabled && !IsBlinking(), "Direct restoration while alive also cancels an invisible blink");
        health.Damage(1); yield return new Until(() => !body.enabled); root.SetActive(false);
        Check(body.enabled && !IsBlinking(), "Disabling the player restores its body and clears the timer"); root.SetActive(true); health.Restore();
        Check(body.enabled && !IsBlinking(), "Re-enabled player has no stale blink timer");
        health.Damage(1); yield return new Until(() => !body.enabled); blink.enabled = false;
        Check(body.enabled && !IsBlinking(), "Disabling the effect component restores renderer state"); blink.enabled = true; health.Restore();
        combo.ResetCombo(); Step(20); attack.Play(data); Step(3);
        Check(attack.CurrentAttack == data && attack.CurrentFrame == 3 && body.enabled && !IsBlinking(), "Recovered player can attack with normal visible AttackData sprites");
        attack.Stop(); UnityEngine.Object.DestroyImmediate(data); UnityEngine.Object.DestroyImmediate(invulnerable); UnityEngine.Object.DestroyImmediate(armor);
        health.Damage(1); yield return new Until(() => !body.enabled); UnityEngine.Object.DestroyImmediate(blink);
        Check(body.enabled, "Destroying the effect restores the surviving renderer");
    }
}
