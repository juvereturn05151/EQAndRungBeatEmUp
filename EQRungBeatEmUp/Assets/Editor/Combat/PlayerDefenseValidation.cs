using System;
using System.Collections.Generic;
using System.IO;
using BeatEmUp;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class PlayerDefenseValidation
{
    const string Pending = "BeatEmUp.PlayerDefenseValidation";
    static ComboController p;
    static EnemyHitReaction e;
    static GameObject po, eo;
    static CombatClock clock;
    static PlayerDefenseData defenseCopy;
    static readonly List<string> results = new List<string>();
    static PlayerDefenseValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Validate player defense (Play Mode)")]
    public static void Run() { if (EditorApplication.isPlaying || EditorApplication.isCompiling) return; SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
    static void Poll()
    {
        if (!EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists("Temp/PlayerDefenseValidation.request"))
        { try { File.Delete("Temp/PlayerDefenseValidation.request"); } catch (IOException) { return; } Run(); return; }
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false); results.Clear();
        try
        {
            foreach (var m in UnityEngine.Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) m.gameObject.SetActive(false);
            foreach (var c in UnityEngine.Object.FindObjectsByType<CombatClock>(FindObjectsSortMode.None)) c.enabled = false;
            Dodge(); GuardParry(); ParryBoundariesAndRearm(); ParryCompatibilityAndCounter(); Knockdown(); Death(); RealEnemyAttacks(); InputBindings();
            Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/PlayerDefenseValidationResults.txt", results);
            Debug.Log("PLAYER DEFENSE VALIDATION PASSED: " + results.Count);
            if (Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.ExitPlaymode();
        }
        catch (Exception ex)
        {
            results.Add("FAIL: " + ex); Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/PlayerDefenseValidationResults.txt", results); Debug.LogException(ex);
            if (Application.isBatchMode) EditorApplication.Exit(1); else EditorApplication.ExitPlaymode();
        }
    }
    static void Check(bool pass, string label) { if (!pass) throw new Exception(label); results.Add("PASS: " + label); }
    static void Fixture()
    {
        // This suite measures motor distances in the prefab's default test
        // arena. Stage lane composition is covered by StageFramingValidation.
        foreach (var framing in UnityEngine.Object.FindObjectsByType<StageFraming>(FindObjectsSortMode.None)) framing.enabled = false;
        if (po) UnityEngine.Object.DestroyImmediate(po); if (eo) UnityEngine.Object.DestroyImmediate(eo);
        if (defenseCopy) UnityEngine.Object.DestroyImmediate(defenseCopy);
        po = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Prefabs/BlueShirtGuy.prefab"));
        eo = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Prefabs/BadGuy.prefab"));
        p = po.GetComponent<ComboController>(); e = eo.GetComponent<EnemyHitReaction>(); eo.GetComponent<EnemyCombat>().enabled = false;
        defenseCopy = UnityEngine.Object.Instantiate(p.defenseData); p.defenseData = defenseCopy;
        // Boundary checks below use an explicit eight-frame fixture. Preserve authored tuning.
        defenseCopy.parryWindowFrames = 8;
        po.transform.position = Vector3.zero; eo.transform.position = new Vector3(.8f, 0, 0); p.motor.Face(1); e.motor.Face(-1);
        p.health.Restore(); e.health.Restore(); clock = UnityEngine.Object.FindFirstObjectByType<CombatClock>(); clock.enabled = false; clock.combatFPS = 60;
        foreach (var input in po.GetComponents<MonoBehaviour>()) if (input && input.GetType().Name == "PlayerCombatInput") input.enabled = false;
        Check(p.defenseData != null, "Actual player prefab has defense frame data");
    }
    static void Step(int count = 1) { for (int i = 0; i < count; i++) { Physics2D.SyncTransforms(); clock.StepFrame(); } }
    static AttackHitboxData Hit(HitType type = HitType.Normal) => new AttackHitboxData { damage = 10, hitstunFrames = 18, hitstopFrames = 3, knockback = .5f, canHitGrounded = true, canHitAirborne = true, hitType = type, blockstunFrames = 10, launchVelocity = new Vector2(.35f, 6.2f) };
    static CombatHurtbox Hurtbox => p.GetComponentInChildren<CombatHurtbox>();
    static void Dodge()
    {
        Fixture(); Check(p.RequestDodge(), "Neutral dodge accepts one press"); Check(!p.RequestDodge(), "Repeated request cannot restart active dodge");
        float start = po.transform.position.x;
        for (int i = 0; i < 20; i++)
        {
            Check(p.State == CombatState.Dodge && p.DefenseFrame == i, "Dodge logical frame " + i);
            Check(p.DodgeInvulnerable == (i >= 4 && i <= 9), "Dodge immunity frame " + i);
            p.RequestAttack(); p.RequestLauncher(); p.RequestGuard(true); p.RequestJump();
            Check(!p.CurrentAttack && p.State == CombatState.Dodge && p.motor.IsGrounded, "Conflicting inputs cannot change Dodge frame " + i);
            Step();
        }
        Check(p.State == CombatState.Idle && Mathf.Abs(po.transform.position.x - start + 1.2f) < .0001f, "Default dodge moves backward 1.2 units across eight movement frames and ends at 20");
        Fixture(); p.motor.MoveInput = new Vector2(1, 1); p.RequestDodge(); Step(20);
        Check(Mathf.Abs(po.transform.position.x - .848528f) < .001f && Mathf.Abs(po.transform.position.y - .848528f) < .001f, "Directional dodge uses normalized intended lane movement");
        foreach (int frame in new[] { 0, 3, 4, 9, 10, 19 })
        {
            Fixture(); p.RequestDodge(); Step(frame); float hp = p.health.Current;
            bool accepted = Hurtbox.Receive(Hit(), -1, e.motor);
            Check(accepted == !(frame >= 4 && frame <= 9) && p.health.Current == hp - (accepted ? 10 : 0), "Incoming hit honors dodge vulnerability at frame " + frame);
            if (accepted) Check(p.State == CombatState.Hitstun && p.motor.DefenseVelocity == Vector2.zero, "Vulnerable dodge hit aborts its movement");
        }
        Fixture(); p.RequestDodge(); Step(5); int saved = p.DefenseFrame; float x = po.transform.position.x; p.attackPlayer.Freeze(5); Step(5);
        Check(p.DefenseFrame == saved && po.transform.position.x == x, "Hitstop freezes dodge timing and velocity integration");
    }
    static void GuardParry()
    {
        Fixture(); p.RequestGuard(true); Check(p.State == CombatState.GuardEnter && p.DefenseFrame == 0, "Fresh Guard press opens guard/parry window");
        Step(4); Hurtbox.Receive(Hit(), -1, e.motor);
        Check(p.State == CombatState.Parry && p.health.Current == p.health.maximumHealth && Hurtbox.LastHitOutcome == CombatHitOutcome.Parry, "Parry includes frame 4 and negates damage/blockstun");
        Check(e.State == EnemyReaction.Stunned && e.RecoveryFrames == p.defenseData.parryAttackerStunFrames && !e.GetComponent<AttackPlayer>().CurrentAttack && p.BlockstunFrames == 0, "Parry interrupts attacker for dedicated 90-frame stun");
        var first = p.motor.sprite.sprite; p.RequestGuard(false); Step(2);
        Check(p.State == CombatState.Parry && p.motor.sprite.sprite != first, "Parry displays intentional contact/deflection sprite holds");
        Step(6); Check(p.State == CombatState.Idle && !p.motor.MovementLocked, "Released parry recovers in eight logical frames");
        Fixture(); p.RequestGuard(true); Step(8); Hurtbox.Receive(Hit(), -1, e.motor);
        Check(Hurtbox.LastHitOutcome == CombatHitOutcome.Block && p.State == CombatState.GuardHold && p.health.Current == 200 && p.BlockstunFrames == 10, "Frame 8 yields normal zero-damage guard and ten-frame blockstun");
        p.RequestGuard(false); p.RequestGuard(true);
        Check(p.State == CombatState.GuardHold, "Repress during blockstun cannot rearm parry");
        p.RequestGuard(false); Step(9); Check(p.State == CombatState.GuardHold, "Release during blockstun cannot escape early");
        Step(); Check(p.State == CombatState.Idle, "Guard release returns neutral after blockstun");
        Fixture(); p.RequestGuard(true); Step(100); int timer = p.DefenseFrame; p.RequestGuard(true);
        Check(p.DefenseFrame == timer && p.State == CombatState.GuardHold, "Long-held or repeated Guard never restarts parry");
        Hurtbox.Receive(Hit(), -1, e.motor); Check(Hurtbox.LastHitOutcome == CombatHitOutcome.Block, "Early Guard is a block, never a parry");
        Fixture(); eo.transform.position = new Vector3(-.8f, 0, 0); p.RequestGuard(true); Hurtbox.Receive(Hit(), 1, e.motor);
        Check(p.health.Current == 190 && p.State == CombatState.Hitstun, "Rear attack bypasses fresh guard/parry using attacker position");
        Fixture(); p.motor.Face(-1); eo.transform.position = new Vector3(-.8f, 0, 0); p.RequestGuard(true); Hurtbox.Receive(Hit(), 1, e.motor);
        Check(p.State == CombatState.Parry && p.health.Current == 200, "Left-facing front parry mirrors correctly");
        Fixture(); p.RequestGuard(true); var unblockable = Hit(); unblockable.unblockable = true; Hurtbox.Receive(unblockable, -1, e.motor);
        Check(p.State == CombatState.Hitstun && p.health.Current == 190, "Attack-authored unblockable bypasses defense");
        Fixture(); Hurtbox.Receive(Hit(), -1, e.motor); p.RequestGuard(true);
        Check(p.State == CombatState.Hitstun && p.health.Current == 190, "Late guard cannot erase an already accepted hit");
        Fixture(); p.RequestGuard(true); Step(8); var chip = Hit(); chip.blockDamage = 2; chip.blockstunFrames = 12; Hurtbox.Receive(chip, -1, e.motor);
        Check(p.health.Current == 198 && p.BlockstunFrames == 12 && p.State == CombatState.GuardHold, "Attack data controls chip damage and blockstun");
    }
    static void Knockdown()
    {
        foreach (bool launched in new[] { false, true })
        {
            Fixture(); Hurtbox.Receive(Hit(launched ? HitType.Launcher : HitType.KnockDown), -1, e.motor);
            Check(p.State == CombatState.KnockDown, "Marked knockdown or launcher enters player KnockDown");
            if (launched) { for (int i = 0; !p.motor.IsGrounded && i < 160; i++) Step(); Check(p.motor.IsGrounded && p.DefenseFrame == 0, "Actual floor contact begins grounded knockdown timing"); }
            p.RequestGuard(true); p.RequestDodge(); p.RequestAttack(); p.RequestJump();
            Check(p.State == CombatState.KnockDown && !p.CurrentAttack, "KnockDown rejects guard/dodge/attack/jump");
            Step(17); Check(p.State == CombatState.KnockDown, "KnockDown impact/fall lasts all 18 frames");
            Step(); Check(p.State == CombatState.Downed && p.DefenseFrame == 0, "KnockDown transitions to downed hold");
            var pose = p.motor.sprite.sprite; var position = po.transform.position; Step(44);
            Check(p.State == CombatState.Downed && p.motor.sprite.sprite == pose && po.transform.position == position, "Downed holds final sprite and ground position through frame 44");
            Step(); Check(p.State == CombatState.GetUp, "Downed recovery starts at frame 45");
            p.RequestGuard(true); p.RequestDodge(); p.RequestAttack(); Step(23);
            Check(p.State == CombatState.GetUp && !p.CurrentAttack, "Existing knockdown poses reverse for full 24-frame GetUp without input conflicts");
            Step(); Check(p.State == CombatState.Idle && !p.motor.MovementLocked, "GetUp returns to neutral");
        }
    }
    static void Death()
    {
        foreach (int setup in new[] { 0, 1, 2, 3, 4, 5, 6, 7 })
        {
            Fixture();
            if (setup == 1) p.RequestAttack();
            if (setup == 2) p.RequestGuard(true);
            if (setup == 3) p.RequestDodge();
            if (setup == 4) Hurtbox.Receive(Hit(HitType.KnockDown), -1, e.motor);
            if (setup == 5) { Hurtbox.Receive(Hit(HitType.KnockDown), -1, e.motor); Step(63); }
            if (setup == 6) { p.RequestGuard(true); Hurtbox.Receive(Hit(), -1, e.motor); }
            if (setup == 7) Hurtbox.Receive(Hit(), -1, e.motor);
            p.attackPlayer.Freeze(4); p.health.Damage(9999);
            Check(p.State == CombatState.Die && !p.CurrentAttack && !p.GuardHeld && p.motor.DefenseVelocity == Vector2.zero, "Death immediately overrides state case " + setup + " including hitstop");
            p.RequestAttack(); p.RequestLauncher(); p.RequestGuard(true); p.RequestDodge(); p.RequestJump(); Step(200);
            Check(p.State == CombatState.Die && !p.CurrentAttack && p.motor.IsGrounded && p.motor.MovementLocked, "Dead player cannot act, recover or GetUp in case " + setup);
            var pose = p.motor.sprite.sprite; Step(120); Check(p.motor.sprite.sprite == pose, "Death final pose holds indefinitely in case " + setup);
            p.health.Restore(); Check(p.State == CombatState.Idle, "Only explicit existing health restore respawns player");
        }
        Fixture(); Hurtbox.Receive(Hit(HitType.Launcher), -1, e.motor); p.health.Damage(9999); Step(240);
        Check(p.State == CombatState.Die && p.motor.IsGrounded, "Airborne death falls to floor and never recovers");
    }
    static void RealEnemyAttacks()
    {
        foreach (bool parry in new[] { true, false })
        {
            Fixture(); p.RequestGuard(true); if (!parry) Step(8);
            var attack = eo.GetComponent<EnemyCombat>().attack; e.GetComponent<AttackPlayer>().Play(attack);
            // Position the actual active hitbox against the player and keep
            // the first five guard ticks aligned with the active attack sample.
            e.GetComponent<AttackPlayer>().Stop();
            var copy = UnityEngine.Object.Instantiate(attack); copy.frames.RemoveRange(0, copy.FirstActiveFrame);
            e.GetComponent<AttackPlayer>().Play(copy);
            Check(p.health.Current == 200 && Hurtbox.LastHitOutcome == (parry ? CombatHitOutcome.Parry : CombatHitOutcome.Block), "Actual enemy attack hitbox produces " + (parry ? "Parry" : "Guard"));
            Check(p.attackPlayer.HitstopRemaining == (parry ? 6 : 2) && e.GetComponent<AttackPlayer>().HitstopRemaining == (parry ? 6 : 2), "Both actors receive stronger parry / smaller guard hitstop");
            if (parry) Check(!e.GetComponent<AttackPlayer>().CurrentAttack && e.RecoveryFrames == p.defenseData.parryAttackerStunFrames, "Parry safely aborts the currently sampling enemy attack");
            UnityEngine.Object.DestroyImmediate(copy);
        }
    }
    static void ParryBoundariesAndRearm()
    {
        foreach (int window in new[] { 6, 8, 10 }) foreach (int contact in new[] { 0, window - 1, window })
        {
            Fixture(); p.defenseData.parryWindowFrames = window; p.RequestGuard(true); Step(contact);
            Hurtbox.Receive(Hit(HitType.KnockDown), -1, e.motor);
            Check(Hurtbox.LastHitOutcome == (contact < window ? CombatHitOutcome.Parry : CombatHitOutcome.Block), "Editable " + window + "-frame window accepts frame " + contact + " with correct exclusive end");
            Check(p.health.Current == 200 && p.motor.IsGrounded && p.motor.HorizontalRecoil == 0 && !p.IsKnockdownState, "Parry / guard prevents incoming damage, recoil and knockdown at " + contact + "/" + window);
        }
        for (int contact = 0; contact < 8; contact++)
        {
            Fixture(); p.RequestGuard(true); Step(contact); Hurtbox.Receive(Hit(), -1, e.motor);
            Check(Hurtbox.LastHitOutcome == CombatHitOutcome.Parry, "Default parry is active at zero-based frame " + contact);
        }
        Fixture(); p.RequestGuard(true); int cycle = p.EffectiveParryWindow + p.defenseData.parryRearmDelayFrames;
        Check(p.ParryActive && cycle == 14, "Fresh Guard is immediately active; default re-arm interval includes eight active plus six delay frames");
        p.RequestGuard(false);
        for (int frame = 0; frame < cycle; frame++)
        {
            p.RequestGuard(true);
            Check(p.GuardActive && !p.ParryActive, "Guard mashing cannot refresh parry, but Guard stays responsive at re-arm frame " + frame);
            p.RequestGuard(false); Step();
        }
        p.RequestGuard(true); Check(p.ParryActive && p.DefenseFrame == 0, "Fresh press after full re-arm interval starts a new window");
        Fixture(); p.RequestGuard(true); p.RequestGuard(false); p.RequestGuard(true); Step(30);
        Check(p.GuardActive && !p.ParryActive && p.State == CombatState.GuardHold, "Holding Guard through re-arm expiry cannot silently generate another window");
        p.RequestGuard(false); p.RequestGuard(true); Check(p.ParryActive, "Release and a new eligible press deliberately rearm parry");
        Fixture(); p.RequestGuard(true); Step(3); int savedFrame = p.DefenseFrame, savedRearm = p.ParryRearmRemaining;
        p.attackPlayer.Freeze(6); Step(6);
        Check(p.DefenseFrame == savedFrame && p.ParryRearmRemaining == savedRearm, "Parry window and re-arm timing both freeze with combat hitstop");
        Step(); Check(p.DefenseFrame == savedFrame + 1, "Window resumes on the next combat frame after hitstop");
        Fixture(); p.motor.Jump(); p.RequestGuard(true); Check(!p.ParryActive && !p.GuardActive, "Airborne guard press cannot activate parry");
        Fixture(); p.Interrupt(20); p.RequestGuard(true); Check(!p.ParryActive && p.State == CombatState.Hitstun, "Hitstun cannot be escaped through parry");
        Fixture(); p.RequestAttack(); p.RequestGuard(true); Check(p.CurrentAttack && !p.ParryActive, "Incompatible running attack prevents parry");
    }
    static void ParryCompatibilityAndCounter()
    {
        Fixture(); p.RequestGuard(true); var blockOnly = Hit(); blockOnly.canBeParried = false; blockOnly.blockDamage = 2;
        Hurtbox.Receive(blockOnly, -1, e.motor);
        Check(Hurtbox.LastHitOutcome == CombatHitOutcome.Block && p.health.Current == 198 && e.State == EnemyReaction.Normal, "Attack opt-out preserves chip / normal Guard without parry stun");
        Check(!p.GetComponent<AttackFeedback>() || p.GetComponent<AttackFeedback>().ParryCount == 0, "Normal Guard does not emit successful-parry feedback");
        Fixture(); p.RequestGuard(true);
        var copy = UnityEngine.Object.Instantiate(eo.GetComponent<EnemyCombat>().attack);
        copy.frames.RemoveRange(0, copy.FirstActiveFrame);
        try
        {
            Check(copy.frames[0].hitboxes[0].canBeParried, "Existing enemy attacks inherit parryable compatibility");
            e.GetComponent<AttackPlayer>().Play(copy);
            Check(Hurtbox.LastHitOutcome == CombatHitOutcome.Parry && p.health.Current == 200, "Real authoritative hitbox produces a damage-free immediate parry");
            var feedback = p.GetComponent<AttackFeedback>();
            Check(feedback && feedback.ParryCount == 1 && feedback.LastImpact, "Successful parry creates one distinct effect through existing feedback renderer");
            Check(feedback.LastSound && feedback.LastSound.clip == p.defenseData.parryFeedback.impactSound && feedback.LastSound.clip, "Successful parry plays assigned distinct sound hook");
            Check(p.attackPlayer.HitstopRemaining == 6 && e.GetComponent<AttackPlayer>().HitstopRemaining == 6 && e.RecoveryFrames == p.defenseData.parryAttackerStunFrames, "Real parry freezes both actors for six frames and stuns normal enemy for 90 frames");
            foreach (var particles in feedback.LastImpact.GetComponentsInChildren<ParticleSystem>()) particles.Simulate(.08f, false, false, false);
            CaptureParry();
            p.RequestGuard(false); Step(6);
            Check(p.DefenseFrame == 0 && e.RecoveryFrames == p.defenseData.parryAttackerStunFrames, "Parry hitstop does not consume recovery or enemy stun frames");
            Step(p.defenseData.parryRecoveryFrames);
            Check(p.State == CombatState.Idle && e.RecoveryFrames == 82 && p.CounterAdvantageFrames == 82, "Eight-frame player recovery leaves eighty-two combat frames of counter advantage");
            p.RequestAttack(); Check(p.CurrentAttack && !e.CanAct, "Player can start a counterattack while enemy remains stunned");
        }
        finally { UnityEngine.Object.DestroyImmediate(copy); }
    }
    static void CaptureParry()
    {
        var go = new GameObject("Parry validation camera"); var camera = go.AddComponent<Camera>();
        camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 1.5f; camera.transform.position = new Vector3(.4f,.8f,-10);
        var target = new RenderTexture(960,540,24); camera.targetTexture = target; var old = RenderTexture.active;
        try
        {
            camera.Render(); RenderTexture.active = target;
            var texture = new Texture2D(960,540,TextureFormat.RGB24,false); texture.ReadPixels(new Rect(0,0,960,540),0,0); texture.Apply();
            Directory.CreateDirectory("Documentation/ParryPreview"); File.WriteAllBytes("Documentation/ParryPreview/Success.png",texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        }
        finally { RenderTexture.active = old; camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(go); }
    }
    static void InputBindings()
    {
        Fixture();
        // Measure cardinal travel away from the prefab's y=1 arena boundary.
        p.motor.arenaMin=new Vector2(-6,-4); p.motor.arenaMax=new Vector2(6,4);
        var updateMode=InputSystem.settings.updateMode;
        var background=InputSystem.settings.backgroundBehavior;
        var editorBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard=InputSystem.AddDevice<Keyboard>(); var mouse=InputSystem.AddDevice<Mouse>(); var gamepad=InputSystem.AddDevice<Gamepad>();
        var input=po.GetComponent<PlayerInput>(); var bridge=po.GetComponent<PlayerCombatInput>();
        SessionInput source=null;
        try
        {
            Check(input.actions!=null,"PlayerInput prefab resolves the updated actions asset");
            input.enabled=false; input.enabled=true;
            input.SwitchCurrentControlScheme("Keyboard&Mouse",keyboard,mouse); input.SwitchCurrentActionMap("Player");
            bridge.enabled=true; bridge.SendMessage("Start");
            Check(input.actions.FindAction("Player/Dodge",false)==null && input.actions.FindAction("Player/Parry",false)==null,"Only the existing Guard action owns Guard, Parry and directional Dodge");
            Action<Key[]> keys=held=>{ InputSystem.QueueStateEvent(keyboard,new KeyboardState(held)); InputSystem.Update(); bridge.SendMessage("Update"); };
            keys(new[]{Key.L}); Check(p.State==CombatState.GuardEnter && p.GuardHeld,"Standing still plus L enters Guard");
            Step(8); int frame=p.DefenseFrame; keys(new[]{Key.L});
            Check(p.State==CombatState.GuardHold && p.DefenseFrame==frame,"Holding L keeps Guard without restarting its animation or parry window");
            keys(new Key[0]); Check(p.State==CombatState.Idle && !p.GuardHeld,"Releasing L releases Guard");
            keys(new[]{Key.LeftAlt}); Check(p.State==CombatState.Idle,"Removed Left Alt binding no longer dodges");
            var directions=new[]{Key.A,Key.D,Key.W,Key.S};
            var vectors=new[]{Vector2.left,Vector2.right,Vector2.up,Vector2.down};
            for(int i=0;i<directions.Length;i++)
            {
                keys(new Key[0]); p.health.Restore(); p.motor.ResetForStage(Vector2.zero);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(directions[i],Key.L)); InputSystem.Update();
                Check(p.State==CombatState.Idle && p.ParryRearmRemaining==0,"Simultaneous direction and L does not briefly trigger Guard: "+directions[i]);
                bridge.SendMessage("Update");
                Check(p.State==CombatState.Dodge && !p.GuardHeld && p.ParryRearmRemaining==0,"Direction plus L prioritizes Dodge: "+directions[i]);
                Step(20); Vector2 displacement=p.transform.position;
                Check(Vector2.Distance(displacement,vectors[i]*1.2f)<.001f,"Dodge preserves authored distance and direction: "+directions[i]);
                keys(new[]{directions[i],Key.L}); Step(25); keys(new[]{directions[i],Key.L});
                Check(p.State==CombatState.Idle,"Held direction and L never repeat Dodge: "+directions[i]);
                keys(new[]{Key.L}); Check(p.GuardHeld && p.GuardActive,"Releasing movement while L remains held enters Guard: "+directions[i]);
            }
            keys(new Key[0]); p.health.Restore(); p.motor.ResetForStage(Vector2.zero);
            keys(new[]{Key.W,Key.D,Key.L}); Step(20);
            Check(Vector2.Distance(p.transform.position,new Vector2(1,1).normalized*1.2f)<.001f,"Keyboard diagonal dodge is normalized and preserves total distance");
            keys(new Key[0]); p.health.Restore(); p.motor.ResetForStage(Vector2.zero);
            keys(new[]{Key.L}); keys(new[]{Key.L,Key.D});
            Check(!p.GuardHeld && p.State==CombatState.Idle,"Adding movement to held L releases Guard without triggering Dodge");
            keys(new[]{Key.D}); keys(new[]{Key.D,Key.L});
            Check(p.State==CombatState.Dodge,"Repressing L with movement triggers the next Dodge"); Step(20); keys(new Key[0]);
            p.health.Restore(); p.motor.ResetForStage(Vector2.zero);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.L));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.L,Key.D)); InputSystem.Update();
            Check(!p.GuardHeld && p.ParryRearmRemaining==0,"Button event before movement in the same input update never starts Guard");
            bridge.SendMessage("Update"); Check(p.State==CombatState.Dodge,"Button-first simultaneous input resolves the final intended Dodge direction");
            Step(20); keys(new Key[0]);
            p.health.Restore(); p.motor.ResetForStage(Vector2.zero);
            keys(new[]{Key.D,Key.L}); Step(5); keys(new[]{Key.L});
            Check(p.State==CombatState.Dodge && !p.GuardHeld,"Movement release during Dodge cannot cancel its authored recovery into Guard");
            Step(15); keys(new[]{Key.L});
            Check(p.GuardActive,"Holding L after releasing movement enters Guard once Dodge recovery finishes");
            keys(new Key[0]);
            p.health.Restore(); input.SwitchCurrentControlScheme("Gamepad",gamepad);
            Action<GamepadState> pad=state=>{InputSystem.QueueStateEvent(gamepad,state);InputSystem.Update();bridge.SendMessage("Update");};
            pad(new GamepadState().WithButton(GamepadButton.LeftShoulder));
            Check(p.GuardHeld && p.GuardActive,"Controller left shoulder alone Guards");
            pad(new GamepadState()); Check(!p.GuardHeld,"Controller shoulder release stops Guard");
            foreach(var direction in vectors)
            {
                p.health.Restore(); p.motor.ResetForStage(Vector2.zero);
                pad(new GamepadState{leftStick=direction}.WithButton(GamepadButton.LeftShoulder));
                Check(p.State==CombatState.Dodge && !p.GuardHeld && p.ParryRearmRemaining==0,"Controller stick plus left shoulder prioritizes Dodge: "+direction);
                Step(20); Check(Vector2.Distance(p.transform.position,(Vector3)(direction*1.2f))<.001f,"Controller dodge preserves directional distance: "+direction);
                pad(new GamepadState{leftStick=direction}.WithButton(GamepadButton.LeftShoulder));
                Check(p.State==CombatState.Idle,"Held controller shoulder does not repeat Dodge");
                pad(new GamepadState().WithButton(GamepadButton.LeftShoulder)); Check(p.GuardActive,"Releasing stick while holding shoulder enters Guard");
                pad(new GamepadState());
            }
            p.health.Restore(); pad(new GamepadState().WithButton(GamepadButton.RightShoulder));
            Check(p.State==CombatState.Idle,"Removed right-shoulder binding no longer dodges"); pad(new GamepadState());
            bridge.enabled=false;
            source=new SessionInput(input.actions,keyboard);
            foreach(var direction in directions)
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update(); source.Read();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(direction,Key.L)); InputSystem.Update(); var command=source.Read();
                Check(!command.guard && ((PlayerButtons)command.buttons & PlayerButtons.Dodge)!=0,"Paired multiplayer input sends Dodge without Guard: "+direction);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(direction,Key.L)); InputSystem.Update(); command=source.Read();
                Check(!command.guard && ((PlayerButtons)command.buttons & PlayerButtons.Dodge)==0,"Paired multiplayer input sends no repeated held-button Dodge");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.L)); InputSystem.Update(); command=source.Read();
                Check(command.guard && ((PlayerButtons)command.buttons & PlayerButtons.Dodge)==0,"Paired multiplayer input sends Guard after movement release");
            }
            source.Dispose(); source=new SessionInput(input.actions,gamepad);
            InputSystem.QueueStateEvent(gamepad,new GamepadState{leftStick=Vector2.right}.WithButton(GamepadButton.LeftShoulder)); InputSystem.Update();
            var controllerCommand=source.Read(); var wire=JsonUtility.FromJson<PlayerCommand>(JsonUtility.ToJson(controllerCommand));
            Check(!wire.guard && ((PlayerButtons)wire.buttons & PlayerButtons.Dodge)!=0 && wire.move.x>0,"Controller multiplayer command preserves directional Dodge and priority across serialization");
            InputSystem.QueueStateEvent(gamepad,new GamepadState{leftStick=Vector2.right}.WithButton(GamepadButton.LeftShoulder)); InputSystem.Update();
            Check(((PlayerButtons)source.Read().buttons & PlayerButtons.Dodge)==0,"Controller multiplayer input does not repeat a held Dodge");
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.LeftShoulder)); InputSystem.Update();
            Check(source.Read().guard,"Controller multiplayer input Guards after releasing the stick");
        }
        finally
        {
            source?.Dispose(); bridge.enabled=false;
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(gamepad);
            InputSystem.settings.updateMode=updateMode; InputSystem.settings.backgroundBehavior=background; InputSystem.settings.editorInputBehaviorInPlayMode=editorBehavior;
        }
    }
}
