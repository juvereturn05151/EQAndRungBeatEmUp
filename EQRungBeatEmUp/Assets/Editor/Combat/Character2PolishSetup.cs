using System;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

// Updates character-specific presentation only; existing combat tuning is retained.
public static class Character2PolishSetup
{
    const string Root=Character2Setup.Root+"/Character2/";
    static Sprite Sprite(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>(Character2Setup.Art+(name.StartsWith("Walk_") ? "/Walk/" : "/")+name+".png");
    static void Move(string oldName,string newName)
    {
        if(!AssetDatabase.LoadMainAssetAtPath(Root+oldName)) return;
        var error=AssetDatabase.MoveAsset(Root+oldName,Root+newName);
        if(!string.IsNullOrEmpty(error)) throw new Exception(error);
    }
    [MenuItem("Beat Em Up/Characters/Apply GrayShirtGuy presentation polish")]
    public static void Build()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        AssetDatabase.Refresh();
        Move("LanternGuardian.asset","WandBarrier.asset");
        Move("Character2_GuardianCast.asset","Character2_WandBarrierCast.asset");
        foreach(string suffix in new[]{".prefab",".controller","Feedback.asset"})
        {
            Move("LanternGuardian"+suffix,"WandBarrier"+suffix);
            Move("SpiritBlast"+suffix,"BarrierPulse"+suffix);
        }
        Move("Character2_LanternGuardian.anim","Character2_WandBarrier.anim");
        Move("Character2_SpiritBlast.anim","Character2_BarrierPulse.anim");
        foreach(var path in AssetDatabase.FindAssets("t:Texture2D",new[]{Character2Setup.Art}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!p.Contains("Source") && !p.Contains("Sheet") && !p.Contains("Concept")))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency=true; importer.spritePixelsPerUnit=100;
            var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;
            importer.GetSourceTextureWidthAndHeight(out int width,out int height);
            settings.spritePivot=path.Contains("Pulse_") ? new Vector2(.5f,.22f) : path.Contains("Barrier_") || path.Contains("WarningRing") ? new Vector2(.5f,.5f) : new Vector2(.5f,8f/height);
            settings.spriteMeshType=SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
        var walk=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"Character2_Walk.anim");
        var binding=AnimationUtility.GetObjectReferenceCurveBindings(walk).Single();
        var keys=Enumerable.Range(0,12).Select(i=>new ObjectReferenceKeyframe{time=i*3/60f,value=Sprite("Walk_"+(i+1).ToString("00"))}).ToList();
        keys.Add(new ObjectReferenceKeyframe{time=36/60f,value=Sprite("Walk_01")});
        AnimationUtility.SetObjectReferenceCurve(walk,binding,keys.ToArray());
        var settingsWalk=AnimationUtility.GetAnimationClipSettings(walk); settingsWalk.loopTime=true; settingsWalk.stopTime=36/60f; AnimationUtility.SetAnimationClipSettings(walk,settingsWalk);
        EditorUtility.SetDirty(walk);
        var punch=AssetDatabase.LoadAssetAtPath<AttackData>(Root+"Character2_Punch3.asset");
        int first=punch.FirstActiveFrame,last=punch.LastActiveFrame;
        for(int i=0;i<punch.frames.Count;i++)
        {
            int pose=i<first/2 ? 1 : i<first ? 2 : i<=last ? 3 : 4;
            punch.frames[i].sprite=Sprite("Punch3_"+pose.ToString("00"));
            foreach(var hit in punch.frames[i].hitboxes) { hit.offset=new Vector2(.53f,.88f); hit.size=new Vector2(.9f,.42f); }
        }
        punch.attackName="GrayShirtGuy Head Snake";
        punch.artworkNotes="Scalp snake extends on existing active frames, then retracts. Shared combo, hit ID, damage, cancels and wall bounce retained. Hitbox at head/snake height; tune in Frame Attack Editor.";
        EditorUtility.SetDirty(punch);
        var skill=AssetDatabase.LoadAssetAtPath<PlayerSkillData>(Character2Setup.SkillPath);
        skill.name="WandBarrier"; skill.displayName="Wand Barrier"; skill.guardianEvent="RaiseBarrier"; skill.releaseEvent="BarrierPulse";
        skill.guardianFeedback=AssetDatabase.LoadAssetAtPath<AttackData>(Root+"WandBarrierFeedback.asset");
        skill.releaseFeedback=AssetDatabase.LoadAssetAtPath<AttackData>(Root+"BarrierPulseFeedback.asset");
        var cast=skill.cast; cast.name="Character2_WandBarrierCast"; cast.attackName="GrayShirtGuy Wand Barrier";
        for(int i=0;i<cast.frames.Count;i++)
        {
            cast.frames[i].sprite=Sprite("Cast_"+(i<6 ? 1 : i<12 ? 2 : i<18 ? 3 : i<24 ? 4 : i<30 ? 5 : 6).ToString("00"));
            var events=cast.frames[i].events;
            for(int e=0;e<events.Count;e++) { if(events[e]=="SummonGuardian") events[e]=skill.guardianEvent; if(events[e]=="SpiritBlast") events[e]=skill.releaseEvent; }
        }
        cast.feedback.areaRingSprite=null; // Shared crisp pixel ring maps exactly to the damage ellipse.
        cast.feedback.warningColor=new Color(.35f,.8f,1,.55f); cast.feedback.waveColor=new Color(.6f,1,1,.85f);
        cast.artworkNotes="Wand drawn in startup, protective barrier appears at frame 16, surrounding pulse hits at frames 24–27, recovery through 47. Existing shared one-bar area skill and recoil. No guardian summon.";
        Effect(skill.guardianFeedback,"WandBarrier","Barrier",8,5,.68f,-20,new Vector3(0,.8f,0),new Vector3(1.84f,.8f,1));
        Effect(skill.releaseFeedback,"BarrierPulse","Pulse",4,5,.4f,150,Vector3.zero,new Vector3(1.84f,.9f,1));
        EditorUtility.SetDirty(cast); EditorUtility.SetDirty(skill);
        ArchiveLegacyEffects(); AssetDatabase.SaveAssets(); MultiplayerSetup.Build();
    }
    static void ArchiveLegacyEffects()
    {
        var names=Enumerable.Range(1,4).Select(i=>"Guardian_"+i.ToString("00")+".png")
            .Concat(Enumerable.Range(1,4).Select(i=>"Wave_"+i.ToString("00")+".png"))
            .Concat(new[]{"Guardian_Source.png","WarningRing.png"});
        System.IO.Directory.CreateDirectory("Tools/Character2/Archive");
        foreach(string name in names)
        {
            string path=Character2Setup.Art+"/"+name;
            if(!System.IO.File.Exists(path)) continue;
            System.IO.File.Copy(path,"Tools/Character2/Archive/"+name,true);
            if(System.IO.File.Exists(path+".meta")) System.IO.File.Copy(path+".meta","Tools/Character2/Archive/"+name+".meta",true);
            if(!AssetDatabase.DeleteAsset(path)) throw new Exception("Unable to archive legacy effect "+path);
        }
    }
    static void Effect(AttackData feedback,string name,string sprites,int count,int hold,float lifetime,int order,Vector3 position,Vector3 scale)
    {
        var prefab=feedback.feedback.impactPrefab;
        var go=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
        try
        {
            go.name=name; var renderer=go.GetComponentInChildren<SpriteRenderer>();
            renderer.sprite=Sprite(sprites+"_01"); renderer.sortingOrder=order; renderer.color=new Color(1,1,1,name=="WandBarrier" ? .7f : .8f); renderer.transform.localPosition=position; renderer.transform.localScale=scale;
            var animator=go.GetComponentInChildren<Animator>();
            var clip=animator.runtimeAnimatorController.animationClips.Single(); clip.name="Character2_"+name;
            var binding=AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
            var keys=Enumerable.Range(0,count).Select(i=>new ObjectReferenceKeyframe{time=i*hold/60f,value=Sprite(sprites+"_"+(i+1).ToString("00"))}).ToList();
            keys.Add(new ObjectReferenceKeyframe{time=count*hold/60f,value=keys.Last().value});
            AnimationUtility.SetObjectReferenceCurve(clip,binding,keys.ToArray());
            var settings=AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime=false; settings.stopTime=count*hold/60f; AnimationUtility.SetAnimationClipSettings(clip,settings);
            EditorUtility.SetDirty(clip); PrefabUtility.SaveAsPrefabAsset(go,AssetDatabase.GetAssetPath(prefab));
        }
        finally { PrefabUtility.UnloadPrefabContents(go); }
        feedback.name=name+"Feedback"; feedback.attackName=name+" feedback";
        feedback.feedback.impactLifetime=lifetime; feedback.feedback.impactScale=1; feedback.feedback.impactRotateWithFacing=false; EditorUtility.SetDirty(feedback);
    }
}
