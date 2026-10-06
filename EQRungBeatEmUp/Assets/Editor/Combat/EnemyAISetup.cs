using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

public static class EnemyAISetup
{
    public const string Root="Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/AI/";
    static EnemyAITransition Go(string target,params EnemyAICondition[] conditions) => new EnemyAITransition{targetState=target,conditions=conditions.Select(c=>new EnemyAIRequirement{condition=c}).ToList()};
    public static EnemyAIProfile Example(EnemyCombat brain,bool ranged)
    {
        var profile=ScriptableObject.CreateInstance<EnemyAIProfile>();
        profile.meleeRange=brain.attackRange; profile.projectileRange=ranged ? brain.attackRange : 6; profile.preferredDistance=ranged ? brain.minimumAttackRange+.3f : brain.minimumAttackRange;
        profile.laneTolerance=brain.laneRange; profile.reactionDelay=.05f; profile.recoveryTime=.15f;
        var range=ranged ? EnemyAICondition.InProjectileRange : EnemyAICondition.InMeleeRange;
        profile.states.Add(new EnemyAIState{id="Idle",action=EnemyAIAction.Idle,transitions={Go("Approach",EnemyAICondition.HasTarget)}});
        profile.states.Add(new EnemyAIState{id="Approach",action=EnemyAIAction.Approach,transitions={Go("Idle",EnemyAICondition.NoTarget),Go("Retreat",EnemyAICondition.TooClose),Go("LaneAlign",range,EnemyAICondition.NotLaneAligned),Go("Attack",range,EnemyAICondition.LaneAligned,EnemyAICondition.CooldownReady),Go("Wait",range,EnemyAICondition.LaneAligned)}});
        profile.states.Add(new EnemyAIState{id="LaneAlign",action=EnemyAIAction.LaneAlign,transitions={Go("Idle",EnemyAICondition.NoTarget),Go("Approach",EnemyAICondition.LaneAligned)}});
        profile.states.Add(new EnemyAIState{id="Retreat",action=EnemyAIAction.Retreat,transitions={Go("Idle",EnemyAICondition.NoTarget),Go("Approach",EnemyAICondition.TooFar)}});
        profile.states.Add(new EnemyAIState{id="Attack",action=EnemyAIAction.WeightedAttack,transitions={Go("Recover",EnemyAICondition.AttackFinished),Go("Idle",EnemyAICondition.NoTarget),Go("Approach",EnemyAICondition.NotLaneAligned)}});
        profile.states.Add(new EnemyAIState{id="Recover",action=EnemyAIAction.Wait,transitions={Go(ranged ? "Reposition" : "Approach",EnemyAICondition.RecoveryFinished)}});
        profile.states.Add(new EnemyAIState{id="Wait",action=EnemyAIAction.Wait,duration=.25f,transitions={Go("Approach",EnemyAICondition.TimerFinished),Go("Idle",EnemyAICondition.NoTarget)}});
        if(ranged) profile.states.Add(new EnemyAIState{id="Reposition",action=EnemyAIAction.Reposition,duration=.2f,movementScale=.4f,transitions={Go("Approach",EnemyAICondition.TimerFinished),Go("Idle",EnemyAICondition.NoTarget)}});
        foreach(var role in new[]{EnemyAIStateRole.Hurt,EnemyAIStateRole.Knockdown,EnemyAIStateRole.GetUp,EnemyAIStateRole.Dead}) profile.states.Add(new EnemyAIState{id=role.ToString(),role=role});
        profile.attacks.Add(new EnemyAIAttackChoice{id=ranged ? "ThrowNotebook" : "Primary",attack=brain.attack,projectile=ranged,minimumRange=brain.minimumAttackRange,maximumRange=brain.attackRange,laneTolerance=brain.laneRange,cooldown=brain.attackCooldownFrames*CombatClock.FrameSeconds});
        return profile;
    }
    [MenuItem("Beat Em Up/Enemies/Create missing AI examples and assign enemy prefabs")]
    public static void Build()
    {
        if(Application.isPlaying) return;
        Directory.CreateDirectory(Root); AssetDatabase.Refresh();
        foreach(var name in new[]{"Rusher","Thrower","GrapplerBruiser","Screamer","Ambusher","Prefect"})
        {
            var path=ThrowerProjectileSetup.Root+"Prefabs/"+name+".prefab"; var enemy=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var brain=enemy.GetComponent<EnemyCombat>(); string profilePath=Root+name+"AIProfile.asset";
                var profile=AssetDatabase.LoadAssetAtPath<EnemyAIProfile>(profilePath);
                if(!profile)
                {
                    profile=Example(brain,name=="Thrower");
                    if(name=="Rusher")
                    {
                        string sprintPath=Root+"Rusher_SprintAttack.asset", slashPath=Root+"Rusher_SlashCombo.asset";
                        var sprint=AssetDatabase.LoadAssetAtPath<AttackData>(sprintPath);
                        if(!sprint) { sprint=Object.Instantiate(brain.attack); sprint.attackName="Rusher Sprint Attack"; for(int i=0;i<Mathf.Max(0,sprint.FirstActiveFrame);i++) sprint.frames[i].movement=new Vector2(.025f,0); AssetDatabase.CreateAsset(sprint,sprintPath); }
                        var slash=AssetDatabase.LoadAssetAtPath<AttackData>(slashPath);
                        if(!slash)
                        {
                            slash=Object.Instantiate(brain.attack); slash.attackName="Rusher Slash Combo";
                            var second=Object.Instantiate(brain.attack);
                            foreach(var frame in second.frames) foreach(var box in frame.hitboxes) box.hitId+=100;
                            slash.frames.AddRange(second.frames); Object.DestroyImmediate(second); AssetDatabase.CreateAsset(slash,slashPath);
                        }
                        profile.attacks[0].id="SlashCombo"; profile.attacks[0].attack=slash;
                        profile.attacks.Add(new EnemyAIAttackChoice{id="SprintAttack",attack=sprint,maximumRange=brain.attackRange,laneTolerance=brain.laneRange,cooldown=2,weight=.6f});
                    }
                    AssetDatabase.CreateAsset(profile,profilePath);
                }
                // Respect custom assignments and never reset edited profiles.
                if(!brain.aiProfile) { brain.aiProfile=profile; PrefabUtility.SaveAsPrefabAsset(enemy,path); }
            }
            finally { PrefabUtility.UnloadPrefabContents(enemy); }
        }
        AssetDatabase.SaveAssets(); MultiplayerSetup.Build(); Debug.Log("ENEMY AI SETUP COMPLETE: six editable enemy profiles; existing profiles and custom assignments preserved.");
    }
}
