using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class PlayerSanctuaryValidation
{
    const string Pending="Sanctuary.Validation";
    static readonly List<string> results=new List<string>();
    static StageFlowController flow;
    static PlayerHubController hub;
    static MetaProgress meta;
    static CombatClock clock;
    static bool waiting,ready,failed;
    static string permanent;
    static PlayerSanctuaryValidation()
    {
        if(SessionState.GetBool(Pending,false)) MetaSave.DirectoryOverride=SessionState.GetString("Sanctuary.SaveDirectory",null);
        EditorApplication.update+=Poll;
    }
    [MenuItem("Beat Em Up/Hub/Validate sanctuary (Play Mode)")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        MetaSave.DirectoryOverride=Path.GetFullPath("Temp/HubValidationSaves-"+Guid.NewGuid().ToString("N"));
        SessionState.SetString("Sanctuary.SaveDirectory",MetaSave.DirectoryOverride);
        MetaSave.Save(0,new MetaProfile{characterId="gray-shirt",essence=1000});
        EditorSceneManager.OpenScene(PlayerSanctuarySetup.ScenePath);
        SessionState.SetBool(Pending,true); SessionState.SetBool("Sanctuary.Finished",false); EditorApplication.EnterPlaymode();
    }
    static void Check(bool value,string label) { if(!value) throw new Exception(label); results.Add("PASS: "+label); }
    static void Step(int count) { for(int i=0;i<count;i++) { Physics2D.SyncTransforms(); clock.StepFrame(); } }
    static void Poll()
    {
        if(SessionState.GetBool("Sanctuary.Finished",false) && !EditorApplication.isPlayingOrWillChangePlaymode && Application.isBatchMode) { EditorApplication.Exit(SessionState.GetInt("Sanctuary.Exit",1)); return; }
        if(waiting && ready)
        {
            waiting=false;
            try
            {
                Check(hub.InHub && flow.StageIndex==0,"Death automatically returns to actual Hub after configured transition");
                Check(Vector2.Distance(flow.player.transform.position,hub.definition.spawn)<.001f && flow.player.Facing==1,"Death always respawns at Buddha point facing into Hub");
                Check(!flow.player.GetComponent<CharacterHealth>().IsDead && flow.player.GetComponent<CharacterHealth>().SafeStageProtection,"Death return restores protected health");
                Check(flow.player.GetComponent<RunBuildState>().Acquired.Count==0,"Temporary run build clears on death return");
                Check(JsonUtility.ToJson(meta.profile)==permanent,"Death preserves permanent character/stats/skill/currency");
                Check(!flow.LivingEnemies.Any() && !Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None).Any(),"Returning Hub removes stage enemies and projectiles");
                Check(Vector2.Distance(flow.player.transform.position,hub.definition.stations[2])>5,"Last used station cannot become a death checkpoint");
                Capture("BuddhaSpawn");
            }
            catch(Exception ex) { failed=true; results.Add("FAIL: "+ex); }
            Finish(); return;
        }
        if(!SessionState.GetBool(Pending,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        SessionState.SetBool(Pending,false); results.Clear(); failed=false;
        try
        {
            flow=Object.FindFirstObjectByType<StageFlowController>(); hub=flow.GetComponent<PlayerHubController>(); clock=Object.FindFirstObjectByType<CombatClock>();
            meta=flow.player.GetComponent<MetaProgress>(); clock.enabled=false; flow.enabled=false;
            flow.player.GetComponent<PlayerCombatInput>().enabled=false;
            flow.player.GetComponent<CharacterHealth>().SafeStageProtection=flow.CurrentStage.IsSafeStage;
            Check(hub && hub.InHub && flow.CurrentStage.stageId==PlayerHubSetup.HubId,"PlayerHub scene starts in safe Hub using shared stage flow");
            Check(flow.player.GetComponent<PlayerCharacterLoadout>().character.characterId=="gray-shirt","Saved character applies on fresh scene spawn");
            Check(hub.definition.width>=35 && flow.CurrentStage.artWidth==hub.definition.width,"Hub spans more than five reference gameplay view widths");
            Check(flow.CurrentStage.encounters.Count==0 && flow.Destructibles.Count==0 && !flow.LivingEnemies.Any(),"Sanctuary contains no normal enemies or destructibles");
            var protectedHealth=flow.player.GetComponent<CharacterHealth>(); float protectedHp=protectedHealth.Current;
            Check(!protectedHealth.Damage(100000) && protectedHealth.Current==protectedHp,"Hub rejects direct lethal damage without spending run protection");
            Check(!flow.player.GetComponentInChildren<CombatHurtbox>().Receive(new AttackHitboxData{damage=100000,canHitGrounded=true},-1),"Hub rejects combat damage before player hit reactions");
            Check(flow.CurrentStage.completionMode==StageCompletion.Event,"Walking near gate cannot accidentally launch a run");
            Check(Object.FindObjectsByType<HubLandmarks>(FindObjectsSortMode.None).Count(m=>!m.editorPreview)==1,"One actual modular environment is instantiated");
            var markers=Object.FindObjectsByType<HubLandmarks>(FindObjectsSortMode.None).Single(m=>!m.editorPreview);
            var panels=markers.GetComponentsInChildren<SpriteRenderer>();
            Check(panels.Length==5 && panels.Select(p=>p.sprite.texture).Distinct().Count()==5,"Five unique environment modules use distinct artwork");
            Check(panels.All(p=>Mathf.Approximately(p.transform.localScale.x,p.transform.localScale.y)),"Environment panels scale uniformly without horizontal stretching");
            Check(markers.transform.Find("HubPlayerSpawnPoint") && !markers.GetComponentInChildren<CombatHurtbox>() && !markers.GetComponentInChildren<DestructibleObject>(),"Buddha and spawn landmark have no attackable/destructible components");
            float x=flow.player.transform.position.x; flow.player.MoveInput=Vector2.right; Step(12); flow.player.MoveInput=Vector2.zero;
            Check(flow.player.transform.position.x>x+.4f,"Hub player physically walks with shared motor");
            Check(!hub.Interact(flow.player),"Stations reject interaction outside physical radius");
            flow.player.ResetForStage(new Vector2(-18,0)); flow.framing.ApplyFraming(0,true); float left=flow.framing.transform.position.x;
            flow.player.ResetForStage(new Vector2(18,0)); flow.framing.ApplyFraming(0,true); float right=flow.framing.transform.position.x;
            var view=flow.framing.GetComponent<Camera>(); float half=view.orthographicSize*view.aspect;
            Check(right>left+20 && left-half>=-19.01f && right+half<=19.01f,"Existing camera follows horizontally and stays within Hub edges");
            Check(!flow.ActiveCameraBounds.HasValue,"Hub has no combat camera lock");
            flow.player.ResetForStage(hub.definition.stations[3]); Capture("WorldGate");
            flow.player.ResetForStage(hub.definition.stations[1]); Capture("StatPavilion");
            flow.player.ResetForStage(hub.definition.stations[0]); Check(hub.Interact(flow.player) && meta.OpenStation==0,"Approach and interaction open character station");
            Capture("CharacterWardrobe");
            Check(hub.Execute(flow.player,HubAction.Character,0),"BlueShirtGuy can be selected in physical Hub");
            Check(flow.player.GetComponent<PlayerCharacterLoadout>().character.characterId=="blue-shirt" && MetaSave.Load(0,hub.definition).characterId=="blue-shirt","Immediate shared loadout and saved character update together");
            Check(hub.Execute(flow.player,HubAction.Character,1),"Character 2 can be reselected without restarting Hub");
            Check(flow.player.GetComponent<ComboController>().groundCombo[0]==Resources.Load<MultiplayerCatalog>("MultiplayerCatalog").characters[1].groundCombo[0],"Runtime character switch applies own frame attack data");
            Check(!hub.Execute(flow.player,HubAction.Character,999),"Unknown character is rejected");
            Check(hub.Execute(flow.player,HubAction.Close,0) && meta.OpenStation==-1,"Station closes and restores normal control");
            flow.player.ResetForStage(hub.definition.stations[1]); Check(hub.Interact(flow.player),"Base-stat station opens through existing interaction path");
            for(int i=0;i<4;i++) Check(hub.Execute(flow.player,HubAction.Stat,i),"Persistent stat upgrade succeeds: "+i);
            Check(meta.profile.healthLevel==1 && meta.profile.attackLevel==1 && meta.profile.defenseLevel==1 && meta.profile.meterLevel==1,"Four reusable saved base-stat levels increment independently");
            Check(flow.player.GetComponent<CharacterHealth>().EffectiveMaximum==210,"Persistent health increases shared effective maximum");
            Check(Mathf.Approximately(meta.DamageMultiplier(false),1.05f) && Mathf.Approximately(meta.DamageReduction,.025f) && Mathf.Approximately(meta.MeterMultiplier,1.1f),"Attack, defense and meter multipliers resolve from persistent data");
            int money=meta.profile.essence; meta.profile.essence=0;
            Check(!hub.Execute(flow.player,HubAction.Stat,0) && meta.profile.healthLevel==1,"Unaffordable upgrade cannot modify levels"); meta.profile.essence=money;
            Check(!hub.Execute(flow.player,HubAction.Stat,99),"Invalid stat request is rejected");
            meta.profile.healthLevel=hub.definition.maximumLevel;
            Check(!hub.Execute(flow.player,HubAction.Stat,0),"Maximum stat level is enforced"); meta.profile.healthLevel=1;
            hub.Execute(flow.player,HubAction.Close,0); flow.player.ResetForStage(hub.definition.stations[2]); hub.Interact(flow.player);
            Check(hub.Execute(flow.player,HubAction.Skill,0) && meta.profile.graySkillLevel==1 && meta.profile.blueSkillLevel==0,"Skill shrine saves separate equipped-character skill level");
            Check(Mathf.Approximately(meta.DamageMultiplier(true),1.155f),"Skill power composes with base attack without changing authored assets");
            Capture("SkillShrine"); meta.Save(); var restored=MetaSave.Load(0,hub.definition);
            Check(JsonUtility.ToJson(restored)==JsonUtility.ToJson(meta.profile),"Profile round-trip persists selected character, four stats, skill and currency");
            File.WriteAllText(MetaSave.PathFor(0),"invalid json");
            Check(MetaSave.Load(0,hub.definition).characterId=="gray-shirt","Corrupt primary save recovers a valid backup"); meta.Save();
            hub.Execute(flow.player,HubAction.Close,0); flow.player.ResetForStage(hub.definition.stations[3]); hub.Interact(flow.player);
            Check(hub.Execute(flow.player,HubAction.EnterWorld,0) && flow.StageIndex==1,"Physical World 1 gate starts existing Entrance Gate stage");
            Check(meta.OpenStation==-1 && !flow.player.GetComponent<CharacterHealth>().SafeStageProtection,"Starting run closes Hub UI and restores normal combat damage");
            var health=flow.player.GetComponent<CharacterHealth>(); float hp=health.Current; health.Damage(100);
            Check(Mathf.Abs(health.Current-(hp-97.5f))<.001f,"Permanent defense reduces damage through shared health path"); health.Restore();
            var enemy=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Prefabs/BadGuy.prefab")); enemy.GetComponent<EnemyCombat>().enabled=false;
            enemy.GetComponent<CharacterMotor>().ResetForStage(flow.player.transform.position+Vector3.right*.7f);
            var target=enemy.GetComponent<CharacterHealth>(); float enemyHp=target.Current;
            var box=enemy.GetComponentInChildren<CombatHurtbox>();
            var meter=flow.player.GetComponent<PlayerMeter>(); meter.TrySpend(1);
            Check(box.Receive(new AttackHitboxData{damage=20,canHitGrounded=true,hitstunFrames=1},1,flow.player) && Mathf.Abs(target.Current-(enemyHp-21))<.001f,"Permanent attack power affects real accepted hit damage"); Object.DestroyImmediate(enemy);
            Check(Mathf.Abs(meter.CurrentMeter-.066f)<.001f,"Permanent meter gain scales accepted-hit gain while keeping one-bar cap");
            hub.ReturnToHub();
            Check(hub.InHub && Vector2.Distance(flow.player.transform.position,hub.definition.spawn)<.001f,"Explicit return also uses Buddha spawn");
            Check(meta.profile.graySkillLevel==1 && meta.profile.healthLevel==1,"Explicit return retains permanent upgrades");
            hub.BeginRun(); var build=flow.player.GetComponent<RunBuildState>(); var upgrade=flow.RunUpgrades.pool.upgrades.First(u=>build.CanAcquire(u)); build.Acquire(upgrade);
            permanent=JsonUtility.ToJson(meta.profile); health.SafeStageProtection=false; for(int i=0;i<4 && !health.IsDead;i++) health.Damage(100000);
            Check(health.IsDead && !hub.InHub,"Run death begins outside Hub before transition");
            waiting=true; ready=false; clock.StartCoroutine(Wait());
        }
        catch(Exception ex) { failed=true; results.Add("FAIL: "+ex); Debug.LogException(ex); Finish(); }
    }
    static System.Collections.IEnumerator Wait() { yield return new WaitForSecondsRealtime(2.8f); ready=true; }
    static void Capture(string name)
    {
        flow.framing.ApplyFraming(0,true); var camera=flow.framing.GetComponent<Camera>(); var render=new RenderTexture(1280,720,24); var previous=camera.targetTexture; var active=RenderTexture.active;
        try { camera.targetTexture=render; camera.Render(); RenderTexture.active=render; var image=new Texture2D(1280,720,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply(); Directory.CreateDirectory("Documentation/PlayerSanctuaryPreview"); File.WriteAllBytes("Documentation/PlayerSanctuaryPreview/"+name+".png",image.EncodeToPNG()); Object.DestroyImmediate(image); }
        finally { camera.targetTexture=previous; RenderTexture.active=active; render.Release(); Object.DestroyImmediate(render); }
    }
    static void Finish()
    {
        results.Add(failed ? "PLAYER SANCTUARY FAILED" : "ALL PLAYER SANCTUARY CHECKS PASSED"); Directory.CreateDirectory("Documentation"); File.WriteAllLines("Documentation/PlayerSanctuaryValidationResults.txt",results); Debug.Log(string.Join("\n",results));
        MetaSave.DirectoryOverride=null; SessionState.SetBool("Sanctuary.Finished",true); SessionState.SetInt("Sanctuary.Exit",failed ? 1 : 0); EditorApplication.ExitPlaymode();
    }
}
