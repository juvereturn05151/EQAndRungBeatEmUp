using UnityEngine;
namespace BeatEmUp
{
    [CreateAssetMenu(menuName="Beat Em Up/Player Run")]
    public sealed class PlayerRunData : ScriptableObject
    {
        [Min(.1f)] public float runSpeed=3.6f;
        [Min(1),Tooltip("Zero-based combat frame where held Dash/Dodge recovery cancels into Run. Clamped past dash movement and invulnerability frames.")]
        public int dashToRunFrame=11;
        [Tooltip("Run loop poses and holds in fixed combat frames; hitstop pauses both movement and the loop.")]
        public CharacterPoseHold[] poses=new CharacterPoseHold[0];
    }
}

