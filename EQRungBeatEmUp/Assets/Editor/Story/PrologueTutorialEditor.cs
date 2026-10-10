using BeatEmUp.Story;
using UnityEditor;
using UnityEngine;

public sealed partial class PrologueSequenceEditor
{
    [SerializeField] bool tutorialMode;
    [SerializeField] int tutorialStep;
    Vector2 tutorialScroll;
    static readonly string[] PracticeNames={"Movement / reach goal","Defeat practice opponent","Punch → Punch → Headbutt","Block","Perfect parry","Dash and run","Grounded launcher","Launch / jump / air smash","Defeat delinquent group"};

    [MenuItem("Beat Em Up/Story/Tutorial editor")]
    public static void OpenTutorial()
    {
        Open();var window=GetWindow<PrologueSequenceEditor>();window.tutorialMode=true;window.playing=false;window.Repaint();
    }

    public static PrologueSequencePreview.Frame TutorialFrame(PrologueDefinition definition,int step)
    {
        var frame=new PrologueSequencePreview.Frame{environment=definition.hideout,environmentWidth=11,camera=definition.tutorialCamera,zoom=Mathf.Max(.1f,definition.tutorialZoom)};
        frame.actors["Player start"]=new PrologueSequencePreview.Actor{name="Player",sprite=definition.eq ? definition.eq.idlePose : null,position=definition.ClampTutorialPosition(definition.tutorialPlayerStart)};
        frame.actors["Partner start"]=new PrologueSequencePreview.Actor{name="Partner",sprite=definition.rung ? definition.rung.idlePose : null,position=definition.ClampTutorialPosition(definition.tutorialPartnerStart)};
        var renderer=definition.delinquentPrefab ? definition.delinquentPrefab.GetComponentInChildren<SpriteRenderer>(true) : null;
        int count=step==0 ? 0 : step==8 ? 3 : 1;
        for(int i=0;i<count;i++)frame.actors["Enemy "+(i+1)]=new PrologueSequencePreview.Actor{name="Enemy "+(i+1),sprite=renderer ? renderer.sprite : null,position=definition.TutorialEnemyPosition(i),scale=definition.delinquentPrefab ? definition.delinquentPrefab.transform.localScale.x : 1,flip=true};
        return frame;
    }

    void DrawTutorialPreview()
    {
        GUILayout.Label("PLAYABLE TUTORIAL · "+(tutorialStep+1)+"/9  "+PracticeNames[tutorialStep],EditorStyles.boldLabel);
        DrawPreview(TutorialFrame(definition,tutorialStep));
        EditorGUILayout.HelpBox("Placement preview uses the game's tutorial settings. Practice steps advance through player actions in Play Mode; combat is not simulated here. Player / partner markers represent slots, so choosing Rung as player swaps the displayed heroes.",MessageType.Info);
        var min=definition.TutorialArenaMin;var max=definition.TutorialArenaMax;
        if(max.x-min.x<.5f || max.y-min.y<.1f)EditorGUILayout.HelpBox("The walking area is very small. Leave room to move in all four directions and fight.",MessageType.Warning);
        if(definition.tutorialGoalX!=definition.TutorialGoal)EditorGUILayout.HelpBox("Goal X is outside the area. The game uses the nearest edge; move the goal inside the green area.",MessageType.Warning);
        if(definition.ClampTutorialPosition(definition.tutorialPlayerStart)!=definition.tutorialPlayerStart || definition.ClampTutorialPosition(definition.tutorialPartnerStart)!=definition.tutorialPartnerStart || definition.ClampTutorialPosition(definition.tutorialEnemyStart)!=definition.tutorialEnemyStart)
            EditorGUILayout.HelpBox("A spawn is outside the area. Preview and game clamp it to the nearest edge.",MessageType.Warning);
    }

    void DrawTutorialSettings()
    {
        GUILayout.Label("PRACTICE STEPS",EditorStyles.boldLabel);
        for(int i=0;i<PracticeNames.Length;i++)
        {
            var old=GUI.backgroundColor;if(i==tutorialStep)GUI.backgroundColor=new Color(.45f,.7f,1);
            if(GUILayout.Button((i+1).ToString("00")+"  "+PracticeNames[i],GUILayout.Height(23)))tutorialStep=i;
            GUI.backgroundColor=old;
        }
        tutorialScroll=EditorGUILayout.BeginScrollView(tutorialScroll);
        GUILayout.Label("WALKING AREA / PLACEMENT",EditorStyles.boldLabel);
        var data=new SerializedObject(definition);data.Update();
        EditorGUILayout.PropertyField(data.FindProperty("tutorialAreaMin"),new GUIContent("Left / bottom (X, Y)"));
        EditorGUILayout.PropertyField(data.FindProperty("tutorialAreaMax"),new GUIContent("Right / top (X, Y)"));
        EditorGUILayout.PropertyField(data.FindProperty("tutorialGoalX"),new GUIContent("Movement goal X"));
        EditorGUILayout.PropertyField(data.FindProperty("tutorialPlayerStart"),new GUIContent("Player start"));
        EditorGUILayout.PropertyField(data.FindProperty("tutorialPartnerStart"),new GUIContent("Partner start"));
        EditorGUILayout.PropertyField(data.FindProperty("tutorialEnemyStart"),new GUIContent("Enemy start"));
        EditorGUILayout.PropertyField(data.FindProperty("tutorialEnemySpacing"),new GUIContent("Wave spacing (X / Y)"));
        EditorGUILayout.PropertyField(data.FindProperty("tutorialCamera"),new GUIContent("Camera position"));
        EditorGUILayout.PropertyField(data.FindProperty("tutorialZoom"),new GUIContent("Camera size"));
        data.ApplyModifiedProperties();
        GUILayout.Label("Both players and moving enemies use the green area. Practice dummies stay near their spawn. Wave spacing places the three enemies in step 9. These settings apply only to the tutorial.",wrap);
        EditorGUILayout.EndScrollView();
    }

    void DrawTutorialGuides()
    {
        Vector2 a=ToScreen(definition.TutorialArenaMin),b=ToScreen(definition.TutorialArenaMax);
        Rect area=Rect.MinMaxRect(a.x,b.y,b.x,a.y);
        EditorGUI.DrawRect(area,new Color(.1f,.9f,.6f,.15f));
        Handles.BeginGUI();Handles.color=new Color(.3f,1,.7f);
        Handles.DrawAAPolyLine(3,new Vector3(area.xMin,area.yMin),new Vector3(area.xMax,area.yMin),new Vector3(area.xMax,area.yMax),new Vector3(area.xMin,area.yMax),new Vector3(area.xMin,area.yMin));
        GUI.Label(new Rect(area.xMin+4,area.yMin-18,200,18),"Walking area · drag edges",EditorStyles.whiteMiniLabel);
        float goal=ToScreen(new Vector3(definition.TutorialGoal,0)).x;
        Handles.color=Color.yellow;Handles.DrawDottedLine(new Vector3(goal,0),new Vector3(goal,canvas.height),4);
        GUI.Label(new Rect(goal+3,4,160,18),"Goal "+definition.TutorialGoal.ToString("0.00"),EditorStyles.whiteMiniLabel);
        Vector2[] starts={definition.tutorialPlayerStart,definition.tutorialPartnerStart,definition.tutorialEnemyStart};
        string[] labels={"Player start","Partner start","Enemy start"};
        var points=new Vector2[3];
        for(int i=0;i<3;i++)
        {
            points[i]=ToScreen(definition.ClampTutorialPosition(starts[i]));var p=points[i];
            Handles.color=new Color(1,.6f,.2f);Handles.DrawAAPolyLine(3,new Vector3(p.x-8,p.y),new Vector3(p.x+8,p.y));Handles.DrawAAPolyLine(3,new Vector3(p.x,p.y-8),new Vector3(p.x,p.y+8));
            GUI.Label(new Rect(p.x+9,p.y-20,120,18),labels[i],EditorStyles.whiteMiniLabel);
        }
        Handles.EndGUI();
        if(stageView)
        {
            var camera=definition.tutorialCamera;float zoom=Mathf.Max(.1f,definition.tutorialZoom);
            var cornerA=ToScreen(camera+new Vector3(-zoom*16f/9,zoom));var cornerB=ToScreen(camera+new Vector3(zoom*16f/9,-zoom));
            Handles.BeginGUI();Handles.color=Color.white;
            Handles.DrawDottedLines(new[]{new Vector3(cornerA.x,cornerA.y),new Vector3(cornerB.x,cornerA.y),new Vector3(cornerB.x,cornerA.y),new Vector3(cornerB.x,cornerB.y),new Vector3(cornerB.x,cornerB.y),new Vector3(cornerA.x,cornerB.y),new Vector3(cornerA.x,cornerB.y),new Vector3(cornerA.x,cornerA.y)},4);
            Handles.EndGUI();
        }
        var input=Event.current;int control=GUIUtility.GetControlID(FocusType.Passive);
        if(input.type==EventType.MouseDown && input.button==0 && new Rect(0,0,canvas.width,canvas.height).Contains(input.mousePosition))
        {
            drag=0;
            for(int i=0;i<3;i++)if(Vector2.Distance(input.mousePosition,points[i])<14) { drag=10+i;break; }
            if(drag==0 && Mathf.Abs(input.mousePosition.x-goal)<6)drag=9;
            if(drag==0 && input.mousePosition.x>=area.xMin-6 && input.mousePosition.x<=area.xMax+6)
                drag=Mathf.Abs(input.mousePosition.y-area.yMin)<6 ? 5 : Mathf.Abs(input.mousePosition.y-area.yMax)<6 ? 6 : 0;
            if(drag==0 && input.mousePosition.y>=area.yMin-6 && input.mousePosition.y<=area.yMax+6)
                drag=Mathf.Abs(input.mousePosition.x-area.xMin)<6 ? 7 : Mathf.Abs(input.mousePosition.x-area.xMax)<6 ? 8 : 0;
            if(drag>0) { Undo.IncrementCurrentGroup();Undo.RecordObject(definition,"Adjust tutorial placement");GUIUtility.hotControl=control;input.Use(); }
        }
        if(input.type==EventType.MouseDrag && drag>0)
        {
            Vector2 p=ToWorld(input.mousePosition);var min=definition.TutorialArenaMin;var max=definition.TutorialArenaMax;
            if(drag==5)max.y=Mathf.Max(p.y,min.y+.1f);
            if(drag==6)min.y=Mathf.Min(p.y,max.y-.1f);
            if(drag==7)min.x=Mathf.Min(p.x,max.x-.5f);
            if(drag==8)max.x=Mathf.Max(p.x,min.x+.5f);
            if(drag>=5 && drag<=8) { definition.tutorialAreaMin=min;definition.tutorialAreaMax=max; }
            if(drag==9)definition.tutorialGoalX=Mathf.Clamp(p.x,min.x,max.x);
            if(drag==10)definition.tutorialPlayerStart=definition.ClampTutorialPosition(p);
            if(drag==11)definition.tutorialPartnerStart=definition.ClampTutorialPosition(p);
            if(drag==12)definition.tutorialEnemyStart=definition.ClampTutorialPosition(p);
            EditorUtility.SetDirty(definition);input.Use();Repaint();
        }
        if(input.type==EventType.MouseUp && drag>0) { drag=0;GUIUtility.hotControl=0;input.Use(); }
    }
}
