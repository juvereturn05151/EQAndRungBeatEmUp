using BeatEmUp;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AttackCoordinationData))]
public sealed class AttackCoordinationDataDrawer : PropertyDrawer
{
    static readonly string[] fields={"requiresMeleeSlot","requiresRangedSlot","requiresCrowdControlSlot","requiresSupportSlot","priorityModifier","ignoreCoordinator"};
    public override float GetPropertyHeight(SerializedProperty property,GUIContent label)=>(fields.Length+1)*(EditorGUIUtility.singleLineHeight+2);
    public override void OnGUI(Rect rect,SerializedProperty property,GUIContent label)
    {
        EditorGUI.BeginProperty(rect,label,property); rect.height=EditorGUIUtility.singleLineHeight;
        EditorGUI.LabelField(rect,label,EditorStyles.boldLabel); rect.y+=rect.height+2;
        EditorGUI.indentLevel++;
        foreach(var name in fields) { EditorGUI.PropertyField(rect,property.FindPropertyRelative(name)); rect.y+=rect.height+2; }
        EditorGUI.indentLevel--; EditorGUI.EndProperty();
    }
}
public static class AttackCoordinationSceneDebug
{
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    static void Draw(EnemyCombat brain,GizmoType type)
    {
        if(!Application.isPlaying || !brain.coordinator || !brain.coordinator.settings.showDebug) return;
        Handles.color=brain.coordinator.Owns(brain) ? Color.green : Color.yellow;
        Handles.Label(brain.transform.position+Vector3.up*1.65f,brain.name+"\n"+brain.coordinator.Describe(brain));
    }
}
