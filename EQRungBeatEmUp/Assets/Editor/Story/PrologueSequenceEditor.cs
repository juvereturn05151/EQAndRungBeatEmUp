using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BeatEmUp.Story;
using UnityEditor;
using UnityEngine;

public sealed partial class PrologueSequenceEditor : EditorWindow
{
    const string DefinitionPath="Assets/EQ_Rung_BeatEmUp/Story/Prologue.asset";
    static readonly string[] SequenceFields={"reunion","attack","escape","cornered","sanctuaryIntro","kidnapping","defeat","awakening","rescue","memorySpell","returnPresent","festivalChickenMemory","festivalPepsiMemory"};
    static readonly string[] SequenceNames={"01  Reunion","02  Chanai at the festival","03  Festival escape","04  Teleport to sanctuary","05  Sealed memories","06  Cream kidnapped","07  Scripted defeat","08  Powers awaken","09  Cream rescued","10  Memory spell","11  Return to the present","Festival · Chicken Pop memory","Festival · Pepsi memory"};
    [SerializeField] PrologueDefinition definition;
    [SerializeField] CutsceneSequence sequence;
    [SerializeField] int selected, chapter;
    [SerializeField] bool finalList, stageView, showGuides=true, thai;
    [SerializeField] float time, dialogueSeconds=3, previewSpeed=1;
    Vector2 listScroll, detailScroll, timelineScroll, chapterScroll;
    bool playing, loop, settings=true, storeSettings;
    double lastTick;
    int drag;
    Rect world, canvas;
    List<PrologueSequencePreview.Span> schedule;
    float length;
    GUIStyle wrap;

    [MenuItem("Beat Em Up/Story/Prologue sequence editor")]
    public static void Open()
    {
        var window=GetWindow<PrologueSequenceEditor>("Prologue Studio");window.minSize=new Vector2(1060,800);
        if(!window.definition)window.definition=AssetDatabase.LoadAssetAtPath<PrologueDefinition>(DefinitionPath);
        if(!window.sequence && window.definition)window.PickChapter(0);
        if(!window.docked)window.position=new Rect(80,80,1280,850);
        window.Show();window.Focus();
    }
    void OnEnable()
    {
        minSize=new Vector2(1060,800);EditorApplication.update+=Tick;Undo.undoRedoPerformed+=Changed;
        if(!definition)definition=AssetDatabase.LoadAssetAtPath<PrologueDefinition>(DefinitionPath);
        if(!sequence && definition)PickChapter(0);
    }
    void OnDisable() { EditorApplication.update-=Tick;Undo.undoRedoPerformed-=Changed; }
    void Changed() { playing=false;Repaint(); }
    void Tick()
    {
        double now=EditorApplication.timeSinceStartup;
        if(playing && sequence && definition)
        {
            time+=(float)Math.Min(.1,now-lastTick)*previewSpeed;
            if(time>=length) { if(loop && length>0)time%=length;else { time=length;playing=false; } }
            Repaint();
        }
        lastTick=now;
    }
    CutsceneSequence ChapterSequence(int index)
    {
        if(!definition)return null;
        return (CutsceneSequence)typeof(PrologueDefinition).GetField(SequenceFields[index]).GetValue(definition);
    }
    void PickChapter(int index)
    {
        chapter=index;sequence=ChapterSequence(index);selected=0;time=0;playing=false;finalList=false;
        listScroll=detailScroll=Vector2.zero;
    }
    void OnGUI()
    {
        wrap ??= new GUIStyle(EditorStyles.wordWrappedLabel);
        using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("PROLOGUE STUDIO",EditorStyles.boldLabel,GUILayout.Width(160));
            var next=(PrologueDefinition)EditorGUILayout.ObjectField(definition,typeof(PrologueDefinition),false,GUILayout.Width(210));
            if(next!=definition) { definition=next;if(definition)PickChapter(0); }
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("Save assets",EditorStyles.toolbarButton,GUILayout.Width(85)))
            {
                if(definition)AssetDatabase.SaveAssetIfDirty(definition);
                if(sequence)AssetDatabase.SaveAssetIfDirty(sequence);
                if(definition && definition.dialogue)AssetDatabase.SaveAssetIfDirty(definition.dialogue);
            }
            if(GUILayout.Button("User guide",EditorStyles.toolbarButton,GUILayout.Width(80)))Application.OpenURL(Path.GetFullPath("Documentation/PrologueEditor.md"));
        }
        if(!definition) { EditorGUILayout.HelpBox("Choose a Prologue definition asset to begin.",MessageType.Info);return; }
        schedule=PrologueSequencePreview.Schedule(sequence,definition.dialogue,dialogueSeconds);length=PrologueSequencePreview.Length(schedule);time=Mathf.Clamp(time,0,length);
        using(new EditorGUILayout.HorizontalScope())
        {
            using(new EditorGUILayout.VerticalScope(GUILayout.Width(215)))DrawChapters();
            if(tutorialMode)
            {
                using(new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))DrawTutorialPreview();
                using(new EditorGUILayout.VerticalScope(GUILayout.Width(315)))DrawTutorialSettings();
            }
            else
            {
            using(new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                DrawTransport();
                if(sequence)
                {
                    var frame=PrologueSequencePreview.Evaluate(definition,sequence,time,dialogueSeconds,contextSequence:ChapterSequence(chapter));
                    DrawPreview(frame);DrawTimeline();DrawDialogue(frame);DrawNotes(frame);
                }
                else EditorGUILayout.HelpBox("Select or create a sequence asset.",MessageType.Info);
            }
            using(new EditorGUILayout.VerticalScope(GUILayout.Width(315)))DrawEvents();
            }
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("Edit Mode preview · Dialogue timing is estimated; combat, audio and gameplay signals require Play Mode.  Build/reset prologue assets overwrites authored sequences.",EditorStyles.miniLabel);
    }
    void DrawChapters()
    {
        if(GUILayout.Button("Playable tutorial",GUILayout.Height(26))) { tutorialMode=true;playing=false;GUIUtility.ExitGUI(); }
        GUILayout.Label("STORY SEQUENCES",EditorStyles.boldLabel);
        chapterScroll=EditorGUILayout.BeginScrollView(chapterScroll,GUILayout.Height(285));
        for(int i=0;i<SequenceFields.Length;i++)
        {
            var old=GUI.backgroundColor;if(sequence && sequence==ChapterSequence(i))GUI.backgroundColor=new Color(.45f,.7f,1);
            if(GUILayout.Button(SequenceNames[i],GUILayout.Height(23))) { tutorialMode=false;PickChapter(i);GUIUtility.ExitGUI(); }GUI.backgroundColor=old;
        }
        EditorGUILayout.EndScrollView();
        if(tutorialMode)
        {
            GUILayout.Label("Choose a practice step on the right. Drag the green walking edges, yellow goal, or orange spawn markers.",wrap);
            stageView=EditorGUILayout.Toggle("Show whole stage",stageView);
            showGuides=EditorGUILayout.Toggle("Position / lane guides",showGuides);
            if(GUILayout.Button("Select definition in Inspector"))Selection.activeObject=definition;
            return;
        }
        var next=(CutsceneSequence)EditorGUILayout.ObjectField("Sequence",sequence,typeof(CutsceneSequence),false);
        if(next!=sequence) { sequence=next;time=0;selected=0;playing=false;GUIUtility.ExitGUI(); }
        if(GUILayout.Button("Duplicate sequence..."))DuplicateSequence();
        if(sequence && sequence!=ChapterSequence(chapter) && GUILayout.Button("Assign to "+SequenceNames[chapter]))
        {
            Undo.RecordObject(definition,"Assign prologue sequence");typeof(PrologueDefinition).GetField(SequenceFields[chapter]).SetValue(definition,sequence);EditorUtility.SetDirty(definition);
        }
        bool nextSettings=EditorGUILayout.Foldout(settings,"Festival settings",true);
        if(nextSettings && !settings)storeSettings=false;
        settings=nextSettings;
        if(settings)
        {
            var data=new SerializedObject(definition);data.Update();
            EditorGUILayout.PropertyField(data.FindProperty("festivalWidth"),new GUIContent("Map width"));
            EditorGUILayout.PropertyField(data.FindProperty("festivalLaneMin"),new GUIContent("Lane bottom Y"));
            EditorGUILayout.PropertyField(data.FindProperty("festivalLaneMax"),new GUIContent("Lane top Y"));
            data.ApplyModifiedProperties();
            GUILayout.Label("Width also sets the panorama scale. Drag the green lane edges in the preview. Visit both stores to finish exploration.",wrap);
        }
        GUILayout.Space(6);
        bool nextStoreSettings=EditorGUILayout.Foldout(storeSettings,"Festival stores",true);
        if(nextStoreSettings && !storeSettings)settings=false;
        storeSettings=nextStoreSettings;
        if(storeSettings)
        {
            var data=new SerializedObject(definition);data.Update();
            EditorGUILayout.PropertyField(data.FindProperty("festivalChickenPosition"),new GUIContent("Chicken Pop position"));
            EditorGUILayout.PropertyField(data.FindProperty("festivalPepsiPosition"),new GUIContent("Pepsi position"));
            EditorGUILayout.PropertyField(data.FindProperty("festivalStoreWidth"),new GUIContent("Stall width"));
            EditorGUILayout.PropertyField(data.FindProperty("festivalStoreInteractRadius"),new GUIContent("Talk radius"));
            data.ApplyModifiedProperties();
            GUILayout.Label("Both memory conversations are required before Chanai arrives. Choose their entries above to edit dialogue.",wrap);
        }
        GUILayout.Label("PREVIEW",EditorStyles.boldLabel);
        stageView=EditorGUILayout.Toggle("Show whole stage",stageView);
        showGuides=EditorGUILayout.Toggle("Position / lane guides",showGuides);
        thai=EditorGUILayout.Toggle("Thai dialogue",thai);
        dialogueSeconds=EditorGUILayout.Slider("Seconds per line",dialogueSeconds,.5f,8);
        GUILayout.Label("Starting cast uses chapter defaults. Each sequence's Spawn events define exact opening positions.",wrap);
        if(GUILayout.Button("Select definition in Inspector"))Selection.activeObject=definition;
    }
    void DuplicateSequence()
    {
        if(!sequence)return;
        string path=EditorUtility.SaveFilePanelInProject("Duplicate prologue sequence",sequence.name+"_Copy","asset","Choose a location for the new sequence.");
        if(string.IsNullOrEmpty(path))return;
        var copy=Instantiate(sequence);copy.name=Path.GetFileNameWithoutExtension(path);copy.id=copy.name;
        AssetDatabase.CreateAsset(copy,path);Undo.RegisterCreatedObjectUndo(copy,"Duplicate story sequence");sequence=copy;time=0;selected=0;playing=false;AssetDatabase.SaveAssetIfDirty(copy);
    }
    void DrawTransport()
    {
        using(new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if(GUILayout.Button("|<",EditorStyles.toolbarButton,GUILayout.Width(30))) { time=0;playing=false; }
            if(GUILayout.Button(playing ? "Pause" : "Play",EditorStyles.toolbarButton,GUILayout.Width(48))) { playing=!playing;lastTick=EditorApplication.timeSinceStartup;if(playing && time>=length)time=0; }
            if(GUILayout.Button("End / skip",EditorStyles.toolbarButton,GUILayout.Width(70))) { time=length;playing=false; }
            loop=GUILayout.Toggle(loop,"Loop",EditorStyles.toolbarButton,GUILayout.Width(38));
            previewSpeed=EditorGUILayout.FloatField(previewSpeed,GUILayout.Width(35));previewSpeed=Mathf.Clamp(previewSpeed,.1f,4);
            GUILayout.Label("×",GUILayout.Width(12));GUILayout.FlexibleSpace();
            GUILayout.Label(time.ToString("0.00")+" / "+length.ToString("0.00")+" s",EditorStyles.miniLabel);
        }
        var next=GUILayout.HorizontalSlider(time,0,Mathf.Max(.01f,length));if(next!=time) { time=next;playing=false;Repaint(); }
    }
    void DrawPreview(PrologueSequencePreview.Frame frame)
    {
        Rect allocation=GUILayoutUtility.GetRect(200,Mathf.Clamp(position.height-400,200,340),GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(allocation,new Color(.045f,.055f,.075f));
        float aspect=stageView ? Mathf.Max(frame.environmentWidth/6.4f,16f/9) : 16f/9;
        float width=Mathf.Min(allocation.width,allocation.height*aspect),height=width/aspect;
        canvas=new Rect(allocation.x+(allocation.width-width)/2,allocation.y+(allocation.height-height)/2,width,height);
        world=stageView ? new Rect(-frame.environmentWidth/2,1.6f-3.2f,frame.environmentWidth,6.4f) : new Rect(frame.camera.x-frame.zoom*16f/9,frame.camera.y-frame.zoom,frame.zoom*32f/9,frame.zoom*2);
        GUI.BeginGroup(canvas);
        if(frame.environment)
        {
            float scale=Mathf.Max(6.4f/frame.environment.bounds.size.y,frame.environmentWidth/frame.environment.bounds.size.x);
            DrawSprite(frame.environment,new Vector3(0,1.6f),scale,false,Color.white);
        }
        foreach(var actor in frame.actors.Values.Where(a=>a.visible).OrderByDescending(a=>a.name.StartsWith("Student") ? 100 : a.position.y))
        {
            DrawSprite(actor.sprite,actor.position,actor.scale,actor.flip,actor.color);
            if(showGuides)
            {
                Vector2 feet=ToScreen(actor.position);EditorGUI.DrawRect(new Rect(feet.x-3,feet.y-1,6,2),Color.cyan);
                GUI.Label(new Rect(feet.x-40,feet.y+2,80,18),actor.name,EditorStyles.whiteMiniLabel);
            }
        }
        if(frame.illustration)GUI.DrawTexture(new Rect(0,0,canvas.width,canvas.height),frame.illustration.texture,ScaleMode.ScaleAndCrop);
        foreach(var effect in frame.effects)
        {
            Vector3 point=frame.actors.TryGetValue(effect.target ?? "",out var actor) ? actor.position+Vector3.up*.6f : effect.position;
            Handles.BeginGUI();Handles.color=effect.color;var center=ToScreen(point);
            float radius=20+Mathf.PingPong(time*40,15);var points=new Vector3[33];for(int i=0;i<points.Length;i++)points[i]=new Vector3(center.x+Mathf.Cos(i*Mathf.PI/16)*radius,center.y+Mathf.Sin(i*Mathf.PI/16)*radius);
            Handles.DrawAAPolyLine(2,points);Handles.EndGUI();
        }
        if(frame.fade>0)EditorGUI.DrawRect(new Rect(0,0,canvas.width,canvas.height),new Color(frame.fadeColor.r,frame.fadeColor.g,frame.fadeColor.b,Mathf.Clamp01(frame.fade)));
        if(showGuides) { if(tutorialMode)DrawTutorialGuides();else DrawGuides(frame); }
        GUI.EndGroup();
        GUILayout.Label(tutorialMode ? "Tutorial preview · drag area edges, goal, and spawn markers" : stageView ? "Whole stage · dashed frame shows the camera" : "Camera preview · drag the selected event's orange target to place it",EditorStyles.miniLabel);
    }
    Vector2 ToScreen(Vector3 point) => new Vector2((point.x-world.xMin)/world.width*canvas.width,(world.yMax-point.y)/world.height*canvas.height);
    Vector3 ToWorld(Vector2 point,float z=0) => new Vector3(world.xMin+point.x/canvas.width*world.width,world.yMax-point.y/canvas.height*world.height,z);
    void DrawSprite(Sprite sprite,Vector3 point,float scale,bool flip,Color color)
    {
        if(!sprite || !sprite.texture)return;
        var pixels=sprite.rect;float ppu=sprite.pixelsPerUnit;
        float pivotX=flip ? pixels.width-sprite.pivot.x : sprite.pivot.x;
        Vector2 top=ToScreen(point+new Vector3(-pivotX/ppu,(pixels.height-sprite.pivot.y)/ppu)*scale);
        var rect=new Rect(top.x,top.y,pixels.width/ppu*scale/world.width*canvas.width,pixels.height/ppu*scale/world.height*canvas.height);
        var uv=new Rect(pixels.x/sprite.texture.width,pixels.y/sprite.texture.height,pixels.width/sprite.texture.width,pixels.height/sprite.texture.height);
        if(flip) { uv.x+=uv.width;uv.width=-uv.width; }
        var old=GUI.color;GUI.color=color;GUI.DrawTextureWithTexCoords(rect,sprite.texture,uv,true);GUI.color=old;
    }
    CutsceneEvent SelectedEvent()
    {
        if(!sequence)return null;var events=finalList ? sequence.finalEvents : sequence.events;
        return selected>=0 && selected<events.Count ? events[selected] : null;
    }
    void DrawGuides(PrologueSequencePreview.Frame frame)
    {
        bool fair=frame.environment==definition.schoolFair;
        Vector2 bottom=ToScreen(new Vector3(0,definition.festivalLaneMin)),top=ToScreen(new Vector3(0,definition.festivalLaneMax));
        var evt=SelectedEvent();bool positionEvent=evt!=null && (evt.action==StoryAction.Move || evt.action==StoryAction.Spawn || evt.action==StoryAction.Vfx || evt.action==StoryAction.Camera);
        Vector2 target=positionEvent ? ToScreen(evt.position) : Vector2.zero;
        Handles.BeginGUI();
        if(fair)
        {
            float left=ToScreen(definition.FestivalArenaMin).x,right=ToScreen(definition.FestivalArenaMax).x;
            EditorGUI.DrawRect(new Rect(left,Mathf.Min(top.y,bottom.y),right-left,Mathf.Abs(bottom.y-top.y)),new Color(.1f,.9f,.6f,.12f));
            Handles.color=new Color(.3f,1,.7f);Handles.DrawLine(new Vector3(left,top.y),new Vector3(left,bottom.y));Handles.DrawLine(new Vector3(right,top.y),new Vector3(right,bottom.y));
            Handles.color=new Color(.3f,1,.7f);Handles.DrawLine(new Vector3(0,top.y),new Vector3(canvas.width,top.y));Handles.DrawLine(new Vector3(0,bottom.y),new Vector3(canvas.width,bottom.y));
            GUI.Label(new Rect(5,top.y-18,180,18),"Lane top "+definition.festivalLaneMax.ToString("0.00"),EditorStyles.whiteMiniLabel);
            GUI.Label(new Rect(5,bottom.y+2,180,18),"Lane bottom "+definition.festivalLaneMin.ToString("0.00"),EditorStyles.whiteMiniLabel);
        }
        if(stageView)
        {
            var a=ToScreen(frame.camera+new Vector3(-frame.zoom*16f/9,frame.zoom));var b=ToScreen(frame.camera+new Vector3(frame.zoom*16f/9,-frame.zoom));
            Handles.color=Color.white;Handles.DrawDottedLines(new[]{new Vector3(a.x,a.y),new Vector3(b.x,a.y),new Vector3(b.x,a.y),new Vector3(b.x,b.y),new Vector3(b.x,b.y),new Vector3(a.x,b.y),new Vector3(a.x,b.y),new Vector3(a.x,a.y)},4);
        }
        if(positionEvent)
        {
            EditorGUI.DrawRect(new Rect(target.x-10,target.y-10,20,20),new Color(.07f,.04f,.01f,.85f));
            Handles.color=new Color(1,.6f,.2f);Handles.DrawLine(new Vector3(target.x-8,target.y),new Vector3(target.x+8,target.y));Handles.DrawLine(new Vector3(target.x,target.y-8),new Vector3(target.x,target.y+8));
            if(frame.actors.TryGetValue(evt.target ?? "",out var actor))Handles.DrawDottedLine(ToScreen(actor.position),target,4);
            GUI.Label(new Rect(target.x+10,target.y-18,140,18),"Target "+evt.position.x.ToString("0.0")+", "+evt.position.y.ToString("0.0"),EditorStyles.whiteMiniLabel);
        }
        Handles.EndGUI();
        HandleDrag(fair,positionEvent,evt,target,top,bottom);
    }
    void HandleDrag(bool fair,bool positionEvent,CutsceneEvent evt,Vector2 target,Vector2 top,Vector2 bottom)
    {
        var input=Event.current;int control=GUIUtility.GetControlID(FocusType.Passive);
        if(input.type==EventType.MouseDown && input.button==0 && new Rect(0,0,canvas.width,canvas.height).Contains(input.mousePosition))
        {
            drag=positionEvent && Vector2.Distance(input.mousePosition,target)<14 ? 1 : fair && Mathf.Abs(input.mousePosition.y-top.y)<6 ? 2 : fair && Mathf.Abs(input.mousePosition.y-bottom.y)<6 ? 3 : 0;
            if(drag>0) { playing=false;Undo.IncrementCurrentGroup();Undo.RecordObject(drag==1 ? (UnityEngine.Object)sequence : definition,"Place prologue "+(drag==1 ? "event" : "walk area"));GUIUtility.hotControl=control;input.Use(); }
        }
        if(input.type==EventType.MouseDrag && drag>0)
        {
            var point=ToWorld(input.mousePosition,evt?.position.z ?? 0);
            if(drag==1 && evt!=null) { evt.position=point;EditorUtility.SetDirty(sequence); }
            if(drag==2)definition.festivalLaneMax=Mathf.Max(point.y,definition.festivalLaneMin+.05f);
            if(drag==3)definition.festivalLaneMin=Mathf.Min(point.y,definition.festivalLaneMax-.05f);
            if(drag!=1)EditorUtility.SetDirty(definition);input.Use();Repaint();
        }
        if(input.type==EventType.MouseUp && drag>0) { drag=0;GUIUtility.hotControl=0;input.Use(); }
    }
    void DrawTimeline()
    {
        GUILayout.Label("TIMELINE · click a clip to edit it",EditorStyles.boldLabel);
        timelineScroll=EditorGUILayout.BeginScrollView(timelineScroll,GUILayout.Height(125));
        int lanes=schedule.Count==0 ? 1 : schedule.Max(s=>s.lane)+1;
        Rect rect=GUILayoutUtility.GetRect(Mathf.Max(400,length*38),Mathf.Max(95,19+lanes*31));
        EditorGUI.DrawRect(rect,new Color(.09f,.1f,.13f));float scale=(rect.width-15)/Mathf.Max(length,1);
        for(int i=0;i<=Mathf.CeilToInt(length);i++)
        {
            float x=rect.x+8+i*scale;EditorGUI.DrawRect(new Rect(x,rect.y,1,rect.height),new Color(1,1,1,.07f));GUI.Label(new Rect(x+2,rect.y,32,16),i.ToString(),EditorStyles.miniLabel);
        }
        foreach(var span in schedule)
        {
            var evt=sequence.events[span.index];int lane=span.lane;
            var clip=new Rect(rect.x+8+span.start*scale,rect.y+19+lane*31,Mathf.Max(7,span.duration*scale-2),27);
            var old=GUI.backgroundColor;GUI.backgroundColor=!finalList && selected==span.index ? new Color(.4f,.75f,1) : evt.parallelGroup>0 ? new Color(.6f,.45f,.9f) : new Color(.5f,.65f,.65f);
            if(GUI.Button(clip,new GUIContent(EventLabel(evt),"#"+(span.index+1)+" · "+span.start.ToString("0.00")+" s"),EditorStyles.miniButton)) { selected=span.index;finalList=false;time=Mathf.Min(length,span.start+.01f);playing=false; }
            GUI.backgroundColor=old;
        }
        EditorGUI.DrawRect(new Rect(rect.x+8+time*scale,rect.y,2,rect.height),new Color(1,.65f,.25f));
        EditorGUILayout.EndScrollView();
    }
    static string EventLabel(CutsceneEvent evt) => evt.action+(string.IsNullOrEmpty(evt.target) ? "" : " · "+evt.target)+(string.IsNullOrEmpty(evt.value) ? "" : " · "+evt.value);
    public static int MakeOpeningInstant(CutsceneSequence sequence)
    {
        if(!sequence)return 0;
        Undo.RecordObject(sequence,"Make opening placement instant");int count=0;
        foreach(var evt in sequence.events)
        {
            if(evt.action!=StoryAction.Spawn && evt.action!=StoryAction.Face)break;
            evt.duration=0;count++;
        }
        EditorUtility.SetDirty(sequence);return count;
    }
    void DrawDialogue(PrologueSequencePreview.Frame frame)
    {
        if(frame.dialogue==null) { GUILayout.Label("No dialogue at this time",EditorStyles.miniLabel);return; }
        using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUILayout.Label(frame.dialogue.speaker+"  ·  "+frame.conversation+" / line "+(frame.dialogueLine+1),EditorStyles.boldLabel);
            GUILayout.Label(frame.dialogue.Text(thai),wrap);
            if(GUILayout.Button("Edit this dialogue line")) { selected=schedule.First(s=>sequence.events[s.index].action==StoryAction.Dialogue && sequence.events[s.index].value==frame.conversation && time>=s.start && time<s.End).index;finalList=false; }
        }
    }
    void DrawNotes(PrologueSequencePreview.Frame frame)
    {
        var warnings=sequence.events.Where(e=>e.action==StoryAction.Dialogue && (!definition.dialogue || definition.dialogue.Find(e.value)==null)).Select(e=>"Missing dialogue: "+e.value).ToList();
        if(definition.festivalLaneMin>=definition.festivalLaneMax)warnings.Add("Lane bottom must be below the lane top.");
        for(int i=0;i<2;i++)
        {
            var authored=i==0 ? definition.festivalChickenPosition : definition.festivalPepsiPosition;
            if(authored.x!=definition.FestivalStorePosition(i).x)warnings.Add("A festival store is outside the walking bounds; its X position is clamped in the game.");
        }
        if(warnings.Count>0)EditorGUILayout.HelpBox(string.Join("\n",warnings.Distinct()),MessageType.Warning);
        if(frame.notes.Count>0)GUILayout.Label(string.Join(" · ",frame.notes.Distinct()),wrap);
    }
    void DrawEvents()
    {
        if(!sequence)return;
        GUILayout.Label(sequence.name,EditorStyles.boldLabel);
        finalList=GUILayout.Toolbar(finalList ? 1 : 0,new[]{"Sequence events","On finish / skip"})==1;
        if(!finalList && GUILayout.Button(new GUIContent("Make opening placement instant","Set the leading Spawn and Face events to duration 0, so the complete opening arrangement appears at once.")))
        { MakeOpeningInstant(sequence);time=0;playing=false;GUIUtility.ExitGUI(); }
        var data=new SerializedObject(sequence);data.Update();var list=data.FindProperty(finalList ? "finalEvents" : "events");
        selected=Mathf.Clamp(selected,0,Mathf.Max(0,list.arraySize-1));
        listScroll=EditorGUILayout.BeginScrollView(listScroll,GUILayout.Height(205));
        var events=finalList ? sequence.finalEvents : sequence.events;
        for(int i=0;i<events.Count;i++)
        {
            var old=GUI.backgroundColor;if(i==selected)GUI.backgroundColor=new Color(.45f,.7f,1);
            if(GUILayout.Button((i+1).ToString("00")+"  "+EventLabel(events[i]),EditorStyles.miniButton,GUILayout.Height(23)))
            {
                selected=i;playing=false;if(!finalList)time=schedule[i].start+.01f;
            }
            GUI.backgroundColor=old;
        }
        EditorGUILayout.EndScrollView();
        using(new EditorGUILayout.HorizontalScope())
        {
            if(GUILayout.Button("+ Add"))
            {
                Undo.RecordObject(sequence,"Add story event");events.Add(new CutsceneEvent{action=StoryAction.Wait});EditorUtility.SetDirty(sequence);selected=events.Count-1;GUIUtility.ExitGUI();
            }
            using(new EditorGUI.DisabledScope(events.Count==0))
            {
                if(GUILayout.Button("Copy"))
                {
                    Undo.RecordObject(sequence,"Copy story event");events.Insert(selected+1,JsonUtility.FromJson<CutsceneEvent>(JsonUtility.ToJson(events[selected])));EditorUtility.SetDirty(sequence);selected++;GUIUtility.ExitGUI();
                }
                if(GUILayout.Button("Remove")) { list.DeleteArrayElementAtIndex(selected);data.ApplyModifiedProperties();GUIUtility.ExitGUI(); }
            }
        }
        using(new EditorGUILayout.HorizontalScope())
        {
            using(new EditorGUI.DisabledScope(selected<=0 || list.arraySize==0))if(GUILayout.Button("Move up")) { list.MoveArrayElement(selected,selected-1);selected--;data.ApplyModifiedProperties();GUIUtility.ExitGUI(); }
            using(new EditorGUI.DisabledScope(selected>=list.arraySize-1 || list.arraySize==0))if(GUILayout.Button("Move down")) { list.MoveArrayElement(selected,selected+1);selected++;data.ApplyModifiedProperties();GUIUtility.ExitGUI(); }
        }
        detailScroll=EditorGUILayout.BeginScrollView(detailScroll);
        EditorGUILayout.PropertyField(data.FindProperty("id"),new GUIContent("Sequence ID"));
        EditorGUILayout.PropertyField(data.FindProperty("playsOnce"));
        if(list.arraySize>0)DrawEventFields(list.GetArrayElementAtIndex(selected));
        if(data.ApplyModifiedProperties()) { playing=false;Repaint(); }
        if(list.arraySize>0 && (StoryAction)list.GetArrayElementAtIndex(selected).FindPropertyRelative("action").enumValueIndex==StoryAction.Dialogue)DrawDialogueEditor(SelectedEvent()?.value);
        if(finalList)EditorGUILayout.HelpBox("These events apply instantly on completion AND skip. Use them to set final positions, poses and story flags. Their durations are ignored.",MessageType.Info);
        EditorGUILayout.EndScrollView();
    }
    static void Field(SerializedProperty evt,string field,string label=null) => EditorGUILayout.PropertyField(evt.FindPropertyRelative(field),label==null ? null : new GUIContent(label),true);
    void DrawEventFields(SerializedProperty evt)
    {
        GUILayout.Space(6);Field(evt,"action");var action=(StoryAction)evt.FindPropertyRelative("action").enumValueIndex;
        if(!finalList) { Field(evt,"duration","Duration (seconds)");Field(evt,"parallelGroup","Parallel group"); }
        if(!finalList && (action==StoryAction.Spawn || action==StoryAction.Face))GUILayout.Label("Duration 0 applies immediately. A positive duration adds a pause after placement.",wrap);
        if(new[]{StoryAction.Move,StoryAction.Face,StoryAction.Pose,StoryAction.Spawn,StoryAction.Despawn,StoryAction.Vfx,StoryAction.Signal}.Contains(action))Field(evt,"target","Actor name");
        switch(action)
        {
            case StoryAction.Move: Field(evt,"position","Destination");Field(evt,"easeMovement");Field(evt,"walkPlaybackSpeed","Walk animation speed");break;
            case StoryAction.Spawn: Field(evt,"position");Field(evt,"prefab");Field(evt,"sprite");break;
            case StoryAction.Face: Field(evt,"amount","Direction (−1 left, +1 right)");break;
            case StoryAction.Pose: Field(evt,"sprite");Field(evt,"frames");Field(evt,"animation");break;
            case StoryAction.Camera: Field(evt,"position","Camera position");Field(evt,"amount","Orthographic size");break;
            case StoryAction.Fade: Field(evt,"amount","Opacity (0–1)");Field(evt,"color");break;
            case StoryAction.Dialogue:
                var value=evt.FindPropertyRelative("value");
                var choices=definition.dialogue ? definition.dialogue.conversations.Select(c=>c.id).ToArray() : Array.Empty<string>();
                if(choices.Length>0)
                {
                    int current=Array.IndexOf(choices,value.stringValue);int next=EditorGUILayout.Popup("Conversation",current,choices);
                    if(next>=0 && next!=current)value.stringValue=choices[next];
                }
                Field(evt,"value","Conversation ID");break;
            case StoryAction.Vfx: Field(evt,"position");Field(evt,"color");Field(evt,"amount","Size");break;
            case StoryAction.Sound: case StoryAction.Music: Field(evt,"sound","Audio clip");Field(evt,"amount","Volume");break;
            case StoryAction.Environment: Field(evt,"value","Fair / Hideout / Sanctuary");break;
            case StoryAction.Signal: Field(evt,"value","Signal name");break;
            case StoryAction.Flag: Field(evt,"value","Flag name");break;
            case StoryAction.CameraShake: Field(evt,"amount","Shake strength");break;
            case StoryAction.Gameplay: Field(evt,"amount","0 lock / 1 unlock");break;
            case StoryAction.StageTransition: Field(evt,"amount","Stage index");break;
        }
        if(!finalList && evt.FindPropertyRelative("parallelGroup").intValue>0)GUILayout.Label("Consecutive events with the same positive group run together.",wrap);
    }
    void DrawDialogueEditor(string id)
    {
        if(!definition.dialogue)return;
        int index=definition.dialogue.conversations.FindIndex(c=>c.id==id);if(index<0)return;
        GUILayout.Space(10);GUILayout.Label("DIALOGUE · edits the shared database",EditorStyles.boldLabel);
        var data=new SerializedObject(definition.dialogue);data.Update();
        var conversation=data.FindProperty("conversations").GetArrayElementAtIndex(index);
        EditorGUILayout.PropertyField(conversation.FindPropertyRelative("lines"),new GUIContent("Lines / portraits / choices"),true);data.ApplyModifiedProperties();
        if(GUILayout.Button("Select dialogue database"))Selection.activeObject=definition.dialogue;
    }
}

[InitializeOnLoad]
public static class PrologueEditorRequests
{
    static PrologueEditorRequests() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating)return;
        if(File.Exists("Temp/PrologueEditor.open-request")) { File.Delete("Temp/PrologueEditor.open-request");PrologueSequenceEditor.Open(); }
        if(File.Exists("Temp/TutorialEditor.open-request")) { File.Delete("Temp/TutorialEditor.open-request");PrologueSequenceEditor.OpenTutorial(); }
    }
}
