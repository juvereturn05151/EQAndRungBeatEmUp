using System.IO;
using System.Linq;
using System.Collections.Generic;
using BeatEmUp;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class PrefectSetup
{
    public const string Root=GrapplerSetup.Root,PrefabPath=Root+"/Prefabs/Prefect.prefab",RusherPath=Root+"/Prefabs/Rusher.prefab";
    public const string CallPath=Root+"/Attacks/Prefect_CallBackup.asset",PushPath=Root+"/Attacks/Prefect_PushAttack.asset";
    static PrefectSetup(){EditorApplication.update+=Poll;}
    static void Poll(){if(EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists("Temp/PrefectSetup.request"))return;try{File.Delete("Temp/PrefectSetup.request");}catch(IOException){return;}Configure();}
    static Sprite[] Sprites(string phase)=>Directory.GetFiles("Assets/ArtAssets/Characters/Enemies/Prefect/Animations/"+phase,"*.png").Where(p=>System.Text.RegularExpressions.Regex.IsMatch(p,@"_\d{2}\.png$")).OrderBy(p=>p).Select(p=>AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\','/'))).Where(s=>s).ToArray();
    static AttackData Asset(string path){var a=AssetDatabase.LoadAssetAtPath<AttackData>(path);if(!a){a=ScriptableObject.CreateInstance<AttackData>();AssetDatabase.CreateAsset(a,path);}a.frames.Clear();return a;}
    static EnemyAITransition Transition(string to,EnemyAICondition condition,bool invert=false)=>new EnemyAITransition{targetState=to,conditions=new List<EnemyAIRequirement>{new EnemyAIRequirement{condition=condition,invert=invert}}};
    [MenuItem("Beat Em Up/Enemies/Configure Prefect defaults")]
    public static void Configure()
    {
        if(EditorApplication.isPlaying)return;AssetDatabase.Refresh();var command=Sprites("Command");var whistle=Sprites("Whistle_Call");var baton=Sprites("BatonAttack");var recovery=Sprites("Recovery");
        var call=Asset(CallPath);call.attackName="Prefect: signal / whistle / Rusher backup";call.cooldownFrames=360;call.cooldownOnInterrupt=true;call.activeEvent="CallBackup";call.eventActiveFrames=1;
        for(int i=0;i<42;i++){
            var f=new AttackFrameData{movementInputScale=0,sprite=i<12 ? command[4] : i<30 ? whistle[Mathf.Min(5,(i-12)/3)] : recovery[Mathf.Min(5,(i-30)/2)]};
            if(i==0)f.events.Add("Telegraph");if(i==24)f.events.Add("CallBackup");call.frames.Add(f);
        }
        call.artworkNotes="Existing Command hand-up / Whistle_Call:24f interruptible telegraph; CallBackup at24; recovery25–41. StageFlow spawns only typed Rushers into owner's wave and shared room/encounter caps.";
        var push=Asset(PushPath);push.attackName="Prefect: defensive baton push";push.cooldownFrames=90;push.cooldownOnInterrupt=true;
        for(int i=0;i<28;i++){
            var f=new AttackFrameData{movementInputScale=0,sprite=baton[i<6 ? i/3 : i<9 ? 2 : i<15 ? 4 : 5]};
            if(i==0)f.events.Add("Telegraph");if(i==6)f.events.Add("Swing");
            if(i>=6&&i<=8)f.hitboxes.Add(new AttackHitboxData{hitId=0,offset=new Vector2(.55f,.5f),size=new Vector2(1,.9f),laneTolerance=.45f,damage=4,hitstunFrames=18,hitstopFrames=3,knockback=6,outwardGroundKnockback=true,hitType=HitType.Normal});push.frames.Add(f);
        }
        push.artworkNotes="Existing baton windup0–5, forceful push hitboxes6–8, recovery9–27. Low4 damage, normal18f hitstun, outward XY recoil6; block/parry/dodge use normal combat rules.";
        var defense=AssetDatabase.LoadAssetAtPath<PlayerDefenseData>(PlayerStunSetup.DefensePath);push.feedback.impactPrefab=defense.parryFeedback.impactPrefab;push.feedback.impactScale=.1f;push.feedback.impactLifetime=.3f;push.feedback.impactSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Deadly Kombat Free version/metal_punch_06.wav");
        var rusher=PrefabUtility.LoadPrefabContents(RusherPath);try{rusher.GetComponent<EnemyCombat>().role=EnemyRole.Rusher;PrefabUtility.SaveAsPrefabAsset(rusher,RusherPath);}finally{PrefabUtility.UnloadPrefabContents(rusher);}
        var prefab=PrefabUtility.LoadPrefabContents(PrefabPath);
        try{
            var brain=prefab.GetComponent<EnemyCombat>();brain.attack=call;brain.passiveTrainingDummy=false;brain.motor.moveSpeed=2.8f;brain.reaction.health.maximumHealth=45;
            var support=prefab.GetComponent<PrefectSupport>() ?? prefab.AddComponent<PrefectSupport>();support.rusherPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(RusherPath);support.callBackup=call;support.pushAttack=push;support.rushersPerCall=2;support.spawnEnemyClearance=1.25f;support.retreatDistance=3;support.emergencyPushDistance=1.05f;support.minimumCallDistance=3.2f;support.debugDraw=true;
            var profile=brain.aiProfile;profile.useGroundPlaneRange=true;profile.preferredDistance=3.5f;profile.meleeRange=1.05f;profile.laneTolerance=.45f;profile.commitToAttack=true;profile.reactionDelay=.05f;profile.recoveryTime=.25f;profile.defaultState="Wait";
            profile.attacks.Clear();profile.attacks.Add(new EnemyAIAttackChoice{id="CallBackup",attack=call,minimumRange=3.2f,maximumRange=10,laneTolerance=10,cooldown=2,weight=1});profile.attacks.Add(new EnemyAIAttackChoice{id="Push",attack=push,maximumRange=1.05f,laneTolerance=.45f,cooldown=.5f,weight=1});
            profile.states.Clear();
            var wait=new EnemyAIState{id="Wait",action=EnemyAIAction.Wait};wait.transitions.Add(Transition("Push",EnemyAICondition.EmergencyPushReady));wait.transitions.Add(Transition("Retreat",EnemyAICondition.SupportRetreatNeeded));wait.transitions.Add(Transition("CallBackup",EnemyAICondition.CallBackupReady));
            var retreat=new EnemyAIState{id="Retreat",action=EnemyAIAction.Retreat};retreat.transitions.Add(Transition("Push",EnemyAICondition.EmergencyPushReady));retreat.transitions.Add(Transition("Wait",EnemyAICondition.SupportHasSpace));retreat.transitions.Add(Transition("Wait",EnemyAICondition.NoTarget));
            var calling=new EnemyAIState{id="CallBackup",action=EnemyAIAction.UseAttack,attackChoice="CallBackup"};calling.transitions.Add(Transition("Recovery",EnemyAICondition.AttackFinished));calling.transitions.Add(Transition("Wait",EnemyAICondition.CallBackupReady,true));
            var pushing=new EnemyAIState{id="Push",action=EnemyAIAction.UseAttack,attackChoice="Push"};pushing.transitions.Add(Transition("Retreat",EnemyAICondition.AttackFinished));pushing.transitions.Add(Transition("Retreat",EnemyAICondition.EmergencyPushReady,true));
            var recover=new EnemyAIState{id="Recovery",action=EnemyAIAction.Wait};recover.transitions.Add(Transition("Wait",EnemyAICondition.RecoveryFinished));
            profile.states.AddRange(new[]{wait,retreat,calling,pushing,recover});
            foreach(var role in new[]{EnemyAIStateRole.Hurt,EnemyAIStateRole.Knockdown,EnemyAIStateRole.GetUp,EnemyAIStateRole.Dead})profile.states.Add(new EnemyAIState{id=role.ToString(),role=role});
            EditorUtility.SetDirty(profile);PrefabUtility.SaveAsPrefabAsset(prefab,PrefabPath);
        }finally{PrefabUtility.UnloadPrefabContents(prefab);}
        var level=AssetDatabase.LoadAssetAtPath<LevelDefinition>(HauntedLevelBuilder.LevelPath);foreach(var stage in level.stages){if(stage.maxActiveEnemies<1)stage.maxActiveEnemies=12;foreach(var encounter in stage.encounters)if(encounter.maxActiveEnemies<1)encounter.maxActiveEnemies=8;}EditorUtility.SetDirty(level);
        EditorUtility.SetDirty(call);EditorUtility.SetDirty(push);AssetDatabase.SaveAssets();MultiplayerSetup.Build();Debug.Log("PREFECT CONFIGURED: call24 spawns2 Rushers /8s cooldown; evade3–3.5; push6–8 /4 damage /6 outward recoil /2s cooldown.");
    }
}
