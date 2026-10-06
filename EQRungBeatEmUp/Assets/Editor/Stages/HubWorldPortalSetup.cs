using System;
using BeatEmUp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HubWorldPortalSetup
{
    public const string AuraPath="Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Magic Misc/CFXR3 Magic Aura A (Runic).prefab";
    [MenuItem("Beat Em Up/Hub/Apply World 1 Runic Portal")]
    public static void Build()
    {
        if(EditorApplication.isPlaying) return;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AuraPath);
        if(!prefab) throw new Exception("Missing CFXR3 Magic Aura A (Runic).");
        var root=PrefabUtility.LoadPrefabContents(PlayerSanctuarySetup.EnvironmentPath);
        try
        {
            var existing=root.GetComponentInChildren<HubWorldPortal>(true);
            if(!existing)
            {
                var go=new GameObject("World 1 Entrance Portal"); go.transform.SetParent(root.transform,false);
                existing=go.AddComponent<HubWorldPortal>();
                existing.definition=root.GetComponent<HubLandmarks>().definition; existing.runicAuraReference=prefab;
                var aura=(GameObject)PrefabUtility.InstantiatePrefab(prefab); aura.transform.SetParent(go.transform,false); existing.aura=aura.transform;
                foreach(var effect in aura.GetComponentsInChildren<CartoonFX.CFXR_Effect>(true)) effect.enabled=false;
                foreach(var light in aura.GetComponentsInChildren<Light>(true)) light.enabled=false;
                foreach(var particles in aura.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main=particles.main; main.loop=true; main.prewarm=true; main.playOnAwake=true;
                    main.scalingMode=ParticleSystemScalingMode.Hierarchy; main.stopAction=ParticleSystemStopAction.None;
                    var collision=particles.collision; collision.enabled=false;
                    var trigger=particles.trigger; trigger.enabled=false;
                }
                var label=new GameObject("WORLD 1 ENTRANCE label"); label.transform.SetParent(go.transform,false);
                var text=label.AddComponent<TextMesh>(); text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.GetComponent<MeshRenderer>().sharedMaterial=text.font.material;
                text.fontSize=48; text.characterSize=.035f; text.anchor=TextAnchor.MiddleCenter; text.alignment=TextAlignment.Center;
                text.color=new Color(.65f,1,1); existing.entranceLabel=text;
            }
            existing.entranceLabel.characterSize=.035f;
            if(existing.vfxScale==new Vector3(.65f,.65f,.65f)) existing.vfxScale=new Vector3(.9f,.9f,.9f);
            foreach(var particles in existing.aura.GetComponentsInChildren<ParticleSystem>(true))
            { var main=particles.main; main.startColor=new Color(.35f,1,1,1); }
            existing.RefreshPresentation(); PrefabUtility.SaveAsPrefabAsset(root,PlayerSanctuarySetup.EnvironmentPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        // Existing scene previews inherit this prefab; retain their EditorOnly and preview overrides.
        MultiplayerSetup.Build();
    }
}

[CustomEditor(typeof(HubWorldPortal))]
public sealed class HubWorldPortalEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); var portal=(HubWorldPortal)target;
        EditorGUILayout.HelpBox("Position and interaction radius come from PlayerHub.asset, station 3. Move its Scene handle to keep visuals and interaction aligned. Availability controls cosmetics only.",MessageType.Info);
        if(GUILayout.Button("Select entrance / interaction settings")) Selection.activeObject=portal.definition;
    }
    void OnSceneGUI()
    {
        var portal=(HubWorldPortal)target; if(!portal.definition || portal.definition.stations.Length<=3) return;
        Handles.Label((Vector3)portal.GroundPosition+Vector3.up*portal.labelHeight,"WORLD 1 ENTRANCE");
        Handles.color=Color.cyan; Handles.DrawWireDisc(portal.GroundPosition,Vector3.forward,portal.InteractionRadius);
        EditorGUI.BeginChangeCheck(); var position=(Vector2)Handles.PositionHandle(portal.GroundPosition,Quaternion.identity);
        if(EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(portal.definition,"Move World 1 entrance"); portal.definition.stations[3]=position; EditorUtility.SetDirty(portal.definition);
            var level=AssetDatabase.LoadAssetAtPath<LevelDefinition>(HauntedLevelBuilder.LevelPath);
            Undo.RecordObject(level,"Align Hub gate point"); level.stages[0].playerExitPoint=position; EditorUtility.SetDirty(level); portal.RefreshPresentation();
        }
    }
}
