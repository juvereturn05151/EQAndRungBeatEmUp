using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class GrapplerSetup
{
    public const string Root="Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse";
    public const string PrefabPath=Root+"/Prefabs/GrapplerBruiser.prefab";
    public const string GrabPath=Root+"/Attacks/Grappler_Grab.asset", HoldPath=Root+"/Attacks/Grappler_HoldSlam.asset";
    public const string HitFeedbackPath=Root+"/Attacks/Grappler_ArmorHitFeedback.asset", BreakFeedbackPath=Root+"/Attacks/Grappler_ArmorBreakFeedback.asset";
    public const string HeldPosePath="Assets/ArtAssets/Characters/BlueShirtGuy/Animations/Grabbed/BlueShirtGuy_Grabbed_PLACEHOLDER.png";
    static GrapplerSetup(){EditorApplication.update+=Poll;}
    static void Poll(){if(EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Temp/GrapplerSetup.request"))return;File.Delete("Temp/GrapplerSetup.request");Configure();}
    static Sprite[] Sprites(string phase)=>Directory.GetFiles("Assets/ArtAssets/Characters/Enemies/GrapplerBruiser/Animations/"+phase,"*.png").Where(p=>System.Text.RegularExpressions.Regex.IsMatch(p,@"_\d{2}\.png$")).OrderBy(p=>p).Select(p=>AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\','/'))).Where(s=>s).ToArray();
    static AttackData Asset(string path){var asset=AssetDatabase.LoadAssetAtPath<AttackData>(path);if(!asset){asset=ScriptableObject.CreateInstance<AttackData>();AssetDatabase.CreateAsset(asset,path);}asset.frames.Clear();return asset;}
    static AttackFrameData Pose(Sprite[] poses,int frame,int length)=>new AttackFrameData{sprite=poses[Mathf.Min(poses.Length-1,frame*poses.Length/length)],movementInputScale=0};
    [MenuItem("Beat Em Up/Enemies/Configure Grappler defaults")]
    public static void Configure()
    {
        if(EditorApplication.isPlaying)return;
        AssetDatabase.Refresh();
        var start=Sprites("Grab_Start");var hold=Sprites("Grab_Hold");var slam=Sprites("BodySlam");var recover=Sprites("Recovery");
        if(start.Length==0 || hold.Length==0 || slam.Length==0 || recover.Length==0)throw new System.InvalidOperationException("Grappler sprite sequences missing");
        var grab=Asset(GrabPath);grab.attackName="Grappler: telegraph / grab / miss recovery";grab.cooldownFrames=90;grab.cooldownOnInterrupt=true;
        grab.activeEvent="EnableGrabHitbox";grab.eventActiveFrames=16;grab.artworkNotes="Existing Grab_Start: 30f telegraph, lock XY target at 26; lunge 30–47; grab 31–46; miss recovery 48–89. Committed movement scale x speed x locked ground direction. Successful capture branches to HoldSlam.";
        for(int i=0;i<90;i++){
            var f=i<30 ? Pose(start,i,30) : i<48 ? Pose(start,Mathf.Min(5,2+(i-30)/4),6) : Pose(recover,i-48,42);
            if(i==0)f.events.Add("Telegraph");
            if(i==26)f.events.Add("LockGrabDirection");
            if(i==30)f.events.Add("StartGrabLunge");
            if(i==31)f.events.Add("EnableGrabHitbox");
            if(i>=30 && i<48)f.grabLungeMovementScale=1;
            if(i>=31 && i<=46)f.grabHitboxes.Add(new GrabHitboxData());
            if(i==47)f.events.Add("DisableGrabHitbox");
            if(i==48)f.events.Add("StopGrabLunge");grab.frames.Add(f);
        }
        var success=Asset(HoldPath);success.attackName="Grappler: hold / slam / release";success.cooldownOnInterrupt=true;
        for(int i=0;i<54;i++){
            var f=i<30 ? Pose(hold,i%20,20) : i<36 ? Pose(slam,i-30,6) : Pose(recover,i-36,18);
            if(i==0){f.events.Add("DisableGrabHitbox");f.events.Add("AttachGrabbedTarget");f.events.Add("ApplyGrabDamage");}
            if(i==30){f.events.Add("ThrowTarget");f.events.Add("ReleaseTarget");}success.frames.Add(f);
        }
        success.artworkNotes="Hold 0–29 (30f). ThrowTarget / ReleaseTarget at frame 30 apply separate slam hit via CombatHurtbox; vulnerable recovery through 53.";
        var defense=AssetDatabase.LoadAssetAtPath<PlayerDefenseData>(PlayerStunSetup.DefensePath);
        var importer=(TextureImporter)AssetImporter.GetAtPath(HeldPosePath);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.5f,.0625f);importer.SetTextureSettings(settings);importer.SaveAndReimport();
        defense.grabbed=new[]{new CharacterPoseHold{sprite=AssetDatabase.LoadAssetAtPath<Sprite>(HeldPosePath),frames=6}};
        var hit=Asset(HitFeedbackPath);var broken=Asset(BreakFeedbackPath);
        foreach(var fx in new[]{hit,broken}){fx.frames.Add(new AttackFrameData{sprite=start[0]});fx.feedback.impactPrefab=defense.parryFeedback.impactPrefab;fx.feedback.impactScale=fx==hit ? .12f : .25f;fx.feedback.impactLifetime=.5f;}
        hit.feedback.impactSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Deadly Kombat Free version/metal_punch_06.wav");
        broken.feedback.impactSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Deadly Kombat Free version/metal_punch_finisher_07.wav");
        var prefab=PrefabUtility.LoadPrefabContents(PrefabPath);
        try{
            var brain=prefab.GetComponent<EnemyCombat>();brain.attack=grab;brain.passiveTrainingDummy=false;brain.motor.moveSpeed=1.3f;brain.reaction.health.maximumHealth=200;
            var armor=prefab.GetComponent<HitCountArmor>() ?? prefab.AddComponent<HitCountArmor>();armor.maxArmorHits=6;armor.armorBreakStunFrames=60;armor.armorRecoveryDelayFrames=300;armor.restoreFullArmor=true;armor.armorHitFeedback=hit;armor.armorBreakFeedback=broken;
            var capture=prefab.GetComponent<CombatGrabController>() ?? prefab.AddComponent<CombatGrabController>();
            var anchor=prefab.transform.Find("GrabAnchor");if(!anchor){anchor=new GameObject("GrabAnchor").transform;anchor.SetParent(prefab.transform,false);}anchor.localPosition=new Vector3(.7f,0,0);
            capture.grabAnchor=anchor;capture.grabAttack=grab;capture.successfulGrab=success;capture.grabDamage=0;capture.slamHit.damage=24;capture.debugDraw=true;
            capture.grabLungeSpeed=8;capture.grabLungeDistance=2.5f;capture.grabAcceleration=0;
            var profile=brain.aiProfile;profile.meleeRange=3;profile.preferredDistance=.5f;profile.laneTolerance=1.8f;profile.commitToAttack=true;profile.recoveryTime=.25f;
            profile.attacks.Clear();profile.attacks.Add(new EnemyAIAttackChoice{id="Primary",attack=grab,maximumRange=3,laneTolerance=1.8f,cooldown=1,weight=1});
            EditorUtility.SetDirty(profile);PrefabUtility.SaveAsPrefabAsset(prefab,PrefabPath);
        }finally{PrefabUtility.UnloadPrefabContents(prefab);}
        foreach(var data in new Object[]{grab,success,hit,broken,defense})EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();MultiplayerSetup.Build();Debug.Log("GRAPPLER CONFIGURED: six-hit armor, 30f telegraph / committed 18f XY lunge / 16f grab / 42f miss; 30f hold / 24 damage slam. Held-player art is explicitly placeholder.");
    }
}
