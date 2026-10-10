using System.IO;
using System.Linq;
using BeatEmUp.Story;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SchoolFairSequenceSetup
{
    public const string Art="Assets/EQ_Rung_BeatEmUp/ArtAssets/Story";
    static SchoolFairSequenceSetup() { EditorApplication.update+=Poll; }
    static Sprite[] Activity(string name)
    {
        return Enumerable.Range(0,2).Select(i=>AssetDatabase.LoadAssetAtPath<Sprite>(Art+"/StudentActivities/"+name+"_"+i+".png")).ToArray();
    }
    public static void Configure(PrologueDefinition definition)
    {
        AssetDatabase.Refresh();
        foreach(string path in Directory.GetFiles(Art+"/StudentActivities","*.png").Concat(new[]{Art+"/SchoolFairEscape.png"}))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=100;
            bool activity=path.Contains("StudentActivities");importer.filterMode=activity ? FilterMode.Point : FilterMode.Bilinear;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.maxTextureSize=2048;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(.5f,activity ? .05f : .5f);
            importer.SetTextureSettings(settings);importer.SaveAndReimport();
        }
        definition.studentIceCream=Activity("IceCream");definition.studentDrink=Activity("Drink");
        definition.studentPhone=Activity("Phone");definition.studentWave=Activity("Wave");
        definition.escapeConceptArt=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"/SchoolFairEscape.png");
        string pathSequence=PrologueSetup.Root+"/03_SchoolFairEscape.asset";
        var escape=AssetDatabase.LoadAssetAtPath<CutsceneSequence>(pathSequence);
        if(!escape) { escape=ScriptableObject.CreateInstance<CutsceneSequence>();AssetDatabase.CreateAsset(escape,pathSequence); }
        escape.id="03_SchoolFairEscape";
        escape.events.Clear();escape.finalEvents.Clear();
        escape.events.Add(new CutsceneEvent{action=StoryAction.Signal,value="ShowEscape",duration=0});
        escape.events.Add(new CutsceneEvent{action=StoryAction.Camera,position=new Vector3(.1f,1.2f,-10),amount=2.16f,duration=6});
        escape.events.Add(new CutsceneEvent{action=StoryAction.Wait,duration=1});
        escape.finalEvents.Add(new CutsceneEvent{action=StoryAction.Signal,value="ShowEscape",duration=0});
        escape.finalEvents.Add(new CutsceneEvent{action=StoryAction.Flag,value="IllustratedEscapeSeen",duration=0});
        definition.escape=escape;EditorUtility.SetDirty(escape);EditorUtility.SetDirty(definition);AssetDatabase.SaveAssets();
    }
    [MenuItem("Beat Em Up/Story/Update school fair activities and illustrated escape")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        Configure(AssetDatabase.LoadAssetAtPath<PrologueDefinition>(PrologueSetup.Root+"/Prologue.asset"));
        MultiplayerSetup.Build();Debug.Log("SCHOOL FAIR: four student activities and illustrated escape assigned.");
    }
    static void Poll()
    {
        const string request="Temp/SchoolFairSequenceBuild.request";
        if(!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(request);Build();
    }
}
