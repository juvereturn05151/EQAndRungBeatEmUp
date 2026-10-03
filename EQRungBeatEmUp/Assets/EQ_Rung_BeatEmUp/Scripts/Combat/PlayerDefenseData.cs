using System;
using UnityEngine;

namespace BeatEmUp
{
    [Serializable]
    public sealed class CharacterPoseHold
    {
        public Sprite sprite;
        [Min(1)] public int frames = 1;
    }

    // Reaction/defense data deliberately has no offensive hitboxes/cancel routes.
    [CreateAssetMenu(menuName = "Beat Em Up/Player Defense and Reactions")]
    public sealed class PlayerDefenseData : ScriptableObject
    {
        [Header("Dodge: zero-based 60 FPS frames")]
        [Min(1)] public int dodgeTotalFrames = 20;
        public int dodgeMoveFirstFrame = 3, dodgeMoveLastFrame = 10;
        public int dodgeInvulnerableFirstFrame = 4, dodgeInvulnerableLastFrame = 9;
        [Min(0)] public float dodgeSpeed = 9;
        [Header("Guard and parry")]
        [Min(1)] public int parryWindowFrames = 5;
        [Min(1)] public int parryRecoveryFrames = 8;
        [Min(0)] public int parryAttackerStunFrames = 18;
        [Min(0)] public int parryHitstopFrames = 5, guardHitstopFrames = 2;
        [Header("Knockdown, get-up and death")]
        [Min(1)] public int knockdownFrames = 18;
        [Min(0)] public int downedFrames = 45;
        [Min(1)] public int getUpFrames = 24, dieFrames = 18;
        [Header("Sprite holds (logical frames, last pose holds)")]
        public CharacterPoseHold[] dodge, guard, parry, knockdown, getUp, die;
        public Sprite blockPose;
        public static Sprite Pose(CharacterPoseHold[] holds, int frame)
        {
            if (holds == null || holds.Length == 0) return null;
            foreach (var hold in holds)
            {
                if (hold == null) continue;
                if (frame < Mathf.Max(1, hold.frames)) return hold.sprite;
                frame -= Mathf.Max(1, hold.frames);
            }
            return holds[holds.Length - 1]?.sprite;
        }
    }
    public enum DefenseFeedback { Block, Parry }
    public enum CombatHitOutcome { None, Hit, Block, Parry }
}
