using UnityEngine;
namespace BeatEmUp
{
    [CreateAssetMenu(menuName = "Beat Em Up/Playable Character")]
    public sealed class PlayableCharacterData : ScriptableObject
    {
        public string characterId, displayName;
        public int sortOrder;
        public GameObject prefab;
        public Sprite portrait, idlePose;
        public RuntimeAnimatorController locomotion;
        public AttackData[] groundCombo = new AttackData[3], airCombo = new AttackData[3];
        public AttackData launcher, airDive;
        public PlayerDefenseData defense;
        public PlayerRunData run;
        public PlayerSkillData skill;
    }
}
