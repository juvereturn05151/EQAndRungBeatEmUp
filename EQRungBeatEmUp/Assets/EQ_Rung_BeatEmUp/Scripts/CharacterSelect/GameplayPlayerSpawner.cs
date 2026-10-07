using UnityEngine;
namespace BeatEmUp
{
    // Called by the existing authoritative spawner; no independent spawn lifecycle.
    public static class GameplayPlayerSpawner
    {
        public static GameObject ResolvePrefab(MultiplayerCatalog catalog, LobbySlot selection)
        {
            var definition = catalog.SelectionAt(selection.character);
            if(definition && definition.Prefab) return definition.Prefab;
            var character = catalog.CharacterAt(selection.character);
            return character && character.prefab ? character.prefab : catalog.playerPrefab;
        }
    }
}
