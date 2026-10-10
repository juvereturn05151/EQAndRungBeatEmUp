using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp;
using BeatEmUp.Story;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class FemaleRusherSetup
{
    public const string Root="Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/Enemies/FemaleRusher";
    public const string PrefabPath=Root+"/FemaleRusher.prefab";
    const string Source="Assets/EQ_Rung_BeatEmUp/Levels/HauntedHouse/Prefabs/Rusher.prefab";
    static FemaleRusherSetup() { EditorApplication.update+=Poll; }
    static Sprite Pose(int index)=>AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Frames/FemaleRusher_"+index.ToString("D2")+".png");
    static Sprite Remap(Sprite source)
    {
        if(!source)return null;
        string path=AssetDatabase.GetAssetPath(source);
        if(!path.Contains("/Enemies/Rusher/"))return source;
        string category=Path.GetFileName(Path.GetDirectoryName(path));
        int[] poses;
        switch(category)
        {
            case "Idle": poses=new[]{0,1};break;
            case "Walk": poses=new[]{2,3};break;
            case "Sprint": poses=new[]{4,5};break;
            case "Attack_Slash1": case "Attack_Slash2": poses=new[]{6,7,8,8,7,0};break;
            case "Attack_Lunge": poses=new[]{6,4,9,9,7,0};break;
            case "Hurt_Light": poses=new[]{10};break;
            case "Hurt_Heavy": poses=new[]{11};break;
            case "Air_Hit": poses=new[]{12};break;
            case "Knockdown": poses=new[]{11,12,13,14};break;
            case "Downed": case "Defeated": poses=new[]{14};break;
            case "GetUp": poses=new[]{14,15,15,0};break;
            default: poses=new[]{0,1};break;
        }
        int.TryParse(source.name.Split('_').Last(),out int frame);
        int index=Mathf.Max(0,frame-1);
        if(category=="Idle" || category=="Walk" || category=="Sprint")index%=poses.Length;
        return Pose(poses[Mathf.Clamp(index,0,poses.Length-1)]);
    }
    static void Rewrite(Object target,Dictionary<Object,Object> replacements)
    {
        var serialized=new SerializedObject(target);var property=serialized.GetIterator();
        while(property.Next(true))
        {
            if(property.propertyType!=SerializedPropertyType.ObjectReference)continue;
            var reference=property.objectReferenceValue;
            if(reference is Sprite sprite)property.objectReferenceValue=Remap(sprite);
            else if(reference && replacements.TryGetValue(reference,out var replacement))property.objectReferenceValue=replacement;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(target);
    }
    public static GameObject Create()
    {
        Directory.CreateDirectory(Root+"/Combat");AssetDatabase.Refresh();
        for(int i=0;i<16;i++)
        {
            string path=Root+"/Frames/FemaleRusher_"+i.ToString("D2")+".png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer)throw new Exception("Missing female Rusher frame: "+path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=100;importer.filterMode=FilterMode.Point;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.5f,.0625f);
            importer.SetTextureSettings(settings);importer.SaveAndReimport();
        }
        var replacements=new Dictionary<Object,Object>();
        var dependencies=AssetDatabase.GetDependencies(Source,true).SelectMany(p=>AssetDatabase.LoadAllAssetsAtPath(p));
        foreach(var original in dependencies.Where(o=>o is AttackData || o is EnemyAIProfile || o is AnimationClip).Distinct())
        {
            string extension=original is AnimationClip ? ".anim" : ".asset";
            string path=Root+"/Combat/Female_"+original.name+extension;
            var copy=Object.Instantiate(original);var existing=AssetDatabase.LoadMainAssetAtPath(path);
            if(existing) { EditorUtility.CopySerialized(copy,existing);Object.DestroyImmediate(copy);copy=existing; }
            else AssetDatabase.CreateAsset(copy,path);
            copy.name="Female_"+original.name;replacements[original]=copy;
        }
        foreach(var copy in replacements.Values)
        {
            Rewrite(copy,replacements);
            if(copy is AnimationClip clip)
                foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    var keys=AnimationUtility.GetObjectReferenceCurve(clip,binding);
                    for(int i=0;i<keys.Length;i++)if(keys[i].value is Sprite sprite)keys[i].value=Remap(sprite);
                    AnimationUtility.SetObjectReferenceCurve(clip,binding,keys);
                }
        }
        var go=PrefabUtility.LoadPrefabContents(Source);
        try
        {
            go.name="Female Rusher";
            foreach(var component in go.GetComponentsInChildren<Component>(true))if(component)Rewrite(component,replacements);
            var animator=go.GetComponentInChildren<Animator>();var controller=new AnimatorOverrideController(animator.runtimeAnimatorController);
            var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();controller.GetOverrides(pairs);
            for(int i=0;i<pairs.Count;i++)
                if(replacements.TryGetValue(pairs[i].Key,out var clip))pairs[i]=new KeyValuePair<AnimationClip,AnimationClip>(pairs[i].Key,(AnimationClip)clip);
            controller.ApplyOverrides(pairs);
            string path=Root+"/FemaleRusher.overrideController";
            var existing=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if(existing) { EditorUtility.CopySerialized(controller,existing);Object.DestroyImmediate(controller);controller=existing; }
            else AssetDatabase.CreateAsset(controller,path);
            animator.runtimeAnimatorController=controller;
            // Assign after the controller: changing it can resample the source pose.
            go.GetComponent<CharacterMotor>().sprite.sprite=Pose(0);
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);AssetDatabase.SaveAssets();return prefab;
        }
        finally { PrefabUtility.UnloadPrefabContents(go); }
    }
    [MenuItem("Beat Em Up/Story/Build female Rusher variant")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var prefab=Create();var definition=AssetDatabase.LoadAssetAtPath<PrologueDefinition>(PrologueSetup.Root+"/Prologue.asset");
        definition.possessedSchoolgirlPrefab=prefab;EditorUtility.SetDirty(definition);AssetDatabase.SaveAssets();
        MultiplayerSetup.Build();Debug.Log("FEMALE RUSHER: schoolgirl transformation and all combat sprite references assigned.");
    }
    static void Poll()
    {
        const string request="Temp/FemaleRusherBuild.request";
        if(!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(request);Build();
    }
}
