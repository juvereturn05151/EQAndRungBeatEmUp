using UnityEngine;

namespace BeatEmUp
{
    // Stable ordered asset catalog is shared by all builds; network messages contain indices, never asset timelines.
    public sealed class MultiplayerCatalog : ScriptableObject
    {
        public GameObject playerPrefab;
        public PlayableCharacterData[] characters = new PlayableCharacterData[0];
        public CharacterDefinition[] selectionCharacters = new CharacterDefinition[0];
        public CharacterDefinition SelectionAt(int index) => System.Array.Find(selectionCharacters, c => c && c.GameplayCharacter == CharacterAt(index));
        public PlayableCharacterData CharacterAt(int index) => characters != null && index >= 0 && index < characters.Length ? characters[index] : null;
        public LevelDefinition level;
        public Sprite[] sprites;
        public AttackData[] attacks;
        public Sprite menuBackground;
        public string gameplayScene = "HauntedHouse";
        public string contentHash;
        System.Collections.Generic.Dictionary<Sprite,int> spriteIndices;
        public Sprite SpriteAt(int id) => id>=0 && id<sprites.Length ? sprites[id] : null;
        public int SpriteId(Sprite sprite)
        {
            if(!sprite) return -1;
            if(spriteIndices==null)
            {
                spriteIndices=new System.Collections.Generic.Dictionary<Sprite,int>();
                for(int i=0;i<sprites.Length;i++) if(sprites[i]) spriteIndices[sprites[i]]=i;
            }
            return spriteIndices.TryGetValue(sprite,out int id) ? id : -1;
        }
        void OnEnable() => spriteIndices=null;
        public int AttackId(AttackData attack) => System.Array.IndexOf(attacks,attack);
    }
}
