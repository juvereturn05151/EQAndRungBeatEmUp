using System.Text.RegularExpressions;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

public abstract class EncounterFoldoutDrawer : PropertyDrawer
{
    protected abstract string IdField { get; }
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight + 4;
        if (!property.isExpanded) return height;
        var child = property.Copy(); var end = child.GetEndProperty();
        if (child.NextVisible(true)) do
        {
            if (SerializedProperty.EqualContents(child, end)) break;
            if (child.name != "enabled") height += EditorGUI.GetPropertyHeight(child, true) + 3;
        } while (child.NextVisible(false));
        return height;
    }
    public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(rect, label, property);
        rect.height = EditorGUIUtility.singleLineHeight;
        string id = property.FindPropertyRelative(IdField).stringValue;
        bool opened = EditorGUI.Foldout(new Rect(rect.x, rect.y, Mathf.Max(30, rect.width - 155), rect.height), property.isExpanded, string.IsNullOrEmpty(id) ? label.text : id, true);
        float labelWidth = EditorGUIUtility.labelWidth; int indent = EditorGUI.indentLevel;
        EditorGUIUtility.labelWidth = 60; EditorGUI.indentLevel = 0;
        EditorGUI.PropertyField(new Rect(rect.xMax - 150, rect.y, 95, rect.height), property.FindPropertyRelative("enabled"), new GUIContent("Enabled"));
        EditorGUIUtility.labelWidth = labelWidth; EditorGUI.indentLevel = indent;
        bool select = GUI.Button(new Rect(rect.xMax - 50, rect.y, 50, rect.height), "Select");
        if (select || opened != property.isExpanded && opened)
        {
            var matches = Regex.Matches(property.propertyPath, @"data\[(\d+)\]");
            if (matches.Count >= 2 && property.serializedObject.targetObject is LevelDefinition level)
                EncounterPreview.RequestSelection(level, int.Parse(matches[0].Groups[1].Value), int.Parse(matches[1].Groups[1].Value), matches.Count >= 3 ? int.Parse(matches[2].Groups[1].Value) + 1 : 0);
        }
        property.isExpanded = opened;
        if (opened)
        {
            EditorGUI.indentLevel++;
            var child = property.Copy(); var end = child.GetEndProperty(); rect.y += rect.height + 4;
            if (child.NextVisible(true)) do
            {
                if (SerializedProperty.EqualContents(child, end)) break;
                if (child.name == "enabled") continue;
                rect.height = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(rect, child, true); rect.y += rect.height + 3;
            } while (child.NextVisible(false));
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}
[CustomPropertyDrawer(typeof(EncounterDefinition))]
public sealed class EncounterDefinitionDrawer : EncounterFoldoutDrawer { protected override string IdField => "encounterId"; }
[CustomPropertyDrawer(typeof(WaveDefinition))]
public sealed class WaveDefinitionDrawer : EncounterFoldoutDrawer { protected override string IdField => "waveId"; }
