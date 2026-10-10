using BeatEmUp.Story;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(CutsceneSequence))]
public sealed class CutsceneSequenceEditor : Editor
{
    ReorderableList events;
    void OnEnable()
    {
        events=new ReorderableList(serializedObject,serializedObject.FindProperty("events"),true,true,true,true);
        events.drawHeaderCallback=rect=>EditorGUI.LabelField(rect,"Timeline — drag to reorder; shared groups run together");
        events.elementHeightCallback=i=>EditorGUI.GetPropertyHeight(events.serializedProperty.GetArrayElementAtIndex(i),true)+6;
        events.drawElementCallback=(rect,i,active,focused)=>{rect.y+=3;EditorGUI.PropertyField(rect,events.serializedProperty.GetArrayElementAtIndex(i),new GUIContent("Event "+i),true);};
    }
    public override void OnInspectorGUI()
    {
        serializedObject.Update();EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));EditorGUILayout.PropertyField(serializedObject.FindProperty("playsOnce"));events.DoLayoutList();EditorGUILayout.PropertyField(serializedObject.FindProperty("finalEvents"),true);serializedObject.ApplyModifiedProperties();
        EditorGUILayout.HelpBox("Final events run after normal completion and skip. Host owns scene skipping. A positive parallel group joins consecutive events. Dialogue references a conversation ID in PrologueDialogue.",MessageType.Info);
        var director=PrologueDirector.Active;
        using(new EditorGUI.DisabledScope(!Application.isPlaying || !director || !director.ActiveStory || director.Cutscenes.IsCutscenePlaying))
            if(GUILayout.Button("Preview in Play Mode"))director.Cutscenes.PlayCutscene((CutsceneSequence)target);
        if(Application.isPlaying && director) { EditorGUILayout.LabelField("Current step",director.Cutscenes.CurrentStep.ToString());if(GUILayout.Button("Pause / Resume")) { if(director.Cutscenes.Paused)director.Cutscenes.ResumeCutscene();else director.Cutscenes.PauseCutscene(); }if(GUILayout.Button("Skip current scene"))director.RequestSkip(); }
    }
}
