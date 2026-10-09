using System;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HubReadabilitySetup
{
    public const string Art="Assets/EQ_Rung_BeatEmUp/ArtAssets/Props/HubInteractables/";
    public const string Prefabs="Assets/EQ_Rung_BeatEmUp/Prefabs/HubInteractables/";
    public static readonly string[] Names={"CharacterWardrobe","StatBlessingGong","SkillFlameShrine","WorldEntranceGate"};
    public static readonly string[] Labels={"Change Character","Upgrade Base Stats","Upgrade Skill","Enter World 1"};
    [MenuItem("Beat Em Up/Hub/Improve interactable readability")]
    public static void Build()
    {
        AssetDatabase.Refresh();
        foreach(var file in Directory.GetFiles(Art,"*.png"))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=100;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteAlignment=(int)SpriteAlignment.Custom;
            settings.spritePivot=file.Contains("Cue")||file.Contains("Diamond")?new Vector2(.5f,.5f):new Vector2(.5f,8f/208);
            settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);importer.SaveAndReimport();
        }
        var material=AssetDatabase.LoadAssetAtPath<Material>(Art+"InteractableOutline.mat");
        if(!material){material=new Material(Shader.Find("BeatEmUp/Interactable Pixel Outline"));AssetDatabase.CreateAsset(material,Art+"InteractableOutline.mat");}
        var environment=PrefabUtility.LoadPrefabContents(PlayerSanctuarySetup.EnvironmentPath);
        try
        {
            var definition=environment.GetComponent<HubLandmarks>().definition;
            foreach(var old in environment.GetComponentsInChildren<HubInteractionStation>(true).Where(s=>s.stationIndex!=3).ToArray())UnityEngine.Object.DestroyImmediate(old.gameObject);
            var oldPortal=environment.GetComponentInChildren<HubWorldPortal>(true);
            GameObject portalTemplate=oldPortal?UnityEngine.Object.Instantiate(oldPortal.gameObject):new GameObject(Names[3]);
            portalTemplate.transform.SetParent(null);portalTemplate.name=Names[3];
            if(oldPortal)UnityEngine.Object.DestroyImmediate(oldPortal.gameObject);
            for(int i=0;i<4;i++)
            {
                var go=i==3?portalTemplate:new GameObject(Names[i]);
                foreach(var old in go.GetComponentsInChildren<HubInteractionStation>(true))UnityEngine.Object.DestroyImmediate(old);
                var existingArt=go.transform.Find("Readable foreground");if(existingArt)UnityEngine.Object.DestroyImmediate(existingArt.gameObject);
                var station=go.AddComponent<HubInteractionStation>();station.definition=definition;station.stationIndex=i;station.displayName=Labels[i];
                var parts=new GameObject("Readable foreground");parts.transform.SetParent(go.transform,false);
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+Names[i]+".png");
                station.body=Renderer(parts.transform,"Object sprite",sprite,new Vector3(0,.24f,0));
                station.outline=Renderer(parts.transform,"Pixel outline",sprite,new Vector3(0,.24f,0));station.outline.sharedMaterial=material;
                station.groundCue=Renderer(parts.transform,"Approach marker",AssetDatabase.LoadAssetAtPath<Sprite>(Art+"GroundCue.png"),Vector3.zero);
                station.indicator=Renderer(parts.transform,"Nearby interaction diamond",AssetDatabase.LoadAssetAtPath<Sprite>(Art+"InteractDiamond.png"),Vector3.zero);
                station.indicator.transform.localScale=Vector3.one*1.5f;
                station.indicatorHeight=new[]{1.8f,1.5f,1.8f,2.25f}[i];
                var foot=new GameObject("Solid object footprint");foot.transform.SetParent(parts.transform,false);
                station.footprint=foot.AddComponent<BoxCollider2D>();station.footprint.size=new Vector2(.7f,.12f);station.footprint.offset=new Vector2(0,.34f);
                var wall=foot.AddComponent<CombatWall>();wall.allowsBounce=false;wall.debugDraw=false;
                var portal=go.GetComponent<HubWorldPortal>();
                if(portal)
                {
                    portal.vfxScale=new Vector3(.45f,.45f,.45f);portal.visualHeight=.16f;portal.sortingOffset=-26;
                    if(portal.entranceLabel)portal.entranceLabel.gameObject.SetActive(false);
                    portal.RefreshPresentation();
                }
                station.RefreshPresentation();
                var prefab=PrefabUtility.SaveAsPrefabAsset(go,Prefabs+Names[i]+".prefab");UnityEngine.Object.DestroyImmediate(go);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);instance.transform.SetParent(environment.transform,false);
                instance.GetComponent<HubInteractionStation>().RefreshPresentation();
            }
            PrefabUtility.SaveAsPrefabAsset(environment,PlayerSanctuarySetup.EnvironmentPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(environment); }
        AssetDatabase.SaveAssets();
    }
    static SpriteRenderer Renderer(Transform parent,string name,Sprite sprite,Vector3 position)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;
        var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite;return renderer;
    }
}
