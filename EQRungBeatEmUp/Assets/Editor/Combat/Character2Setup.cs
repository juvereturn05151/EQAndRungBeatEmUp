using System;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class Character2Setup
{
    public const string Root="Assets/EQ_Rung_BeatEmUp/Characters";
    public const string Art="Assets/ArtAssets/Characters/Character2";
    public const string DefinitionPath=Root+"/Character2/Character2.asset";
    public const string PrefabPath=Root+"/Character2/Character2.prefab";
    public const string SkillPath=Root+"/Character2/WandBarrier.asset";
    static Sprite[] Sprites(string name,int count) => Enumerable.Range(1,count).Select(i=>AssetDatabase.LoadAssetAtPath<Sprite>(Art+"/"+name+"_"+i.ToString("00")+".png")).ToArray();
    static T Asset<T>(string path) where T:ScriptableObject
    {
        var asset=AssetDatabase.LoadAssetAtPath<T>(path);
        if(!asset) { asset=ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset,path); }
        return asset;
    }
    [MenuItem("Beat Em Up/Characters/Create Character 2 defaults")]
    public static void Build()
    {
        if(EditorApplication.isPlaying) return;
        Directory.CreateDirectory(Root+"/Character2"); AssetDatabase.Refresh();
        foreach(var path in Directory.GetFiles(Art,"*.png").Where(p=>!Path.GetFileName(p).Contains("Source") && !Path.GetFileName(p).Contains("Sheet") && !Path.GetFileName(p).Contains("Concept")))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency=true; importer.spritePixelsPerUnit=100;
            importer.GetSourceTextureWidthAndHeight(out int width,out int height);
            var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;
            settings.spritePivot=Path.GetFileName(path).StartsWith("Pulse") ? new Vector2(.5f,.22f) : Path.GetFileName(path).StartsWith("Barrier") || Path.GetFileName(path).StartsWith("Pulse") || Path.GetFileName(path).StartsWith("Warning") ? new Vector2(.5f,.5f) : new Vector2(.5f,8f/height);
            settings.spriteMeshType=SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
        var source=PrefabUtility.LoadPrefabContents(ComboTrackingSetup.PlayerPath);
        try
        {
            var combat=source.GetComponent<ComboController>();
            var blue=Asset<PlayableCharacterData>(Root+"/BlueShirtGuy.asset");
            blue.characterId="blue-shirt"; blue.displayName="BlueShirtGuy"; blue.sortOrder=0;
            blue.prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ComboTrackingSetup.PlayerPath);
            blue.idlePose=blue.portrait=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EQ_Rung_BeatEmUp/Sprites/BlueShirtGuy_Idle2_01.png");
            blue.groundCombo=combat.groundCombo.ToArray(); blue.airCombo=combat.airCombo.ToArray();
            blue.launcher=combat.launcher; blue.airDive=combat.airDive; blue.defense=combat.defenseData;
            blue.skill=source.GetComponent<PlayerSkillController>().equippedSkill;
            blue.locomotion=combat.animationDriver.animator.runtimeAnimatorController;
            var definition=Asset<PlayableCharacterData>(DefinitionPath);
            definition.characterId="gray-shirt"; definition.displayName="GrayShirtGuy"; definition.sortOrder=1;
            definition.portrait=definition.idlePose=Sprites("Idle",4)[0];
            definition.groundCombo=Enumerable.Range(1,3).Select(i=>CopyAttack(combat.groundCombo[i-1],"Punch"+i,4)).ToArray();
            definition.launcher=CopyAttack(combat.launcher,"Launcher",4);
            definition.airCombo=new[]{CopyAttack(combat.airCombo[0],"AirPunch1",3),CopyAttack(combat.airCombo[1],"AirPunch2",3),CopyAttack(combat.airCombo[2],"AirPunch3",4)};
            definition.airDive=CopyAttack(combat.airDive,"AirDive",4);
            var defense=Asset<PlayerDefenseData>(Root+"/Character2/Character2_Defense.asset");
            EditorUtility.CopySerialized(combat.defenseData,defense);
            defense.name="Character2_Defense";
            defense.dodge=Holds(defense.dodge,Sprites("Dodge",3)); defense.guard=Holds(defense.guard,Sprites("Guard",2));
            defense.parry=Holds(defense.parry,Sprites("Parry",2)); defense.knockdown=Holds(defense.knockdown,Sprites("Knockdown",2));
            defense.getUp=Holds(defense.getUp,Sprites("GetUp",2)); defense.die=Holds(defense.die,Sprites("Die",2));
            defense.grabbed=Holds(defense.grabbed,Sprites("Grabbed",1)); defense.stunned=Holds(defense.stunned,Sprites("Stun",2));
            defense.blockPose=Sprites("Guard",2)[1]; definition.defense=defense;
            string binding=AnimationUtility.CalculateTransformPath(combat.motor.sprite.transform,combat.animationDriver.animator.transform);
            var controller=Controller(Root+"/Character2/Character2_Locomotion.controller");
            State(controller,"Idle",Clip("Idle",Sprites("Idle",4),12,true,binding));
            State(controller,"Walk",Clip("Walk",Sprites("Walk",12),3,true,binding));
            State(controller,"Jumping",Clip("Jump",Sprites("Jump",2),8,false,binding));
            State(controller,"GroundHit",Clip("Hit",Sprites("Hit",2),6,false,binding));
            definition.locomotion=controller; definition.skill=Guardian();
            var loadout=source.GetComponent<PlayerCharacterLoadout>(); if(!loadout) loadout=source.AddComponent<PlayerCharacterLoadout>();
            loadout.character=blue;
            PrefabUtility.SaveAsPrefabAsset(source,ComboTrackingSetup.PlayerPath);
            EditorUtility.SetDirty(blue); EditorUtility.SetDirty(defense); EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
            var variant=(GameObject)PrefabUtility.InstantiatePrefab(blue.prefab);
            try
            {
                variant.name="Character2";
                variant.GetComponent<PlayerCharacterLoadout>().Apply(definition);
                definition.prefab=PrefabUtility.SaveAsPrefabAsset(variant,PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(variant); }
            EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets(); MultiplayerSetup.Build();
        }
        finally { PrefabUtility.UnloadPrefabContents(source); }
        Debug.Log("CHARACTER 2: shared BlueShirtGuy prefab variant, separate frame assets, Wand Barrier manifests on frame 16 and area activates on frame 24.");
    }
    static AttackData CopyAttack(AttackData source,string name,int poses)
    {
        var copy=Asset<AttackData>(Root+"/Character2/Character2_"+name+".asset"); EditorUtility.CopySerialized(source,copy);
        copy.name="Character2_"+name;
        copy.attackName="Character 2 "+name; var sprites=Sprites(name,poses);
        int first=Mathf.Max(1,copy.FirstActiveFrame),last=copy.LastActiveFrame;
        for(int frame=0;frame<copy.frames.Count;frame++)
        {
            int pose=frame<first ? (poses==4 && frame>=first/2 ? 1 : 0) : frame<=last ? poses-2 : poses-1;
            copy.frames[frame].sprite=sprites[Mathf.Clamp(pose,0,poses-1)];
        }
        if(name=="Punch3") foreach(var hit in copy.frames.SelectMany(f=>f.hitboxes)) { hit.offset=new Vector2(.53f,.88f); hit.size=new Vector2(.9f,.42f); }
        copy.artworkNotes="Character 2 sprites; gameplay frame values, hitboxes, movement, cancels and bounce flags copied from "+source.name+". Tune independently in the Frame Attack Editor.";
        EditorUtility.SetDirty(copy); return copy;
    }
    static CharacterPoseHold[] Holds(CharacterPoseHold[] original,Sprite[] sprites)
    {
        if(original==null || original.Length==0) return sprites.Select(s=>new CharacterPoseHold{sprite=s,frames=8}).ToArray();
        return original.Select((hold,index)=>new CharacterPoseHold{frames=hold?.frames ?? 8,sprite=sprites[Mathf.Min(sprites.Length-1,index*sprites.Length/original.Length)]}).ToArray();
    }
    static AnimatorController Controller(string path) => AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
    static void State(AnimatorController controller,string name,AnimationClip clip)
    {
        var machine=controller.layers[0].stateMachine;
        var state=machine.states.FirstOrDefault(s=>s.state.name==name).state;
        if(!state) state=machine.AddState(name); state.motion=clip;
        if(name=="Idle") machine.defaultState=state;
        EditorUtility.SetDirty(controller);
    }
    static AnimationClip Clip(string name,Sprite[] sprites,int hold,bool loop,string binding="")
    {
        string path=Root+"/Character2/Character2_"+name+".anim";
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(!clip) { clip=new AnimationClip(); AssetDatabase.CreateAsset(clip,path); }
        clip.frameRate=60;
        var keys=sprites.Select((s,i)=>new ObjectReferenceKeyframe{time=i*hold/60f,value=s}).ToList();
        keys.Add(new ObjectReferenceKeyframe{time=sprites.Length*hold/60f,value=loop ? sprites[0] : sprites.Last()});
        AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path=binding,type=typeof(SpriteRenderer),propertyName="m_Sprite"},keys.ToArray());
        var settings=AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime=loop; AnimationUtility.SetAnimationClipSettings(clip,settings);
        EditorUtility.SetDirty(clip); return clip;
    }
    static AttackData Effect(string name,Sprite[] sprites,int hold,float lifetime,int order,Vector3 position,Vector3 scale)
    {
        string path=Root+"/Character2/"+name+".prefab";
        var controller=Controller(Root+"/Character2/"+name+".controller"); State(controller,"Idle",Clip(name,sprites,hold,false));
        var go=new GameObject(name); GameObject prefab;
        try
        {
            var visual=new GameObject("Visual"); visual.transform.SetParent(go.transform,false);
            var renderer=visual.AddComponent<SpriteRenderer>(); renderer.sprite=sprites[0]; renderer.sortingOrder=order; renderer.color=new Color(1,1,1,name=="WandBarrier" ? .7f : .8f);
            visual.transform.localPosition=position; visual.transform.localScale=scale;
            visual.AddComponent<Animator>().runtimeAnimatorController=controller;
            prefab=PrefabUtility.SaveAsPrefabAsset(go,path);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
        var effect=Asset<AttackData>(Root+"/Character2/"+name+"Feedback.asset");
        effect.attackName=name+" feedback";
        effect.feedback=new AttackFeedbackData{impactPrefab=prefab,impactScale=1,impactLifetime=lifetime,impactRotateWithFacing=false,impactVolume=.65f,
            impactSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Deadly Kombat Free version/block_large_71.wav")};
        EditorUtility.SetDirty(effect); return effect;
    }
    static PlayerSkillData Guardian()
    {
        var cast=Asset<AttackData>(Root+"/Character2/Character2_WandBarrierCast.asset");
        var sprites=Sprites("Cast",6); cast.attackName="Wand Barrier Cast"; cast.domain=AttackDomain.Ground; cast.frames.Clear();
        for(int frame=0;frame<48;frame++)
        {
            var data=new AttackFrameData{sprite=sprites[frame<6 ? 0 : frame<12 ? 1 : frame<18 ? 2 : frame<24 ? 3 : frame<30 ? 4 : 5],movementInputScale=0};
            if(frame==0) data.events.Add("Swing"); if(frame==16) data.events.Add("RaiseBarrier"); if(frame==24) data.events.Add("BarrierPulse");
            if(frame>=24 && frame<=27) data.hitboxes.Add(new AttackHitboxData{hitId=901,repeatAfterFrames=0,groundArea=true,offset=Vector2.zero,size=new Vector2(4.5f,2.4f),laneTolerance=1.2f,damage=32,hitstunFrames=32,hitstopFrames=7,knockback=5,outwardGroundKnockback=true,hitType=HitType.Normal,canHitGrounded=true,canHitAirborne=true});
            cast.frames.Add(data);
        }
        cast.feedback=new AttackFeedbackData{areaWarning=true,areaRingSprite=null,warningColor=new Color(.5f,.9f,1,.6f),waveColor=new Color(.7f,1,1,.85f),swingSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Deadly Kombat Free version/punch_long_whoosh_21.wav"),swingVolume=.65f};
        cast.artworkNotes="Original Wand Barrier: 48 logical frames. Guardian event on zero-based frame 16; area hitboxes active 24–27 with one shared hit ID; recovery 28–47. Uses existing ground-area and outward recoil, not a projectile or a separate damage system.";
        var skill=Asset<PlayerSkillData>(SkillPath); skill.displayName="Wand Barrier"; skill.meterCost=1; skill.cast=cast;
        skill.delivery=PlayerSkillDelivery.Area; skill.projectilePrefab=null; skill.releaseEvent="BarrierPulse"; skill.guardianEvent="RaiseBarrier";
        skill.guardianFeedback=Effect("WandBarrier",Sprites("Barrier",8),5,.68f,-20,new Vector3(0,.8f,0),new Vector3(1.84f,.8f,1));
        skill.releaseFeedback=Effect("BarrierPulse",Sprites("Pulse",4),5,.4f,150,Vector3.zero,new Vector3(1.84f,.9f,1));
        EditorUtility.SetDirty(cast); EditorUtility.SetDirty(skill); return skill;
    }
}




