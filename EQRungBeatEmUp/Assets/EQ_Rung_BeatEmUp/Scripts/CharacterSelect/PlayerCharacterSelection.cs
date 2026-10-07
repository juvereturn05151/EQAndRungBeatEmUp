using System;

namespace BeatEmUp
{
    // Read-only projection of the persistent, already networked LobbySlot.
    [Serializable]
    public sealed class PlayerCharacterSelection
    {
        public int PlayerIndex;
        public string CharacterId;
        public CharacterDefinition CharacterDefinition;
        public bool IsConfirmed;
        public static PlayerCharacterSelection From(LobbySlot slot, MultiplayerCatalog catalog)
        {
            var definition = catalog.SelectionAt(slot.character);
            return new PlayerCharacterSelection { PlayerIndex = slot.slot, CharacterId = catalog.CharacterAt(slot.character)?.characterId,
                CharacterDefinition = definition, IsConfirmed = slot.ready };
        }
    }
}
