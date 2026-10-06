using UnityEngine;
namespace BeatEmUp
{
    public sealed partial class CombatProjectile
    {
        public bool Deflect(CharacterMotor defender)
        {
            if (!CanDeflect || !defender || CombatClock.IsPaused) return false;
            var hurtbox = defender.GetComponentInChildren<CombatHurtbox>(); if (!hurtbox) return false;
            owner = defender; team = hurtbox.team; DeflectionCount++;
            Velocity = -Velocity * Mathf.Max(.01f, deflectSpeedMultiplier);
            heightVelocity = -heightVelocity * Mathf.Max(.01f, deflectSpeedMultiplier);
            facing = Velocity.x < 0 ? -1 : 1;
            hitHistory.Clear(); AcceptedHits = 0; // A parry is not a damage hit against the new faction.
            launchGround = groundPosition; launchSourceX = defender.transform.position.x;
            if (groundWave) { var scale = transform.localScale; scale.x = Mathf.Abs(scale.x) * facing; transform.localScale = scale; }
            else if (visual) visual.flipX = facing < 0;
            deflectFlash = 6; if (visual) visual.color = new Color(.5f, 1, 1);
            AttackFeedback.PlayRemote(deflectFeedback, transform.position, facing, true);
            Deflected?.Invoke(this, transform.position);
            // Age and DistanceTraveled are preserved; reflection cannot extend lifetime indefinitely.
            return true;
        }
        AttackHitboxData ImpactHit()
        {
            var result = hit.RuntimeCopy();
            if (DeflectionCount > 0)
            {
                result.damage *= Mathf.Max(0, deflectDamageMultiplier);
                result.hitType = HitType.Normal; result.hitstunFrames = deflectHitstunFrames; result.knockback = deflectKnockback;
                result.groundBounce = result.wallBounce = result.forceAirborneTargetDownward = false;
            }
            return result;
        }
        void OnGUI()
        {
            if (!debugDraw || !initialized) return;
            var camera = Camera.main; if (!camera) return;
            var screen = camera.WorldToScreenPoint(transform.position);
            GUI.Label(new Rect(screen.x, Screen.height - screen.y, 300, 100),
                $"Owner: {(owner ? owner.name : "None")} / Faction: {team}\nDirection: {facing} / Velocity: {Velocity}\nParry: {hit.canBeParried} / Deflect: {CanDeflect}\nDeflections: {DeflectionCount}/{maxDeflections} / Age: {age}/{lifetimeFrames}f");
        }
    }
}
