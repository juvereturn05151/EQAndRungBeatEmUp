using System;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

public static class Character2WalkRepair
{
    public const string Art=Character2Setup.Art+"/Walk";
    public const string ClipPath=Character2Setup.Root+"/Character2/Character2_Walk.anim";
    [MenuItem("Beat Em Up/Characters/Apply corrected GrayShirtGuy walk")]
    public static void Build()
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        AssetDatabase.Refresh();
        var idle=AssetDatabase.LoadAssetAtPath<Sprite>(Character2Setup.Art+"/Idle_01.png");
        var sprites=Enumerable.Range(1,12).Select(i=>AssetDatabase.LoadAssetAtPath<Sprite>(Art+"/Walk_"+i.ToString("00")+".png")).ToArray();
        if(sprites.Any(s=>!s)) throw new Exception("Expected twelve existing Walk sprites.");
        foreach(var sprite in sprites)
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
            importer.GetSourceTextureWidthAndHeight(out int width,out int height);
            if(width!=128 || height!=128) throw new Exception("Walk sprites require the fixed 128x128 canvas.");
            importer.spritePixelsPerUnit=idle.pixelsPerUnit; importer.filterMode=FilterMode.Point;
            importer.mipmapEnabled=false; importer.textureCompression=TextureImporterCompression.Uncompressed;
            var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom; settings.spritePivot=new Vector2(.5f,.0625f);
            settings.spriteMeshType=SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        var clipSettings=AnimationUtility.GetAnimationClipSettings(clip);
        var binding=AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
        var keys=AnimationUtility.GetObjectReferenceCurve(clip,binding);
        if(keys.Length!=13) throw new Exception("Expected existing twelve-pose loop and closing key; retain authored timing.");
        for(int i=0;i<12;i++) keys[i].value=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"/Walk_"+(i+1).ToString("00")+".png");
        keys[12].value=keys[0].value;
        AnimationUtility.SetObjectReferenceCurve(clip,binding,keys);
        // Unity extends the end by one sample when replacing object keys.
        // Restore the original authored loop endpoint and all clip settings.
        AnimationUtility.SetAnimationClipSettings(clip,clipSettings); EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets(); MultiplayerSetup.Build();
    }
}
