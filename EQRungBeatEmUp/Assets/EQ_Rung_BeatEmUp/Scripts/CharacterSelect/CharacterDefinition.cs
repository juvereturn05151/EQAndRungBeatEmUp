using UnityEngine;

namespace BeatEmUp
{
    // Presentation layered over the existing combat data, not a second loadout system.
    [CreateAssetMenu(menuName = "Beat Em Up/Character Select/Character Definition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        public PlayableCharacterData GameplayCharacter;
        public string CharacterId;
        public string DisplayName;
        public GameObject CharacterPrefab;
        public Sprite PortraitSprite, LargePreviewSprite;
        [Range(0, 5)] public int Power = 3, Speed = 3, Defense = 3, Technique = 3;
        public string Archetype = "BALANCED";
        public bool IsUnlocked = true;
        [TextArea] public string Description;
        public AudioClip CharacterSelectVoice, ConfirmVoice;
        public Color ThemeColor = Color.white;
        public Sprite[] CharacterSelectAnimation;
        public GameObject Prefab => CharacterPrefab ? CharacterPrefab : GameplayCharacter ? GameplayCharacter.prefab : null;
        public Sprite Portrait => PortraitSprite ? PortraitSprite : GameplayCharacter ? GameplayCharacter.portrait : null;
        public Sprite Preview => LargePreviewSprite ? LargePreviewSprite : GameplayCharacter && GameplayCharacter.idlePose ? GameplayCharacter.idlePose : Portrait;
    }
}
