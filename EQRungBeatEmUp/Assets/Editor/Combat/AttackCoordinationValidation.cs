using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class AttackCoordinationValidation
{
    const string Pending="AttackCoordination.Validation";
    static readonly List<string> results=new List<string>();
    static readonly List<GameObject> fixtures=new List<GameObject>();
    static CombatClock clock; static ComboController player; static EnemyAttackCoordinator coordinator;
    static EnemyCombat[] enemies;
    static AttackCoordinationValidation()=>EditorApplication.update+=Poll;
    public static void GrapplerRegression() { EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath); SessionState.SetString("Coordination.RegressionReport","Documentation/GrapplerValidationResults.txt"); GrapplerValidation.Run(); }
    public static void AmbusherRegression() { EditorSceneManager.OpenScene(HauntedLevelBuilder.ScenePath); SessionState.SetString("Coordination.RegressionReport","Documentation/AmbusherValidationResults.txt"); AmbusherValidation.Run(); }
    [MenuItem("Beat Em Up/Enemies/Attack Coordination/Validate tokens and mixed encounter (Play Mode)")]
    public static void Run()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(AttackCoordinationSetup.ScenePath); SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode();
    }
    static void Check(bool condition,string label) { if(!condition) throw new Exception(label); results.Add("PASS: "+label); }
    static GameObject Keep(GameObject value) { fixtures.Add(value); return value; }
    static AttackCoordinationData Data(AttackTokenCategory c)=>new AttackCoordinationData{requiresMeleeSlot=(c & AttackTokenCategory.Melee)!=0,requiresRangedSlot=(c & AttackTokenCategory.Ranged)!=0,requiresCrowdControlSlot=(c & AttackTokenCategory.CrowdControl)!=0,requiresSupportSlot=(c & AttackTokenCategory.Support)!=0};
    static bool Request(int i,AttackTokenCategory c,float score=0)=>coordinator.RequestAttack(enemies[i],player.transform,enemies[i].aiProfile.attacks[0].attack,Data(c),score);
    static void Tick(int count=1) { for(int i=0;i<count;i++) clock.StepFrame(); }
    static void Reset()
    {
        coordinator.Clear(); coordinator.settings=new AttackCoordinationSettings{minimumGlobalAttackGapFrames=0,randomPriorityVariation=0};
        player.ResetCombo(); player.health.SafeStageProtection=false; player.health.Restore(); player.motor.ResetForStage(Vector2.zero);
        player.GetComponentInChildren<CombatHurtbox>().externalInvulnerable=true;
        for(int i=0;i<enemies.Length;i++)
        {
            var enemy=enemies[i]; if(!enemy) continue;
            enemy.gameObject.SetActive(true); enemy.enabled=true; enemy.attackPlayer.Stop(); enemy.reaction.health.Restore();
            enemy.motor.ResetForStage(new Vector2(i%2==0 ? -2 : 2,0)); enemy.target=player.transform; enemy.passiveTrainingDummy=false;
            enemy.RefreshAI(); CombatClock.Unregister(enemy); enemy.coordinator=coordinator;
        }
    }
    static void Commit(int i)
    {
        Check(coordinator.Owns(enemies[i]),"Atomic grant exists before telegraph for "+enemies[i].name);
        Check(enemies[i].attackPlayer.Play(enemies[i].aiProfile.attacks[0].attack),"Existing authored attack starts: "+enemies[i].name);
        coordinator.Confirm(enemies[i]);
    }
    static void Poll()
    {
        string regression=SessionState.GetString("Coordination.RegressionReport","");
        if(Application.isBatchMode && regression.Length>0 && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling)
        { SessionState.EraseString("Coordination.RegressionReport"); EditorApplication.Exit(File.Exists(regression) && !File.ReadAllText(regression).Contains("FAIL:") ? 0 : 1); return; }
        if(!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending,false); results.Clear(); bool passed=false;
        try
        {
            var flow=Object.FindFirstObjectByType<StageFlowController>(); flow.enabled=false;
            foreach(var actor in Object.FindObjectsByType<CharacterMotor>(FindObjectsSortMode.None)) actor.gameObject.SetActive(false);
            clock=Object.FindFirstObjectByType<CombatClock>(); clock.enabled=false;
            foreach(var old in Object.FindObjectsByType<EnemyAttackCoordinator>(FindObjectsSortMode.None)) old.gameObject.SetActive(false);
            player=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ComboTrackingSetup.PlayerPath))).GetComponent<ComboController>();
            player.GetComponent<PlayerCombatInput>().enabled=false; player.health.maximumHealth=100000; player.health.Restore();
            player.motor.arenaMin=new Vector2(-20,-2); player.motor.arenaMax=new Vector2(20,2);
            coordinator=Keep(new GameObject("Token validation coordinator")).AddComponent<EnemyAttackCoordinator>();
            string[] names={"Rusher","Rusher","Thrower","Screamer","GrapplerBruiser","Ambusher","Prefect"};
            enemies=names.Select((name,i)=> { var brain=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AttackCoordinationSetup.Prefabs+name+".prefab"))).GetComponent<EnemyCombat>(); brain.name=name+"_"+i; brain.motor.arenaMin=new Vector2(-20,-2); brain.motor.arenaMax=new Vector2(20,2); return brain; }).ToArray();
            Categories(); Priority(); Cleanup(); CrowdControl(); Integration(); EditorData();
            results.Add("ALL ATTACK COORDINATION CHECKS PASSED"); passed=true;
        }
        catch(Exception ex) { results.Add("FAIL: "+ex); Debug.LogException(ex); }
        foreach(var fixture in fixtures) if(fixture) Object.DestroyImmediate(fixture); fixtures.Clear();
        Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/AttackCoordinationValidationResults.txt",results); Debug.Log(string.Join("\n",results));
        if(Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1); else EditorApplication.ExitPlaymode();
    }
    static void Categories()
    {
        Reset(); Request(0,AttackTokenCategory.Melee,10); Request(1,AttackTokenCategory.Melee); Request(2,AttackTokenCategory.Ranged); Request(6,AttackTokenCategory.Support); Tick();
        Check(coordinator.Occupied(AttackTokenCategory.Melee)==1 && !coordinator.Owns(enemies[1]),"Default Melee limit is one");
        Check(coordinator.Occupied(AttackTokenCategory.Ranged)==1 && coordinator.Occupied(AttackTokenCategory.Support)==1,"Ranged and Support overlap independently with Melee");
        Request(3,AttackTokenCategory.Ranged|AttackTokenCategory.CrowdControl); Request(4,AttackTokenCategory.Melee|AttackTokenCategory.CrowdControl); Tick();
        Check(!coordinator.Owns(enemies[3]) && !coordinator.Owns(enemies[4]) && coordinator.Occupied(AttackTokenCategory.CrowdControl)==0,"Blocked multi-category requests reserve no partial CC token");
        coordinator.Release(enemies[2]); Request(3,AttackTokenCategory.Ranged|AttackTokenCategory.CrowdControl,100); Tick();
        Check(coordinator.Owns(enemies[3]) && coordinator.Occupied(AttackTokenCategory.Ranged)==1 && coordinator.Occupied(AttackTokenCategory.CrowdControl)==1,"Screamer atomically acquires Ranged plus CC");
        coordinator.Release(enemies[0]); Request(4,AttackTokenCategory.Melee|AttackTokenCategory.CrowdControl); Tick();
        Check(!coordinator.Owns(enemies[4]) && coordinator.Occupied(AttackTokenCategory.Melee)==0,"Grappler cannot grab while Screamer owns CC, even with free Melee");
        coordinator.Release(enemies[3]); Request(4,AttackTokenCategory.Melee|AttackTokenCategory.CrowdControl); Request(5,AttackTokenCategory.Melee|AttackTokenCategory.CrowdControl,-100); Tick();
        Check(coordinator.Owns(enemies[4]) && !coordinator.Owns(enemies[5]),"Only one Grappler/Ambusher CC commitment is approved");
        Reset(); coordinator.settings.maxMeleeAttackers=2; Request(0,AttackTokenCategory.Melee); Request(1,AttackTokenCategory.Melee); Tick();
        Check(coordinator.Occupied(AttackTokenCategory.Melee)==2,"Editable capacity allows two Melee commitments");
        Reset(); coordinator.settings.maxRangedAttackers=0; Request(2,AttackTokenCategory.Ranged); Tick(); Check(!coordinator.Owns(enemies[2]),"Zero category capacity disables new commitments");
        Reset(); coordinator.settings.minimumGlobalAttackGapFrames=20; Request(0,AttackTokenCategory.Melee,100); Request(2,AttackTokenCategory.Ranged); Tick(); Commit(0);
        for(int i=0;i<19;i++) { Request(2,AttackTokenCategory.Ranged); Tick(); }
        Check(!coordinator.Owns(enemies[2]),"20-frame global gap blocks commitments through frame 19"); Request(2,AttackTokenCategory.Ranged); Tick();
        Check(coordinator.Owns(enemies[2]),"Different category can overlap at exactly frame 20");
        Reset(); var mutable=Data(AttackTokenCategory.Melee|AttackTokenCategory.CrowdControl);
        coordinator.RequestAttack(enemies[4],player.transform,enemies[4].aiProfile.attacks[0].attack,mutable,0); Tick(); Commit(4);
        mutable.requiresCrowdControlSlot=false;
        Check(coordinator.Occupied(AttackTokenCategory.CrowdControl)==1,"Live action-data edits cannot remove an existing lease's reserved categories");
        Request(3,AttackTokenCategory.Ranged|AttackTokenCategory.CrowdControl); Tick();
        Check(!coordinator.Owns(enemies[3]),"Atomic lease still protects CC when action configuration changes during attack");
        Reset(); coordinator.settings.useAttackCoordination=false;
        Check(Request(0,AttackTokenCategory.Melee) && !coordinator.Requests.Any(),"Disabling encounter coordination immediately bypasses token requests");
    }
    static void Priority()
    {
        Reset(); Request(0,AttackTokenCategory.Melee); Request(1,AttackTokenCategory.Melee,20); Tick();
        Check(coordinator.Owns(enemies[1]),"Highest priority wins rather than request order"); Commit(1);
        var waiting=coordinator.Requests.Single(r=>r.owner==enemies[0]); float initial=coordinator.Score(waiting);
        for(int i=0;i<30;i++) { Request(0,AttackTokenCategory.Melee); Tick(); }
        Check(coordinator.Score(waiting)>initial,"Waiting priority increases with combat frames");
        enemies[1].attackPlayer.Stop(); Request(0,AttackTokenCategory.Melee); Request(1,AttackTokenCategory.Melee); Tick();
        Check(coordinator.Owns(enemies[0]),"Recent-attacker penalty favors the waiting Rusher");
        Reset(); Request(0,AttackTokenCategory.Melee); Tick(); Commit(0); enemies[0].attackPlayer.Stop(); Request(0,AttackTokenCategory.Melee); Tick();
        Check(coordinator.Owns(enemies[0]),"Single eligible enemy can repeat normally");
        coordinator.Release(enemies[0]); Request(0,AttackTokenCategory.Melee); waiting=coordinator.Requests.Single(); float penalized=coordinator.Score(waiting);
        for(int i=0;i<181;i++) { Request(0,AttackTokenCategory.Melee); coordinator.settings.maxMeleeAttackers=0; Tick(); }
        Check(coordinator.Score(waiting)>penalized+25,"Recent penalty expires while wait priority grows");
    }
    static void Cleanup()
    {
        Reset(); Request(0,AttackTokenCategory.Melee); Tick(); Commit(0); enemies[0].reaction.health.Damage(10000);
        Check(!coordinator.Owns(enemies[0]),"Enemy death releases tokens immediately");
        Reset(); Request(0,AttackTokenCategory.Melee); Tick(); Commit(0); enemies[0].enabled=false;
        Check(!coordinator.Owns(enemies[0]),"Enemy disable releases tokens immediately");
        Reset(); Request(0,AttackTokenCategory.Melee); Tick(); Commit(0); enemies[0].gameObject.SetActive(false);
        Check(!coordinator.Owns(enemies[0]),"Enemy despawn releases tokens immediately");
        Reset(); Request(0,AttackTokenCategory.Melee); Tick(); Commit(0); coordinator.Clear();
        Check(!coordinator.Requests.Any() && !coordinator.LastAttacker,"Encounter reset clears owners, candidates and recent history");
        Reset(); Request(0,AttackTokenCategory.Melee); Tick(); Commit(0); player.health.Damage(1000000); Tick();
        Check(!coordinator.Requests.Any(),"Player death clears target's reservations");
        Reset(); coordinator.settings.maximumTokenHoldFrames=3; Request(0,AttackTokenCategory.Melee); Tick(); Commit(0); Tick(3);
        Check(!coordinator.Owns(enemies[0]) && !enemies[0].attackPlayer.CurrentAttack,"Watchdog cancels broken long-held attack and releases reservation");
        Reset(); Request(0,AttackTokenCategory.Melee); Tick(4);
        Check(!coordinator.Requests.Any(),"Unconsumed grants expire defensively");
        Reset(); Request(0,AttackTokenCategory.Melee); Tick(); Commit(0); player.gameObject.SetActive(false); Tick(); player.gameObject.SetActive(true);
        Check(!coordinator.Owns(enemies[0]) && !enemies[0].attackPlayer.CurrentAttack,"Invalid target cancels active attack and reservation");
    }
    static void CrowdControl()
    {
        Reset(); player.EnterStun(3); Request(3,AttackTokenCategory.Ranged|AttackTokenCategory.CrowdControl); Tick();
        Check(!coordinator.Owns(enemies[3]),"Stunned player blocks new CC");
        for(int i=0;i<3;i++) { Request(3,AttackTokenCategory.Ranged|AttackTokenCategory.CrowdControl); Tick(); }
        Check(!player.IsStunned && !coordinator.Owns(enemies[3]),"Recovery begins CC grace rather than allowing an immediate chain");
        long recovery=CombatClock.CurrentTick;
        while(CombatClock.CurrentTick<recovery+44) { Request(3,AttackTokenCategory.Ranged|AttackTokenCategory.CrowdControl); Tick(); }
        Check(!coordinator.Owns(enemies[3]),"45-frame CC grace remains active before expiry"); Request(3,AttackTokenCategory.Ranged|AttackTokenCategory.CrowdControl); Tick();
        Check(coordinator.Owns(enemies[3]),"CC grace releases at configured combat frame");
        Reset(); player.GetComponentInChildren<CombatHurtbox>().externalInvulnerable=false;
        Check(player.TryEnterGrab(enemies[4].GetComponent<CombatGrabController>()),"Player enters existing Grabbed state"); Request(3,AttackTokenCategory.Ranged|AttackTokenCategory.CrowdControl); Tick();
        Check(!coordinator.Owns(enemies[3]),"Grabbed player blocks new CC");
        Reset(); player.ReceiveHit(new AttackHitboxData{hitType=HitType.KnockDown,hitstunFrames=20},1); Request(3,AttackTokenCategory.Ranged|AttackTokenCategory.CrowdControl); Tick();
        Check(player.IsKnockdownState && !coordinator.Owns(enemies[3]),"Knockdown/GetUp blocks new CC");
        Reset(); coordinator.settings.pauseNewCrowdControlWhilePlayerDisabled=false; player.EnterStun(10); Request(3,AttackTokenCategory.Ranged|AttackTokenCategory.CrowdControl); Tick();
        Check(coordinator.Owns(enemies[3]),"Disabled-player CC protection is configurable");
        Reset(); coordinator.settings.pauseAllAttacksWhilePlayerDisabled=true; player.EnterStun(10); Request(0,AttackTokenCategory.Melee); Tick();
        Check(!coordinator.Owns(enemies[0]),"Optional general aggression pause blocks Melee during incapacity");
        Reset(); var second=Keep(Object.Instantiate(player.gameObject)).GetComponent<ComboController>(); second.health.Restore(); second.ResetCombo(); player.EnterStun(10);
        coordinator.RequestAttack(enemies[3],player.transform,enemies[3].aiProfile.attacks[0].attack,Data(AttackTokenCategory.Ranged|AttackTokenCategory.CrowdControl),100);
        coordinator.RequestAttack(enemies[4],second.transform,enemies[4].aiProfile.attacks[0].attack,Data(AttackTokenCategory.Melee|AttackTokenCategory.CrowdControl),0); Tick();
        Check(coordinator.Owns(enemies[4]) && !coordinator.Owns(enemies[3]),"CC protection follows each target, not a hardcoded Player 1"); second.gameObject.SetActive(false);
    }
    static void Integration()
    {
        Reset();
        AttackTokenCategory[] expected={AttackTokenCategory.Melee,AttackTokenCategory.Melee,AttackTokenCategory.Ranged,AttackTokenCategory.Ranged|AttackTokenCategory.CrowdControl,AttackTokenCategory.Melee|AttackTokenCategory.CrowdControl,AttackTokenCategory.Melee|AttackTokenCategory.CrowdControl,AttackTokenCategory.Support};
        for(int i=0;i<enemies.Length;i++) Check(enemies[i].aiProfile.attacks[0].coordination.Categories==expected[i],"Serialized category mapping: "+enemies[i].name);
        Check(enemies[6].aiProfile.attacks.Single(a=>a.id=="Push").coordination.Categories==AttackTokenCategory.Melee,"Prefect Push uses Melee");
        // Run real AI with both Rushers in range: denied actor must move, granted actor keeps authored timing.
        for(int i=0;i<2;i++) { enemies[i].motor.ResetForStage(new Vector2(i==0 ? -.7f : .7f,0)); CombatClock.Register(enemies[i]); }
        bool moved=false, attacked=false;
        for(int frame=0;frame<260;frame++)
        {
            Tick(); Check(coordinator.Occupied(AttackTokenCategory.Melee)<=1,"Live Rusher overlap cap at frame "+frame);
            moved|=enemies.Take(2).Any(e=>!e.attackPlayer.CurrentAttack && e.motor.MoveInput.sqrMagnitude>.01f && coordinator.Requests.Any(r=>r.owner==e && !r.granted));
            attacked|=enemies.Take(2).Any(e=>e.attackPlayer.CurrentAttack);
            if(enemies.Take(2).Any(e=>e.attackPlayer.CurrentAttack && !coordinator.Owns(e))) throw new Exception("Live attack started without reservation");
        }
        Check(moved && attacked,"Waiting Rusher repositions while another performs its existing attack");
        foreach(var enemy in enemies) CombatClock.Unregister(enemy);
        Reset(); Request(4,expected[4]); Tick(); Commit(4);
        var armor=enemies[4].GetComponent<HitCountArmor>();
        for(int i=0;i<armor.maxArmorHits;i++) enemies[4].GetComponentInChildren<CombatHurtbox>().Receive(new AttackHitboxData{damage=1,hitstunFrames=18,hitstopFrames=0,canHitAirborne=true},1,player.motor);
        Check(armor.ArmorBroken && !coordinator.Owns(enemies[4]),"Actual Grappler armor break releases Melee and CC");
        Reset(); Request(4,expected[4]); Tick(); Commit(4); enemies[4].reaction.EnterStun(30);
        Check(!coordinator.Owns(enemies[4]),"Grappler parry Stun releases both categories");
        Reset(); Request(5,expected[5]); Tick(); Commit(5); enemies[5].reaction.Receive(new AttackHitboxData{hitstunFrames=18},1);
        Check(!coordinator.Owns(enemies[5]),"Interrupted Ambusher telegraph releases both categories");
        Reset(); var thrower=enemies[2]; thrower.motor.ResetForStage(new Vector2(-3,0)); CombatClock.Register(thrower);
        var ranged=thrower.GetComponent<EnemyProjectileAttack>(); int released=ranged.ProjectilesReleased;
        for(int i=0;i<200 && ranged.ProjectilesReleased==released;i++) Tick();
        Check(ranged.ProjectilesReleased>released && ranged.LastProjectile,"Thrower still emits its existing projectile through real AI");
        CombatClock.Unregister(ranged.LastProjectile);
        for(int i=0;i<100 && coordinator.Owns(thrower);i++) Tick();
        Check(!coordinator.Owns(thrower) && ranged.LastProjectile,"Thrower recovery releases Ranged while emitted projectile still exists"); CombatClock.Unregister(thrower);
        foreach(int i in new[]{4,5})
        {
            Reset(); Request(i,expected[i]); Tick(); Commit(i);
            int guard=0; while(enemies[i].attackPlayer.CurrentAttack && guard++<600) Tick(); Tick();
            Check(guard<600 && !coordinator.Owns(enemies[i]),"Missed grab completes authored recovery and releases both categories: "+enemies[i].name);
        }
        foreach(var projectile in Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None)) Object.DestroyImmediate(projectile.gameObject);
        var boss=Keep(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AttackCoordinationSetup.Prefabs+"WhiteGhostBoss.prefab"))).GetComponent<EnemyCombat>(); boss.coordinator=coordinator; boss.target=player.transform;
        Reset(); Request(0,AttackTokenCategory.Melee); Tick();
        var bossData=boss.GetComponent<TotemBossController>().data;
        Check(bossData.phase1.All(a=>a.coordination.ignoreCoordinator) && bossData.phase2.All(a=>a.coordination.ignoreCoordinator),"Boss actions explicitly bypass normal tokens");
        Check(boss.RequestCoordination(bossData.phase1.First(a=>a.action==BossAction.Swipe).attack,bossData.phase1.First(a=>a.action==BossAction.Swipe).coordination),"Boss Swipe ignores occupied minion Melee slot"); boss.gameObject.SetActive(false);
        Check(coordinator.Requests.All(r=>r.owner!=boss),"Boss bypass does not occupy minion categories");
        Reset(); var flow=Object.FindFirstObjectByType<StageFlowController>(); player.gameObject.SetActive(false); foreach(var enemy in enemies) enemy.gameObject.SetActive(false);
        flow.player.gameObject.SetActive(true); flow.player.GetComponent<PlayerCombatInput>().enabled=false; flow.player.GetComponent<CharacterHealth>().maximumHealth=100000; flow.player.GetComponent<CharacterHealth>().Restore();
        flow.Restart(true); flow.Tick(CombatClock.FrameSeconds);
        var spawned=flow.LivingEnemies.Select(h=>h.GetComponent<EnemyCombat>()).ToArray();
        Check(spawned.Length==7 && spawned.Select(e=>e.coordinator).Distinct().Count()==1 && spawned.All(e=>e.coordinator),"Seven-enemy authored encounter binds one shared coordinator");
        var prefect=spawned.Single(e=>e.GetComponent<PrefectSupport>()); int count=flow.RequestRusherBackup(prefect,AssetDatabase.LoadAssetAtPath<GameObject>(AttackCoordinationSetup.Prefabs+"Rusher.prefab"),1,0,0);
        Check(count==1 && flow.LivingEnemies.All(h=>h.GetComponent<EnemyCombat>().coordinator==prefect.coordinator),"Tracked reinforcements inherit encounter coordinator");
        var ownedCoordinator=prefect.coordinator;
        bool tokenViolation=false; var commitments=new HashSet<string>();
        foreach(var brain in spawned) brain.attackPlayer.Started+=attack=> { if(!brain.coordinator.Owns(brain)) tokenViolation=true; commitments.Add(brain.name); };
        for(int frame=0;frame<1600;frame++)
        {
            Tick(); flow.Tick(CombatClock.FrameSeconds);
            foreach(var category in EnemyAttackCoordinator.Categories) if(ownedCoordinator.Occupied(category)>ownedCoordinator.settings.Capacity(category)) tokenViolation=true;
        }
        Check(!tokenViolation && commitments.Count>=4,"Mixed live encounter preserves category caps and multiple enemy types commit naturally");
        Check(commitments.Any(n=>n.Contains("Prefect")),"Prefect performs coordinated Support/Push through existing AI");
        flow.EnterStage(0); Check(!ownedCoordinator.Requests.Any(),"Stage reset clears encounter token state");
        var originalLevel=flow.level; var temporaryLevel=Object.Instantiate(originalLevel); flow.level=temporaryLevel;
        var encounter=temporaryLevel.stages[0].encounters[0];
        encounter.waves=new List<WaveDefinition>{
            new WaveDefinition{waveId="Default",enemySpawns=new List<EnemySpawnDefinition>{new EnemySpawnDefinition{prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AttackCoordinationSetup.Prefabs+"Rusher.prefab"),spawnPoints=new List<Vector2>{new Vector2(5,0)}}}},
            new WaveDefinition{waveId="Override",trigger=WaveTrigger.Time,spawnDelay=.15f,overrideAttackCoordination=true,attackCoordination=new AttackCoordinationSettings{maxMeleeAttackers=2}},
            new WaveDefinition{waveId="Restore defaults",trigger=WaveTrigger.Time,spawnDelay=.4f}};
        flow.Restart(true); flow.Tick(CombatClock.FrameSeconds); var waveCoordinator=flow.LivingEnemies.First().GetComponent<EnemyCombat>().coordinator;
        Check(waveCoordinator.settings.maxMeleeAttackers==1,"First wave uses encounter coordination defaults");
        for(int i=0;i<12;i++) { Tick(); flow.Tick(CombatClock.FrameSeconds); }
        Check(waveCoordinator.settings.maxMeleeAttackers==2,"Starting override wave changes shared encounter intensity");
        for(int i=0;i<15;i++) { Tick(); flow.Tick(CombatClock.FrameSeconds); }
        Check(waveCoordinator.settings.maxMeleeAttackers==1,"Later non-override wave restores encounter defaults");
        flow.level=originalLevel; flow.Restart(true); Object.DestroyImmediate(temporaryLevel);
    }
    static void EditorData()
    {
        var level=AssetDatabase.LoadAssetAtPath<LevelDefinition>(AttackCoordinationSetup.LevelPath);
        Check(level.stages[0].encounters[0].attackCoordination.maxCrowdControlAttackers==1,"Encounter default CrowdControl capacity is one");
        var so=new SerializedObject(level); var settings=so.FindProperty("stages").GetArrayElementAtIndex(0).FindPropertyRelative("encounters").GetArrayElementAtIndex(0).FindPropertyRelative("attackCoordination");
        Check(settings!=null && settings.FindPropertyRelative("minimumGlobalAttackGapFrames")!=null,"Encounter editor exposes serialized slots and combat-frame gaps");
        Undo.RecordObject(level,"Coordination fixture Undo"); level.stages[0].encounters[0].attackCoordination.maxMeleeAttackers=2; EditorUtility.SetDirty(level); Undo.FlushUndoRecordObjects();
        Undo.PerformUndo(); Check(level.stages[0].encounters[0].attackCoordination.maxMeleeAttackers==1,"Coordination edits support Undo");
        Undo.PerformRedo(); Check(level.stages[0].encounters[0].attackCoordination.maxMeleeAttackers==2,"Coordination edits support Redo");
        level.stages[0].encounters[0].attackCoordination.maxMeleeAttackers=1; EditorUtility.SetDirty(level); AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AttackCoordinationSetup.LevelPath,ImportAssetOptions.ForceUpdate);
        Check(AssetDatabase.LoadAssetAtPath<LevelDefinition>(AttackCoordinationSetup.LevelPath).stages[0].encounters[0].attackCoordination.minimumGlobalAttackGapFrames==20,"Coordination settings persist after asset reload");
        Check(new WaveDefinition().attackCoordination!=null && !new WaveDefinition().overrideAttackCoordination,"Wave overrides are serialized and opt-in");
        Check(coordinator.Describe(enemies[0])!=null,"Runtime ownership debug description is available");
    }
}
