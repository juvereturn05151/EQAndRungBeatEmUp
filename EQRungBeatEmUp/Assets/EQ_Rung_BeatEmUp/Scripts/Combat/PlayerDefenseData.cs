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
        [Min(1), Tooltip("Fresh Guard is immediately parry-active on frames 0 through N-1; frame N becomes normal Guard.")]
        public int parryWindowFrames = 8;
        [Min(1)] public int parryRecoveryFrames = 8;
        [Min(1), Tooltip("Normal enemy parry stun duration, in combat frames.")] public int parryAttackerStunFrames = 90;
        [Min(0)] public int parryHitstopFrames = 6, guardHitstopFrames = 2;
        [Min(0), Tooltip("Additional combat frames after the complete active window before a fresh press can rearm. Guard remains available during this delay; releasing early does not reset it.")]
        public int parryRearmDelayFrames = 6;
        [Header("Successful parry feedback (existing combat feedback renderer)")]
        public AttackFeedbackData parryFeedback = new AttackFeedbackData();
        public int CounterAdvantageFrames => Mathf.Max(0, parryAttackerStunFrames - parryRecoveryFrames);
        [Header("Knockdown, get-up and death")]
        [Min(1)] public int knockdownFrames = 18;
        [Min(0)] public int downedFrames = 45;
        [Min(1)] public int getUpFrames = 24, dieFrames = 18;
        [Header("Sprite holds (logical frames, last pose holds)")]
        public CharacterPoseHold[] dodge, guard, parry, knockdown, getUp, die;
        public Sprite blockPose;
        [Header("Grabbed: held pose loop")]
        public CharacterPoseHold[] grabbed;
        [Header("Stunned status: looping combat-frame poses / overhead effect")]
        public CharacterPoseHold[] stunned;
        public Sprite[] stunVfx = new Sprite[0];
        [Min(1)] public int stunVfxHoldFrames = 5;
        public Vector2 stunVfxOffset = new Vector2(0, 1.2f);
        [Min(.01f)] public float stunVfxScale = 1;
        public static Sprite LoopPose(CharacterPoseHold[] holds, int frame)
        {
            int length = 0;
            if (holds != null) foreach (var hold in holds) if (hold != null) length += Mathf.Max(1, hold.frames);
            return length > 0 ? Pose(holds, Mathf.Max(0, frame) % length) : null;
        }
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
    public enum CombatHitOutcome { None, Hit, Block, Parry, Armor, ArmorBreak }
}
