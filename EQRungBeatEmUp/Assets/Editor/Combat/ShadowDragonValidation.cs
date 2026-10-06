using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class ShadowDragonValidation
{
    const string Pending = "BeatEmUp.ShadowDragonValidation";
    static readonly List<string> results = new List<string>();
    static GameObject po, eo;
    static ComboController p;
    static PlayerMeter meter;
    static PlayerSkillController skill;
    static EnemyHitReaction enemy;
    static CombatClock clock;
    static ShadowDragonValidation() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Skills/Validate Shadow Dragon (Play Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    static void Poll()
    {
        if (!EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists("Temp/ShadowDragonValidation.request"))
        { try { File.Delete("Temp/ShadowDragonValidation.request"); } catch (IOException) { return; } Run(); return; }
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending, false); results.Clear();
        try
        {
            foreach (var flow in UnityEngine.Object.FindObjectsByType<StageFlowController>(FindObjectsSortMode.None)) flow.enabled = false;
            foreach (var framing in UnityEngine.Object.FindObjectsByType<StageFraming>(FindObjectsSortMode.None)) framing.enabled = false;
            foreach (var actor in UnityEngine.Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) actor.gameObject.SetActive(false);
            foreach (var c in UnityEngine.Object.FindObjectsByType<CombatClock>(FindObjectsSortMode.None)) c.enabled = false;
            MeterRules(); CastAndDirection(); ProjectileRules(); StateRules(); InputsAndAssets(); Previews();
            File.WriteAllLines("Documentation/ShadowDragonValidationResults.txt", results);
            Debug.Log("SHADOW DRAGON VALIDATION PASSED: " + results.Count);
        }
        catch (Exception ex)
        {
            results.Add("FAIL: " + ex); File.WriteAllLines("Documentation/ShadowDragonValidationResults.txt", results); Debug.LogException(ex);
        }
        finally { EditorApplication.ExitPlaymode(); }
    }
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); results.Add("PASS: " + label); }
    static bool Near(float a, float b) => Mathf.Abs(a-b) < .0001f;
    static void Fixture(float enemyX = .8f)
    {
        foreach (var shot in UnityEngine.Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(shot.gameObject);
        if (po) UnityEngine.Object.DestroyImmediate(po); if (eo) UnityEngine.Object.DestroyImmediate(eo);
        po = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ComboTrackingSetup.PlayerPath));
        eo = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Prefabs/BadGuy.prefab"));
        po.GetComponent<PlayerCombatInput>().enabled = false; po.GetComponent<PlayerInput>().enabled = false;
        p = po.GetComponent<ComboController>(); meter = po.GetComponent<PlayerMeter>(); skill = po.GetComponent<PlayerSkillController>();
        enemy = eo.GetComponent<EnemyHitReaction>(); eo.GetComponent<EnemyCombat>().enabled = false;
        po.transform.position = Vector3.zero; eo.transform.position = new Vector3(enemyX,0,0);
        p.motor.Face(1); enemy.motor.Face(-1); p.health.Restore(); enemy.health.Restore();
        clock = UnityEngine.Object.FindFirstObjectByType<CombatClock>(); clock.enabled = false; clock.combatFPS = 60; Physics2D.SyncTransforms();
    }
    static void Step(int n=1) { for (int i=0;i<n;i++) { Physics2D.SyncTransforms(); clock.StepFrame(); } }
    static CombatHurtbox PlayerHurtbox => po.GetComponentInChildren<CombatHurtbox>();
    static AttackHitboxData Incoming(HitType type=HitType.Normal) => new AttackHitboxData {damage=10,hitstunFrames=18,hitstopFrames=0,knockback=0,canHitAirborne=true,hitType=type};
    static void MeterRules()
    {
        Fixture(); Check(Near(meter.CurrentMeter,1) && Near(meter.MaxMeter,1) && Near(meter.Normalized,1),"Starts at one full bar");
        Check(meter.TrySpend(1) && Near(meter.CurrentMeter,0),"Reusable spending empties one bar");
        Check(!meter.TrySpend(1) && !meter.TrySpend(float.NaN) && !meter.TrySpend(-1),"Insufficient / invalid spending rejected");
        var widget = UnityEngine.Object.FindObjectsByType<MeterUIWidget>(FindObjectsSortMode.None).First(w=>w.meter==meter);
        Check(Near(widget.fill.rectTransform.anchorMax.x,0) && widget.label.text.Contains("0 / 1"),"Existing solo canvas displays empty bar");
        p.RequestAttack(); Step(65);
        Check(po.GetComponent<ComboTracker>().HitCount>0 && Near(meter.CurrentMeter,po.GetComponent<ComboTracker>().HitCount*.06f),"Actual accepted punch hit grants .06, repeated overlap does not double gain");
        Check(Near(widget.fill.rectTransform.anchorMax.x,meter.Normalized),"UI fill updates on confirmed-hit gain");
        Fixture(5); meter.TrySpend(1); p.RequestAttack(); Step(65); Check(Near(meter.CurrentMeter,0),"Whiff grants no meter");
        Fixture(); meter.TrySpend(1); p.RequestGuard(true); PlayerHurtbox.Receive(Incoming(),-1,enemy.motor);
        Check(Near(meter.CurrentMeter,.4f) && p.State==CombatState.Parry,"Successful existing parry grants .4 bar");
        Check(meter.gainOnParry>meter.gainOnHit*6,"Parry gain is substantially larger than regular hit");
        meter.Add(100); Check(Near(meter.CurrentMeter,1),"Gain clamps at max");
        Fixture(); meter.TrySpend(1); p.RequestGuard(true); Step(8); PlayerHurtbox.Receive(Incoming(),-1,enemy.motor);
        Check(Near(meter.CurrentMeter,0),"Normal block grants no parry meter");
        Fixture(); meter.maxMeter=3; meter.startingMeter=2.5f; meter.ResetMeter(); meter.TrySpend(.75f); meter.Add(.2f);
        Check(Near(meter.CurrentMeter,1.95f) && Near(meter.Normalized,.65f),"Multiple bars and fractional costs / gains supported");
        meter.maxMeter=1; meter.startingMeter=1;
        Fixture(); meter.TrySpend(1); p.health.Restore(); Check(Near(meter.CurrentMeter,1),"New-run health restore resets starting meter");
        meter.TrySpend(1); CombatClock.SetPaused(po,true); meter.Add(.4f); Check(!skill.RequestSkill() && Near(meter.CurrentMeter,0),"Global pause blocks skill and resource gain"); CombatClock.SetPaused(po,false);
    }
    static void CastAndDirection()
    {
        foreach (int facing in new[]{1,-1})
        {
            Fixture(5*facing); p.motor.Face(facing);
            Check(skill.RequestSkill() && Near(meter.CurrentMeter,0),"Full bar accepts and consumes once, facing "+facing);
            Check(!skill.RequestSkill() && skill.ProjectilesReleased==0,"Repeated input cannot restart or spawn early");
            Check(p.State==CombatState.GroundAttack && p.motor.MovementLocked,"Cast uses grounded combat state and movement lock");
            for(int i=0;i<12;i++) { Check(skill.ProjectilesReleased==0 && p.attackPlayer.CurrentFrame==i,"No dragon before authored release frame "+i+", facing "+facing); Step(); }
            Check(skill.ProjectilesReleased==1 && p.attackPlayer.CurrentFrame==12,"Named frame event releases exactly on frame 12");
            var shot=skill.LastProjectile;
            Check(shot && Near(shot.transform.position.x,.8f*facing) && Near(shot.transform.position.y,.75f),"Dragon emerges at configured hand offset");
            Check(shot.visual.flipX==(facing<0) && Mathf.Sign(shot.Velocity.x)==facing,"Visual and travel mirror with facing");
            float before=shot.transform.position.x; Step(); Check((shot.transform.position.x-before)*facing>0,"Dragon moves forward");
            Step(19); Check(!p.CurrentAttack && p.State==CombatState.Idle && !p.motor.MovementLocked,"Cast finishes all 32 frames and returns controllable neutral");
            Check(skill.ProjectilesReleased==1,"No repeated release while cast frame advances");
            p.RequestAttack(); Check(p.CurrentAttack==p.groundCombo[0],"Normal attack still starts after skill recovery");
        }
        Fixture(5); skill.RequestSkill(); Step(5); PlayerHurtbox.Receive(Incoming(),-1,enemy.motor); Step(50);
        Check(skill.ProjectilesReleased==0 && Near(meter.CurrentMeter,0),"Startup interruption cancels pending spawn; accepted cost stays spent");
        Fixture(5); meter.TrySpend(1); Check(!skill.RequestSkill() && !p.CurrentAttack,"Insufficient bar leaves state unchanged");
        Fixture(5); var data=UnityEngine.Object.Instantiate(skill.equippedSkill); var cast=UnityEngine.Object.Instantiate(data.cast);
        data.cast=cast; foreach(var frame in cast.frames) frame.events.Remove("SpawnDragon"); cast.frames[4].events.Add("SpawnDragon"); data.meterCost=.5f;
        Check(skill.RequestSkill(data) && Near(meter.CurrentMeter,.5f),"Alternate skill asset and fractional cost accepted"); Step(3); Check(skill.ProjectilesReleased==0,"Moved event has no hardcoded release"); Step(); Check(skill.ProjectilesReleased==1,"Changing authored event moves release to frame 4");
        p.ResetCombo(); UnityEngine.Object.DestroyImmediate(cast); UnityEngine.Object.DestroyImmediate(data);
    }
    static void ProjectileRules()
    {
        Fixture(1.6f); var second=UnityEngine.Object.Instantiate(eo); second.transform.position=new Vector3(2.5f,0,0);
        float hp=enemy.health.Current,hp2=second.GetComponent<CharacterHealth>().Current;
        skill.RequestSkill(); Step(12); var shot=skill.LastProjectile;
        int impacts=0; Action<CombatProjectile,Vector2> callback=(s,point)=>{if(s==shot)impacts++;}; CombatProjectile.Impact+=callback;
        for(int i=0;i<20 && shot.AcceptedHits==0;i++)Step();
        Check(shot.AcceptedHits==1 && enemy.State==EnemyReaction.Launched,"First dragon contact uses real enemy launch reaction");
        Check(p.attackPlayer.HitstopRemaining==7 && enemy.motor.attackPlayer.HitstopRemaining==7,"Accepted projectile hit freezes attacker and victim for seven frames");
        Check(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(a=>a.clip==skill.equippedSkill.cast.feedback.impactSound),"Projectile impact plays assigned sound source");
        Check(GameObject.Find("Network combat impact")!=null,"Projectile impact instantiates shared spark VFX");
        Step(65);
        Check(Near(enemy.health.Current,hp-32) && Near(second.GetComponent<CharacterHealth>().Current,hp2-32),"One dragon pierces and damages two enemies exactly once each");
        Check(shot.AcceptedHits==2 && impacts==2,"One confirmed feedback cue per accepted target");
        Check(Near(meter.CurrentMeter,.16f),"Projectile damage uses accepted player hit tracking and launcher bonus for meter");
        Check(enemy.health.Current<hp,"Dragon damage accepted through existing enemy reaction path");
        Step(80); Check(!shot || !shot.gameObject.activeInHierarchy,"Lifetime expires and dissipating object cleans up"); CombatProjectile.Impact-=callback;
        UnityEngine.Object.DestroyImmediate(second);
        Fixture(1.6f); skill.RequestSkill(); Step(12); shot=skill.LastProjectile; shot.speed=0; shot.InitializeForward(p.motor,p.hitbox.team,new Vector2(1.6f,0),.65f,1); hp=enemy.health.Current; Step(45);
        Check(shot.AcceptedHits==1 && Near(enemy.health.Current,hp-32),"Stationary overlapping projectile never repeatedly damages same enemy");
        Fixture(1.6f); skill.RequestSkill(); Step(12); shot=skill.LastProjectile; shot.speed=0; shot.hit.hitType=HitType.Normal; shot.hit.knockback=0; shot.hit.hitstopFrames=0; shot.repeatHitFrames=10;
        shot.InitializeForward(p.motor,p.hitbox.team,new Vector2(1.6f,0),.65f,1); Step(21);
        Check(shot.AcceptedHits==3,"Explicit repeat interval permits only intended timed multi-hits");
        Fixture(1.6f); skill.RequestSkill(); Step(12); shot=skill.LastProjectile; shot.maximumTargets=1; Step(20); Check(shot.Resolved,"Finite target limit resolves projectile");
        Fixture(1.6f); eo.transform.position=new Vector3(1.6f,1,0); hp=enemy.health.Current; skill.RequestSkill(); Step(45); Check(Near(enemy.health.Current,hp),"Lane separation rejects off-lane enemies");
        Fixture(1.6f); eo.GetComponentInChildren<CombatHurtbox>().externalInvulnerable=true; hp=enemy.health.Current; skill.RequestSkill(); Step(45); Check(Near(enemy.health.Current,hp) && Near(meter.CurrentMeter,0),"Invulnerable target grants neither damage nor meter");
        Fixture(5); skill.RequestSkill(); Step(12); shot=skill.LastProjectile; var original=shot.transform.position; CombatClock.SetPaused(po,true); Step(50); Check(shot.Age==0 && shot.transform.position==original,"Pause freezes projectile age, movement and animation"); CombatClock.SetPaused(po,false);
        Step(4); Check(shot.visual.sprite!=shot.emergenceSprites[0],"Emergence advances into generated travel animation");
        shot.lifetimeFrames=5; Step(2); Check(shot.Resolved && shot.visual.sprite==shot.dissipateSprites[0],"Lifetime enters collision-free dissipate art");
        Step(4); Check(shot.visual.sprite==shot.dissipateSprites[1],"Dissipate progresses on combat frames"); Step(4); Check(!shot || !shot.gameObject.activeInHierarchy,"Dissipate ends after authored sprite holds");
    }
    static void StateRules()
    {
        for(int state=0;state<7;state++)
        {
            Fixture(5);
            if(state==0)p.RequestAttack();
            if(state==1)p.RequestJump();
            if(state==2)p.RequestGuard(true);
            if(state==3)p.RequestDodge();
            if(state==4)PlayerHurtbox.Receive(Incoming(),-1,enemy.motor);
            if(state==5)PlayerHurtbox.Receive(Incoming(HitType.KnockDown),-1,enemy.motor);
            if(state==6)p.health.Damage(9999);
            Check(!skill.RequestSkill() && Near(meter.CurrentMeter,1),"Incompatible state rejects without cost: "+p.State);
        }
        Fixture(); PlayerHurtbox.Receive(Incoming(HitType.KnockDown),-1,enemy.motor); Step(18);
        Check(p.State==CombatState.Downed && !skill.RequestSkill(),"Downed rejects skill"); Step(45);
        Check(p.State==CombatState.GetUp && !skill.RequestSkill(),"GetUp rejects skill");
        Fixture(); p.attackPlayer.Freeze(4); Check(!skill.RequestSkill(),"Actor hitstop rejects skill");
    }
    static void InputsAndAssets()
    {
        Fixture(5);
        var actions=po.GetComponent<PlayerInput>().actions; var action=actions.FindAction("Player/Skill",true);
        Check(action.bindings.Any(b=>b.path=="<Keyboard>/i") && action.bindings.Any(b=>b.path=="<Gamepad>/rightTrigger"),"New Input System skill keyboard / gamepad bindings");
        Check(actions.FindAction("Player/Guard").bindings.Any(b=>b.path=="<Keyboard>/l") && actions.FindAction("Player/Dodge").bindings.Any(b=>b.path=="<Gamepad>/rightShoulder"),"Existing guard and dodge bindings preserved");
        foreach(bool gamepad in new[]{false,true})
        {
            var device=gamepad ? (InputDevice)InputSystem.AddDevice<Gamepad>() : InputSystem.AddDevice<Keyboard>();
            try
            {
                using(var input=new SessionInput(actions,device))
                {
                    if(gamepad) InputSystem.QueueStateEvent((Gamepad)device,new GamepadState{rightTrigger=1});
                    else InputSystem.QueueStateEvent((Keyboard)device,new KeyboardState(Key.I));
                    InputSystem.Update(); Check((((PlayerButtons)input.Read().buttons)&PlayerButtons.Skill)!=0,"Session command routes Skill for "+device.displayName);
                }
            }
            finally {InputSystem.RemoveDevice(device);}
        }
        var data=skill.equippedSkill;
        Check(data.cast.frames.All(f=>f.sprite && !f.canCancelIntoAttack && !f.canCancelIntoJump && f.hitboxes.Count==0),"Generated cast frames use attack override and no accidental melee / cancels");
        Check(data.cast.frames.Select(f=>f.sprite).Distinct().Count()==4,"Four new cast poses hooked to frame data");
        var prefab=data.projectilePrefab; Check(prefab.emergenceSprites.Length==1 && prefab.flightSprites.Length==3 && prefab.dissipateSprites.Length==2,"Six original dragon sprites wired to emerge / travel / dissipate");
        Check(data.cast.feedback.swingSound && data.cast.feedback.impactSound && data.cast.feedback.impactPrefab,"Existing release and strong impact VFX / SFX references assigned");
        var catalog=AssetDatabase.LoadAssetAtPath<MultiplayerCatalog>(MultiplayerSetup.CatalogPath);
        Check(catalog.AttackId(data.cast)>=0 && prefab.flightSprites.All(s=>catalog.SpriteId(s)>=0),"Multiplayer catalog registers cast and dragon art");
        var snapshot=new PlayerState{meter=.4f,maxMeter=1}; var copy=JsonUtility.FromJson<PlayerState>(JsonUtility.ToJson(snapshot)); Check(Near(copy.meter,.4f)&&Near(copy.maxMeter,1),"Meter resource survives snapshot serialization");
        foreach(var s in data.cast.frames.Select(f=>f.sprite).Concat(prefab.flightSprites).Concat(prefab.emergenceSprites).Concat(prefab.dissipateSprites).Distinct())
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(s)); Check(importer.filterMode==FilterMode.Point && Near(s.pixelsPerUnit,100),"Native-scale point-filtered sprite: "+s.name);
        }
    }
    static void Previews()
    {
        Fixture(5); skill.RequestSkill(); Step(12); Render("ReleaseRight");
        Step(7); Render("TravelRight");
        Fixture(-5); p.motor.Face(-1); skill.RequestSkill(); Step(12); Render("ReleaseLeft");
        Fixture(1.6f); skill.RequestSkill(); Step(12); var shot=skill.LastProjectile;
        for(int i=0;i<20 && shot.AcceptedHits==0;i++)Step(); Render("Impact");
        Check(true,"Rendered actual cast, travel, mirrored release, impact and meter HUD previews");
    }
    static void Render(string name)
    {
        Directory.CreateDirectory("Documentation/ShadowDragonPreview");
        var go=new GameObject("Skill validation camera"); var camera=go.AddComponent<Camera>();
        camera.orthographic=true; camera.orthographicSize=1.55f;
        camera.transform.position=new Vector3(p.motor.Facing<0 ? -1 : 1,.7f,-10);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.035f,.025f,.06f);
        var target=new RenderTexture(1600,900,24); camera.targetTexture=target;
        var canvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var modes=canvases.Select(c=>c.renderMode).ToArray(); var cams=canvases.Select(c=>c.worldCamera).ToArray();
        for(int i=0;i<canvases.Length;i++) { canvases[i].renderMode=RenderMode.ScreenSpaceCamera; canvases[i].worldCamera=camera; canvases[i].planeDistance=1; }
        Canvas.ForceUpdateCanvases();
        foreach(var particles in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) particles.Simulate(.07f,true,true);
        camera.Render(); var previous=RenderTexture.active; RenderTexture.active=target;
        var texture=new Texture2D(1600,900,TextureFormat.RGBA32,false); texture.ReadPixels(new Rect(0,0,1600,900),0,0); texture.Apply();
        File.WriteAllBytes("Documentation/ShadowDragonPreview/"+name+".png",texture.EncodeToPNG()); RenderTexture.active=previous;
        for(int i=0;i<canvases.Length;i++) { canvases[i].renderMode=modes[i]; canvases[i].worldCamera=cams[i]; }
        UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(go);
    }
}
