using UnityEngine;

namespace BeatEmUp
{
    public sealed class CombatDebugOverlay : MonoBehaviour
    {
        public bool showDebug;
        public ComboController player;
        public PlayerCombatInput input;
        public EnemyHitReaction enemy;
        private void OnGUI()
        {
            if (!showDebug || !player || !enemy) return;
            GUI.Box(new Rect(10, 10, 650, 235), "Combat debug");
            GUI.Label(new Rect(20, 35, 630, 205),
                $"Player: {player.State} | grounded {player.motor.IsGrounded} | height {player.motor.Height:F2}\n" +
                $"Combo: {player.ComboIndex} | attack: {(player.CurrentAttack ? player.CurrentAttack.name : "None")} | frame {player.attackPlayer.CurrentFrame} | hitstop {player.attackPlayer.HitstopRemaining}f\n" +
                $"Buffered: {player.BufferedInput} | jump: {player.JumpBuffered} | Input Action: {input.LastAction}\n" +
                $"Enemy: {enemy.State} | height: {enemy.motor.Height:F2} | juggle hits: {enemy.JuggleHits}\n" +
                $"Ground eligible: {enemy.GroundBounceEligible} | used: {enemy.GroundBouncesUsed}/{enemy.maxGroundBounces} | Wall eligible: {enemy.WallBounceEligible} | used: {enemy.WallBouncesUsed}/{enemy.maxWallBounces}\n" +
                $"Recoil X: {enemy.motor.HorizontalRecoil:F2} | height velocity: {enemy.motor.VerticalVelocity:F2} | last reaction: {enemy.LastHitReaction}\n" +
                $"HP: player {player.health.Current:F0} | enemy {enemy.health.Current:F0}\n" +
                "Move WASD / stick | Attack Enter / West | Launcher K / North | Jump Space / South\n" +
                "Guard / Parry L / Left shoulder | Dodge Left Alt / Right shoulder");
        }
    }
}
