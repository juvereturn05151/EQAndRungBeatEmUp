using UnityEngine;
namespace BeatEmUp
{
    // Data assignment only. Movement, input, combat, health and resources remain shared.
    [DefaultExecutionOrder(-200), DisallowMultipleComponent, RequireComponent(typeof(ComboController))]
    public sealed class PlayerCharacterLoadout : MonoBehaviour
    {
        public PlayableCharacterData character;
        void Awake() { if (character) Apply(character); }
        public void Apply(PlayableCharacterData definition)
        {
            if (!definition) return;
            character = definition;
            var combat = GetComponent<ComboController>();
            combat.groundCombo = (AttackData[])definition.groundCombo.Clone();
            combat.airCombo = (AttackData[])definition.airCombo.Clone();
            combat.launcher = definition.launcher; combat.airDive = definition.airDive;
            combat.defenseData = definition.defense;
            var skills = GetComponent<PlayerSkillController>(); if (skills) skills.equippedSkill = definition.skill;
            if (combat.animationDriver && combat.animationDriver.animator && definition.locomotion)
                combat.animationDriver.animator.runtimeAnimatorController = definition.locomotion;
            if (combat.motor && combat.motor.sprite && definition.idlePose) combat.motor.sprite.sprite = definition.idlePose;
        }
    }
}
