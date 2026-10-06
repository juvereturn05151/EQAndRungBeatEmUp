using System;
using UnityEngine;
namespace BeatEmUp
{
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyHitReaction))]
    public sealed class HitCountArmor : MonoBehaviour, ICombatFrameListener
    {
        [Min(1)] public int maxArmorHits=6, armorBreakStunFrames=60, armorRecoveryDelayFrames=300;
        public bool restoreFullArmor=true;
        [Min(0)] public int hitstopFrames=2;
        public AttackData armorHitFeedback, armorBreakFeedback;
        public Color armorColor=new Color(1,.7f,.2f), breakColor=new Color(.3f,1,1);
        public int ArmorRemaining { get; private set; }
        public bool ArmorActive => ArmorRemaining>0;
        public bool ArmorBroken => ArmorRemaining==0;
        public int RecoveryRemaining { get; private set; }
        public CombatHitOutcome LastOutcome { get; private set; }
        public int FeedbackCount { get; private set; }
        public event Action<AttackData,string> Feedback;
        public int FrameOrder => 45;
        EnemyHitReaction reaction; CharacterHealth health; CharacterMotor motor;
        int flash; Color originalColor;
        void Awake() { reaction=GetComponent<EnemyHitReaction>(); health=reaction.health; motor=reaction.motor; originalColor=motor.sprite.color; ResetArmor(); }
        void OnEnable() { if(!health) health=GetComponent<CharacterHealth>(); health.Restored+=ResetArmor; CombatClock.Register(this); }
        void OnDisable() { CombatClock.Unregister(this); if(health) health.Restored-=ResetArmor; if(motor && motor.sprite) motor.sprite.color=originalColor; }
        public void ResetArmor() { ArmorRemaining=Mathf.Max(1,maxArmorHits); RecoveryRemaining=0; flash=0; if(motor && motor.sprite) motor.sprite.color=originalColor; }
        public bool Absorb(AttackHitboxData hit)
        {
            if(hit.damage<=0 || health.IsDead) return false;
            RecoveryRemaining=Mathf.Max(1,armorRecoveryDelayFrames);
            if(!ArmorActive || reaction.IsStunState) return false;
            ArmorRemaining--; bool broken=ArmorBroken;
            LastOutcome=broken ? CombatHitOutcome.ArmorBreak : CombatHitOutcome.Armor;
            GetComponent<HitBlinkEffect>()?.StopBlink();
            flash=broken ? 12 : 6; motor.sprite.color=broken ? breakColor : armorColor;
            var feedback=broken ? armorBreakFeedback : armorHitFeedback;
            var point=(Vector2)motor.sprite.transform.position+Vector2.up*.6f;
            AttackFeedback.PlayRemote(feedback,point,motor.Facing,true);
            FeedbackCount++; Feedback?.Invoke(feedback,broken ? "ArmorBreak" : "Armor");
            if(broken) { motor.AddKnockback(0); reaction.InterruptWithHitstun(Mathf.Max(1,armorBreakStunFrames)); }
            return true;
        }
        public void CombatFrame()
        {
            if(health.IsDead || motor.attackPlayer.IsFrozen) return;
            if(flash>0 && --flash==0) motor.sprite.color=originalColor;
            if(ArmorRemaining>=Mathf.Max(1,maxArmorHits)) return;
            if(RecoveryRemaining>0) RecoveryRemaining--;
            if(RecoveryRemaining==0 && reaction.CanAct && !motor.attackPlayer.CurrentAttack)
            { ArmorRemaining=restoreFullArmor ? Mathf.Max(1,maxArmorHits) : Mathf.Min(Mathf.Max(1,maxArmorHits),ArmorRemaining+1); RecoveryRemaining=Mathf.Max(1,armorRecoveryDelayFrames); }
        }
    }
}
