using BeatEmUp;
using UnityEditor;

public static class ComboTrackingSetup
{
    public const string PlayerPath = "Assets/EQ_Rung_BeatEmUp/Prefabs/BlueShirtGuy.prefab";
    public static void Build()
    {
        var player = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            var tracker = player.GetComponent<ComboTracker>();
            if (!tracker) tracker = player.AddComponent<ComboTracker>();
            var ui = player.GetComponent<ComboUIController>();
            if (!ui) ui = player.AddComponent<ComboUIController>();
            ui.tracker = tracker;
            PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
    }
}
