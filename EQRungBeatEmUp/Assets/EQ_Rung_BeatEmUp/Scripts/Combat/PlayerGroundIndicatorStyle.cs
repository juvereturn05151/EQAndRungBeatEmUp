using UnityEngine;

namespace BeatEmUp
{
    [CreateAssetMenu(menuName = "Beat Em Up/Player Ground Indicator Style")]
    public sealed class PlayerGroundIndicatorStyle : ScriptableObject
    {
        public Sprite shadowSprite;
        [Tooltip("Thin to thick, selected by Ring Thickness Pixels. Shared imported sprites; no runtime textures.")]
        public Sprite[] ringSprites = new Sprite[4];
        public Vector2 shadowSize = new Vector2(.68f, .24f);
        public Vector2 ringSize = new Vector2(.9f, .32f);
        [Range(0, 1)] public float shadowOpacity = .34f;
        [Range(0, 1)] public float ringOpacity = .65f;
        [Range(1, 4)] public int ringThicknessPixels = 2;
        public Vector2 groundOffset = new Vector2(0, .015f);
        public Color[] playerColors = {
            new Color(.18f, .56f, 1), new Color(1, .22f, .25f),
            new Color(.25f, .85f, .32f), new Color(1, .82f, .22f)
        };
        static PlayerGroundIndicatorStyle shared;
        static readonly Color[] fallbackColors = {
            new Color(.18f, .56f, 1), new Color(1, .22f, .25f),
            new Color(.25f, .85f, .32f), new Color(1, .82f, .22f)
        };
        public static PlayerGroundIndicatorStyle Shared
        {
            get { if (!shared) shared = Resources.Load<PlayerGroundIndicatorStyle>("PlayerGroundIndicatorStyle"); return shared; }
        }
        public static Color[] SharedPlayerColors => Shared && Shared.playerColors != null && Shared.playerColors.Length == 4 ? Shared.playerColors : fallbackColors;
        public Color ColorForSlot(int slot) => playerColors != null && playerColors.Length == 4 ? playerColors[Mathf.Clamp(slot, 0, 3)] : fallbackColors[Mathf.Clamp(slot, 0, 3)];
        public Sprite RingSprite => ringSprites != null && ringSprites.Length > 0 ? ringSprites[Mathf.Clamp(ringThicknessPixels - 1, 0, ringSprites.Length - 1)] : null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetShared() => shared = null;
    }
}
