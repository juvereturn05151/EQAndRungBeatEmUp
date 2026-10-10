using System;
using System.IO;
using System.Linq;
using BeatEmUp;
using BeatEmUp.Story;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PrologueSetup
{
    public const string Root="Assets/EQ_Rung_BeatEmUp/Story";
    const string Art="Assets/EQ_Rung_BeatEmUp/ArtAssets/Story";
    static PrologueSetup() { EditorApplication.update+=Poll; }
    static T Asset<T>(string name) where T:ScriptableObject
    {
        string path=Root+"/"+name+".asset";var asset=AssetDatabase.LoadAssetAtPath<T>(path);
        if(!asset) { asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path); }return asset;
    }
    public static Sprite Sprite(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>(Art+"/"+name+".png");
    static CutsceneEvent E(StoryAction action,string target="",string value="",float duration=.5f,float amount=1,Vector3? position=null,Color? color=null,Sprite sprite=null,int group=0)
        =>new CutsceneEvent{action=action,target=target,value=value,duration=duration,amount=amount,position=position??Vector3.zero,color=color??Color.white,sprite=sprite,parallelGroup=group};
    static CutsceneEvent D(string id)=>E(StoryAction.Dialogue,value:id);
    static CutsceneEvent Sound(string path,float volume=.45f) { var evt=E(StoryAction.Sound,duration:0,amount:volume);evt.sound=AssetDatabase.LoadAssetAtPath<AudioClip>(path);return evt; }
    static CutsceneEvent F(string flag)=>E(StoryAction.Flag,value:flag,duration:0);
    static CutsceneEvent Show(string actor,float x,float y=0)=>E(StoryAction.Spawn,actor,position:new Vector3(x,y));
    static CutsceneEvent ReunionApproach(string actor,float x)
    {
        var evt=E(StoryAction.Move,actor,position:new Vector3(x,0),duration:2.4f,group:1);
        evt.easeMovement=true;evt.walkPlaybackSpeed=.55f;return evt;
    }
    static CutsceneEvent CaptorWalk(string actor)
    {
        var evt=E(StoryAction.Pose,actor,duration:0);
        evt.frames=Directory.GetFiles("Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/NPCs/ThaiBadBoy/Animations/Walk","ThaiBadBoy_Walk_*.png")
            .Where(path=>!path.EndsWith("_Sheet.png",StringComparison.OrdinalIgnoreCase)).OrderBy(path=>path).Select(AssetDatabase.LoadAssetAtPath<Sprite>).ToArray();
        return evt;
    }
    static CutsceneSequence Sequence(string id,CutsceneEvent[] events,params CutsceneEvent[] final)
    {
        var sequence=Asset<CutsceneSequence>(id);sequence.id=id;sequence.events=events.ToList();sequence.finalEvents=final.ToList();EditorUtility.SetDirty(sequence);return sequence;
    }
    [MenuItem("Beat Em Up/Story/Build or reset prologue assets")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        Directory.CreateDirectory(Root);AssetDatabase.Refresh();
        if(!Resources.Load<TMP_Settings>("TMP Settings"))
        {
            var package=Directory.GetFiles("Library/PackageCache","TMP Essential Resources.unitypackage",SearchOption.AllDirectories).FirstOrDefault();
            if(package==null)throw new Exception("TextMeshPro essential resources package is missing.");
            AssetDatabase.ImportPackage(package,false);AssetDatabase.Refresh();
        }
        foreach(string path in Directory.GetFiles(Art,"*.png"))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.maxTextureSize=2048;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;
            bool actor=new[]{"Prapot.png","Cream.png","Chanai.png","GoodStudentBoy.png","GoodStudentGirl.png"}.Contains(Path.GetFileName(path));settings.spritePivot=actor ? new Vector2(.5f,.05f) : new Vector2(.5f,.5f);importer.SetTextureSettings(settings);importer.SaveAndReimport();
        }
        var database=Asset<DialogueDatabase>("PrologueDialogue");JsonUtility.FromJsonOverwrite(File.ReadAllText(Root+"/DialogueSeed.json"),database);
        var definition=Asset<PrologueDefinition>("Prologue");definition.dialogue=database;
        definition.eq=AssetDatabase.LoadAssetAtPath<PlayableCharacterData>("Assets/EQ_Rung_BeatEmUp/Characters/BlueShirtGuy.asset");definition.rung=AssetDatabase.LoadAssetAtPath<PlayableCharacterData>("Assets/EQ_Rung_BeatEmUp/Characters/Character2/GrayShirtGuy.asset");
        if(!definition.rung) definition.rung=AssetDatabase.FindAssets("t:PlayableCharacterData").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<PlayableCharacterData>).First(c=>c.characterId=="gray-shirt");
        foreach(var line in database.conversations.SelectMany(c=>c.lines))
            line.portrait=line.speaker=="EQ" ? definition.eq.idlePose : line.speaker=="Rung" ? definition.rung.idlePose : line.speaker=="Prapot" || line.speaker=="Cream" || line.speaker=="Chanai" ? Sprite(line.speaker+"Portrait") : null;
        EditorUtility.SetDirty(database);
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"/Fonts/StoryThai.asset");
        if(!font)
        {
            font=TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(Root+"/Fonts/NotoSansThai-Regular.ttf"),64,8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
            font.name="StoryThai";AssetDatabase.CreateAsset(font,Root+"/Fonts/StoryThai.asset");AssetDatabase.AddObjectToAsset(font.material,font);
            foreach(var texture in font.atlasTextures)AssetDatabase.AddObjectToAsset(texture,font);
        }
        var characters=string.Concat(database.conversations.SelectMany(c=>c.lines).Select(l=>l.thai+l.english))+string.Concat(Enumerable.Range(32,95).Select(i=>(char)i))+string.Concat(Enumerable.Range(0xE01,0x5A).Select(i=>(char)i));
        font.TryAddCharacters(characters,out string missing);EditorUtility.SetDirty(font);definition.font=font;
        definition.schoolFair=Sprite("SchoolFairPanorama");definition.hideout=Sprite("Hideout");definition.prapot=Sprite("Prapot");definition.cream=Sprite("Cream");definition.chanai=Sprite("Chanai");
        definition.sanctuary=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EQ_Rung_BeatEmUp/ArtAssets/Environments/PlayerSanctuary/HubPanel2.png");
        definition.student=Sprite("GoodStudentBoy");definition.studentGirl=Sprite("GoodStudentGirl");
        definition.possessedStudentPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/Rusher.prefab");
        definition.possessedSchoolgirlPrefab=FemaleRusherSetup.Create();
        var captor=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/NPCs/ThaiBadBoy/Animations/Idle/ThaiBadBoy_Idle_01.png");
        definition.destinationArrow=AssetDatabase.LoadAssetAtPath<Sprite>(StageExitMarkerSetup.Art+"ExitChevron_01.png");
        definition.delinquentPrefab=Delinquent();definition.throwerPrefab=ThrowerBoss();
        definition.reunion=Sequence("01_Reunion",new[]{
            E(StoryAction.Spawn,"EQ",position:new Vector3(-3.2f,0),duration:0),
            E(StoryAction.Spawn,"Rung",position:new Vector3(3.05f,0),duration:0),
            E(StoryAction.Face,"EQ",amount:1,duration:0),E(StoryAction.Face,"Rung",amount:-1,duration:0),
            E(StoryAction.Camera,position:new Vector3(-.075f,1.35f,-10),amount:2.5f,duration:.8f),
            ReunionApproach("EQ",-.7f),ReunionApproach("Rung",.55f),
            E(StoryAction.Wait,duration:.35f),D("reunion")
        },F("ReunionSeen"),E(StoryAction.Move,"EQ",position:new Vector3(-.7f,0),duration:0),E(StoryAction.Move,"Rung",position:new Vector3(.55f,0),duration:0),E(StoryAction.Face,"EQ",amount:1,duration:0),E(StoryAction.Face,"Rung",amount:-1,duration:0),E(StoryAction.Camera,position:new Vector3(0,1.15f,-10),amount:2.35f));
        definition.attack=Sequence("02_ChanaiFair",new[]{E(StoryAction.Fade,amount:.35f,color:new Color(.08f,0,.16f),duration:.8f),Show("Chanai",2,.2f),E(StoryAction.Vfx,"Chanai",color:new Color(.7f,.2f,1),duration:1),D("chanai-fair"),E(StoryAction.Signal,value:"Possess"),E(StoryAction.Vfx,position:new Vector3(0,.7f),color:Color.cyan)},F("ChanaiRecognizesHeroes"),E(StoryAction.Fade,amount:0));
        definition.cornered=Sequence("03_Teleport",new[]{D("cornered"),E(StoryAction.Vfx,"EQ",color:Color.white,amount:3),E(StoryAction.Vfx,"Rung",color:Color.white,amount:3),E(StoryAction.Fade,amount:1,color:Color.white,duration:.7f)},F("SchoolFairCompleted"),E(StoryAction.Despawn,"Chanai"));
        definition.sanctuaryIntro=Sequence("04_SealedMemories",new[]{Show("Prapot",1.4f,.2f),E(StoryAction.Face,"Prapot",amount:-1),D("sanctuary-seal"),E(StoryAction.Camera,position:new Vector3(.2f,.6f,-10),amount:1.55f,duration:.8f),E(StoryAction.Vfx,"Prapot",color:new Color(.6f,.8f,1)),E(StoryAction.Fade,amount:1,color:Color.black)},F("MemorySealConfirmed"));
        definition.kidnapping=Sequence("05_CreamKidnapped",new[]{
            E(StoryAction.Signal,value:"HideHeroes",duration:0),
            E(StoryAction.Camera,position:new Vector3(.3f,1.1f,-10),amount:2.1f,duration:0),
            Show("Cream",.3f,.2f),E(StoryAction.Wait,duration:.8f),
            E(StoryAction.Spawn,"Captor1",position:new Vector3(-3.1f,0),sprite:captor),
            E(StoryAction.Spawn,"Captor2",position:new Vector3(3.1f,0),sprite:captor),
            CaptorWalk("Captor1"),CaptorWalk("Captor2"),
            E(StoryAction.Move,"Captor1",position:new Vector3(-.35f,0),duration:1.2f,group:1),
            E(StoryAction.Move,"Captor2",position:new Vector3(.95f,0),duration:1.2f,group:1),
            E(StoryAction.Wait,duration:.8f),
            E(StoryAction.Move,"Captor1",position:new Vector3(5.15f,0),duration:2.8f,group:2),
            E(StoryAction.Move,"Cream",position:new Vector3(5.8f,.2f),duration:2.8f,group:2),
            E(StoryAction.Move,"Captor2",position:new Vector3(6.45f,0),duration:2.8f,group:2),
            E(StoryAction.Despawn,"Cream",duration:0),E(StoryAction.Despawn,"Captor1",duration:0),E(StoryAction.Despawn,"Captor2",duration:0),
            E(StoryAction.Wait,duration:.35f),
            E(StoryAction.Camera,position:new Vector3(0,1.15f,-10),amount:2.35f,duration:0),
            Show("EQ",-5.2f),Show("Rung",-4.6f),E(StoryAction.Signal,value:"ShowHeroes",duration:0),
            E(StoryAction.Move,"EQ",position:new Vector3(-.7f,0),duration:1.4f,group:3),
            E(StoryAction.Move,"Rung",position:new Vector3(.55f,0),duration:1.4f,group:3),
            D("flashback-go")
        },F("FlashbackStarted"),E(StoryAction.Despawn,"Cream"),E(StoryAction.Despawn,"Captor1"),E(StoryAction.Despawn,"Captor2"),
            Show("EQ",-.7f),Show("Rung",.55f),E(StoryAction.Signal,value:"ShowHeroes",duration:0),
            E(StoryAction.Face,"EQ",amount:1),E(StoryAction.Face,"Rung",amount:1),
            E(StoryAction.Camera,position:new Vector3(0,1.15f,-10),amount:2.35f));
        definition.defeat=Sequence("06_ScriptedDefeat",new[]{D("defeat"),E(StoryAction.Fade,amount:.9f,color:Color.black,duration:1)},F("FirstBossDefeatSeen"));
        definition.awakening=Sequence("07_Awakening",new[]{E(StoryAction.Vfx,"EQ",color:new Color(.22f,.015f,.45f),duration:1,amount:3),E(StoryAction.Vfx,"Rung",color:new Color(.6f,.9f,1),duration:1,amount:3),E(StoryAction.Signal,value:"Awaken"),E(StoryAction.Pose,"Rung",sprite:AssetDatabase.LoadAssetAtPath<Sprite>(Character2Setup.Art+"/Cast_02.png")),E(StoryAction.Fade,amount:0,duration:.5f),D("awakening")},F("PowersAwakened"),E(StoryAction.Signal,value:"Awaken"));
        definition.rescue=Sequence("08_CreamRescue",new[]{Show("Cream",1.4f,0),E(StoryAction.Move,"EQ",position:new Vector3(.5f,0),duration:.7f),D("rescue"),E(StoryAction.Move,"EQ",position:new Vector3(2.8f,0),duration:1,group:2),E(StoryAction.Move,"Rung",position:new Vector3(2.2f,0),duration:1,group:2),E(StoryAction.Move,"Cream",position:new Vector3(2.5f,0),duration:1,group:2),E(StoryAction.Fade,amount:1,color:Color.white,duration:.6f)},F("CreamRescued"));
        definition.memorySpell=Sequence("09_MemorySpell",new[]{E(StoryAction.Despawn,"EQ"),E(StoryAction.Despawn,"Rung"),E(StoryAction.Despawn,"Cream"),E(StoryAction.Fade,amount:.75f,color:Color.black),Show("Chanai",.4f,.15f),E(StoryAction.Signal,"Chanai","Silhouette"),E(StoryAction.Camera,position:new Vector3(.4f,.8f,-10),amount:1.55f,duration:.6f),E(StoryAction.Vfx,"Chanai",color:new Color(.5f,.1f,.8f),duration:1.8f,amount:3),E(StoryAction.Wait,duration:1.8f),E(StoryAction.Fade,amount:1,color:Color.white,duration:.3f)},F("MemoryErasureWitnessed"),F("ChanaiMotiveUnresolved"),E(StoryAction.Despawn,"Chanai"));
        definition.returnPresent=Sequence("10_ChanaiRevelation",new[]{E(StoryAction.Despawn,"Cream"),Show("Prapot",1.4f,.2f),E(StoryAction.Face,"Prapot",amount:-1),D("return-present")},F("ChanaiResponsible"),F("ChanaiMotiveUnresolved"));
        definition.attack.finalEvents.Add(E(StoryAction.Signal,value:"Possess",duration:0));
        definition.cornered.events.Insert(1,Sound("Assets/Deadly Kombat Free version/punch_long_whoosh_30.wav"));
        definition.awakening.events.Insert(0,E(StoryAction.Signal,value:"ShowPowers",duration:0));
        definition.awakening.events.Insert(1,Sound("Assets/Deadly Kombat Free version/fire_punch_finisher_06.wav"));
        definition.defeat.events.Insert(0,E(StoryAction.CameraShake,amount:.08f,duration:.35f));
        var silhouette=definition.memorySpell.events.First(e=>e.action==StoryAction.Signal && e.value=="Silhouette");definition.memorySpell.events.Remove(silhouette);
        silhouette.duration=0;definition.memorySpell.events.Insert(definition.memorySpell.events.FindIndex(e=>e.action==StoryAction.Spawn),silhouette);
        foreach(var timeline in new[]{definition.attack,definition.cornered,definition.awakening,definition.defeat,definition.memorySpell})EditorUtility.SetDirty(timeline);
        SchoolFairSequenceSetup.Configure(definition);
        CreamCaptureAnimationSetup.Configure(definition);
        FestivalStoreSetup.Configure(definition);
        EditorUtility.SetDirty(definition);AssetDatabase.SaveAssets();
        foreach(string path in new[]{"Assets/EQ_Rung_BeatEmUp/Scenes/PlayerHub.unity","Assets/EQ_Rung_BeatEmUp/Scenes/HauntedHouse.unity"})
        {
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid() || !scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            var flow=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<StageFlowController>(true)).First();
            var director=flow.GetComponent<PrologueDirector>() ?? flow.gameObject.AddComponent<PrologueDirector>();director.Definition=definition;EditorUtility.SetDirty(director);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            if(opened)EditorSceneManager.CloseScene(scene,true);
        }
        MultiplayerSetup.Build();Debug.Log("PROLOGUE: bilingual dialogue, ten timelines, story checkpoints and playable tutorial connected.");
    }
    static T Copy<T>(T source,string name) where T:ScriptableObject
    {
        var copy=UnityEngine.Object.Instantiate(source);string path=Root+"/"+name+".asset";var prior=AssetDatabase.LoadAssetAtPath<T>(path);
        if(prior) { EditorUtility.CopySerialized(copy,prior);UnityEngine.Object.DestroyImmediate(copy);copy=prior; }else AssetDatabase.CreateAsset(copy,path);
        copy.name=name;EditorUtility.SetDirty(copy);return copy;
    }
    static GameObject ThrowerBoss()
    {
        var go=PrefabUtility.LoadPrefabContents("Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/Thrower.prefab");
        try
        {
            go.name="Thrower Boss — prologue";var combat=go.GetComponent<EnemyCombat>();
            var attack=Copy(combat.attack,"ThrowerBossThrow");
            // Eight extra startup frames make the stronger projectile readable and avoid instant releases.
            for(int i=0;i<8;i++)attack.frames.Insert(0,JsonUtility.FromJson<AttackFrameData>(JsonUtility.ToJson(attack.frames[0])));
            combat.attack=attack;combat.attackCooldownFrames=80;
            var profile=Copy(combat.aiProfile,"ThrowerBossAI");
            foreach(var choice in profile.attacks) { choice.attack=attack;choice.cooldown=Mathf.Max(.85f,choice.cooldown); }
            combat.aiProfile=profile;
            var shooter=go.GetComponent<EnemyProjectileAttack>();var projectileRoot=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(shooter.projectilePrefab));
            try
            {
                var projectile=projectileRoot.GetComponent<CombatProjectile>();projectile.hit.damage=Mathf.Max(10,projectile.hit.damage*1.5f);projectile.speed*=1.1f;projectile.hit.canBeParried=true;
                projectile.visual.color=new Color(1,.85f,.65f);shooter.projectilePrefab=PrefabUtility.SaveAsPrefabAsset(projectileRoot,Root+"/ThrowerBossProjectile.prefab").GetComponent<CombatProjectile>();
            }
            finally { PrefabUtility.UnloadPrefabContents(projectileRoot); }
            shooter.lockAimAtAttackStart=true;return PrefabUtility.SaveAsPrefabAsset(go,Root+"/ThrowerBoss.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(go); }
    }
    static GameObject Delinquent()
    {
        string path=Root+"/ThaiDelinquent.prefab";
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/Rusher.prefab");
        var go=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(source));
        try
        {
            go.name="Thai technical-school delinquent";
            var combat=go.GetComponent<EnemyCombat>();combat.aiProfile=null;combat.role=EnemyRole.Generic;combat.passiveTrainingDummy=false;combat.attackCooldownFrames=65;
            combat.minimumAttackRange=.45f;combat.laneRange=.12f;
            var attack=UnityEngine.Object.Instantiate(combat.attack);attack.name="TutorialDelinquentAttack";
            string attackPath=Root+"/TutorialDelinquentAttack.asset";var existing=AssetDatabase.LoadAssetAtPath<AttackData>(attackPath);
            if(existing) { EditorUtility.CopySerialized(attack,existing);UnityEngine.Object.DestroyImmediate(attack);attack=existing; }else AssetDatabase.CreateAsset(attack,attackPath);
            var poses=AssetDatabase.FindAssets("t:Sprite",new[]{"Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/NPCs/ThaiBadBoy/Animations/Attack1"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!Path.GetFileNameWithoutExtension(p).EndsWith("_Sheet",StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p).Select(AssetDatabase.LoadAssetAtPath<Sprite>).ToArray();
            if(poses.Length>0)for(int i=0;i<attack.frames.Count;i++) { attack.frames[i].sprite=poses[Mathf.Min(poses.Length-1,i*poses.Length/attack.frames.Count)];foreach(var hit in attack.frames[i].hitboxes)hit.damage=Mathf.Min(hit.damage,8); }
            combat.attack=attack;EditorUtility.SetDirty(attack);
            ThaiDelinquentAnimationSetup.Configure(go);
            return PrefabUtility.SaveAsPrefabAsset(go,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(go); }
    }
    static void Poll()
    {
        const string request="Temp/PrologueBuild.request";
        if(!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(request);Build();
    }
}
