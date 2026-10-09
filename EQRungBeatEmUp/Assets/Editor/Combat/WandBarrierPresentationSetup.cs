using System.IO;
using BeatEmUp;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class WandBarrierPresentationSetup
{
    static WandBarrierPresentationSetup() { EditorApplication.update += Poll; }
    [MenuItem("Beat Em Up/Characters/Polish wand barrier presentation")]
    public static void Build()
    {
        foreach (string name in new[] { "WandBarrier", "BarrierPulse" })
        {
            string path = Character2Setup.Root + "/Character2/" + name + ".prefab";
            var go = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var renderer = go.GetComponentInChildren<SpriteRenderer>();
                bool dome = name == "WandBarrier";
                renderer.transform.localScale = dome ? new Vector3(1.2f, 1.05f, 1) : new Vector3(1.4f, .9f, 1);
                renderer.transform.localPosition = dome ? new Vector3(0, .95f, 0) : Vector3.zero;
                renderer.color = new Color(1, 1, 1, dome ? .42f : .45f);
                renderer.sortingOrder = dome ? -20 : -10;
                if (!go.GetComponent<GroundSortedEffect>()) go.AddComponent<GroundSortedEffect>();
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }
        var cast = AssetDatabase.LoadAssetAtPath<AttackData>(Character2Setup.Root + "/Character2/Character2_WandBarrierCast.asset");
        cast.feedback.warningColor = new Color(.35f, .8f, 1, .3f);
        cast.feedback.waveColor = new Color(.6f, 1, 1, .22f);
        EditorUtility.SetDirty(cast);
        MultiplayerSetup.Build();
        AssetDatabase.SaveAssets();
    }
    static void Poll()
    {
        const string request = "Temp/WandBarrierPresentation.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        Build(); Character2Validation.RunPresentation();
    }
}
