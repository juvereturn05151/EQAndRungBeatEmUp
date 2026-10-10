using System;
using System.IO;
using System.Linq;
using BeatEmUp.Story;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class FestivalStoreSetup
{
    public const string Art="Assets/EQ_Rung_BeatEmUp/ArtAssets/Story/FestivalStores/";
    static FestivalStoreSetup() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Temp/FestivalStores.setup-request"))return;
        File.Delete("Temp/FestivalStores.setup-request");
        var definition=AssetDatabase.LoadAssetAtPath<PrologueDefinition>(PrologueSetup.Root+"/Prologue.asset");
        Configure(definition);AssetDatabase.SaveAssetIfDirty(definition);AssetDatabase.SaveAssetIfDirty(definition.dialogue);
        MultiplayerSetup.Build();Debug.Log("FESTIVAL STORES: Chicken Pop and Pepsi memories installed without resetting authored sequences.");
    }
    public static void Configure(PrologueDefinition definition)
    {
        if(!definition || !definition.dialogue)throw new InvalidOperationException("Festival stores require the prologue definition and dialogue database.");
        definition.festivalChickenStore=Import("ChickenPopStore");definition.festivalPepsiStore=Import("PepsiStore");
        var seed=ScriptableObject.CreateInstance<DialogueDatabase>();
        try
        {
            JsonUtility.FromJsonOverwrite(File.ReadAllText(PrologueSetup.Root+"/DialogueSeed.json"),seed);
            foreach(string id in new[]{"festival-chicken-memory","festival-pepsi-memory"})
            {
                if(definition.dialogue.Find(id)!=null)continue;
                var conversation=seed.Find(id);if(conversation==null)throw new InvalidOperationException("Missing store conversation: "+id);
                foreach(var line in conversation.lines)line.portrait=line.speaker=="EQ" ? definition.eq.idlePose : definition.rung.idlePose;
                definition.dialogue.conversations.Add(conversation);
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(seed); }
        if(!definition.festivalChickenMemory)definition.festivalChickenMemory=Memory("Festival_ChickenPopMemory","festival-chicken-memory");
        if(!definition.festivalPepsiMemory)definition.festivalPepsiMemory=Memory("Festival_PepsiMemory","festival-pepsi-memory");
        EditorUtility.SetDirty(definition);EditorUtility.SetDirty(definition.dialogue);
    }
    static Sprite Import(string name)
    {
        string path=Art+name+".png";
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(!importer)throw new FileNotFoundException("Festival store sprite is missing",path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;
        importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.maxTextureSize=2048;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.5f,0);
        importer.SetTextureSettings(settings);importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static CutsceneSequence Memory(string name,string conversation)
    {
        string path=PrologueSetup.Root+"/"+name+".asset";var sequence=AssetDatabase.LoadAssetAtPath<CutsceneSequence>(path);
        if(!sequence)
        {
            sequence=ScriptableObject.CreateInstance<CutsceneSequence>();sequence.id=name;sequence.playsOnce=false;
            sequence.events.Add(new CutsceneEvent{action=StoryAction.Dialogue,value=conversation});AssetDatabase.CreateAsset(sequence,path);
        }
        AssetDatabase.SaveAssetIfDirty(sequence);return sequence;
    }
}
