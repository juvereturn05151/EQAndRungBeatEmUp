using System;
using System.IO;
using System.Linq;
using BeatEmUp;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class CharacterSelectSetup
{
    public const string Root = "Assets/EQ_Rung_BeatEmUp/Resources/CharacterSelect";
    const string Request = "Temp/CharacterSelect.setup-request";
    static CharacterSelectSetup() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if(!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        bool rebuild=File.ReadAllText(Request).Trim()=="rebuild"; File.Delete(Request);
        try { BuildAssets(rebuild); File.WriteAllText("Documentation/CharacterSelectSetupResults.txt", "PASS: Unity compiled and created Character Select assets and prefabs.\n"); }
        catch(Exception exception) { File.WriteAllText("Documentation/CharacterSelectSetupResults.txt", exception.ToString()); Debug.LogException(exception); }
    }
    [MenuItem("Beat Em Up/Character Select/Create or refresh screen")]
    public static void Build() => BuildAssets(false);
    [MenuItem("Beat Em Up/Character Select/Rebuild generated visual layout")]
    public static void RebuildVisuals() => BuildAssets(true);
    static void BuildAssets(bool rebuild)
    {
        if(EditorApplication.isPlaying) return;
        Directory.CreateDirectory(Root); AssetDatabase.Refresh();
        var catalog = AssetDatabase.LoadAssetAtPath<MultiplayerCatalog>(MultiplayerSetup.CatalogPath);
        if(!catalog) throw new InvalidOperationException("Create the multiplayer catalog first.");
        var definitions = catalog.characters.Select(CreateDefinition).ToArray();
        // Preserve authored definitions and include future characters; existing gameplay indices remain stable.
        catalog.selectionCharacters = definitions.Concat(AssetDatabase.FindAssets("t:CharacterDefinition")
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<CharacterDefinition>))
            .Where(c=>c).Distinct().ToArray();
        foreach(var definition in catalog.selectionCharacters)
            if(definition.GameplayCharacter && !catalog.characters.Contains(definition.GameplayCharacter))
                catalog.characters = catalog.characters.Concat(new[]{definition.GameplayCharacter}).ToArray();
        EditorUtility.SetDirty(catalog);
        var portrait = AssetDatabase.LoadAssetAtPath<CharacterPortraitUI>(Root+"/CharacterPortraitUI.prefab");
        if(!portrait || rebuild) portrait=BuildPortrait();
        var screen = AssetDatabase.LoadAssetAtPath<CharacterSelectManager>(Root+"/CharacterSelectCanvas.prefab");
        if(!screen || rebuild) screen=BuildScreen(catalog,portrait);
        else
        {
            var contents=PrefabUtility.LoadPrefabContents(Root+"/CharacterSelectCanvas.prefab");
            try { contents.GetComponent<CharacterSelectManager>().Characters=catalog.selectionCharacters; PrefabUtility.SaveAsPrefabAsset(contents,Root+"/CharacterSelectCanvas.prefab"); }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        // Standalone canvases built in an empty editor scene may serialize a zero driven transform.
        // Store useful prefab-stage dimensions; Bind also normalizes placement under the menu canvas.
        var canvasTransform=new SerializedObject(screen.transform);
        canvasTransform.FindProperty("m_LocalScale").vector3Value=Vector3.one;
        canvasTransform.FindProperty("m_AnchorMin").vector2Value=new Vector2(0,1);
        canvasTransform.FindProperty("m_AnchorMax").vector2Value=new Vector2(0,1);
        canvasTransform.FindProperty("m_Pivot").vector2Value=new Vector2(0,1);
        canvasTransform.FindProperty("m_SizeDelta").vector2Value=new Vector2(1920,1080);
        canvasTransform.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        MultiplayerSetup.Build();
        Debug.Log("CHARACTER SELECT: Created " + screen.name + " with " + catalog.selectionCharacters.Length + " roster entries.");
    }
    static CharacterDefinition CreateDefinition(PlayableCharacterData gameplay)
    {
        string path = Root + "/" + gameplay.characterId + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path); if(existing) return existing;
        var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
        definition.GameplayCharacter = gameplay; definition.CharacterId = gameplay.characterId;
        definition.DisplayName = gameplay.displayName; definition.CharacterPrefab = gameplay.prefab;
        definition.PortraitSprite = gameplay.portrait; definition.LargePreviewSprite = gameplay.idlePose;
        if(gameplay.characterId == "blue-shirt")
        {
            const string idle = "Assets/EQ_Rung_BeatEmUp/ArtAssets/Characters/BlueShirtGuy/Animations/Idle/";
            definition.PortraitSprite = definition.LargePreviewSprite = AssetDatabase.LoadAssetAtPath<Sprite>(idle + "BlueShirtGuy_Idle2_01.png");
            definition.CharacterSelectAnimation = Enumerable.Range(1,8).Select(i=>AssetDatabase.LoadAssetAtPath<Sprite>(idle + "BlueShirtGuy_Idle2_" + i.ToString("00") + ".png")).Where(s=>s).ToArray();
            definition.DisplayName = "BLUE SHIRT GUY"; definition.Archetype = "BRAWLER";
            definition.Power = 4; definition.Speed = 3; definition.Defense = 4; definition.Technique = 2;
            definition.Description = "Fists up. Spirits down.\nA stubborn schoolyard fighter.";
        }
        else if(gameplay.characterId == "gray-shirt") { definition.DisplayName = "GRAY SHIRT GUY"; definition.Archetype = "TECHNICAL"; definition.Power = 2; definition.Speed = 4; definition.Defense = 3; definition.Technique = 5; definition.Description = "Keep your distance. Break the curse.\nWand barriers and quick strikes."; }
        AssetDatabase.CreateAsset(definition,path); return definition;
    }
    static RectTransform Rect(GameObject go, Transform parent, Rect area)
    {
        go.transform.SetParent(parent,false); var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1);
        rect.anchoredPosition = new Vector2(area.x,-area.y); rect.sizeDelta = area.size; return rect;
    }
    static Image Panel(string name, Transform parent, Rect area, Color color)
    {
        var go = new GameObject(name,typeof(RectTransform),typeof(Image)); Rect(go,parent,area);
        var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
    }
    static Text Label(string value, Transform parent, Rect area, int size, Color color)
    {
        var go = new GameObject(value,typeof(RectTransform),typeof(Text)); Rect(go,parent,area);
        var label = go.GetComponent<Text>(); label.text = value; label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontStyle = FontStyle.Bold; label.fontSize = size; label.color = color; label.raycastTarget = false;
        return label;
    }
    static Image Frame(string name, Transform parent, Rect area, Color color, int thickness)
    {
        // A hollow sprite generated as a native UI asset. Point filtering keeps edges crisp.
        var image = Panel(name,parent,area,color);
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/PixelFrame.png"); image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f / thickness;
        return image;
    }
    static void CreateFrame()
    {
        string path = Root + "/PixelFrame.png";
        if(File.Exists(path)) return;
        var texture = new Texture2D(8,8,TextureFormat.RGBA32,false);
        for(int y=0;y<8;y++) for(int x=0;x<8;x++) texture.SetPixel(x,y,x==0 || x==7 || y==0 || y==7 ? Color.white : Color.clear);
        texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Single; importer.spriteBorder=new Vector4(1,1,1,1); importer.spritePixelsPerUnit=100;
        importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
    }
    static CharacterPortraitUI BuildPortrait()
    {
        CreateFrame();
        var root = new GameObject("CharacterPortraitUI",typeof(RectTransform),typeof(Image),typeof(Button),typeof(CharacterPortraitUI));
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(156,156);
        root.GetComponent<Image>().color = new Color(.06f,.065f,.095f);
        root.GetComponent<Button>().navigation = new Navigation { mode = Navigation.Mode.None };
        var view = root.GetComponent<CharacterPortraitUI>();
        Frame("Normal frame",root.transform,new Rect(0,0,156,156),new Color(.38f,.39f,.48f),2);
        var crop = Panel("Portrait crop",root.transform,new Rect(7,7,142,142),Color.white);
        crop.gameObject.AddComponent<Mask>().showMaskGraphic=false;
        view.PortraitImage = Panel("Portrait Image",crop.transform,new Rect(-78,-18,300,320),Color.white);
        view.PortraitImage.preserveAspect=true;
        view.SelectionBorder = Frame("Selection Border",root.transform,new Rect(0,0,156,156),Color.white,4);
        view.PlayerMarker = Label("P1",root.transform,new Rect(4,4,75,25),18,Color.white);
        view.ReadyIndicator = Label("READY",root.transform,new Rect(8,125,140,26),20,Color.white); view.ReadyIndicator.alignment=TextAnchor.MiddleCenter;
        view.PlayerBorders = new Image[4]; view.PlayerMarkers = new Text[4];
        for(int i=0;i<4;i++)
        {
            view.PlayerBorders[i]=Frame("P"+(i+1)+" border",root.transform,new Rect(i*3,i*3,156-i*6,156-i*6),CharacterPortraitUI.PlayerColors[i],2);
            var marker=Panel("P"+(i+1)+" marker",root.transform,new Rect((i%2)*78,(i/2)*27,78,25),new Color(.01f,.01f,.02f,.9f));
            view.PlayerMarkers[i]=Label("P"+(i+1),marker.transform,new Rect(2,1,76,24),14,CharacterPortraitUI.PlayerColors[i]);
            // Parent remains transparent; disabled text/border fully removes unowned cursors.
            marker.color=Color.clear; view.PlayerBorders[i].enabled=false; view.PlayerMarkers[i].enabled=false;
        }
        view.LockedOverlay=Panel("Locked Overlay",root.transform,new Rect(7,7,142,142),new Color(0,0,0,.82f)).gameObject;
        var locked=Label("?",view.LockedOverlay.transform,new Rect(0,15,142,100),70,Color.white); locked.alignment=TextAnchor.MiddleCenter;
        view.SelectionBorder.enabled=false; view.PlayerMarker.enabled=false; view.ReadyIndicator.enabled=false; view.LockedOverlay.SetActive(false);
        var saved=PrefabUtility.SaveAsPrefabAsset(root,Root+"/CharacterPortraitUI.prefab").GetComponent<CharacterPortraitUI>();
        UnityEngine.Object.DestroyImmediate(root); return saved;
    }
    static Button Button(string value, Transform parent, Rect area, Color color)
    {
        var image=Panel(value,parent,area,new Color(.025f,.025f,.04f,.95f)); image.raycastTarget=true;
        var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image; button.navigation=new Navigation { mode=Navigation.Mode.None };
        Frame("Button frame",image.transform,new Rect(0,0,area.width,area.height),color,2);
        var label=Label(value,image.transform,new Rect(8,4,area.width-16,area.height-8),24,color); label.alignment=TextAnchor.MiddleCenter;
        return button;
    }
    static CharacterStatBar Stat(string name, Transform parent, int y, Color color)
    {
        Label(name,parent,new Rect(62,y,190,30),23,new Color(.88f,.85f,.82f));
        var go=new GameObject(name+"Bar",typeof(RectTransform),typeof(CharacterStatBar)); Rect(go,parent,new Rect(260,y,250,28));
        var bar=go.GetComponent<CharacterStatBar>(); bar.FillColor=color; bar.Segments=new Image[5];
        for(int i=0;i<5;i++) { bar.Segments[i]=Panel("Segment "+i,go.transform,new Rect(i*43,2,37,22),color); Frame("Edge",go.transform,new Rect(i*43,2,37,22),new Color(.3f,.28f,.31f),1); }
        return bar;
    }
    static CharacterSelectManager BuildScreen(MultiplayerCatalog catalog, CharacterPortraitUI portrait)
    {
        var root=new GameObject("CharacterSelectCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(CharacterSelectManager),typeof(AudioSource));
        root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay; root.GetComponent<Canvas>().sortingOrder=101; root.GetComponent<Canvas>().pixelPerfect=true;
        var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
        root.GetComponent<RectTransform>().sizeDelta=new Vector2(1920,1080);
        var view=root.GetComponent<CharacterSelectManager>(); view.Characters=catalog.selectionCharacters; view.GameplayScene=catalog.gameplayScene; view.Audio=root.GetComponent<AudioSource>(); view.Audio.playOnAwake=false;
        view.BackgroundSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/EQ_Rung_BeatEmUp/ArtAssets/Environments/HauntedHouse/Stage02_BloodSheetCorridor/Background/Stage02_BloodSheetCorridor_Background_v2.png");
        view.Background=Panel("Haunted corridor",root.transform,new Rect(0,0,1920,1080),new Color(.45f,.37f,.45f)); view.Background.sprite=view.BackgroundSprite;
        Panel("Night shade",root.transform,new Rect(0,0,1920,1080),new Color(.025f,.018f,.04f,.6f));
        Panel("Preview shadow",root.transform,new Rect(0,0,610,1080),new Color(.008f,.01f,.022f,.72f));
        Panel("Roster shadow",root.transform,new Rect(610,590,1310,490),new Color(.008f,.01f,.02f,.87f));
        var crimson=new Color(.85f,.08f,.17f);
        Label("GHOST FAIR",root.transform,new Rect(650,55,600,52),38,crimson);
        Label("CHOOSE YOUR FIGHTER",root.transform,new Rect(650,109,1100,78),56,new Color(.96f,.91f,.83f));
        Label("THE SCHOOL GATES ARE OPEN.",root.transform,new Rect(652,194,900,40),22,new Color(.65f,.62f,.68f));
        Frame("Preview corners",root.transform,new Rect(40,48,515,545),new Color(.22f,.17f,.25f),2);
        view.LargePreview=Panel("LargePreview",root.transform,new Rect(65,60,460,530),Color.white); view.LargePreview.preserveAspect=true;
        // Rough arcade name plate uses staggered rectangular strokes, all native UI.
        for(int i=0;i<4;i++) { var stroke=Panel("Name slash "+i,root.transform,new Rect(40+i*6,612+i*11,510-i*12,17),crimson); stroke.rectTransform.localEulerAngles=new Vector3(0,0,4); }
        view.CharacterName=Label("CHARACTER",root.transform,new Rect(60,610,490,60),38,Color.white); view.CharacterName.fontStyle=FontStyle.BoldAndItalic;
        view.ArchetypeText=Label("BRAWLER",root.transform,new Rect(63,683,475,38),24,new Color(1,.66f,.35f));
        view.PowerBar=Stat("POWER",root.transform,741,CharacterPortraitUI.PlayerColors[0]);
        view.SpeedBar=Stat("SPEED",root.transform,780,CharacterPortraitUI.PlayerColors[2]);
        view.DefenseBar=Stat("DEFENSE",root.transform,819,CharacterPortraitUI.PlayerColors[1]);
        view.TechniqueBar=Stat("TECHNIQUE",root.transform,858,CharacterPortraitUI.PlayerColors[3]);
        view.DescriptionText=Label("",root.transform,new Rect(63,907,490,88),22,new Color(.68f,.66f,.72f));
        view.RoomText=Label("LOCAL",root.transform,new Rect(650,510,1150,50),24,new Color(.77f,.7f,.68f));
        var slots=new CharacterSelectPlayerSlot[4];
        for(int i=0;i<4;i++)
        {
            var panel=Panel("Player"+(i+1)+"Slot",root.transform,new Rect(650+i*294,575,278,98),new Color(.05f,.045f,.07f,.96f));
            var slot=panel.gameObject.AddComponent<CharacterSelectPlayerSlot>(); slots[i]=slot;
            slot.ColorStrip=Panel("ColorStrip",panel.transform,new Rect(0,0,278,5),CharacterPortraitUI.PlayerColors[i]);
            slot.PlayerLabel=Label("P"+(i+1),panel.transform,new Rect(15,22,60,58),34,CharacterPortraitUI.PlayerColors[i]);
            slot.StateLabel=Label("PRESS ENTER / A\nTO JOIN",panel.transform,new Rect(80,24,190,68),18,Color.white);
        }
        view.Player1Slot=slots[0]; view.Player2Slot=slots[1]; view.Player3Slot=slots[2]; view.Player4Slot=slots[3];
        var viewport=Panel("Roster viewport",root.transform,new Rect(650,707,1170,245),new Color(0,0,0,0)); viewport.gameObject.AddComponent<RectMask2D>();
        var content=new GameObject("RosterContainer",typeof(RectTransform),typeof(GridLayoutGroup),typeof(ContentSizeFitter));
        view.RosterContainer=Rect(content,viewport.transform,new Rect(10,10,1145,220));
        var grid=content.GetComponent<GridLayoutGroup>(); grid.cellSize=new Vector2(156,156); grid.spacing=new Vector2(24,24); grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount=6;
        content.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.content=view.RosterContainer; scroll.viewport=viewport.rectTransform; scroll.horizontal=false; scroll.vertical=true; scroll.movementType=ScrollRect.MovementType.Clamped;
        view.PortraitPrefab=portrait;
        Label("ENTER / A: JOIN + CONFIRM     ESC / B: CANCEL",root.transform,new Rect(650,965,1170,34),21,new Color(.72f,.68f,.72f));
        view.ConfirmPrompt=Button("A / ENTER  CONFIRM",root.transform,new Rect(650,1012,365,52),CharacterPortraitUI.PlayerColors[3]);
        view.CancelPrompt=Button("B / ESC  BACK",root.transform,new Rect(1030,1012,300,52),CharacterPortraitUI.PlayerColors[0]);
        view.StartPrompt=Button("WAITING FOR READY",root.transform,new Rect(1345,1012,475,52),CharacterPortraitUI.PlayerColors[2]);
        // Short generated arcade tones; editable AudioClip assignments on the prefab.
        view.NavigateSFX=Tone("Navigate",420,.045f); view.ConfirmSFX=Tone("Confirm",740,.09f); view.CancelSFX=Tone("Cancel",180,.075f);
        var saved=PrefabUtility.SaveAsPrefabAsset(root,Root+"/CharacterSelectCanvas.prefab").GetComponent<CharacterSelectManager>(); UnityEngine.Object.DestroyImmediate(root); return saved;
    }
    static AudioClip Tone(string name, float frequency, float duration)
    {
        string path=Root+"/"+name+".wav";
        if(!File.Exists(path))
        {
            int count=(int)(22050*duration);
            using(var writer=new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36+count*2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(22050); writer.Write(44100); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count*2);
                for(int i=0;i<count;i++) writer.Write((short)(Mathf.Sign(Mathf.Sin(i*frequency*2*Mathf.PI/22050))*.08f*short.MaxValue*(1-i/(float)count)));
            }
            AssetDatabase.ImportAsset(path);
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
