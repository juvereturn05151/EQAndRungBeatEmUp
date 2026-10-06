using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class AmbusherSetup
{
    public const string Root=GrapplerSetup.Root;
    public const string PrefabPath=Root+"/Prefabs/Ambusher.prefab";
    public const string LeapPath=Root+"/Attacks/Ambusher_LeapGrab.asset",SuccessPath=Root+"/Attacks/Ambusher_GrabSuccess.asset";
    const string Art="Assets/ArtAssets/Characters/Enemies/Ambusher/Animations/LeapGrab";
    static AmbusherSetup(){EditorApplication.update+=Poll;}
    static void Poll(){if(EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Temp/AmbusherSetup.request"))return;try{File.Delete("Temp/AmbusherSetup.request");}catch(IOException){return;}Configure();}
    static AttackData Asset(string path){var a=AssetDatabase.LoadAssetAtPath<AttackData>(path);if(!a){a=ScriptableObject.CreateInstance<AttackData>();AssetDatabase.CreateAsset(a,path);}a.frames.Clear();return a;}
    static AttackFrameData Pose(Sprite sprite)=>new AttackFrameData{sprite=sprite,movementInputScale=0};
    [MenuItem("Beat Em Up/Enemies/Configure Ambusher defaults")]
    public static void Configure()
    {
        if(EditorApplication.isPlaying)return;AssetDatabase.Refresh();
        var paths=Directory.GetFiles(Art,"*.png").OrderBy(p=>p).ToArray();
        foreach(var path in paths){var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.5f,.0625f);importer.SetTextureSettings(settings);importer.SaveAndReimport();}
        var sprites=paths.Select(p=>AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\','/'))).ToArray();if(sprites.Length!=12)throw new System.InvalidOperationException("Ambusher needs all 12 LeapGrab poses");
        var leap=Asset(LeapPath);leap.attackName="Ambusher: crouch / committed leap / grab / miss";leap.cooldownFrames=120;leap.cooldownOnInterrupt=true;leap.activeEvent="EnableGrabHitbox";leap.eventActiveFrames=5;
        for(int i=0;i<80;i++){
            int pose=i<10 ? 0 : i<20 ? 1 : i<24 ? 2 : i<32 ? 3 : i<40 ? 4 : i<50 ? 5 : 11;
            var frame=Pose(sprites[pose]);if(i==0)frame.events.Add("Telegraph");if(i==17)frame.events.Add("LockGrabDirection");if(i==20)frame.events.Add("StartGrabLeap");
            if(i>=20 && i<50)frame.gravityScale=0;
            if(i==45)frame.events.Add("EnableGrabHitbox");if(i>=45 && i<50)frame.grabHitboxes.Add(new GrabHitboxData{offset=new Vector2(.2f,0),width=.9f,depth=.7f,maximumHeightDifference=.75f});
            if(i==50){frame.events.Add("DisableGrabHitbox");frame.events.Add("LandGrabLeap");}leap.frames.Add(frame);
        }
        leap.artworkNotes="20f crouch telegraph; lock target at17; authored XY leap20–49 with separate parabola height; final5f grab45–49; landing/miss recovery50–79. No homing / no armor.";
        var success=Asset(SuccessPath);success.attackName="Ambusher: hold / repeated face strike / release";success.cooldownOnInterrupt=true;
        for(int i=0;i<38;i++){
            int pose=i<3 ? 6 : i<6 ? 7 : i<9 ? 8 : i<17 ? 9 : i<21 ? 10 : 11;var frame=Pose(sprites[pose]);
            if(i==0){frame.events.Add("DisableGrabHitbox");frame.events.Add("AttachGrabbedTarget");frame.events.Add("ApplyGrabDamage");}
            if(i==6)frame.events.Add("FaceStrike");if(i==16)frame.events.Add("RepeatFaceAttacks");
            if(i==18){frame.events.Add("ThrowTarget");frame.events.Add("ReleaseTarget");}success.frames.Add(frame);
        }
        success.artworkNotes="Impact at frame6, recoil through15, RepeatFaceAttacks16 loops to5 while more strikes remain. Default3 impacts, then release/knockdown18; recovery through37.";
        var defense=AssetDatabase.LoadAssetAtPath<PlayerDefenseData>(PlayerStunSetup.DefensePath);success.feedback.impactPrefab=defense.parryFeedback.impactPrefab;success.feedback.impactScale=.1f;success.feedback.impactLifetime=.3f;success.feedback.impactSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Deadly Kombat Free version/metal_punch_06.wav");
        var prefab=PrefabUtility.LoadPrefabContents(PrefabPath);
        try{
            var brain=prefab.GetComponent<EnemyCombat>();brain.attack=leap;brain.passiveTrainingDummy=false;brain.motor.moveSpeed=2.6f;
            var capture=prefab.GetComponent<CombatGrabController>() ?? prefab.AddComponent<CombatGrabController>();
            var anchor=prefab.transform.Find("GrabAnchor");if(!anchor){anchor=new GameObject("GrabAnchor").transform;anchor.SetParent(prefab.transform,false);}anchor.localPosition=new Vector3(.55f,-.25f,0);
            capture.grabAnchor=anchor;capture.grabAttack=leap;capture.successfulGrab=success;capture.leapEnabled=true;capture.leapHorizontalSpeed=10;capture.leapDistance=4;capture.leapDurationFrames=30;capture.leapHeight=1.6f;capture.faceAttackCount=3;capture.faceHit.damage=8;capture.faceHit.hitstopFrames=2;capture.slamHit.damage=0;capture.slamHit.hitstopFrames=0;capture.slamHit.knockdownDurationFrames=45;capture.debugDraw=true;
            capture.heldImpactPose=defense.stunned.FirstOrDefault()?.sprite;
            var armor=prefab.GetComponent<HitCountArmor>();if(armor)Object.DestroyImmediate(armor);
            var profile=brain.aiProfile;profile.useGroundPlaneRange=true;profile.meleeRange=4;profile.preferredDistance=1.25f;profile.laneTolerance=4;profile.commitToAttack=true;profile.reactionDelay=.05f;profile.recoveryTime=.25f;
            profile.attacks.Clear();profile.attacks.Add(new EnemyAIAttackChoice{id="Primary",attack=leap,minimumRange=1.25f,maximumRange=4,laneTolerance=4,cooldown=1,weight=1});
            EditorUtility.SetDirty(profile);PrefabUtility.SaveAsPrefabAsset(prefab,PrefabPath);
        }finally{PrefabUtility.UnloadPrefabContents(prefab);}
        EditorUtility.SetDirty(leap);EditorUtility.SetDirty(success);AssetDatabase.SaveAssets();MultiplayerSetup.Build();Debug.Log("AMBUSHER CONFIGURED:20f telegraph / lock17 / leap20–49 / grab45–49 / miss30f / 3 face strikes x8 damage.");
    }
}
