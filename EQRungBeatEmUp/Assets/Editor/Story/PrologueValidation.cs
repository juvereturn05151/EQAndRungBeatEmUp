using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using BeatEmUp.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class PrologueValidation
{
    const string Pending="Prologue.Validation";
    static readonly List<string> results=new List<string>();
    static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();
    static PrologueDirector story;
    static ComboController player;
    static CombatClock clock;
    static float deadline;
    static bool originalRunInBackground;
    static PrologueValidation() { EditorApplication.update+=Poll; }
    [MenuItem("Beat Em Up/Story/Validate playable prologue (Play Mode)")]
    public static void Run() => StartValidation(false);
    [MenuItem("Beat Em Up/Story/Validate Cream capture (Play Mode)")]
    public static void RunCapture() => StartValidation(true);
    static void StartValidation(bool captureOnly)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)return;
        SessionState.SetString("Prologue.SaveBackup",PlayerPrefs.GetString(StoryProgress.SaveKey,""));StoryProgress.Reset();
        SessionState.SetBool("Prologue.CaptureOnly",captureOnly);
        if(captureOnly)new StoryProgress{checkpoint=5}.Save();
        EditorSceneManager.OpenScene("Assets/EQ_Rung_BeatEmUp/Scenes/PlayerHub.unity");SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();
    }
    static void Check(bool condition,string message) { if(!condition)throw new Exception(message);results.Add("PASS: "+message);Debug.Log("PROLOGUE CHECK: "+message); }
    static void Poll()
    {
        if(File.Exists("Temp/CreamCapture.validate-request") && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating) { File.Delete("Temp/CreamCapture.validate-request");RunCapture();return; }
        if(File.Exists("Temp/PrologueValidation.request") && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating) { File.Delete("Temp/PrologueValidation.request");Run();return; }
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling)return;
        try
        {
            if(SessionState.GetBool(Pending,false))
            {
                story=Object.FindFirstObjectByType<PrologueDirector>();if(!story || !story.ActiveStory)return;
                originalRunInBackground=Application.runInBackground;Application.runInBackground=true;
                SessionState.SetBool(Pending,false);results.Clear();stack.Clear();deadline=Time.realtimeSinceStartup+180;
                clock=Object.FindFirstObjectByType<CombatClock>();clock.enabled=false;player=story.GetComponent<StageFlowController>().player.GetComponent<ComboController>();
                player.GetComponent<PlayerCombatInput>().enabled=false;stack.Push(SessionState.GetBool("Prologue.CaptureOnly",false) ? CaptureExercise() : Exercise());
            }
            if(stack.Count==0)return;
            if(Time.realtimeSinceStartup>deadline)throw new Exception("Prologue validation timed out at phase "+story.Phase+", tutorial "+story.Progress.tutorialStep);
            var iterator=stack.Peek();if(!iterator.MoveNext())stack.Pop();else if(iterator.Current is IEnumerator nested)stack.Push(nested);
            if(stack.Count==0)Finish(false);
        }
        catch(Exception ex) { results.Add("FAIL: "+ex);Debug.LogException(ex);Finish(true); }
    }
    static void Finish(bool failed)
    {
        Application.runInBackground=originalRunInBackground;
        stack.Clear();results.Add(failed ? "PROLOGUE VALIDATION FAILED" : "ALL PROLOGUE CHECKS PASSED");Directory.CreateDirectory("Documentation");File.WriteAllLines(SessionState.GetBool("Prologue.CaptureOnly",false) ? "Documentation/CreamCapturePlayModeResults.txt" : "Documentation/PrologueValidationResults.txt",results);
        string original=SessionState.GetString("Prologue.SaveBackup","");if(string.IsNullOrEmpty(original))PlayerPrefs.DeleteKey(StoryProgress.SaveKey);else PlayerPrefs.SetString(StoryProgress.SaveKey,original);PlayerPrefs.Save();
        Debug.Log(string.Join("\n",results));EditorApplication.ExitPlaymode();
    }
    static IEnumerator Until(Func<bool> condition,string message,float seconds=12)
    {
        float end=Time.realtimeSinceStartup+seconds;while(!condition()) { if(Time.realtimeSinceStartup>end)throw new Exception(message+" (phase "+story.Phase+", step "+story.Progress.tutorialStep+")");yield return null; }Check(true,message);
    }
    static IEnumerator CaptureExercise()
    {
        var def=story.Definition;
        yield return Until(()=>story.Cutscenes.Current==def.kidnapping,"Kidnapping checkpoint starts its actual timeline");
        yield return Until(()=>story.Actor("Cream").GetComponent<StorySpriteAnimation>()?.frames?.FirstOrDefault()?.name.StartsWith("Cream_Startled_")==true,"Cream and both captors reach the grabbing animation");
        Check(new[]{"Captor1","Captor2"}.All(name=>story.Actor(name).GetComponent<StorySpriteAnimation>().frames[0].name.StartsWith("ThaiCaptor_Grab_")),"Both Thai captors play grabbing frames together");
        Check(new[]{"EQ","Rung"}.All(name=>story.Actor(name).GetComponentsInChildren<SpriteRenderer>().All(r=>!r.enabled)),"Heroes remain hidden during capture");
        yield return Until(()=>story.Actor("Cream").GetComponent<StorySpriteAnimation>().frames[0].name.StartsWith("Cream_Struggle_"),"Cream resists while both captors hold her");
        Check(new[]{"Captor1","Captor2"}.All(name=>story.Actor(name).GetComponent<StorySpriteAnimation>().frames[0].name.StartsWith("ThaiCaptor_Hold_")),"Captors hold during Cream's struggle");
        yield return Until(()=>story.Actor("Cream").GetComponent<StorySpriteAnimation>().frames[0].name.StartsWith("Cream_Escort_"),"All actors switch to escort animation");
        var actors=new[]{"Cream","Captor1","Captor2"}.Select(story.Actor).ToArray();
        Check(actors[1].GetComponent<StorySpriteAnimation>().frames[0].name.StartsWith("ThaiCaptor_Escort_") && actors[2].GetComponent<StorySpriteAnimation>().frames[0].name.StartsWith("ThaiCaptor_Backstep_"),"Captors use forward and backward escort animation");
        story.Cutscenes.PauseCutscene();
        var sprites=actors.Select(a=>a.GetComponent<SpriteRenderer>().sprite).ToArray();var positions=actors.Select(a=>a.transform.position).ToArray();
        float until=Time.realtimeSinceStartup+.3f;while(Time.realtimeSinceStartup<until)yield return null;
        Check(actors.Select((a,i)=>a.transform.position==positions[i] && a.GetComponent<SpriteRenderer>().sprite==sprites[i]).All(v=>v),"Pause freezes all capture animation and movement");
        story.Cutscenes.ResumeCutscene();
        yield return Until(()=>actors[0].transform.position.x>1.2f,"Escort resumes and moves Cream right");
        Check(actors.All(a=>a.GetComponent<SpriteRenderer>().sprite!=sprites[Array.IndexOf(actors,a)]),"All three animations advance after resume");
        Check(Mathf.Abs(actors[0].transform.position.x-actors[1].transform.position.x-.84f)<.04f && Mathf.Abs(actors[2].transform.position.x-actors[0].transform.position.x-.91f)<.04f,"Escort keeps both hand-contact distances");
        Check(!actors[1].GetComponent<SpriteRenderer>().flipX && actors[2].GetComponent<SpriteRenderer>().flipX,"Front captor keeps facing Cream while walking backwards");
        Capture("Cream_AnimatedEscort");yield return null;
        yield return Until(()=>story.UI.IsTalking,"Heroes' dialogue begins after capture");
        Check(actors.All(a=>!a.activeSelf && a.GetComponent<StorySpriteAnimation>().frames==null),"Capture completion clears all actors and animation loops");
        Check(actors[0].GetComponent<SpriteRenderer>().sprite==def.cream,"Cream's original pose is restored before rescue");
        story.RequestSkip();yield return Until(()=>story.Phase==6 && !story.Cutscenes.IsCutscenePlaying,"Capture finishes and returns control to tutorial");
        // Replay a transient copy, then skip during grabbing to exercise early cleanup.
        var replay=Object.Instantiate(def.kidnapping);replay.playsOnce=false;replay.id="CaptureValidationReplay";
        story.Cutscenes.PlayCutscene(replay);
        yield return Until(()=>actors[0].GetComponent<StorySpriteAnimation>()?.frames?.FirstOrDefault()?.name.StartsWith("Cream_Startled_")==true,"Replaying capture restarts fresh grabbing frames");
        story.RequestSkip();yield return Until(()=>!story.Cutscenes.IsCutscenePlaying,"Skipping during grabbing completes the timeline");
        Check(actors.All(a=>!a.activeSelf && a.GetComponent<StorySpriteAnimation>().frames==null),"Early skip removes captors and clears Cream's animation");
        story.Apply(new CutsceneEvent{action=StoryAction.Spawn,target="Cream",position=new Vector3(1.4f,0),duration=0});
        Check(actors[0].GetComponent<SpriteRenderer>().sprite==def.cream,"Cream respawns in her normal pose for rescue");
        Object.Destroy(replay);
    }
    static void Step(int count=1) { var movement=player.motor.MoveInput;player.GetComponent<PlayerCombatInput>().enabled=false;player.motor.MoveInput=movement;for(int i=0;i<count;i++) { Physics2D.SyncTransforms();clock.StepFrame(); } }
    static IEnumerator PracticeReady(int step) => Until(()=>story.Progress.tutorialStep==step && story.Instruction.StartsWith((step+1)+"/9") && (step==0 || Object.FindObjectsByType<EnemyCombat>(FindObjectsSortMode.None).Any(e=>!e.reaction.health.IsDead)),"Practice "+(step+1)+" is ready");
    static void Target(bool restoreEnemy=true)
    {
        player.health.Restore();player.ResetCombo();player.motor.ResetForStage(Vector2.zero);player.motor.Face(1);
        foreach(var enemy in Object.FindObjectsByType<EnemyCombat>(FindObjectsSortMode.None))
        { enemy.enabled=false;enemy.attackPlayer.Stop();enemy.motor.ResetForStage(new Vector2(.65f,0));enemy.motor.arenaMin=new Vector2(.57f,-.08f);enemy.motor.arenaMax=new Vector2(.73f,.08f);if(restoreEnemy)enemy.reaction.health.Restore(); }
    }
    static IEnumerator Route(bool launcher=false,bool aerial=false,bool restoreEnemy=true)
    {
        Target(restoreEnemy);player.RequestAttack();Step(player.groundCombo[0].FirstActiveFrame);player.RequestAttack();
        int limit=120;while(player.CurrentAttack!=player.groundCombo[1] && limit-->0)Step();Check(limit>0,"Ground combo accepts second punch");
        Step(player.groundCombo[1].FirstActiveFrame);if(launcher)player.RequestLauncher();else player.RequestAttack();
        var final=launcher ? player.launcher : player.groundCombo[2];limit=120;while(player.CurrentAttack!=final && limit-->0)Step();Check(limit>0,launcher ? "Grounded launcher route starts" : "Headbutt combo route starts");
        if(aerial)
        {
            player.RequestJump();limit=240;while(player.motor.IsGrounded && limit-->0)Step();Check(limit>0,"Launcher allows manual jump follow-up");
            foreach(var attack in player.airCombo)
            {
                limit=150;while(player.CurrentAttack!=attack && limit-->0) { player.RequestAttack();Step(); }
                Check(limit>0,"Air combo enters "+attack.attackName);
            }
            Step(90);
        }
        else Step(final.TotalFrames+80);
        yield return null;
    }
    static void Capture(string name)
    {
        Directory.CreateDirectory("Documentation/ProloguePreview");ScreenCapture.CaptureScreenshot("Documentation/ProloguePreview/"+name+".png",Screen.width<640 ? 4 : 1);
    }
    static IEnumerator Exercise()
    {
        var def=story.Definition;var db=def.dialogue;
        Check(def.schoolFair && def.hideout && def.sanctuary && def.prapot && def.cream && def.chanai,"All story environment and NPC assets assigned");
        Check(def.student && def.studentGirl && def.student!=def.studentGirl && def.possessedStudentPrefab.GetComponent<EnemyCombat>().role==EnemyRole.Rusher,"Good student variants and possessed Rusher prefab assigned");
        var female=def.possessedSchoolgirlPrefab.GetComponent<EnemyCombat>();
        var male=def.possessedStudentPrefab.GetComponent<EnemyCombat>();
        Check(female.role==EnemyRole.Rusher && female.aiProfile!=male.aiProfile && female.attack!=male.attack,"Female Rusher has separate combat assets with Rusher behavior");
        Check(female.attack.TotalFrames==male.attack.TotalFrames && female.attack.FirstActiveFrame==male.attack.FirstActiveFrame && female.aiProfile.attacks.All(a=>a.attack.frames.All(f=>!f.sprite || AssetDatabase.GetAssetPath(f.sprite).StartsWith(FemaleRusherSetup.Root))),"Female attacks preserve Rusher timing and use female sprites throughout");
        var femaleController=(AnimatorOverrideController)def.possessedSchoolgirlPrefab.GetComponentInChildren<Animator>().runtimeAnimatorController;
        var femaleClips=new List<KeyValuePair<AnimationClip,AnimationClip>>();femaleController.GetOverrides(femaleClips);
        Check(femaleClips.All(pair=>pair.Value && AnimationUtility.GetObjectReferenceCurveBindings(pair.Value).All(binding=>AnimationUtility.GetObjectReferenceCurve(pair.Value,binding).All(key=>!(key.value is Sprite) || AssetDatabase.GetAssetPath(key.value).StartsWith(FemaleRusherSetup.Root)))),"Female locomotion and reaction animations keep female artwork");
        Check(Enumerable.Range(0,4).All(i=>story.Actor("Student"+i).GetComponent<StoryStudentActivity>().schoolgirl==(i%2==1)),"Opening fair uses good students, alternating boy and girl");
        Check(new[]{def.studentIceCream,def.studentDrink,def.studentPhone,def.studentWave}.All(frames=>frames.Length==2 && frames.All(f=>f)) && def.escapeConceptArt && def.escape,"Student activity frames and escape concept art are assigned");
        Check(db.Find("chanai-fair").lines.Any(l=>l.thai=="ผ่านมาตั้ง 16 ปี... พวกเจ้ายังมีชีวิตอยู่อีกหรือ?"),"Canon school-fair recognition line is exact");
        Check(db.Find("chanai-fair").lines.Any(l=>l.thai=="มึงเป็นใครวะ!?") && db.Find("chanai-fair").lines.Any(l=>l.thai=="หึ... จำข้าไม่ได้จริง ๆ สินะ"),"Heroes do not recognize Chanai");
        Check(db.Find("sanctuary-seal").lines.Any(l=>l.thai=="ความทรงจำของพวกเธอไม่ได้หายไปเอง...") && db.Find("sanctuary-seal").lines.Any(l=>l.thai=="มีใครบางคนจงใจลบมันออกไป"),"Prapot describes intentional memory erasure");
        Check(db.Find("return-present").lines.Any(l=>l.thai=="ใครครับอาจารย์?") && db.Find("return-present").lines.Any(l=>l.thai=="ชัยนัย... คนที่พวกเธอเพิ่งเจอที่โรงเรียน"),"Prapot identifies Chanai only after the flashback");
        Check(db.Find("flashback-go").lines[0].thai=="ไปกันเถอะเพื่อน!" && db.Find("flashback-go").lines[1].thai=="เออ!","Flashback's first spoken lines match the requested opening");
        Check(!player.GetComponent<PlayerSkillController>().enabled,"Powers are locked before the awakening");
        Check(!story.GetComponent<StageFlowController>().enabled && !story.GetComponent<PlayerHubController>().enabled,"Existing stage and hub simulation suspend during story");
        yield return Until(()=>story.Cutscenes.IsCutscenePlaying,"New story starts with reunion");
        var eq=story.Actor("EQ");var rung=story.Actor("Rung");
        yield return Until(()=>new[]{eq,rung}.All(hero=>hero.GetComponent<CharacterAnimation>().animator.GetCurrentAnimatorStateInfo(0).IsName("Walk")),"Both heroes walk simultaneously during their reunion approach");
        float eqStart=eq.transform.position.x,rungStart=rung.transform.position.x;
        yield return Until(()=>eq.transform.position.x>eqStart+.15f && rung.transform.position.x<rungStart-.15f,"EQ and Rung both move toward each other");
        Check(Mathf.Abs(eq.transform.position.y-rung.transform.position.y)<.001f && !eq.GetComponentInChildren<SpriteRenderer>().flipX && rung.GetComponentInChildren<SpriteRenderer>().flipX,"Reunion approach keeps both heroes face-to-face in the same lane");
        Check(new[]{eq,rung}.All(hero=>hero.GetComponent<CharacterAnimation>().animator.speed<=.66f),"Reunion approach uses a relaxed walk cycle");
        Capture("SchoolFair_ReunionApproach");
        yield return Until(()=>story.UI.IsTalking,"Reunion dialogue waits until both heroes have arrived");
        Check(new[]{eq,rung}.All(hero=>hero.GetComponent<CharacterAnimation>().animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")) && Mathf.Abs(rung.transform.position.x-eq.transform.position.x-1.25f)<.01f,"Both heroes stop at a comfortable conversation distance");
        var iceCream=story.Actor("Student0").GetComponent<StoryStudentActivity>();
        yield return Until(()=>story.Actor("Student0").GetComponent<SpriteRenderer>().sprite==iceCream.frames[1],"Ice-cream student changes activity pose before possession");
        Capture("SchoolFair_StudentActivities");yield return null;
        long tick=clock.FrameNumber;story.Cutscenes.PauseCutscene();Step(4);Check(clock.FrameNumber==tick && story.UI.Paused,"Cutscene pause freezes combat and dialogue presentation");story.Cutscenes.ResumeCutscene();
        story.RequestSkip();yield return Until(()=>story.Phase==1,"Skipping reunion reaches playable fair walk");
        player.animationDriver.Play("Walk",true);player.animationDriver.animator.Update(0);
        story.SetCinematic(true);
        Check(new[]{"EQ","Rung"}.All(name=>story.Actor(name).GetComponent<CharacterAnimation>().animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")),"Locking the intro resets both heroes from walking to idle immediately");
        story.SetCinematic(false);
        Check(player.motor.arenaMin.x<=-12 && player.motor.arenaMax.x>=12,"Festival has a wide bidirectional exploration area");
        Check(def.festivalChickenStore && def.festivalPepsiStore && story.Actor("FestivalChickenStore") && story.Actor("FestivalPepsiStore"),"Both interactable festival stores use their authored sprites");
        var festivalStart=player.motor.transform.position;
        player.motor.ResetForStage(new Vector2(def.festivalDestination,-.7f));yield return null;yield return null;
        Check(story.Phase==1 && !story.FestivalStoresCompleted,"Reaching the old festival destination cannot bypass the two store memories");
        Check(!story.TryInteractFestivalStore(player.motor),"Store interaction rejects players outside the talk radius");
        player.motor.ResetForStage(festivalStart);
        var festival=Object.FindObjectsByType<SpriteRenderer>().First(r=>r.name=="Prologue environment");
        Check(festival.bounds.size.x>=26,"Panorama covers the expanded festival map");
        Check(AssetDatabase.GetAssetPath(def.schoolFair).EndsWith("SchoolFairPanorama.png") && def.schoolFair.bounds.size.x/def.schoolFair.bounds.size.y>2.9f,"Expanded map uses the wide panorama without stretching the original school image");
        player.motor.MoveInput=Vector2.left;Step(300);yield return null;
        yield return Until(()=>story.View.transform.position.x<-5,"Camera follows the party to the left festival stalls");
        var companion=new[]{story.Actor("EQ"),story.Actor("Rung")}.First(hero=>!hero.GetComponent<CharacterMotor>());
        Check(companion.GetComponent<CharacterAnimation>().animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"),"Following story hero plays the existing walk animation while moving");
        Check(companion.GetComponent<CharacterAnimation>().animator.speed<=.7f,"Companion uses a relaxed walk playback speed while catching up");
        var companionRenderer=companion.GetComponent<SpriteRenderer>();
        var walkSprites=new HashSet<Sprite>();
        float walkSampleEnd=Time.realtimeSinceStartup+.35f;
        while(Time.realtimeSinceStartup<walkSampleEnd)
        {
            walkSprites.Add(companionRenderer.sprite);
            yield return null;
        }
        Check(walkSprites.Count>1,"Following hero visibly advances through walk sprites instead of sliding in a held pose");
        yield return Until(()=>Mathf.Abs(companion.transform.position.x-player.motor.transform.position.x)<1.3f,"Animated companion catches up to the party");
        yield return Until(()=>companion.GetComponent<CharacterAnimation>().animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),"Following hero returns to idle when it stops");
        Check(companion.transform.position.x>player.motor.transform.position.x,"Companion trails to the right when the leader faces left");
        var lanePosition=player.motor.transform.position;lanePosition.y=-.3f;player.motor.transform.position=lanePosition;
        yield return Until(()=>Mathf.Abs(companion.transform.position.y-player.motor.transform.position.y)<.001f,"Companion stays in the leader's ground lane instead of above it");
        for(int i=0;i<45;i++)
        {
            float previousX=companion.transform.position.x;
            player.motor.MoveInput=Vector2.left;Step();yield return null;
            if(Mathf.Abs(companion.transform.position.x-previousX)>.001f &&
                !companion.GetComponent<CharacterAnimation>().animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"))
                throw new Exception("Companion slides in idle during continuous festival walking after catching up");
        }
        Check(true,"Companion keeps walking during continuous movement after catching up");
        player.motor.MoveInput=Vector2.zero;
        Check(story.Phase==1 && player.motor.transform.position.x<-7,"Walking left keeps festival exploration active");
        Capture("SchoolFair_ExploreLeft");yield return null;
        player.motor.ResetForStage(def.FestivalStoreGround(0));yield return null;
        Check(story.GetComponent<StageFlowController>().Interact(),"Existing Interact action opens the left Chicken Pop memory");
        Check(!story.TryInteractFestivalStore(player.motor),"A second interaction cannot queue another memory during the first");
        yield return Until(()=>story.UI.IsTalking && story.UI.ConversationId=="festival-chicken-memory","Chicken Pop memory uses the bilingual authored dialogue");
        Capture("SchoolFair_ChickenPopMemory");
        while(story.InputLocked) { if(story.UI.IsTalking)story.RequestAdvance();yield return null; }
        yield return Until(()=>story.Progress.Has(PrologueDirector.ChickenStoreVisited),"Finishing Chicken Pop memory saves its visited flag");
        Check(story.Phase==1 && !story.FestivalStoresCompleted && !story.TryInteractFestivalStore(player.motor),"One completed store cannot advance the festival or count twice");
        player.motor.MoveInput=Vector2.right;int festivalFrames=1000;
        while(player.motor.transform.position.x<7 && festivalFrames-->0)Step();
        yield return Until(()=>story.View.transform.position.x>5,"Camera follows the party to the right festival stalls");
        yield return Until(()=>companion.GetComponent<CharacterAnimation>().animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),"Companion settles naturally behind the leader after turning right");
        Check(companion.transform.position.x<player.motor.transform.position.x && Mathf.Abs(companion.transform.position.y-player.motor.transform.position.y)<.001f,"Companion trails to the left in the same lane when the leader faces right");
        Check(story.Phase==1 && story.View.transform.position.x+story.View.orthographicSize*16f/9<=festival.bounds.max.x+.01f,"Festival camera stays inside the panorama while exploring");
        Check(Enumerable.Range(0,4).All(i=>story.Actor("Student"+i).GetComponent<SpriteRenderer>().sortingOrder<-100 && story.Actor("Student"+i).transform.localScale.x<1),"All activity students sit behind the heroes at a smaller background scale");
        Capture("SchoolFair_ExploreRight");yield return null;
        player.motor.ResetForStage(def.FestivalStoreGround(1));yield return null;
        Check(story.TryInteractFestivalStore(player.motor),"Interaction opens the right Pepsi memory");
        yield return Until(()=>story.UI.IsTalking && story.UI.ConversationId=="festival-pepsi-memory","EQ recalls Pepsi rising from 3 to 10 baht");
        story.UI.Advance();Capture("SchoolFair_PepsiMemory");yield return null;story.RequestSkip();
        yield return Until(()=>story.Phase==2,"Completing both store interactions advances to Chanai");
        Check(story.FestivalStoresCompleted && StoryProgress.Load().Has(PrologueDirector.PepsiStoreVisited),"Both store visits persist through the story checkpoint");
        yield return Until(()=>story.UI.IsTalking,"Chanai conversation opens");
        while(story.UI.LineIndex<2) { story.RequestAdvance();yield return null; }
        story.UI.Advance();Capture("SchoolFair_Chanai");yield return null;
        story.Apply(new CutsceneEvent{action=StoryAction.Signal,value="Possess"});
        var possessed=Object.FindObjectsByType<EnemyCombat>(FindObjectsSortMode.None).Where(e=>e.name.StartsWith("Possessed Student")).ToArray();
        Check(possessed.Length==4 && possessed.All(e=>e.role==EnemyRole.Rusher && !e.enabled),"Possession replaces all four students with cinematic Rusher demons");
        Check(possessed.Count(e=>e.aiProfile==female.aiProfile)==2 && possessed.Where(e=>e.name.StartsWith("Possessed Student1") || e.name.StartsWith("Possessed Student3")).All(e=>AssetDatabase.GetAssetPath(e.motor.sprite.sprite).StartsWith(FemaleRusherSetup.Root)),"Both schoolgirls transform into female Rushers at possession");
        Check(Enumerable.Range(0,4).All(i=>!story.Actor("Student"+i).activeSelf),"Human student sprites disappear during possession");
        Capture("SchoolFair_PossessedRushers");yield return null;
        int fairStage=story.GetComponent<StageFlowController>().StageIndex;
        story.RequestSkip();yield return Until(()=>story.Phase==3 && story.Cutscenes.Current==def.escape,"Chanai scene skip starts illustrated escape");
        Check(story.InputLocked && (!story.Actor("Destination") || !story.Actor("Destination").activeSelf) && !Object.FindObjectsByType<EnemyCombat>(FindObjectsSortMode.None).Any(e=>e.name.StartsWith("Possessed Student")),"Illustrated chase locks movement and clears playable enemies and destination");
        var escapeArt=Object.FindObjectsByType<SpriteRenderer>().First(r=>r.name=="School fair escape — concept art");
        Check(escapeArt.sprite==def.escapeConceptArt && escapeArt.enabled,"Escape concept illustration is displayed");
        Capture("SchoolFair_IllustratedEscape");yield return null;
        player.motor.MoveInput=Vector2.zero;
        yield return Until(()=>story.Cutscenes.Current==def.cornered,"Illustrated chase reaches teleport dialogue automatically");
        Check(story.GetComponent<StageFlowController>().StageIndex==fairStage && story.Progress.Has("IllustratedEscapeSeen"),"Escape completes without moving player to another gameplay level");
        story.RequestSkip();
        yield return Until(()=>story.Phase==4 && story.Cutscenes.IsCutscenePlaying,"Teleport reaches sanctuary");
        Check(!escapeArt.enabled,"Escape illustration clears before Prapot's sanctuary");
        yield return Until(()=>story.UI.IsTalking,"Prapot dialogue opens");story.UI.Advance();Capture("Sanctuary_Prapot");yield return null;story.RequestSkip();
        var backdrop=Object.FindObjectsByType<SpriteRenderer>().First(r=>r.name=="Prologue environment");Check(backdrop.bounds.size.x>=11,"Sanctuary art covers the camera's full cinematic width");
        yield return Until(()=>story.Phase==5 && story.Cutscenes.IsCutscenePlaying,"Sealed memories lead to kidnapping flashback");
        yield return Until(()=>story.Actor("Cream").GetComponent<StorySpriteAnimation>()?.frames?.FirstOrDefault()?.name.StartsWith("Cream_Startled_")==true,"Thai captors grab Cream with synchronized capture poses");
        Check(new[]{story.Actor("EQ"),story.Actor("Rung"),player.gameObject}.All(hero=>hero.GetComponentsInChildren<SpriteRenderer>().All(r=>!r.enabled)),"EQ, Rung and player visuals are absent during Cream's capture");
        Check(story.Actor("Cream").activeSelf && !story.UI.IsTalking,"Cream is captured before the heroes' dialogue");
        Check(new[]{"Captor1","Captor2"}.All(name=>story.Actor(name).GetComponent<StorySpriteAnimation>().frames.All(s=>s.name.StartsWith("ThaiCaptor_Grab_"))),"Both Thai captors use their own grabbing artwork");
        Capture("Cream_CapturedWithoutHeroes");yield return null;
        yield return Until(()=>story.Actor("Cream").GetComponent<StorySpriteAnimation>().frames[0].name.StartsWith("Cream_Escort_"),"Cream's struggle changes to animated escorted walking");
        var escortActors=new[]{"Cream","Captor1","Captor2"}.Select(story.Actor).ToArray();
        Check(escortActors[1].GetComponent<StorySpriteAnimation>().frames[0].name.StartsWith("ThaiCaptor_Escort_") && escortActors[2].GetComponent<StorySpriteAnimation>().frames[0].name.StartsWith("ThaiCaptor_Backstep_"),"Captors walk forward and backward using dedicated escort frames");
        Check(!escortActors[1].GetComponent<SpriteRenderer>().flipX && escortActors[2].GetComponent<SpriteRenderer>().flipX,"Both escorting captors continue facing Cream");
        story.Cutscenes.PauseCutscene();
        var frozenSprites=escortActors.Select(a=>a.GetComponent<SpriteRenderer>().sprite).ToArray();
        var frozenPositions=escortActors.Select(a=>a.transform.position).ToArray();
        float pauseEnd=Time.realtimeSinceStartup+.25f;while(Time.realtimeSinceStartup<pauseEnd)yield return null;
        Check(escortActors.Select((a,i)=>a.GetComponent<SpriteRenderer>().sprite==frozenSprites[i] && a.transform.position==frozenPositions[i]).All(v=>v),"Pausing capture freezes all three sprites and positions together");
        story.Cutscenes.ResumeCutscene();
        yield return Until(()=>story.Actor("Cream").transform.position.x>4.8f,"Captors visibly escort Cream offscreen");
        Check(Mathf.Abs(story.Actor("Cream").transform.position.x-story.Actor("Captor1").transform.position.x-.84f)<.04f && Mathf.Abs(story.Actor("Captor2").transform.position.x-story.Actor("Cream").transform.position.x-.91f)<.04f,"Escorting actors maintain their hand-contact spacing");
        Check(!story.Actor("EQ").GetComponentInChildren<SpriteRenderer>().enabled && !story.Actor("Rung").GetComponentInChildren<SpriteRenderer>().enabled,"Heroes remain absent until Cream has been taken away");
        yield return Until(()=>story.UI.IsTalking,"Heroes arrive and decide to follow the captors");
        Check(new[]{"EQ","Rung"}.All(name=>story.Actor(name).GetComponent<CharacterAnimation>().animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")),"Timeline movement ends in idle before the heroes' dialogue");
        Check(new[]{"Cream","Captor1","Captor2"}.All(name=>!story.Actor(name).activeSelf) && new[]{"EQ","Rung"}.All(name=>story.Actor(name).GetComponentInChildren<SpriteRenderer>().enabled),"Only EQ and Rung are shown for the let's-go dialogue");
        Check(story.Actor("Cream").GetComponent<SpriteRenderer>().sprite==def.cream && story.Actor("Cream").GetComponent<StorySpriteAnimation>().frames==null,"Cream's capture animation is cleared before her later rescue");
        Capture("Cream_HeroesArriveAfterCapture");yield return null;story.RequestSkip();
        yield return Until(()=>story.Phase==6,"Playable tutorial starts");
        Check(player.GetComponentInChildren<SpriteRenderer>().enabled,"Skipping capture dialogue restores the playable hero");
        yield return PracticeReady(0);
        // Practice progresses through real movement, hit registration and defense APIs.
        player.motor.MoveInput=Vector2.left;Step(6);yield return null;player.motor.MoveInput=Vector2.right;Step(6);yield return null;
        player.motor.MoveInput=Vector2.up;Step(15);yield return null;player.motor.MoveInput=Vector2.down;Step(15);yield return null;
        int movementFrames=600;
        while(Mathf.Abs(player.motor.transform.position.x-def.TutorialGoal)>.09f && movementFrames-->0)
        {
            player.motor.MoveInput=new Vector2(Mathf.Sign(def.TutorialGoal-player.motor.transform.position.x),0);Step();yield return null;
        }
        player.motor.MoveInput=Vector2.zero;
        yield return Until(()=>story.Progress.tutorialStep==1,"Movement practice completes after lane movement and destination");
        yield return PracticeReady(1);
        yield return Until(()=>Object.FindObjectsByType<EnemyCombat>(FindObjectsSortMode.None).Any(),"Basic attack opponent spawns");
        yield return Route();yield return Route(false,false,false);yield return Until(()=>story.Progress.tutorialStep==2,"Successful attacks defeat the practice opponent");
        yield return PracticeReady(2);yield return Route();yield return Until(()=>story.Progress.tutorialStep==3,"Headbutt finisher completes combo objective");
        yield return PracticeReady(3);Target();
        player.RequestGuard(true);Step(player.EffectiveParryWindow+2);var hit=new AttackHitboxData{damage=8,hitstunFrames=8};Check(player.TryDefense(hit,-1,null)==CombatHitOutcome.Block,"Held Guard blocks a real defense sample");yield return null;
        yield return Until(()=>story.Progress.tutorialStep==4,"Block objective completes");yield return PracticeReady(4);Target();Step(90);player.RequestGuard(true);Step(1);Check(player.TryDefense(hit,-1,null)==CombatHitOutcome.Parry,"Fresh Guard performs a perfect parry");yield return null;
        yield return Until(()=>story.Progress.tutorialStep==5,"Parry objective completes");yield return PracticeReady(5);Target();player.motor.MoveInput=Vector2.right;player.RequestRun(true,Vector2.right);Step(5);yield return null;Step(55);yield return null;
        yield return Until(()=>story.Progress.tutorialStep==6,"Direction plus Guard transitions dash into run");player.ResetRunInput();player.motor.MoveInput=Vector2.zero;
        yield return PracticeReady(6);yield return Route(true);yield return Until(()=>story.Progress.tutorialStep==7,"Grounded launcher hit completes practice");
        yield return PracticeReady(7);yield return Route(true,true);yield return Until(()=>story.Progress.tutorialStep==8,"Air smash connects and completes air practice");
        yield return PracticeReady(8);
        yield return Until(()=>Object.FindObjectsByType<EnemyCombat>(FindObjectsSortMode.None).Count()==3,"Final tutorial wave uses three enemies");
        foreach(var enemy in Object.FindObjectsByType<EnemyCombat>(FindObjectsSortMode.None))enemy.reaction.health.Damage(1000);
        yield return Until(()=>story.Phase==7 && story.Boss,"Tutorial wave leads to first Thrower Boss encounter");
        Check(!player.GetComponent<PlayerSkillController>().enabled,"Tutorial never unlocks powers early");
        Capture("Thrower_FirstEncounter");story.Boss.Damage(100000);Check(story.Boss && !story.Boss.IsDead && story.Boss.Current==1,"Scripted boss cannot reach zero health, even under extreme damage");
        yield return Until(()=>story.Cutscenes.IsCutscenePlaying,"First boss defeat triggers cinematic instead of Game Over");story.RequestSkip();
        yield return Until(()=>story.Phase==8 && story.Cutscenes.IsCutscenePlaying,"Defeat leads to awakening");story.RequestSkip();
        yield return Until(()=>story.Phase==9,"Awakening leads to power practice");Check(player.GetComponent<PlayerSkillController>().enabled && story.Progress.Has("PowersAwakened"),"Skipping awakening still unlocks powers and restores health");
        Target();player.GetComponent<PlayerMeter>().ResetMeter();Check(player.GetComponent<PlayerSkillController>().RequestSkill(),"Existing character skill casts after awakening");Step(90);yield return null;
        yield return Until(()=>story.Phase==10 && story.Boss,"Skill practice leads directly to winnable boss rematch");Check(story.Boss.StoryMinimumHealth==0,"Rematch has no scripted defeat health floor");
        Check(!Object.FindObjectsByType<CombatProjectile>().Any(),"Practice projectiles clean up before the boss rematch");
        var beforeRetry=story.Boss;story.RetryCheckpoint();yield return Until(()=>story.Phase==10 && story.Boss && story.Boss!=beforeRetry && story.Boss.Current==story.Boss.EffectiveMaximum,"Retry restores rematch checkpoint without replaying the defeat");
        story.Boss.Damage(100000);yield return Until(()=>story.Phase==11 && story.Cutscenes.IsCutscenePlaying,"Boss defeat triggers rescue once");
        yield return Until(()=>story.UI.IsTalking,"Cream rescue dialogue appears");story.UI.Advance();Capture("Cream_Rescue");yield return null;story.RequestSkip();
        yield return Until(()=>story.Phase==12 && story.Cutscenes.IsCutscenePlaying,"Memory spell occurs only after Cream's rescue");
        Check(story.Progress.Has("CreamRescued") && !story.Progress.Has("ChanaiResponsible"),"Memory silhouette precedes Prapot's identification");
        yield return Until(()=>story.Actor("Chanai").activeSelf,"Chanai silhouette appears");Capture("MemorySpell_Silhouette");yield return null;story.RequestSkip();
        Check(story.Actor("Chanai").GetComponent<SpriteRenderer>().color.r<.03f,"Chanai appears as a silhouette from the first visible frame");
        yield return Until(()=>story.Phase==13 && story.Cutscenes.IsCutscenePlaying,"Flashback returns to the present");story.RequestSkip();
        yield return Until(()=>!story.ActiveStory,"Prologue exits safely into existing sanctuary");
        Check(story.Progress.Has("PrologueCompleted") && story.Progress.Has("ChanaiResponsible") && story.Progress.Has("ChanaiMotiveUnresolved"),"Save retains culprit and unresolved motive as continuing story flags");
        Check(story.GetComponent<StageFlowController>().enabled && story.GetComponent<PlayerHubController>().enabled,"Existing stage and sanctuary systems restore");
        Check(story.GetComponent<StageFlowController>().StageIndex==0 && player.GetComponent<PlayerSkillController>().enabled,"Player returns to hub with unlocked powers");
        var saved=StoryProgress.Load();Check(saved.checkpoint==15 && saved.Has("CreamRescued"),"Completed prologue persists separately from existing meta saves");
        var roundtrip=JsonUtility.FromJson<StorySnapshot>(JsonUtility.ToJson(story.Snapshot()));Check(roundtrip.progress.Has("ChanaiMotiveUnresolved"),"Story state can round-trip through multiplayer snapshots");
        player.motor.ResetForStage(new Vector2(.5f,.3f));Check(StoryNpcConversation.TryInteract(player.motor),"Prapot uses the existing Interact action in the hub");
        yield return Until(()=>story.UI.IsTalking,"Reusable NPC conversation opens");story.UI.SetLanguage(false);Check(!story.UI.Thai,"Dialogue can switch to English");story.UI.SetLanguage(true);story.RequestSkip();
        yield return Until(()=>!story.InputLocked,"NPC conversation skip restores gameplay");
        var choice=new Conversation{id="validation-choice"};choice.lines.Add(new DialogueLine{speaker="EQ",thai="ทดสอบ",english="Test",choices=new[]{new DialogueChoice{thai="ตกลง",english="Yes",flag="ValidationChoice"}}});db.conversations.Add(choice);
        story.Talk(choice.id);yield return Until(()=>story.UI.ConversationId==choice.id,"Reusable choice dialogue opens");story.UI.Advance();story.RequestChoice(0);
        yield return Until(()=>!story.InputLocked,"Choice advances dialogue and completes its timeline");Check(story.Progress.Has("ValidationChoice"),"Dialogue choice invokes its story-flag callback");db.conversations.Remove(choice);
        story.GetComponent<PlayerHubController>().BeginRun();Check(story.GetComponent<StageFlowController>().StageIndex>0,"Existing main game remains enterable after prologue");
    }
}
