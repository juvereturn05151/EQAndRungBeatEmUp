using BeatEmUp;
using UnityEditor;
using UnityEngine;

public static class StageExitMarkerAuthoring
{
    public static void Draw(LevelDefinition level,StageSegmentDefinition stage)
    {
        foreach(var marker in stage.nextAreaMarkers)
        {
            if(!marker.enabled||!marker.markerPreview)continue;
            if(!marker.TryTransitionPosition(stage,out var transition))continue;
            var position=marker.Position(stage);
            Handles.color=new Color(1,.8f,.35f);
            if(marker.target==NextAreaTarget.StageExit)Handles.DrawWireDisc(transition,Vector3.forward,stage.exitRadius);
            else
            {
                var target=stage.encounters.Find(e=>e.encounterId==marker.targetEncounterId);
                var zone=target.triggerZone;
                Handles.DrawSolidRectangleWithOutline(new[]{new Vector3(zone.xMin,zone.yMin),new Vector3(zone.xMax,zone.yMin),new Vector3(zone.xMax,zone.yMax),new Vector3(zone.xMin,zone.yMax)},new Color(1,.8f,.35f,.04f),Handles.color);
            }
            Handles.DrawDottedLine(position,transition,4);Handles.DrawWireDisc(position,Vector3.forward,.24f);
            Handles.DrawLine(position+new Vector3(-.15f,.8f),position+new Vector3(0,.65f));
            Handles.DrawLine(position+new Vector3(.15f,.8f),position+new Vector3(0,.65f));
            Handles.Label(position+Vector3.up*1.15f,"NEXT AREA: "+marker.markerId+"\n"+marker.showAfter);
            if(Application.isPlaying)continue;
            EditorGUI.BeginChangeCheck();var moved=Handles.PositionHandle(position,Quaternion.identity);
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(level,"Move next-area marker");
                if(marker.useTransitionPosition)marker.markerOffset=moved-transition;
                else marker.exitPosition=moved-marker.markerOffset;
                EditorUtility.SetDirty(level);EncounterPreview.RefreshNow();
            }
        }
    }
}
