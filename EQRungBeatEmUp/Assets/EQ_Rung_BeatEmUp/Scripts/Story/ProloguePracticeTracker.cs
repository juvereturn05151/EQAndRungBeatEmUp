using UnityEngine;

namespace BeatEmUp.Story
{
    public sealed class ProloguePracticeTracker : MonoBehaviour
    {
        public int hits, finishers, blocks, parries, launchers, airFinishers, skills;
        public bool dodged, ran;
        ComboController combat;
        PlayerSkillController skill;
        void OnEnable()
        {
            combat=GetComponent<ComboController>();skill=GetComponent<PlayerSkillController>();
            GetComponent<ComboTracker>().AcceptedHit+=Hit;combat.DefenseImpact+=Defense;
        }
        void OnDisable() { GetComponent<ComboTracker>().AcceptedHit-=Hit;combat.DefenseImpact-=Defense; }
        void Defense(DefenseFeedback feedback) { if(feedback==DefenseFeedback.Parry)parries++;else blocks++; }
        void Hit(CombatHurtbox target,AttackHitboxData hit)
        {
            hits++;
            if(combat.groundCombo.Length>2 && combat.CurrentAttack==combat.groundCombo[2])finishers++;
            if(hit.hitType==HitType.Launcher)launchers++;
            if(hit.hitType==HitType.AirFinisher)airFinishers++;
        }
        void Update()
        {
            dodged|=combat.State==CombatState.Dodge;ran|=combat.IsRunning;
            skills=skill ? skill.AreaReleases+skill.ProjectilesReleased : 0;
        }
        public void ResetPractice() { hits=finishers=blocks=parries=launchers=airFinishers=0;dodged=ran=false; }
    }
}
