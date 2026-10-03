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
            GUI.Box(new Rect(10, 10, 550, 180), "Combat debug");
            GUI.Label(new Rect(20, 35, 530, 150),
                $"Player: {player.State} | grounded {player.motor.IsGrounded} | height {player.motor.Height:F2}\n" +
                $"Combo: {player.ComboIndex} | attack: {(player.CurrentAttack ? player.CurrentAttack.name : "None")}\n" +
                $"Buffered: {player.BufferedInput} | jump: {player.JumpBuffered} | Input Action: {input.LastAction}\n" +
                $"Enemy: {enemy.State} | height: {enemy.motor.Height:F2} | juggle hits: {enemy.JuggleHits}\n" +
                $"HP: player {player.health.Current:F0} | enemy {enemy.health.Current:F0}\n" +
                "Move WASD / stick | Attack J / West | Launcher K / North | Jump Space / South");
        }
    }
}
