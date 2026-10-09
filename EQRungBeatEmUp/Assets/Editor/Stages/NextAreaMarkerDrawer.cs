using System.Collections.Generic;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(NextAreaMarkerDefinition))]
public sealed class NextAreaMarkerDrawer : PropertyDrawer
{
    static List<string> Fields(SerializedProperty p)
    {
        var fields=new List<string>{"markerId","enabled","markerPrefab","target"};
        if(p.FindPropertyRelative("target").enumValueIndex==(int)NextAreaTarget.EncounterEntry)fields.Add("targetEncounterId");
        fields.Add("useTransitionPosition");
        if(!p.FindPropertyRelative("useTransitionPosition").boolValue)fields.Add("exitPosition");
        fields.Add("markerOffset");fields.Add("showAfter");
        if(p.FindPropertyRelative("showAfter").enumValueIndex==(int)NextAreaShowAfter.EncounterComplete)fields.Add("afterEncounterId");
        fields.AddRange(new[]{"showEdgeArrow","markerPreview","nearDistance"});return fields;
    }
    public override float GetPropertyHeight(SerializedProperty property,GUIContent label) =>
        (property.isExpanded?Fields(property).Count+3:1)*(EditorGUIUtility.singleLineHeight+2);
    public override void OnGUI(Rect position,SerializedProperty property,GUIContent label)
    {
        EditorGUI.BeginProperty(position,label,property);var row=new Rect(position.x,position.y,position.width,EditorGUIUtility.singleLineHeight);
        property.isExpanded=EditorGUI.Foldout(row,property.isExpanded,label,true);
        if(property.isExpanded)
        {
            EditorGUI.indentLevel++;
            foreach(var field in Fields(property)) {row.y+=row.height+2;EditorGUI.PropertyField(row,property.FindPropertyRelative(field));}
            row.y+=row.height+2;
            if(GUI.Button(EditorGUI.IndentedRect(row),"Use Transition Position"))property.FindPropertyRelative("useTransitionPosition").boolValue=true;
            row.y+=row.height+2;EditorGUI.LabelField(row,"Visibility always respects combat and reward locks.",EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}
