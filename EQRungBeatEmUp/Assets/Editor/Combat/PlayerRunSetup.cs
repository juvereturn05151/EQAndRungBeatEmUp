using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BeatEmUp;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PlayerRunSetup
{
    public static readonly string[] Art={"Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/BlueShirtGuy/Animations/Run/","Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/Character2/Run/"};
    public static readonly string[] Definitions={"Assets/EQ_Rung_BeatEmUp/Characters/BlueShirtGuy.asset","Assets/EQ_Rung_BeatEmUp/Characters/Character2/Character2.asset"};
    public static readonly string[] Data={"Assets/EQ_Rung_BeatEmUp/Characters/BlueShirtGuy_Run.asset","Assets/EQ_Rung_BeatEmUp/Characters/Character2/Character2_Run.asset"};
    public static readonly string[] Clips={"Assets/EQ_Rung_BeatEmUp/Animations/BlueShirtGuy_Run.anim","Assets/EQ_Rung_BeatEmUp/Characters/Character2/Character2_Run.anim"};
    public static readonly string[] Names={"BlueShirtGuy","Character2"};
    [MenuItem("Beat Em Up/Characters/Set up dash-to-run animations")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        var runSprites=new System.Collections.Generic.List<Sprite>();
        for(int i=0;i<2;i++)
        {
            var sprites=Enumerable.Range(1,8).Select(n=>Import(Art[i]+Names[i]+"_Run_"+n.ToString("00")+".png")).ToArray();
            if(sprites.Any(s=>!s))throw new Exception("Missing run sprites for "+Names[i]);
            runSprites.AddRange(sprites);
            var definition=AssetDatabase.LoadAssetAtPath<PlayableCharacterData>(Definitions[i]);
            var data=AssetDatabase.LoadAssetAtPath<PlayerRunData>(Data[i]);
            if(!data)
            {
                data=ScriptableObject.CreateInstance<PlayerRunData>();
                data.runSpeed=definition.prefab.GetComponent<CharacterMotor>().moveSpeed*1.8f;data.dashToRunFrame=11;
                AssetDatabase.CreateAsset(data,Data[i]);
            }
            data.poses=sprites.Select(s=>new CharacterPoseHold{sprite=s,frames=3}).ToArray();definition.run=data;
            var source=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(definition.prefab));
            try
            {
                var combo=source.GetComponent<ComboController>();combo.runData=data;
                string binding=AnimationUtility.CalculateTransformPath(combo.motor.sprite.transform,combo.animationDriver.animator.transform);
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips[i]);
                if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,Clips[i]);}
                clip.frameRate=60;
                var keys=Enumerable.Range(0,9).Select(n=>new ObjectReferenceKeyframe{time=n*3/60f,value=sprites[n%8]}).ToArray();
                AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve(binding,typeof(SpriteRenderer),"m_Sprite"),keys);
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
                var controller=definition.locomotion as AnimatorController;
                if(controller)
                {
                    var machine=controller.layers[0].stateMachine;
                    var state=machine.states.FirstOrDefault(s=>s.state.name=="Run").state??machine.AddState("Run");state.motion=clip;state.speed=1;
                    EditorUtility.SetDirty(controller);
                }
                PrefabUtility.SaveAsPrefabAsset(source,AssetDatabase.GetAssetPath(definition.prefab));EditorUtility.SetDirty(clip);
            }
            finally { PrefabUtility.UnloadPrefabContents(source); }
            EditorUtility.SetDirty(data);EditorUtility.SetDirty(definition);
        }
        // Preserve existing catalog order / selection changes; append new art so remote snapshots can resolve run sprites.
        var catalog=AssetDatabase.LoadAssetAtPath<MultiplayerCatalog>(MultiplayerSetup.CatalogPath);
        catalog.sprites=(catalog.sprites??Array.Empty<Sprite>()).Concat(runSprites).Distinct().ToArray();
        var paths=AssetDatabase.GetDependencies(Definitions.Concat(Data).ToArray(),true).OrderBy(p=>p,StringComparer.Ordinal);
        var content="GhostFairProtocol5|HeldRun1|"+string.Join("|",paths.Select(p=>p+":"+AssetDatabase.GetAssetDependencyHash(p)))+"|"+
            string.Join("|",catalog.sprites.Select(s=>s?AssetDatabase.GetAssetPath(s)+":"+s.name:"null"))+"|"+
            string.Join("|",catalog.selectionCharacters.Where(c=>c).Select(c=>AssetDatabase.GetAssetPath(c)+":"+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(c))));
        using(var sha=SHA256.Create())catalog.contentHash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(content))).Replace("-","").ToLowerInvariant();
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
    }
    static Sprite Import(string path)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.spritePixelsPerUnit=100;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;
        settings.spritePivot=new Vector2(.5f,8f/144);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
