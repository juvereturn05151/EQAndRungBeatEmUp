using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BeatEmUp.Story
{
    [DefaultExecutionOrder(100)]
    public sealed partial class PrologueDirector : MonoBehaviour
    {
        public static PrologueDirector Active { get; private set; }
        public PrologueDefinition Definition;
        public StoryProgress Progress { get; private set; }
        public DialogueController UI { get; private set; }
        public CutsceneController Cutscenes { get; private set; }
        public Camera View { get; private set; }
        public bool Authority => !MultiplayerSession.Active || MultiplayerSession.Active.IsAuthority;
        public bool ActiveStory { get; private set; }
        public bool Cinematic { get; private set; }
        public bool InputLocked => Cinematic || Cutscenes && Cutscenes.IsCutscenePlaying;
        public int Phase { get; private set; }
        public string Instruction { get; private set; }
        public string Objective { get; private set; }
        public CharacterHealth Boss { get; private set; }
        public bool SimulationReady { get; private set; }
        public bool ValidationMode;
        public Transform AttackRoot => stageRoot ? stageRoot.transform : null;
        public UnityEngine.Events.UnityEvent<string> StorySignal = new UnityEngine.Events.UnityEvent<string>();
        StageFlowController flow;
        PlayerHubController hub;
        StageFraming framing;
        GameObject stageRoot, npcRoot;
        SpriteRenderer scenery;
        SpriteRenderer escapeIllustration;
        readonly Dictionary<string,GameObject> actors=new Dictionary<string,GameObject>();
        readonly Dictionary<Behaviour,bool> controls=new Dictionary<Behaviour,bool>();
        readonly Dictionary<SpriteRenderer,bool> hidden=new Dictionary<SpriteRenderer,bool>();
        readonly Dictionary<SpriteRenderer,bool> hiddenHeroes=new Dictionary<SpriteRenderer,bool>();
        readonly Dictionary<Behaviour,bool> hiddenHeroEffects=new Dictionary<Behaviour,bool>();
        readonly Dictionary<SpriteRenderer,Color> festivalHeroColors=new Dictionary<SpriteRenderer,Color>();
        readonly List<GameObject> enemies=new List<GameObject>();
        MenuNavigationInput navigation;
        Coroutine routine;
        bool started, retry, standaloneFlowEnabled, framingEnabled, hubEnabled;
        Rect cameraRect;
        bool remoteStoryActive;
        Rect remoteCameraRect;
        Vector3 cameraPosition;
        float cameraZoom;
        int remoteRevision=-1;
        string remoteSavedProgress;
        int magicId, remoteMagicId;
        readonly List<StoryMagicCue> magicCues=new List<StoryMagicCue>();
        Material magicMaterial;
        AudioSource storyMusic;
        string instructionThai, instructionEnglish, objectiveThai, objectiveEnglish;
        IEnumerable<CharacterMotor> Players => flow.Players.Where(p=>p && p.gameObject.activeInHierarchy);
        IEnumerable<ProloguePracticeTracker> Trackers => Players.Select(p=>p.GetComponent<ProloguePracticeTracker>()).Where(p=>p);
        void Awake()
        {
            Active=this;flow=GetComponent<StageFlowController>();
            hub=GetComponent<PlayerHubController>();
            framing=flow.framing;
            View=framing ? framing.GetComponent<Camera>() : Camera.main;Progress=StoryProgress.Load();
            // Configure the UI while inactive, before its Awake builds TextMeshPro widgets.
            var ui=new GameObject("Dialogue controller");ui.SetActive(false);
            ui.transform.SetParent(transform,false);
            UI=ui.AddComponent<DialogueController>();
            UI.database=Definition.dialogue;
            UI.font=Definition.font;
            ui.SetActive(true);
            Cutscenes=gameObject.AddComponent<CutsceneController>();
            Cutscenes.director=this;
            UI.AdvanceRequested+=RequestAdvance;
            UI.SkipRequested+=RequestSkip;UI.RetryRequested+=RetryCheckpoint;
            UI.Callback+=SetFlag;
            UI.LanguageChanged+=RefreshLanguage;
            UI.ChoiceRequested+=RequestChoice;
        }
        IEnumerator Start()
        {
            while(!flow.player || Authority && flow.StageIndex<0 || MultiplayerSession.Active && (!MultiplayerSession.Active.InGame || !SimulationReady && Authority)) yield return null;
            if(!MultiplayerSession.Active) SimulationReady=true;
            navigation=new MenuNavigationInput(flow.player.GetComponent<PlayerInput>().actions);
            if(Authority && !ValidationMode) Begin();
        }
        public void Ready() { SimulationReady=true; }
        public void Begin(bool replay=false)
        {
            if(!Authority || !Definition || started) return;
            if(replay) { StoryProgress.Reset();Progress=new StoryProgress(); }
            started=true;
            if(Progress.Has("PrologueCompleted")) { SetupHubConversation();UI.SetVisible(false);return; }
            ActiveStory=true;Progress.Set("PrologueStarted");
            standaloneFlowEnabled=flow.enabled;hubEnabled=hub && hub.enabled;framingEnabled=framing && framing.enabled;
            cameraRect=View.rect;cameraPosition=View.transform.position;cameraZoom=View.orthographicSize;
            flow.enabled=false;if(hub)hub.enabled=false;if(framing)framing.enabled=false;flow.RefreshExitMarkers();
            foreach(var renderer in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                if(!renderer.GetComponentInParent<ComboController>()) { hidden[renderer]=renderer.enabled;renderer.enabled=false; }
            stageRoot=new GameObject("Prologue world");npcRoot=new GameObject("Prologue actors");npcRoot.transform.SetParent(stageRoot.transform);
            scenery=new GameObject("Prologue environment").AddComponent<SpriteRenderer>();scenery.transform.SetParent(stageRoot.transform);scenery.sortingOrder=-10000;
            foreach(var p in Players) { if(!p.GetComponent<ProloguePracticeTracker>())p.gameObject.AddComponent<ProloguePracticeTracker>();p.GetComponent<CharacterHealth>().Restore(); }
            actors["EQ"]=FindHero(Definition.eq,-.7f);actors["Rung"]=FindHero(Definition.rung,.6f);
            SpawnActor("Prapot",Definition.prapot,new Vector3(1.4f,.3f));SpawnActor("Cream",Definition.cream,new Vector3(2,0));SpawnActor("Chanai",Definition.chanai,new Vector3(2,.2f));
            foreach(var name in new[]{"Prapot","Cream","Chanai"})actors[name].SetActive(false);
            if(Progress.checkpoint<=3)for(int i=0;i<4;i++)
            {
                var frames=i==0 ? Definition.studentIceCream : i==1 ? Definition.studentDrink : i==2 ? Definition.studentPhone : Definition.studentWave;
                var student=SpawnActor("Student"+i,frames[0],new Vector3(-2.5f+i*1.4f,.25f+(i%2)*.08f));
                student.transform.localScale=Vector3.one*.75f;
                student.GetComponent<SpriteRenderer>().color=new Color(.88f,.77f,.70f,1);
                var activity=student.AddComponent<StoryStudentActivity>();activity.schoolgirl=i%2==1;activity.frames=frames;
                activity.secondsPerPose=1.1f+i*.15f;Face(student,i>=2 ? -1 : 1);
            }
            SetSkills(Progress.Has("PowersAwakened"));routine=StartCoroutine(Run());
        }
        GameObject FindHero(PlayableCharacterData definition,float x)
        {
            var existing=Players.FirstOrDefault(p=>p.GetComponent<PlayerCharacterLoadout>()?.character==definition);
            if(existing) return existing.gameObject;
            var hero=SpawnActor(definition==Definition.eq ? "EQ" : "Rung",definition.idlePose,new Vector3(x,0));
            var animator=hero.AddComponent<Animator>();animator.runtimeAnimatorController=definition.locomotion;animator.updateMode=AnimatorUpdateMode.UnscaledTime;
            var animation=hero.AddComponent<CharacterAnimation>();animation.animator=animator;
            SetActorWalking(hero,false);return hero;
        }
        GameObject SpawnActor(string key,Sprite sprite,Vector3 position)
        {
            var go=new GameObject(key);go.transform.SetParent(npcRoot.transform);go.transform.position=position;
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sortingOrder=Mathf.RoundToInt(-position.y*100);
            actors[key]=go;return go;
        }
        public GameObject Actor(string key) => !string.IsNullOrEmpty(key) && actors.TryGetValue(key,out var actor) ? actor : null;
        public void Face(GameObject actor,float direction) { var motor=actor.GetComponent<CharacterMotor>();if(motor)motor.Face(direction);else if(actor.GetComponent<SpriteRenderer>())actor.GetComponent<SpriteRenderer>().flipX=direction<0; }
        public void SetActorWalking(GameObject actor,bool walking,float playbackSpeed=1)
        {
            if(!actor)return;var animation=actor.GetComponent<CharacterAnimation>();if(!animation)return;
            animation.SetFrozen(false);animation.Play(walking ? "Walk" : "Idle");
            if(animation.animator)animation.animator.speed=walking ? playbackSpeed : 1;
            if(animation.animator && animation.animator.enabled)animation.animator.Update(0);
        }
        public void SetFlag(string flag) { if(Authority && Progress!=null)Progress.Set(flag); }
        public void Apply(CutsceneEvent evt)
        {
            var actor=Actor(evt.target);
            switch(evt.action)
            {
                case StoryAction.Flag: SetFlag(evt.value);break;
                case StoryAction.Environment: Environment(evt.value);break;
                case StoryAction.Spawn:
                    if(evt.prefab) { var go=Instantiate(evt.prefab,evt.position,Quaternion.identity,npcRoot.transform);actors[evt.target]=go; }
                    else if(actor) { actor.GetComponent<StorySpriteAnimation>()?.Stop();actor.SetActive(true);actor.transform.position=evt.position;if(evt.sprite)actor.GetComponent<SpriteRenderer>().sprite=evt.sprite; }
                    else if(evt.sprite)SpawnActor(evt.target,evt.sprite,evt.position);break;
                case StoryAction.Despawn: if(actor) { actor.GetComponent<StorySpriteAnimation>()?.Stop();actor.SetActive(false); }break;
                case StoryAction.Move: if(actor)actor.transform.position=evt.position;break;
                case StoryAction.Face: if(actor)Face(actor,evt.amount);break;
                case StoryAction.Pose:
                    if(actor && evt.frames!=null && evt.frames.Length>0)
                    {
                        var flipbook=actor.GetComponent<StorySpriteAnimation>() ?? actor.AddComponent<StorySpriteAnimation>();
                        flipbook.Play(evt.frames,evt.framesPerSecond,evt.holdLastFrame);
                    }
                    if(actor) { if(evt.sprite)actor.GetComponent<StorySpriteAnimation>()?.Stop(false);var animation=actor.GetComponent<CharacterAnimation>();var renderer=actor.GetComponentInChildren<SpriteRenderer>();if(animation && evt.sprite)animation.HoldSprite(renderer,evt.sprite);else if(renderer && evt.sprite)renderer.sprite=evt.sprite; if(evt.animation && actor.GetComponent<Animator>())actor.GetComponent<Animator>().Play(evt.animation.name); }break;
                case StoryAction.Camera: View.transform.position=evt.position;View.orthographicSize=evt.amount;break;
                case StoryAction.Fade: UI.SetFade(evt.amount,evt.color);break;
                case StoryAction.Vfx: Burst(actor ? actor.transform.position+Vector3.up*.6f : evt.position,evt.color,evt.duration,evt.amount);break;
                case StoryAction.Sound: if(evt.sound)AudioSource.PlayClipAtPoint(evt.sound,View.transform.position,evt.amount);break;
                case StoryAction.Music:
                    if(!storyMusic)storyMusic=gameObject.AddComponent<AudioSource>();storyMusic.Stop();storyMusic.clip=evt.sound;storyMusic.volume=evt.amount;storyMusic.loop=true;if(evt.sound)storyMusic.Play();break;
                case StoryAction.Gameplay: SetCinematic(evt.amount<=0);break;
                case StoryAction.StageTransition: flow.EnterStage(Mathf.RoundToInt(evt.amount));break;
                case StoryAction.Signal:
                    if(evt.value=="Awaken") { SetSkills(true);foreach(var p in Players) { p.GetComponent<CharacterHealth>().Restore();p.GetComponent<PlayerMeter>()?.ResetMeter(); } }
                    if(evt.value=="Silhouette" && actor)actor.GetComponentInChildren<SpriteRenderer>().color=new Color(.025f,.015f,.04f,1);
                    if(evt.value=="Knockdown")foreach(var p in Players)p.GetComponent<ComboController>().ReceiveHit(new AttackHitboxData{hitType=HitType.KnockDown,knockdownDurationFrames=150,knockback=0},1);
                    if(evt.value=="Possess")PossessStudents();
                    if(evt.value=="ShowEscape")ShowEscapeIllustration();
                    if(evt.value=="HideEscape" && escapeIllustration)escapeIllustration.enabled=false;
                    if(evt.value=="ShowPowers")ShowPowers();
                    if(evt.value=="HideHeroes")HideHeroes();
                    if(evt.value=="ShowHeroes")ShowHeroes();
                    StorySignal?.Invoke(evt.value);
                    break;
            }
        }
        void HideHeroes()
        {
            var heroes=Players.Select(p=>p.gameObject).Concat(new[]{Actor("EQ"),Actor("Rung")}).Where(p=>p).Distinct();
            foreach(var hero in heroes)
            {
                hero.transform.position=new Vector3(-5.5f,0,0);
                foreach(var renderer in hero.GetComponentsInChildren<SpriteRenderer>(true))
                    if(!hiddenHeroes.ContainsKey(renderer))hiddenHeroes[renderer]=renderer.enabled;
                // Ground markers and hit blinking own renderer visibility in their updates.
                foreach(var effect in hero.GetComponentsInChildren<Behaviour>(true).Where(b=>b is PlayerGroundIndicator || b is HitBlinkEffect))
                { if(!hiddenHeroEffects.ContainsKey(effect))hiddenHeroEffects[effect]=effect.enabled;effect.enabled=false; }
                foreach(var renderer in hero.GetComponentsInChildren<SpriteRenderer>(true))renderer.enabled=false;
            }
        }
        void ShowHeroes()
        {
            foreach(var entry in hiddenHeroes)if(entry.Key)entry.Key.enabled=entry.Value;
            hiddenHeroes.Clear();
            foreach(var entry in hiddenHeroEffects)if(entry.Key)entry.Key.enabled=entry.Value;
            hiddenHeroEffects.Clear();
        }
        void PossessStudents()
        {
            // Final timeline events also run on skip; keep transformation idempotent.
            if(enemies.Any(e=>e && e.name.StartsWith("Possessed Student")))return;
            foreach(var entry in actors.Where(k=>k.Key.StartsWith("Student")))
            {
                var student=entry.Value;
                Burst(student.transform.position+Vector3.up*.6f,new Color(.7f,.2f,1),.8f,2);
                bool schoolgirl=student.GetComponent<StoryStudentActivity>().schoolgirl;
                var demon=SpawnEnemy(schoolgirl ? Definition.possessedSchoolgirlPrefab : Definition.possessedStudentPrefab,70,false,student.transform.position);
                demon.name="Possessed "+entry.Key+" — Rusher";
                demon.GetComponent<CharacterMotor>().Face(student.transform.position.x<0 ? 1 : -1);
                student.SetActive(false);
                if(Cinematic)
                {
                    var combat=demon.GetComponent<EnemyCombat>();controls[combat]=combat.enabled;combat.enabled=false;
                }
            }
        }
        void ShowEscapeIllustration()
        {
            if(!escapeIllustration)
            {
                var panel=new GameObject("School fair escape — concept art");panel.transform.SetParent(stageRoot.transform);
                escapeIllustration=panel.AddComponent<SpriteRenderer>();escapeIllustration.sprite=Definition.escapeConceptArt;
                escapeIllustration.sortingOrder=10000;
                float height=View.orthographicSize*2,width=height*16f/9;
                escapeIllustration.transform.localScale=Vector3.one*Mathf.Max(width/escapeIllustration.sprite.bounds.size.x,height/escapeIllustration.sprite.bounds.size.y);
                escapeIllustration.transform.position=new Vector3(View.transform.position.x,View.transform.position.y,0);
            }
            escapeIllustration.enabled=true;
        }
        void Burst(Vector3 position,Color color,float duration,float size)
        {
            if(Authority)
            {
                magicCues.RemoveAll(c=>c.expires<Time.unscaledTime);
                magicCues.Add(new StoryMagicCue{id=++magicId,position=position,color=color,duration=duration,size=size,expires=Time.unscaledTime+1});
            }
            var go=new GameObject("Story magic");go.transform.SetParent(stageRoot ? stageRoot.transform : transform);go.transform.position=position;
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.duration=Mathf.Max(.1f,duration);main.loop=false;main.startLifetime=.65f;main.startSpeed=1.2f;main.startSize=.05f*Mathf.Max(1,size);main.startColor=color;main.maxParticles=80;
            var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,50)});
            if(!magicMaterial)magicMaterial=new Material(Shader.Find("Sprites/Default"));
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=magicMaterial;renderer.sortingOrder=200;ps.Play();Destroy(go,main.duration+1);
        }
        public void Environment(string name)
        {
            if(!scenery)return;
            RefreshFestivalStores(name=="Fair");
            scenery.sprite=name=="Fair" ? Definition.schoolFair : name=="Hideout" ? Definition.hideout : Definition.sanctuary;
            float width=name=="Fair" ? Definition.festivalWidth : 11;
            float scale=Mathf.Max(6.4f/scenery.sprite.bounds.size.y,width/scenery.sprite.bounds.size.x);scenery.transform.localScale=Vector3.one*scale;scenery.transform.position=new Vector3(0,1.6f,0);
            View.transform.position=new Vector3(0,1.15f,-10);View.orthographicSize=2.35f;
            float ratio=16f/9;float aspect=(float)Screen.width/Mathf.Max(1,Screen.height);
            View.rect=aspect>ratio ? new Rect((1-ratio/aspect)/2,0,ratio/aspect,1) : new Rect(0,(1-aspect/ratio)/2,1,aspect/ratio);
            float horizontalLimit=name=="Fair" ? Definition.festivalWidth/2-.5f : 3.1f;

            foreach(var p in Players) 
            { 
                p.GetComponent<ComboController>().ResetCombo();
                p.ResetForStage(new Vector2(-1+(p.GetComponent<PlayerIdentity>()?.slot ?? 0)*.6f,0));
                p.arenaMin=name=="Fair" ? Definition.FestivalArenaMin : new Vector2(-horizontalLimit,-1.2f);
                p.arenaMax=name=="Fair" ? Definition.FestivalArenaMax : new Vector2(horizontalLimit,-.2f);
                p.GetComponent<CharacterHealth>().SafeStageProtection=false; 
            }

            foreach(var nameKey in new[]{"EQ","Rung"}) 
            { 
                var hero=Actor(nameKey);
                hero.SetActive(true);
                hero.transform.position=new Vector3(nameKey=="EQ" ? -.7f : .55f,0,0);Face(hero,1); 
            }

            foreach(var nameKey in new[]{"EQ","Rung"})if(!Actor(nameKey).GetComponent<CharacterMotor>())Actor(nameKey).GetComponent<SpriteRenderer>().sprite=(nameKey=="EQ" ? Definition.eq : Definition.rung).idlePose;
            RestoreFestivalLighting();
            if(name=="Fair")foreach(var hero in Players.Select(p=>p.gameObject).Concat(new[]{Actor("EQ"),Actor("Rung")}).Distinct())
            {
                var renderer=hero.GetComponent<CharacterMotor>()?.sprite ?? hero.GetComponent<SpriteRenderer>();
                if(!renderer)continue;festivalHeroColors[renderer]=renderer.color;renderer.color*=new Color(1,.90f,.82f,1);
            }
        }
        void RestoreFestivalLighting()
        {
            foreach(var entry in festivalHeroColors)if(entry.Key)entry.Key.color=entry.Value;
            festivalHeroColors.Clear();
        }
        public void SetCinematic(bool value)
        {
            Cinematic=value;UI.SetCinematic(value,ActiveStory);
            CombatClock.SetPaused(this,value);
            if(value)
            {
                foreach(var p in Players)
                {
                    var input=p.GetComponent<PlayerCombatInput>();if(input && !controls.ContainsKey(input))controls[input]=input.enabled;if(input)input.enabled=false;
                    var legacyHud=p.GetComponent<ComboUIController>();if(legacyHud && !controls.ContainsKey(legacyHud))controls[legacyHud]=legacyHud.enabled;if(legacyHud)legacyHud.enabled=false;
                    var combo=p.GetComponent<ComboController>();combo.ResetCombo();combo.ResetRunInput();combo.RequestGuard(false);
                    p.StopGroundedMotion();p.MoveInput=Vector2.zero;p.MovementLocked=true;
                }
                foreach(var hero in Players.Select(p=>p.gameObject).Concat(new[]{Actor("EQ"),Actor("Rung")}).Where(p=>p).Distinct())
                {
                    var animation=hero.GetComponent<CharacterAnimation>();if(!animation)continue;
                    animation.SetAttackOverride(false);animation.ReleaseReactionControl();SetActorWalking(hero,false);
                }
                foreach(var enemy in enemies.Where(e=>e)) { var combat=enemy.GetComponent<EnemyCombat>();if(combat && !controls.ContainsKey(combat))controls[combat]=combat.enabled;if(combat) { combat.enabled=false;combat.attackPlayer.Stop(); } }
            }
            else
            {
                foreach(var entry in controls)if(entry.Key)entry.Key.enabled=entry.Value;controls.Clear();
                foreach(var p in Players) { p.MovementLocked=false;p.GetComponent<CharacterAnimation>()?.ReleaseReactionControl(); }
            }
        }
        void SetSkills(bool unlocked)
        {
            foreach(var p in Players) { var skill=p.GetComponent<PlayerSkillController>();if(skill)skill.enabled=unlocked; }
            if(unlocked)Progress.Set("PowersAwakened");
        }
        void ShowPowers()
        {
            var dragon=Definition.eq.skill ? Definition.eq.skill.projectilePrefab : null;
            if(dragon && dragon.flightSprites.Length>0)
            {
                var go=new GameObject("EQ — awakening dragon aura");go.transform.SetParent(stageRoot.transform);go.transform.position=Actor("EQ").transform.position+new Vector3(.45f,.8f);
                var visual=go.AddComponent<SpriteRenderer>();visual.sprite=dragon.flightSprites[0];visual.color=new Color(.75f,.55f,1,.8f);visual.sortingOrder=100;
                go.AddComponent<StorySpriteAnimation>().frames=dragon.flightSprites;Destroy(go,3);
            }
            var rung=Actor("Rung");var pose=Definition.rung.skill?.cast?.frames.LastOrDefault(f=>f.sprite)?.sprite;
            if(pose) { var animation=rung.GetComponent<CharacterAnimation>();if(animation)animation.HoldSprite(rung.GetComponentInChildren<SpriteRenderer>(),pose);else rung.GetComponent<SpriteRenderer>().sprite=pose; }
        }
        public void RequestAdvance()
        {
            if(!Authority) { if(UI.Typing)UI.Advance();else MultiplayerSession.Active?.StoryCommand(false,remoteRevision);return; }
            if(Cutscenes.Paused)return;
            if(UI.Advance())UI.Hide();
        }
        public void RequestSkip() { if(Authority)Cutscenes.SkipCutscene(); }
        public void RequestChoice(int index)
        {
            if(!Authority) { MultiplayerSession.Active?.StoryChoice(index,remoteRevision);return; }
            if(UI.Choose(index))RequestAdvance();
        }
        public void RetryCheckpoint() { if(Authority && ActiveStory && !Cinematic)retry=true; }
        void Update()
        {
            UpdateFestivalStores();
            if(ActiveStory || UI.IsTalking)
            {
                var session=MultiplayerSession.Active;bool advance=false,skip=false;
                if(session && session.InGame)
                    foreach(var slot in session.Lobby.slots.Where(s=>session.IsLocalOwner(s.owner)))
                    { var nav=session.LocalInput(slot.slot)?.Menu.Read() ?? default;advance|=nav.Confirm;skip|=nav.Cancel; }
                else if(navigation!=null) { var nav=navigation.Read();advance=nav.Confirm;skip=nav.Cancel; }
                if(advance)RequestAdvance();if(skip && Authority && Cinematic)RequestSkip();
            }
            if(Boss)UI.SetBoss(Boss.Current,Boss.EffectiveMaximum);else UI.SetBoss(0,0);
            if(Authority && ActiveStory && !Cinematic && !Players.Any(p=>!p.GetComponent<CharacterHealth>().IsDead))retry=true;
            foreach(var entry in actors)if(entry.Value && !entry.Value.GetComponent<CharacterMotor>()) { var renderer=entry.Value.GetComponent<SpriteRenderer>();if(renderer)renderer.sortingOrder=Mathf.RoundToInt(-entry.Value.transform.position.y*100)-(entry.Key.StartsWith("Student") ? 200 : 0); }
        }
        void LateUpdate()
        {
            if(!Authority || !ActiveStory || Phase!=1 || InputLocked)return;
            var players=Players.ToArray();if(players.Length==0)return;
            float center=players.Average(p=>p.transform.position.x);
            float halfView=View.orthographicSize*16f/9;
            float cameraLimit=Mathf.Max(0,scenery.bounds.extents.x-halfView);
            var cameraPosition=View.transform.position;
            cameraPosition.x=Mathf.Lerp(cameraPosition.x,Mathf.Clamp(center,-cameraLimit,cameraLimit),1-Mathf.Exp(-8*Time.unscaledDeltaTime));
            View.transform.position=cameraPosition;
            // The story companion walks with the party when only one hero is selected.
            var leader=players[0];
            foreach(var name in new[]{"EQ","Rung"})
            {
                var hero=Actor(name);if(!hero || hero.GetComponent<CharacterMotor>())continue;
                float destination=Mathf.Clamp(leader.transform.position.x-leader.Facing*1.1f,leader.arenaMin.x,leader.arenaMax.x);
                var position=hero.transform.position;
                float dt=Time.unscaledDeltaTime;
                // Ease into the trailing position, with a little extra pace to catch up.
                float distance=Mathf.Abs(destination-position.x);
                float speed=Mathf.Min(leader.moveSpeed*1.15f,distance/.18f);
                float nextX=distance>.02f ? Mathf.MoveTowards(position.x,destination,speed*dt) : position.x;
                // Ground lane follows the leader; jumping still belongs to the leader's visual.
                var nextPosition=new Vector3(nextX,leader.transform.position.y,position.z);
                var movement=nextPosition-position;
                bool walking=movement.sqrMagnitude>0;
                float actualSpeed=dt>0 ? movement.magnitude/dt : 0;
                // A relaxed walk cycle scales with travel speed, including the eased stop.
                float playbackSpeed=Mathf.Clamp(actualSpeed/Mathf.Max(.1f,leader.moveSpeed)*.55f,.15f,.7f);
                SetActorWalking(hero,walking,playbackSpeed);
                if(movement.x!=0)Face(hero,movement.x);
                else if(!walking)Face(hero,leader.Facing);
                hero.transform.position=nextPosition;
                var renderer=hero.GetComponent<SpriteRenderer>();
                if(renderer)renderer.sortingOrder=Mathf.RoundToInt(-nextPosition.y*100);
            }
        }
        void RefreshLanguage() { Instruction=UI.Thai ? instructionThai : instructionEnglish;Objective=UI.Thai ? objectiveThai : objectiveEnglish;UI.SetInstruction(Instruction);UI.SetObjective(Objective); }
        void SetText(string th,string en) { instructionThai=th;instructionEnglish=en;RefreshLanguage(); }
        void Chapter(string th,string en) { objectiveThai=th;objectiveEnglish=en;RefreshLanguage(); }
        IEnumerator Scene(CutsceneSequence sequence) { yield return Cutscenes.Play(sequence); }
        IEnumerator Run()
        {
            for(Phase=Progress.checkpoint;Phase<15;Phase++)
            {
                Progress.checkpoint=Phase;Progress.Save();retry=false;
                if(Phase<=3)Environment("Fair");else if(Phase==4 || Phase>=13)Environment("Sanctuary");else Environment("Hideout");
                SetText("","");UI.SetFade(0,Color.black);Chapter(Phase<=3 ? "งานคืนสู่เหย้า" : Phase>=13 || Phase==4 ? "วิหารของอาจารย์ประพจน์" : "16 ปีก่อน",Phase<=3 ? "GATAI UDOM SUKSA — REUNION FAIR" : Phase>=13 || Phase==4 ? "PRAPOT'S SANCTUARY" : "16 YEARS AGO");
                switch(Phase)
                {
                    case 0:
                        yield return Scene(Definition.reunion);break;
                    case 1:
                        yield return VisitFestivalStores();break;
                    case 2: yield return Scene(Definition.attack);Progress.Set("ChanaiRecognizesHeroes");break;
                    case 3:
                        ClearEnemies();SetText("","");yield return Scene(Definition.escape);
                        foreach(var name in actors.Keys.Where(k=>k.StartsWith("Student")).ToArray())actors[name].SetActive(false);
                        yield return Scene(Definition.cornered);if(escapeIllustration)escapeIllustration.enabled=false;Progress.Set("SchoolFairCompleted");break;
                    case 4: Actor("Chanai").SetActive(false);yield return Scene(Definition.sanctuaryIntro);Progress.Set("MemorySealConfirmed");break;
                    case 5: Actor("Prapot").SetActive(false);HideHeroes();Progress.Set("FlashbackStarted");yield return Scene(Definition.kidnapping);break;
                    case 6: Actor("Cream").SetActive(false);yield return Tutorial();Progress.Set("TutorialCompleted");break;
                    case 7:
                        SpawnBoss(true);SetText("ระวังท่าขว้างของหัวหน้า!","Watch the boss's projectile telegraph!");
                        float until=Time.unscaledTime+12;
                        while(!retry && Time.unscaledTime<until && Boss && Boss.Current>Boss.EffectiveMaximum*.7f)yield return null;
                        SetCinematic(true);SetText("พลังมหาศาลกำลังพุ่งเข้ามา!","OVERWHELMING ATTACK — TAKE COVER!");Burst(Boss.transform.position+Vector3.up,Color.red,1,3);yield return new WaitForSecondsRealtime(1.2f);
                        foreach(var p in Players) { if(p.GetComponent<CharacterHealth>().IsDead)p.GetComponent<CharacterHealth>().Restore();p.GetComponent<ComboController>().ReceiveHit(new AttackHitboxData{hitType=HitType.KnockDown,knockdownDurationFrames=180,knockback=0},1); }
                        yield return new WaitForSecondsRealtime(.45f);ClearEnemies();SetCinematic(false);yield return Scene(Definition.defeat);Progress.Set("FirstBossDefeatSeen");retry=false;break;
                    case 8: yield return Scene(Definition.awakening);SetSkills(true);break;
                    case 9: SetSkills(true);yield return SkillPractice();break;
                    case 10:
                        SetSkills(true);SpawnBoss(false);SetText("ใช้พลังที่ตื่นขึ้นช่วยครีม!","Use your awakened powers to rescue Cream!");
                        while(!retry && Boss && !Boss.IsDead)yield return null;
                        if(!retry) { Boss.GetComponent<EnemyCombat>().enabled=false;yield return new WaitForSecondsRealtime(.8f);Progress.Set("ThrowerBossDefeated");ClearEnemies(); }break;
                    case 11: yield return Scene(Definition.rescue);Progress.Set("CreamRescued");break;
                    case 12: yield return Scene(Definition.memorySpell);Progress.Set("MemoryErasureWitnessed");Progress.Set("ChanaiMotiveUnresolved");break;
                    case 13: yield return Scene(Definition.returnPresent);Progress.Set("ChanaiResponsible");break;
                    case 14: Progress.Set("PrologueCompleted");Progress.checkpoint=15;Progress.Save();Finish();yield break;
                }
                if(retry) { ClearEnemies();foreach(var p in Players)p.GetComponent<CharacterHealth>().Restore();Phase--; }
                else { Progress.checkpoint=Phase+1;Progress.Save(); }
            }
            Finish();
        }
        void Destination(float x)
        {
            if(!Definition.destinationArrow)return;
            var marker=Actor("Destination");if(!marker)marker=SpawnActor("Destination",Definition.destinationArrow,new Vector3(x,.75f));marker.SetActive(true);marker.transform.position=new Vector3(x,.75f);
        }
        public GameObject SpawnEnemy(GameObject prefab,float hp,bool passive,Vector2 position)
        {
            var go=Instantiate(prefab,position,Quaternion.identity,stageRoot.transform);enemies.Add(go);
            var health=go.GetComponent<CharacterHealth>();health.maximumHealth=hp;health.Restore();
            var motor=go.GetComponent<CharacterMotor>();motor.arenaMin=Phase==6 ? Definition.TutorialArenaMin : new Vector2(-3.1f,-.45f);motor.arenaMax=Phase==6 ? Definition.TutorialArenaMax : new Vector2(3.1f,.55f);motor.ResetForStage(position);
            var combat=go.GetComponent<EnemyCombat>();combat.target=Players.First().transform;
            if(passive)
            {
                combat.aiProfile=null;combat.passiveTrainingDummy=true;combat.RefreshAI();
                // A stationary practice dummy keeps launch / air follow-ups within reach.
                motor.arenaMin=Phase==6 ? Definition.ClampTutorialPosition(position-Vector2.one*.08f) : position-Vector2.one*.08f;
                motor.arenaMax=Phase==6 ? Definition.ClampTutorialPosition(position+Vector2.one*.08f) : position+Vector2.one*.08f;
            }
            else combat.passiveTrainingDummy=false;
            return go;
        }
        void SpawnEnemies(int count,float hp)
        {
            for(int i=0;i<count;i++)
            {
                var enemy=SpawnEnemy(Definition.delinquentPrefab,hp,false,Phase==6 ? Definition.TutorialEnemyPosition(i) : new Vector2(1+i*.5f,(i%2)*.25f));
                // Defensive lessons keep a slower cadence, including replacement opponents.
                if(Phase==6 && (Progress.tutorialStep==3 || Progress.tutorialStep==4))
                    enemy.GetComponent<EnemyCombat>().attackCooldownFrames=100;
            }
        }
        void SpawnBoss(bool scripted)
        {
            ClearEnemies();
            Boss=SpawnEnemy(Definition.throwerPrefab,(scripted ? 700 : 260)*Mathf.Max(1,Players.Count()),false,new Vector2(2,.1f)).GetComponent<CharacterHealth>();Boss.name="Thrower — Boss Version";
            Boss.StoryMinimumHealth=scripted ? 1 : 0;Boss.transform.localScale*=1.12f;
        }
        public void ClearEnemies()
        {
            foreach(var enemy in enemies)if(enemy) { enemy.SetActive(false);Destroy(enemy); }enemies.Clear();Boss=null;UI.SetBoss(0,0);
            if(stageRoot)foreach(var projectile in stageRoot.GetComponentsInChildren<CombatProjectile>())Destroy(projectile.gameObject);
        }
        string Keys(string action)
        {
            var input=Players.FirstOrDefault()?.GetComponent<PlayerInput>();if(!input)return action;
            var binding=input.actions.FindAction("Player/"+action);if(binding==null)return action;
            var session=MultiplayerSession.Active;var local=session && session.InGame ? session.Lobby.slots.FirstOrDefault(s=>session.IsLocalOwner(s.owner)) : null;
            bool pad=(local!=null ? session.LocalInput(local.slot)?.Device : navigation?.LastDevice) is Gamepad || Gamepad.current!=null && Keyboard.current==null;
            return binding.GetBindingDisplayString(InputBinding.DisplayStringOptions.DontIncludeInteractions,group:pad ? "Gamepad" : "Keyboard&Mouse");
        }
        public static void ConfigureTutorialPlayer(PrologueDefinition definition,CharacterMotor player,int slot)
        {
            player.arenaMin=definition.TutorialArenaMin;player.arenaMax=definition.TutorialArenaMax;
            player.ResetForStage(definition.ClampTutorialPosition(slot==0 ? definition.tutorialPlayerStart : definition.tutorialPartnerStart));
            player.Face(1);
        }
        IEnumerator Tutorial()
        {
            foreach(var player in Players)
            {
                var slot=player.GetComponent<PlayerIdentity>()?.slot ?? 0;
                ConfigureTutorialPlayer(Definition,player,slot);
            }
            // Keep the single-player companion on the same ground plane as the leader.
            foreach(string hero in new[]{"EQ","Rung"})
            {
                var actor=Actor(hero);
                if(actor && !Players.Any(p=>p.gameObject==actor))actor.transform.position=Definition.ClampTutorialPosition(Definition.tutorialPartnerStart);
            }
            View.transform.position=Definition.tutorialCamera;View.orthographicSize=Mathf.Max(.1f,Definition.tutorialZoom);
            for(int step=Progress.tutorialStep;step<9 && !retry;step++)
            {
                Progress.tutorialStep=step;Progress.Save();ClearEnemies();foreach(var tracker in Trackers)tracker.ResetPractice();
                string[] english={"Move in both directions and reach the marker","Defeat the practice opponent","Land Punch → Punch → Headbutt","Block the slow attack","Time Guard to parry","Direction + Guard: dash, then keep holding to run","Land the grounded launcher","Launch, jump and land Punch → Punch → Smash","Defeat the delinquent group"};
                string[] thai={"เดินซ้ายขวาและขึ้นลง แล้วไปที่จุดหมาย","โจมตีและชนะคู่ฝึก","ต่อคอมโบ หมัด → หมัด → โขกหัว","กดป้องกันเพื่อบล็อกการโจมตี","กดป้องกันให้ตรงจังหวะเพื่อปัดป้อง","ทิศทาง + ป้องกัน: พุ่งแล้วกดค้างเพื่อวิ่ง","ใช้ท่างัดศัตรูขึ้นโดยเท้ายังอยู่บนพื้น","งัดศัตรู กระโดด แล้วต่อคอมโบกลางอากาศ","ชนะกลุ่มนักเลง"};
                string controlsText=step==0 ? Keys("Move") : step<=2 ? Keys("Attack") : step<=5 ? Keys("Guard")+" + "+Keys("Move") : step==6 ? Keys("Attack")+" → "+Keys("Attack")+" → "+Keys("Launcher") : step==7 ? Keys("Launcher")+" / "+Keys("Jump")+" / "+Keys("Attack") : Keys("Attack")+" / "+Keys("Guard");
                SetText((step+1)+"/9  "+thai[step]+"\n"+controlsText,(step+1)+"/9  "+english[step]+"\n"+controlsText);
                if(step>0)SpawnEnemies(step==8 ? 3 : 1,step==1 || step==8 ? 45 : 300);
                bool movedLeft=false,movedRight=false,movedUp=false,movedDown=false;
                if(step==0)Destination(Definition.TutorialGoal);
                while(!retry)
                {
                    movedLeft|=Players.Any(p=>p.MoveInput.x<-.1f);movedRight|=Players.Any(p=>p.MoveInput.x>.1f);movedUp|=Players.Any(p=>p.MoveInput.y>.1f);movedDown|=Players.Any(p=>p.MoveInput.y<-.1f);
                    bool done=step==0 ? movedLeft && movedRight && movedUp && movedDown && Players.Any(p=>Mathf.Abs(p.transform.position.x-Definition.TutorialGoal)<=.1f) : step==1 || step==8 ? enemies.All(e=>!e || e.GetComponent<CharacterHealth>().IsDead) : Trackers.Any(t=>step==2 ? t.finishers>0 : step==3 ? t.blocks>0 : step==4 ? t.parries>0 : step==5 ? t.dodged && t.ran : step==6 ? t.launchers>0 : t.airFinishers>0);
                    if(done)break;
                    if(step>1 && step<8 && enemies.All(e=>!e || e.GetComponent<CharacterHealth>().IsDead)) { ClearEnemies();SpawnEnemies(1,300); }
                    foreach(var p in Players) { var health=p.GetComponent<CharacterHealth>();if(health.IsDead)health.Restore();else if(health.Current<health.EffectiveMaximum*.4f)health.Heal(health.EffectiveMaximum); }
                    yield return null;
                }
                if(!retry) { Actor("Destination")?.SetActive(false);Progress.tutorialStep=step+1;Progress.Save();SetText("สำเร็จ!","COMPLETE!");yield return new WaitForSecondsRealtime(.7f); }
            }
            ClearEnemies();
        }
        IEnumerator SkillPractice()
        {
            ClearEnemies();foreach(var p in Players) { p.GetComponent<PlayerMeter>().ResetMeter();p.GetComponent<CharacterHealth>().Restore(); }
            int before=Trackers.Sum(t=>t.skills);SpawnEnemies(1,160);
            SetText("ใช้พลังใหม่ด้วย "+Keys("Skill"),"Use your new skill: "+Keys("Skill"));
            while(!retry && Trackers.Sum(t=>t.skills)<=before) { foreach(var p in Players)p.GetComponent<PlayerMeter>().Add(.02f);yield return null; }
            ClearEnemies();
        }
        void Finish()
        {
            ShowHeroes();
            RestoreFestivalLighting();
            ClearEnemies();SetCinematic(false);SetSkills(true);ActiveStory=false;UI.Hide();UI.SetFade(0,Color.black);UI.SetCinematic(false,false);UI.SetInstruction("");
            foreach(var entry in hidden)if(entry.Key)entry.Key.enabled=entry.Value;hidden.Clear();
            if(stageRoot)Destroy(stageRoot);actors.Clear();
            if(storyMusic)storyMusic.Stop();
            View.rect=cameraRect;View.transform.position=cameraPosition;View.orthographicSize=cameraZoom;
            if(framing)framing.enabled=framingEnabled;if(hub)hub.enabled=hubEnabled;flow.enabled=standaloneFlowEnabled;flow.EnterStage(0);
            Chapter("ช่วยเหลือเหล่านักเรียน","SAVE THE CHILDREN");SetupHubConversation();
        }
        void SetupHubConversation()
        {
            if(!Authority)return;
            var go=new GameObject("Professor Prapot");go.transform.position=new Vector3(.5f,.3f);var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=Definition.prapot;renderer.sortingOrder=-30;
            var npc=go.AddComponent<StoryNpcConversation>();npc.director=this;npc.conversation="hub-mystery";
        }
        public void Talk(string id)
        {
            if(!Authority || ActiveStory || Cutscenes.IsCutscenePlaying)return;
            var sequence=ScriptableObject.CreateInstance<CutsceneSequence>();sequence.playsOnce=false;sequence.events.Add(new CutsceneEvent{action=StoryAction.Dialogue,value=id});
            StartCoroutine(TalkRoutine(sequence));
        }
        IEnumerator TalkRoutine(CutsceneSequence sequence)
        {
            bool hubWasEnabled=hub && hub.enabled;
            if (hub) 
            { 
                hub.enabled = false; 
            }

            UI.SetVisible(true);
            yield return Cutscenes.Play(sequence);
            Destroy(sequence);
            UI.SetVisible(false);

            if (hub) 
            { 
                hub.enabled = hubWasEnabled; 
            }
        }
        public StorySnapshot Snapshot() => new StorySnapshot {active=ActiveStory,cinematic=Cinematic,paused=Cutscenes.Paused,checkpoint=Phase,tutorialStep=Progress.tutorialStep,conversation=UI.ConversationId,line=UI.LineIndex,revision=UI.Revision,instruction=Instruction,objective=Objective,camera=View.transform.position,zoom=View.orthographicSize,fade=UI.Fade,fadeColor=UI.FadeColor,progress=Progress,bossHp=Boss ? Boss.Current : 0,bossMax=Boss ? Boss.EffectiveMaximum : 0,magic=magicCues.Where(c=>c.expires>=Time.unscaledTime).ToArray()};
        public void ApplySnapshot(StorySnapshot snapshot)
        {
            if(Authority || snapshot==null)return;
            if(snapshot.active && !remoteStoryActive)remoteCameraRect=View.rect;
            if(!snapshot.active && remoteStoryActive)View.rect=remoteCameraRect;
            remoteStoryActive=snapshot.active;
            ActiveStory=snapshot.active;Cinematic=snapshot.cinematic;Phase=snapshot.checkpoint;remoteRevision=snapshot.revision;Progress=snapshot.progress??Progress;
            UI.SetVisible(snapshot.active || snapshot.line>=0);UI.SetInstruction(snapshot.instruction);UI.SetObjective(snapshot.objective);UI.SetCinematic(snapshot.cinematic,false);UI.SetBoss(snapshot.bossHp,snapshot.bossMax);UI.SetFade(snapshot.fade,snapshot.fadeColor);
            UI.Paused=snapshot.paused;
            foreach(var cue in snapshot.magic??Array.Empty<StoryMagicCue>())if(cue.id>remoteMagicId) { remoteMagicId=cue.id;Burst(cue.position,cue.color,cue.duration,cue.size); }
            if(snapshot.line>=0)UI.Show(snapshot.conversation,snapshot.line);else UI.Hide();
            if(snapshot.active) { if(framing)framing.enabled=false;View.transform.position=snapshot.camera;View.orthographicSize=snapshot.zoom;flow.background.enabled=flow.floor.enabled=false;float ratio=16f/9,aspect=(float)Screen.width/Mathf.Max(1,Screen.height);View.rect=aspect>ratio ? new Rect((1-ratio/aspect)/2,0,ratio/aspect,1) : new Rect(0,(1-aspect/ratio)/2,1,aspect/ratio); }
            else
            {
                if(framing)framing.enabled=true;flow.background.enabled=flow.floor.enabled=true;
                if(Progress.Has("PrologueCompleted"))
                {
                    string saved=JsonUtility.ToJson(Progress);if(saved!=remoteSavedProgress) { remoteSavedProgress=saved;Progress.Save(); }
                }
            }
        }
        void OnDestroy() { CombatClock.SetPaused(this,false);navigation?.Dispose();if(magicMaterial)Destroy(magicMaterial);if(Active==this)Active=null; }
    }
}
